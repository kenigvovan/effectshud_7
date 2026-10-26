using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace effectshud.src
{
    /// <summary>
    /// Server command <c>/ef</c>:
    /// <c>add &lt;effect&gt; &lt;minutes&gt; &lt;tier&gt; &lt;player&gt;</c>, <c>remove &lt;effect&gt; &lt;player&gt;</c>,
    /// <c>clear &lt;player&gt;</c>, <c>list [player]</c>. The legacy short form
    /// <c>/ef &lt;effect&gt; &lt;minutes&gt; &lt;tier&gt; &lt;player&gt;</c> still works as <c>add</c>.
    /// </summary>
    public static class EffectsCommands
    {
        public static void Register(ICoreServerAPI api)
        {
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.Create("ef")
                .WithDescription("Manage status effects")
                .RequiresPrivilege(Privilege.controlserver)
                .IgnoreAdditionalArgs()
                .HandleWith(OnLegacyAdd)
                .BeginSubCommand("add")
                    .WithDescription("Apply an effect to an online player")
                    .WithArgs(parsers.Word("effect"), parsers.Int("minutes"), parsers.Int("tier"), parsers.Word("player"))
                    .HandleWith(args => Add(api, args, (string)args[0], (int)args[1], (int)args[2], (string)args[3]))
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove one effect from an online player")
                    .WithArgs(parsers.Word("effect"), parsers.Word("player"))
                    .HandleWith(args => Remove(api, args, (string)args[0], (string)args[1]))
                .EndSubCommand()
                .BeginSubCommand("clear")
                    .WithDescription("Remove all effects from an online player")
                    .WithArgs(parsers.Word("player"))
                    .HandleWith(args => Clear(api, args, (string)args[0]))
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List active effects of an online player (default: yourself)")
                    .WithArgs(parsers.OptionalWord("player"))
                    .HandleWith(args => List(api, args, (string)args[0]))
                .EndSubCommand();
        }

        private static TextCommandResult OnLegacyAdd(TextCommandCallingArgs args)
        {
            var sapi = effectshud.ServerSideApi;
            if (args.RawArgs.Length < 4
                || !int.TryParse(args.RawArgs[1], out int minutes)
                || !int.TryParse(args.RawArgs[2], out int tier))
            {
                return TextCommandResult.Error(L(args, "cmd-usage"));
            }
            return Add(sapi, args, args.RawArgs[0], minutes, tier, args.RawArgs[3]);
        }

        private static TextCommandResult Add(ICoreServerAPI sapi, TextCommandCallingArgs args, string effectId, int minutes, int tier, string playerName)
        {
            if (args.Caller.Player is IServerPlayer caller && caller.WorldData.CurrentGameMode != EnumGameMode.Creative)
                return TextCommandResult.Error(L(args, "cmd-creative-only"));

            var mod = effectshud.Instance;
            if (!mod.effects.TryGetValue(effectId, out Type effectType))
                return TextCommandResult.Error(L(args, "cmd-unknown-effect", effectId));
            if (minutes < 1 || tier < 1)
                return TextCommandResult.Error(L(args, "cmd-usage"));

            var target = FindPlayer(sapi, playerName);
            if (target?.Entity == null)
                return TextCommandResult.Error(L(args, "cmd-player-not-found", playerName));

            Effect ef = (Effect)Activator.CreateInstance(effectType);
            ef.SetExpiryInRealMinutes(minutes);
            ef.Tier = tier;
            ef.positive = mod.effectsPosNeg.TryGetValue(ef.effectTypeId, out bool posneg) ? posneg : true;
            if (!effectshud.ApplyEffectOnEntity(target.Entity, ef))
                return TextCommandResult.Error(L(args, "cmd-no-behavior", target.PlayerName));

            return TextCommandResult.Success(L(args, "effect-set-to-player-tier-duration", EffectName(args, effectId), target.PlayerName, tier, minutes));
        }

        private static TextCommandResult Remove(ICoreServerAPI sapi, TextCommandCallingArgs args, string effectId, string playerName)
        {
            var target = FindPlayer(sapi, playerName);
            if (target?.Entity == null)
                return TextCommandResult.Error(L(args, "cmd-player-not-found", playerName));

            if (!effectshud.RemoveEffectFromEntity(target.Entity, effectId))
                return TextCommandResult.Error(L(args, "cmd-effect-not-active", EffectName(args, effectId), target.PlayerName));

            return TextCommandResult.Success(L(args, "cmd-effect-removed", EffectName(args, effectId), target.PlayerName));
        }

        private static TextCommandResult Clear(ICoreServerAPI sapi, TextCommandCallingArgs args, string playerName)
        {
            var target = FindPlayer(sapi, playerName);
            if (target?.Entity == null)
                return TextCommandResult.Error(L(args, "cmd-player-not-found", playerName));

            var ebea = target.Entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null)
                return TextCommandResult.Error(L(args, "cmd-no-behavior", target.PlayerName));

            int removed = ebea.RemoveAllEffects();
            return TextCommandResult.Success(L(args, "cmd-effects-cleared", removed, target.PlayerName));
        }

        private static TextCommandResult List(ICoreServerAPI sapi, TextCommandCallingArgs args, string playerName)
        {
            IPlayer target = playerName != null ? FindPlayer(sapi, playerName) : args.Caller.Player;
            if (target?.Entity == null)
                return TextCommandResult.Error(L(args, playerName != null ? "cmd-player-not-found" : "cmd-usage", playerName));

            var ebea = target.Entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null || ebea.activeEffects.Count == 0)
                return TextCommandResult.Success(L(args, "cmd-list-empty", target.PlayerName));

            var sb = new StringBuilder(L(args, "cmd-list-header", target.PlayerName));
            foreach (var ef in ebea.activeEffects.Values.OrderBy(e => e.RemainingSeconds()))
            {
                string tier = ef.Tier > 1 ? " " + EffectDisplayUtil.ToRoman(ef.Tier) : "";
                string time = ef.infinite ? L(args, "sheet-effect-permanent") : EffectDisplayUtil.FormatTime(ef.RemainingSeconds());
                sb.Append("\n- ").Append(EffectName(args, ef.effectTypeId)).Append(tier).Append("  ").Append(time);
            }
            return TextCommandResult.Success(sb.ToString());
        }

        private static IPlayer FindPlayer(ICoreServerAPI sapi, string name)
            => sapi.World.AllOnlinePlayers.FirstOrDefault(p => p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase));

        private static string LangCode(TextCommandCallingArgs args)
            => (args.Caller.Player as IServerPlayer)?.LanguageCode ?? Lang.CurrentLocale;

        private static string L(TextCommandCallingArgs args, string key, params object[] p)
            => Lang.GetL(LangCode(args), "effectshud:" + key, p);

        private static string EffectName(TextCommandCallingArgs args, string effectId)
        {
            string key = "effectshud:" + effectId;
            return Lang.HasTranslation(key, true, false) ? Lang.GetL(LangCode(args), key) : effectId;
        }
    }
}
