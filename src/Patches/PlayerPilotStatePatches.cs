using HarmonyLib;
using UnityEngine;

namespace BoscaliOceanTrainingExercise.Patches;

[HarmonyPatch(typeof(PilotPlayerState))]
public static class PilotPlayerStatePatches
{
	[HarmonyPatch(nameof(PilotPlayerState.PlayerControls))]
	[HarmonyPostfix]
	private static void PlayerControls_Postfix(PilotPlayerState __instance)
	{
		if (!GameManager.flightControlsEnabled || __instance.pilotStrength < 0.2f) return;
		if (!__instance.pilot.aircraft.definition.IsShipDefinition()) return;
		
		var pilot = __instance.pilot;
		var aircraft = pilot.aircraft;
		var player = __instance.player;

		if (player.GetButton("Countermeasures") && !aircraft.countermeasureTrigger)
		{
			aircraft.Countermeasures(true, aircraft.countermeasureManager.activeIndex);
		}

		if (player.GetButtonDown("Gear"))
		{
			if (aircraft.gearState == LandingGear.GearState.LockedExtended)
			{
				aircraft.SetGear(deployed: false);
			}
			else if (aircraft.gearState == LandingGear.GearState.LockedRetracted)
			{
				aircraft.SetGear(deployed: true);
			}
		}
	}
	
	[HarmonyPatch(typeof(PilotPlayerState), nameof(PilotPlayerState.UpdateState))]
	[HarmonyPrefix]
	private static bool UpdateState_Prefix(Pilot pilot)
	{
		var aircraft = pilot?.aircraft;
		
		if (aircraft == null || !GameManager.IsLocalAircraft(aircraft) || !aircraft.TryGetShipBridge(out var bridge)
		    || !GameManager.playerInput.GetButtonDown("Eject"))
			return true;
		
		if (!bridge.CombatDisembarkLocked)
		{
			bridge.ClearDisembarkConfirmation();
			return true;
		}
		
		if (bridge.ConfirmDisembarkConfirmation())
			return true;
		
		bridge.ArmDisembarkConfirmation();
		var remaining = bridge.CombatDisembarkRemaining;
		var totalSeconds = Mathf.CeilToInt(remaining);
		var minutes = totalSeconds / 60;
		var seconds = totalSeconds % 60;
		
		SceneSingleton<AircraftActionsReport>.i.ReportText(
			$"<b>COMBAT TIMER ACTIVE ({minutes}:{seconds:00})</b>\n" +
			"Disembarking now will not return ship to inventory!\n" +
			"Press eject again within 10 seconds to confirm.", 9.5f);
		
		return false;
	}
}