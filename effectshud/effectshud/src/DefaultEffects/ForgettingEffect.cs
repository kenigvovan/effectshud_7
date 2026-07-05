using Vintagestory.API.Common;
using Vintagestory.API.Server;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.Forgetting, shouldBeRendered: false)]
    public class ForgettingEffect: Effect
    {
        public override void OnStart()
        {
            var serverPlayer = (entity as EntityPlayer)?.Player as IServerPlayer;
            if (serverPlayer == null)
            {
                return;
            }

            // The vanilla CharacterSystem stores the "already selected a character" flag in the
            // player moddata (IServerPlayer.GetModdata/SetModData), NOT in WorldData. Resetting it
            // here makes the create-character dialog reopen (and the class reset) on the next join.
            serverPlayer.SetModData<bool>("createCharacter", false);

            // The vanilla .charsel command is gated behind this attribute in survival mode
            // (see CharacterSystem.onCharSelCmd). Granting it lets the player use .charsel right now.
            serverPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
            serverPlayer.Entity.MarkTagsDirty();
            // Ask the owning client to open the character selection dialog immediately.
            effectshud.Instance?.serverChannel?.SendPacket(new OpenCharSelPacket(), serverPlayer);
        }
    }
}
