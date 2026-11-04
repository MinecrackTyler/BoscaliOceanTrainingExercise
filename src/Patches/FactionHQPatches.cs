using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Mirage.Collections;
using NuclearOption.SavedMission;

namespace BoscaliOceanTrainingExercise.Patches;

[HarmonyPatch(typeof(FactionHQ))]
public static class FactionHQPatches
{
    [HarmonyPatch(nameof(FactionHQ.DeployAIAircraft))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DeployAIAircraft_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        CodeMatcher matcher = new CodeMatcher(instructions, il);
        
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(SyncIDictionary<AircraftDefinition, FactionHQ.RuntimeSupply>), nameof(SyncIDictionary<,>.TryGetValue)))
        );

        if (matcher.IsInvalid)
        {
            Plugin.Logger.LogError("Failed to find match in FactionHQ.DeployAIAircraft");
            return matcher.InstructionEnumeration();
        }
        
        matcher.Advance(1);
        Label continueLabel = (Label)matcher.Instruction.operand;
        
        matcher.Start()
               .MatchForward(false,
                   new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(List<AircraftDefinition>.Enumerator), "get_Current"))
               );

        if (matcher.IsInvalid)
        {
            Plugin.Logger.LogError("Failed to find match in FactionHQ.DeployAIAircraft");
            return matcher.InstructionEnumeration();
        }
        
        matcher.Advance(1);
        CodeInstruction storeInstruction = matcher.Instruction;

        
        matcher.Advance(1);
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_S, storeInstruction.operand),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnitDefinitionExtensions), nameof(UnitDefinitionExtensions.IsShipDefinition))),
            new CodeInstruction(OpCodes.Brtrue, continueLabel)
        );

        return matcher.InstructionEnumeration();
    }
}