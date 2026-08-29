using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(PilotPlayerState))]
public static class PilotPlayerStatePatches
{
	[HarmonyPatch(nameof(PilotPlayerState.PlayerControls))]
	[HarmonyPostfix]
	private static void PlayerControls_Postfix(PilotPlayerState __instance)
	{
		if (!GameManager.flightControlsEnabled || __instance.pilotStrength < 0.2f) return;
		if (!ModAssets.i.ShipDefinitions.Contains(__instance.pilot.aircraft.definition)) return;

		if (__instance.player.GetButton("Countermeasures") && !__instance.pilot.aircraft.countermeasureTrigger)
		{
			__instance.pilot.aircraft.Countermeasures(true, __instance.pilot.aircraft.countermeasureManager.activeIndex);
		}

		if (__instance.player.GetButtonDown("Gear"))
		{
			if (__instance.pilot.aircraft.gearState == LandingGear.GearState.LockedExtended)
			{
				__instance.pilot.aircraft.SetGear(deployed: false);
			}
			else if (__instance.pilot.aircraft.gearState == LandingGear.GearState.LockedRetracted)
			{
				__instance.pilot.aircraft.SetGear(deployed: true);
			}
		}
	}
}