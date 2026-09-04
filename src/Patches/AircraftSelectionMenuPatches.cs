using System;
using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(AircraftSelectionMenu))]
public class AircraftSelectionMenuPatches
{
	[HarmonyPatch(nameof(AircraftSelectionMenu.SpawnPreview))]
	[HarmonyReversePatch]
	private static void SpawnPreview(object instance) => throw new NotImplementedException();

	[HarmonyPatch(nameof(AircraftSelectionMenu.SpawnPreview))]
	[HarmonyPrefix]
	private static bool SpawnPreview_Prefix(AircraftSelectionMenu __instance)
	{
		if (!__instance.aircraftSelection[__instance.selectionIndex]?.IsShipDefinition() ?? true) return true;
		var origTransform = __instance.airbase.aircraftSelectionTransform;
		// __instance.airbase.aircraftSelectionTransform = figure this out
		SpawnPreview(__instance);
		__instance.airbase.aircraftSelectionTransform = origTransform;
		return false;
	}
}