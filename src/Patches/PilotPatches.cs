using HarmonyLib;
using NuclearOption.Jobs;

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
	
	[HarmonyPatch(nameof(Pilot.TakeWaterDamage))]
	[HarmonyPrefix]
	private static bool TakeWaterDamage_Prefix(Pilot __instance)
	{
		return !IsDeepUnderwaterBotePilot(__instance);
	}
	
	[HarmonyPatch(nameof(Pilot.Pilot_OnAeroInputsApplied))]
	[HarmonyPostfix]
	private static void Pilot_OnAeroInputsApplied_Postfix(Pilot __instance, ref PartResult __result)
	{
		if (__result == PartResult.Remove && IsDeepUnderwaterBotePilot(__instance))
			__result = PartResult.None;
	}
	
	private static bool IsDeepUnderwaterBotePilot(Pilot pilot)
	{
		if (pilot == null || pilot.dead || pilot.ejected) return false;
		var aircraft = pilot.aircraft;
		return aircraft != null && aircraft.LocalSim && aircraft.TryGetShipBridge(out _)
		       && pilot.transform.position.y < Datum.LocalSeaY - 10f;
	}
}