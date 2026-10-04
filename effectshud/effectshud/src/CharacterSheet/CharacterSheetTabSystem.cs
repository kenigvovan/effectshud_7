using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace effectshud.src
{
    /// <summary>
    /// Adds a tab to the vanilla character dialog (the C screen) showing a live character sheet: any
    /// consumer-registered sections (<see cref="ICharacterSheetSection"/>) followed by the player's active effects
    /// as icon + name + tier + remaining time + optional numeric magnitude
    /// (<see cref="EffectClientData.magnitude"/>).
    ///
    /// Generic and self-contained: all the plumbing (finding the shared GuiDialogCharacterBase, appending the tab,
    /// the refresh loop, rendering) lives here. Consumer mods only implement <see cref="ICharacterSheetSection"/> and
    /// register it via <see cref="effectshud.RegisterCharacterSheetSection"/> — no reverse dependency.
    ///
    /// Layout: the vanilla GuiComposer is retained-mode, so effects are drawn as a fixed pool of rows — one
    /// dynamic-text per row (name/tier/time/magnitude) plus one interactive custom-draw element that paints each
    /// row's icon (SVG via <c>capi.Gui.DrawSvg</c>, PNG via a scaled surface blit). Both columns share the same row
    /// grid, so they stay aligned. Live updates just re-set the row texts and Redraw the icon element — no recompose.
    /// </summary>
    public class CharacterSheetTabSystem : ModSystem
    {
        private const string MainComposerKey = "playercharacter";
        private const string ContainerKey = "effectshud-sheet-content";
        private const string ScrollbarKey = "effectshud-sheet-scroll";
        private const int MaxRows = 64;
        /// <summary>Fallback panel width, used only if the dialog hasn't sized itself yet.</summary>
        private const double FallbackWidth = 300.0;
        private const double ScrollbarWidth = 16.0;

        private ICoreClientAPI capi = null!;
        private GuiDialogCharacterBase dlg;
        private GuiTab myTab;
        private int myTabIndex = -1;
        private int curTab;

        // Icons to paint, one per effect row, in display order. Read by the custom-draw delegate; written on
        // compose/refresh. AssetLocation (or null = no icon for that row).
        private AssetLocation[] rowIcons = Array.Empty<AssetLocation>();
        private double rowHeightUnscaled = 20.0;

        // The scrolled content. The dialog is sized by its FIRST tab and never grows for ours, so a long sheet
        // used to draw straight past the window - everything lives in a clipped container instead, and these are
        // updated in place. Held as references (not looked up by key) because they live inside that container;
        // `owner` says which composition they belong to, so a stale set is never touched.
        private GuiComposer owner;
        private GuiElementDynamicText sectionsElem;
        private GuiElementDynamicText effectsElem;
        private GuiElementCustomDraw iconsElem;
        private ElementBounds contentBounds;
        private double viewHeight;
        private double contentHeight;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            dlg = api.Gui.LoadedGuis.Find(d => d is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            if (dlg == null)
            {
                api.Logger.Warning("[effectshud] character dialog not found; effects tab not added.");
                return;
            }

            dlg.TabClicked += idx => curTab = idx;
            // Refresh the tab title on open (a consumer may have set it after us). The tab itself is added on the
            // first game tick — see EnsureTabAdded / OnRefresh.
            dlg.OnOpened += EnsureTabAdded;

            api.Event.RegisterGameTickListener(OnRefresh, 500);
            // Add the tab as soon as possible AFTER all mods' StartClientSide have run (so it lands last with a
            // unique DataInt — see EnsureTabAdded) but BEFORE the player can open the dialog. A one-shot delayed
            // callback runs on the main thread once the world is in; the periodic tick is a backstop.
            api.Event.RegisterCallback(_ => EnsureTabAdded(), 1);
        }

        private void EnsureTabAdded()
        {
            if (myTabIndex >= 0) { if (myTab != null) myTab.Name = TabTitle(); return; } // already added; refresh title
            // DataInt must equal our handler's index in RenderTabHandlers (that's how clicks are routed), and be
            // unique. RenderTabHandlers.Count is exactly the index our handler will occupy.
            myTabIndex = dlg.RenderTabHandlers.Count;
            myTab = new GuiTab { Name = TabTitle(), DataInt = myTabIndex };
            dlg.Tabs.Add(myTab);
            dlg.RenderTabHandlers.Add(ComposeTab);
        }

        private string TabTitle()
        {
            var custom = effectshud.Instance?.characterTabTitle;
            if (custom != null) { try { return custom(); } catch { /* fall through */ } }
            return Lang.Get("effectshud:charactertab-title");
        }

        private void ComposeTab(GuiComposer compo)
        {
            var player = capi.World?.Player?.Entity;
            var font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);
            double scale = RuntimeEnv.GUIScale;
            double rowH = font.GetFontExtents().Height * font.LineHeightMultiplier / scale;
            rowHeightUnscaled = rowH;
            double icon = Math.Max(12.0, rowH - 4.0);

            // The window keeps the size of its FIRST tab, so the room we get - width included - is whatever that
            // tab left us. Sizing to it keeps the scrollbar inside the window.
            var parent = compo.CurParentBounds;
            double panelW = Math.Max(160.0, parent?.fixedWidth > 0 ? parent.fixedWidth : FallbackWidth);
            viewHeight = Math.Max(120.0, (parent?.fixedHeight ?? 420.0) - 34.0);
            double contentW = Math.Max(80.0, panelW - ScrollbarWidth - 6.0);

            var clip = ElementBounds.Fixed(0.0, 25.0, contentW, viewHeight);
            contentBounds = clip.ForkContainingChild(0.0, 0.0, 0.0, 0.0);
            var scrollBounds = ElementBounds.Fixed(contentW + 4.0, 25.0, ScrollbarWidth, viewHeight);

            compo.BeginClip(clip)
                    .AddContainer(contentBounds, ContainerKey)
                .EndClip()
                .AddVerticalScrollbar(OnScroll, scrollBounds, ScrollbarKey);

            var container = compo.GetContainer(ContainerKey);
            if (container == null) return; // no container, no sheet - better blank than a half-built tab

            // Consumer sections (e.g. class stats) AND the "Active effects" header live in ONE text block, so the
            // header flows right after the stats at the normal line spacing — no separately-positioned element and
            // no accumulated vertical gap above it.
            string block = player != null ? BuildBlockText(player) : "";
            var eff = player != null ? BuildEffects(player) : new List<EffRow>();
            rowIcons = eff.Take(MaxRows).Select(e => e.Icon).ToArray();

            double blockH = rowH * Lines(block);
            // Both text elements are cut to a generous fixed height rather than to their current line count: their
            // texture is baked at compose time, and a later refresh with more lines would be clipped mid-sheet.
            double spare = rowH * MaxRows;

            sectionsElem = new GuiElementDynamicText(capi, block, font,
                ElementBounds.Fixed(0.0, 0.0, contentW, spare));
            container.Add(sectionsElem);

            effectsElem = new GuiElementDynamicText(capi, EffectsText(eff), font,
                ElementBounds.Fixed(icon + 8.0, blockH + 2.0, contentW - icon - 8.0, spare));
            container.Add(effectsElem);

            // Icon column: one custom-draw covering all rows, painted in local (scaled) coordinates, sharing the
            // effect rows' grid so the two columns stay aligned.
            // interactive: true, as when it was added via AddDynamicCustomDraw - that is what lets it be redrawn
            // on refresh instead of only at compose time.
            iconsElem = new GuiElementCustomDraw(capi,
                ElementBounds.Fixed(2.0, blockH + 2.0, icon + 4.0, spare), DrawIcons, true);
            container.Add(iconsElem);

            contentHeight = blockH + 2.0 + Math.Max(1, eff.Count) * rowH;
            owner = compo;
        }

        private void OnScroll(float value)
        {
            if (contentBounds == null) return;
            contentBounds.fixedY = -value;
            contentBounds.CalcWorldBounds();
        }

        private static int Lines(string text) => Math.Max(1, 1 + text.Count(c => c == '\n'));

        private static string EffectsText(List<EffRow> eff)
        {
            if (eff.Count == 0) return Lang.Get("effectshud:sheet-no-effects");
            return string.Join("\n", eff.Take(MaxRows).Select(e => e.Line));
        }

        private void OnRefresh(float dt)
        {
            if (dlg == null) return;
            EnsureTabAdded(); // backstop: idempotent, guarantees the tab exists even before the dialog is opened
            if (!dlg.IsOpened() || curTab != myTabIndex) return;
            var player = capi.World?.Player?.Entity;
            if (player == null) return;

            GuiComposer compo = dlg.Composers?[MainComposerKey];
            // Our elements belong to one composition; after a recompose (tab switch, resize) the old ones are
            // disposed, and the new ComposeTab has already replaced them.
            if (compo == null || compo != owner || sectionsElem == null) return;

            string block = BuildBlockText(player);
            sectionsElem.SetNewText(block);

            var eff = BuildEffects(player);
            rowIcons = eff.Take(MaxRows).Select(e => e.Icon).ToArray();
            effectsElem?.SetNewText(EffectsText(eff));

            // The sections block grows and shrinks (a buff comes and goes), so the effect list and its icons ride
            // just under whatever height it has now.
            double blockH = rowHeightUnscaled * Lines(block);
            MoveTo(effectsElem?.Bounds, blockH + 2.0);
            MoveTo(iconsElem?.Bounds, blockH + 2.0);
            iconsElem?.Redraw();

            // Scrollable range follows the real content, so the empty tail of the oversized text elements is not
            // scrollable and a short sheet doesn't scroll at all.
            contentHeight = blockH + 2.0 + Math.Max(1, eff.Count) * rowHeightUnscaled;
            compo.GetScrollbar(ScrollbarKey)?.SetHeights((float)viewHeight, (float)contentHeight);
        }

        // -------- content --------

        /// <summary>Sections (stats) followed by the "Active effects" header, as one block so the header flows
        /// directly under the stats with normal line spacing (no separate element, no accumulated gap).</summary>
        private string BuildBlockText(Entity player)
        {
            string sec = BuildSectionsText(player);
            string header = Lang.Get("effectshud:sheet-effects-header");
            return string.IsNullOrEmpty(sec) ? header : sec + "\n\n" + header;
        }

        private string BuildSectionsText(Entity player)
        {
            var sb = new StringBuilder();
            var sections = effectshud.Instance?.characterSheetSections;
            if (sections != null && sections.Count > 0)
            {
                foreach (var s in sections.OrderBy(s => s.Order))
                {
                    try { s.Append(sb, player); }
                    catch (Exception ex) { capi.Logger.Warning("[effectshud] character sheet section failed: {0}", ex); }
                    sb.AppendLine();
                }
            }
            return sb.ToString().TrimEnd('\n', '\r');
        }

        private struct EffRow { public string Line; public AssetLocation Icon; }

        private List<EffRow> BuildEffects(Entity player)
        {
            var rows = new List<EffRow>();
            var ebef = player.GetBehavior<EBEffectsAffected>();
            var list = ebef?.onlyClientsActiveEffects?.Values
                .Where(e => e.duration > 0 || e.infinite)
                .OrderBy(e => e.infinite ? double.MaxValue : e.duration)
                .ToList();
            if (list == null) return rows;

            foreach (var ecd in list)
            {
                string name = EffectName(ecd.typeId);
                string tier = ecd.tier > 1 ? " " + ToRoman(ecd.tier) : "";
                string time = ecd.infinite ? Lang.Get("effectshud:sheet-effect-permanent") : FormatTime(ecd.duration);
                string mag = ecd.magnitude > 0.05f ? $"  ({ecd.magnitude:0.#})" : "";
                rows.Add(new EffRow { Line = $"{name}{tier}   {time}{mag}", Icon = ResolveIcon(ecd.typeId) });
            }
            return rows;
        }

        // -------- icon drawing (custom-draw, local scaled coords) --------

        private void DrawIcons(Context ctx, ImageSurface surface, ElementBounds bounds)
        {
            var icons = rowIcons;
            if (icons == null || icons.Length == 0) return;

            double scale = RuntimeEnv.GUIScale;
            int size = (int)(Math.Max(12.0, rowHeightUnscaled - 4.0) * scale);
            int rowStep = (int)(rowHeightUnscaled * scale);
            int n = Math.Min(icons.Length, MaxRows);

            for (int i = 0; i < n; i++)
            {
                var loc = icons[i];
                if (loc == null) continue;
                int y = i * rowStep + 1;
                try
                {
                    if (loc.Path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var asset = capi.Assets.TryGet(loc);
                        if (asset != null) capi.Gui.DrawSvg(asset, surface, 0, y, size, size, null);
                    }
                    else
                    {
                        using var img = GuiElement.getImageSurfaceFromAsset(capi, loc, 255);
                        ctx.Save();
                        ctx.Translate(0, y);
                        ctx.Scale(size / (double)img.Width, size / (double)img.Height);
                        ctx.SetSourceSurface(img, 0, 0);
                        ctx.Paint();
                        ctx.Restore();
                    }
                }
                catch { /* a missing/broken icon just shows no picture; the row text still renders */ }
            }
        }

        /// <summary>Resolve an effect's icon asset the same way the HUD does: a registered custom icon (any domain),
        /// else the effectshud png convention. May point at a nonexistent asset — the draw guards against that.</summary>
        private AssetLocation ResolveIcon(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return null;
            var custom = effectshud.Instance?.effectIcons;
            if (custom != null && custom.TryGetValue(typeId, out var loc) && loc != null) return loc;
            return new AssetLocation($"effectshud:textures/effects/{typeId}.png");
        }

        // -------- helpers --------

        private static void MoveTo(ElementBounds bounds, double y)
        {
            if (bounds == null || Math.Abs(bounds.fixedY - y) < 0.01) return;
            bounds.fixedY = y;
            bounds.CalcWorldBounds();
        }

        private string EffectName(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return "?";
            var resolvers = effectshud.Instance?.effectDisplayNames;
            if (resolvers != null && resolvers.TryGetValue(typeId, out var fn) && fn != null)
            {
                try { var n = fn(); if (!string.IsNullOrEmpty(n)) return n; } catch { /* fall through */ }
            }
            string key = "effectshud:" + typeId;
            return Lang.HasTranslation(key, true, false) ? Lang.Get(key) : Humanize(typeId);
        }

        private static string FormatTime(double seconds)
        {
            int total = Math.Max(0, (int)seconds);
            return $"{total / 60}:{total % 60:D2}";
        }

        private static string Humanize(string id)
        {
            string s = id.Replace('_', ' ').Replace('-', ' ').Trim();
            if (s.Length == 0) return id;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static readonly (int value, string numeral)[] RomanMap =
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };

        private static string ToRoman(int n)
        {
            if (n <= 0) return n.ToString();
            var sb = new StringBuilder();
            foreach (var (value, numeral) in RomanMap)
                while (n >= value) { sb.Append(numeral); n -= value; }
            return sb.ToString();
        }
    }
}
