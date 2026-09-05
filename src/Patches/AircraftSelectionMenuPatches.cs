using System;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

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
		if (!__instance.aircraftSelection[__instance.selectionIndex]?.IsShipDefinition() ?? true)
		{
			CameraStateManager.i.FocusAirbase(__instance.airbase, false);
			return true;
		}
		var origTransform = __instance.airbase.aircraftSelectionTransform;
		var go = new GameObject("TempPreview");
		go.layer = PhysicsLayers.Statics;
		go.transform.position = new GlobalPosition(100000, 0, 0).ToLocalPosition();
		var collider = go.AddComponent<BoxCollider>();
		collider.isTrigger = true;
		collider.size = new Vector3(100, 1, 100);
		__instance.airbase.aircraftSelectionTransform = go.transform;
		CameraStateManager.i.FocusAirbase(__instance.airbase, false);
		SpawnPreview(__instance);
		__instance.airbase.aircraftSelectionTransform = origTransform;
		Object.Destroy(go);
		if (__instance.previewAircraft == null)
		{
			SpawnPreview(__instance); //fallback
		}
		return false;
	}
}