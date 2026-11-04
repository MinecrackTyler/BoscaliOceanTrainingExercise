using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NuclearOption.Jobs;
using NuclearOption.NetworkTransforms;
using UnityEngine;

namespace BoscaliOceanTrainingExercise.Patches;

[HarmonyPatch(typeof(AeroPart))]
public class AeroPartPatches
{
    private static FieldInfo ParticleEffectManager_i =
        AccessTools.Field(typeof(SceneSingleton<ParticleEffectManager>), nameof(SceneSingleton<ParticleEffectManager>.i));
    
    private static MethodInfo IsShipMethod = AccessTools.Method(typeof(AeroPartPatches), nameof(IsShip));
    
    private static MethodInfo Object_opInequality = AccessTools.Method(typeof(Object), "op_Inequality", new[] { typeof(Object), typeof(Object) });

    public static bool IsShip(AeroPart part)
    {
        return part.parentUnit.definition.IsShipDefinition();
    }
    
    [HarmonyPatch(nameof(AeroPart.ApplyJobFields))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ApplyJobFields_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldsfld, ParticleEffectManager_i),
            new CodeMatch(OpCodes.Ldnull),
            new CodeMatch(OpCodes.Call, Object_opInequality),
            new CodeMatch(o => o.opcode == OpCodes.Brfalse || o.opcode == OpCodes.Brfalse_S)
        );

        if (matcher.IsInvalid)
        {
            Plugin.Logger.LogError("Failed to find match in AeroPart.ApplyJobFields");
            return matcher.InstructionEnumeration();
        }

        matcher.Advance(3);
        
        var jumpLabel = (Label)matcher.Operand;

        matcher.Advance(1);

        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, IsShipMethod),
            new CodeInstruction(OpCodes.Brfalse, jumpLabel)
        );
        
        return matcher.InstructionEnumeration();
    }
}