using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.GameContent;

namespace effectshud.src
{
    [HarmonyPatch]
    public class TemporalChargePatch
    {
        public static double addNameAndProcessMadeTool(EntityBehaviorTemporalStabilityAffected ebtsa, double gain)
        {
            var tmpVal = ebtsa.entity.Stats.GetBlended("cantemporalcharge");
            if (tmpVal == 1)
            {
                return gain;
            }
            if(gain >= 0)
            {
                gain *= tmpVal;
            }
            else
            {
                gain /= tmpVal;
            }
            return gain;
        }

        public static IEnumerable<CodeInstruction> Prefix_EntityBehaviorTemporalStabilityAffected(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            var codes = new List<CodeInstruction>(instructions);

            var proxyMethod = AccessTools.Method(typeof(TemporalChargePatch), "addNameAndProcessMadeTool");
            for (int i = 0; i < codes.Count; i++)
            {
                if (!found &&
                    codes[i].opcode == OpCodes.Ldloc_2 && codes[i + 1].opcode == OpCodes.Add && codes[i + 2].opcode == OpCodes.Ldc_R8 && codes[i - 1].opcode == OpCodes.Call)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return codes[i];
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                    found = true;
                    continue;
                }
                yield return codes[i];
            }
        }
    }
}
