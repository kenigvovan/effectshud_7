using System.Text;
using Vintagestory.API.Common.Entities;

namespace effectshud.src
{
    /// <summary>
    /// A section a consumer mod contributes to the character-dialog tab that effectshud adds (the "Effects" tab on
    /// the vanilla C screen). Sections render ABOVE the built-in active-effects list. Register instances with
    /// <see cref="effectshud.RegisterCharacterSheetSection"/>; effectshud calls <see cref="Append"/> every refresh
    /// while the tab is open, and the section writes its own lines (including its own header/localization).
    ///
    /// This is the extension point that keeps effectshud generic: effectshud DEFINES this interface and calls it,
    /// consumer mods IMPLEMENT it. So a mod can inject its own stats (class, level, resistances, …) without effectshud
    /// ever referencing that mod — no reverse dependency, no circular assembly reference.
    /// </summary>
    public interface ICharacterSheetSection
    {
        /// <summary>Sort order among registered sections (lower renders higher up). The effect list is always last.</summary>
        double Order { get; }

        /// <summary>Append this section's lines to <paramref name="sb"/> for the given player entity. Called on the
        /// client each refresh (~0.5s) while the tab is open; keep it cheap and side-effect free.</summary>
        void Append(StringBuilder sb, Entity player);
    }
}
