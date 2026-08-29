using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(UnitPart), nameof(UnitPart.Awake))]
public static class UnitPartPatches
{
	[HarmonyPrefix]
	private static void Awake_Prefix(UnitPart __instance)
	{
		if (__instance is AeroPart part && part.joints.Length > 0) return;
		if (__instance.parentUnit == null && __instance.transform.parent != null)
		{
			__instance.parentUnit = __instance.transform.parent.GetComponentInParentWithDepth<UnitPart>(6)?.parentUnit;
		}
	}
}