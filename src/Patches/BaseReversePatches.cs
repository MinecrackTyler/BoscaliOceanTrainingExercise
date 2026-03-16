using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch]
public static class BaseReversePatches
{
	[HarmonyReversePatch]
	[HarmonyPatch(typeof(UnitPart), "Awake")]
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void UnitPartAwake(UnitPart instance) { }
	
	[HarmonyReversePatch]
	[HarmonyPatch(typeof(UnitPart), "UnitPart_OnParentDetached")]
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void UnitPartOnParentDetached(UnitPart instance, UnitPart parentPart) { }
	
	[HarmonyReversePatch]
	[HarmonyPatch(typeof(UnitPart), "Detach")]
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void UnitPartDetach(UnitPart instance, Vector3 velocity, Vector3 relativePos) { }

}