using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Pilot))]
public static class PilotPatches
{
	[HarmonyPatch(nameof(Pilot.ApplyDamage))]
	[HarmonyPrefix]
	private static void ApplyDamage_Prefix(Pilot __instance, ref float impactDamage)
	{
		if (__instance.aircraft != null && ModAssets.i.ShipDefinitions.Contains(__instance.aircraft.definition))
			impactDamage = 0f;
	}
}