using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NOComponentWIP;

[HarmonyPatch(typeof(AircraftSelectionMenu))]
public static class AircraftSelectionMenuPatch
{
	private static GameObject uiInstance;
	
	private static Transform newButton;
	
	[HarmonyPatch("Initialize")]
	[HarmonyPrefix]
	static void Prefix(AircraftSelectionMenu __instance)
	{
		var infoPanel = __instance.transform.Find("LowRow")?.Find("RightPanel")?.Find("InfoPanel");
		if (infoPanel == null) return;
		var container = infoPanel.Find("Container");
		if (container == null) return;
		container.GetComponent<VerticalLayoutGroup>()?.spacing = 5f;
		var vlg = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
		vlg.childControlWidth = true;
		vlg.childControlHeight = true;
		vlg.padding = new RectOffset(5, 5, 5, 5);
		
		var flyButton = infoPanel.Find("FlyButton");
		if (flyButton == null) return;
		flyButton.GetComponent<Button>().onClick.AddListener(() =>
		{
			if (selected) LoadoutBridge.LoadoutSet = true;
		});
		flyButton.GetComponent<LayoutElement>()?.ignoreLayout = false;
		newButton = Object.Instantiate(flyButton, infoPanel);
		newButton.SetSiblingIndex(1);
		var text = newButton.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
		text?.text = "Cargo Options >";
		newButton.GetComponent<Button>().onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
		newButton.GetComponent<Button>().onClick.AddListener(() => {
			SpawnUI(__instance);
		});
		newButton.gameObject.SetActive(false);
	}
	
	private static List<String> nameList = ["LandingKraft", "Destroyer1_Player", "FleetKarrier"];
	private static bool selected = false;

	[HarmonyPatch("SpawnPreview")]
	[HarmonyPostfix]
	static void Postfix(AircraftSelectionMenu __instance)
	{
		if (nameList.Contains(__instance?.previewAircraft?.definition?.jsonKey))
		{
			newButton?.gameObject.SetActive(true);
			selected = true;
		}
		else
		{
			newButton?.gameObject.SetActive(false);
			selected = false;
		}
	}

	private static void SpawnUI(AircraftSelectionMenu menu)
	{
		if (uiInstance != null) return;
		
		Canvas rootCanvas = menu.GetComponentInParent<Canvas>();
		if (rootCanvas == null)
		{
			Debug.LogError("[BOAT] Could not find a Canvas to spawn the UI on.");
			return;
		}
		
		uiInstance = Object.Instantiate(ModAssets.i.CargoEditorUI, rootCanvas.transform);
		uiInstance.transform.SetAsLastSibling();
		
		var controller = uiInstance.GetComponent<CargoUIController>();
		var manager = menu.previewAircraft?.GetComponent<DeploymentManager>();

		if (controller != null && manager != null)
		{
			controller.Initialize(manager);
		}
		else
		{
			Debug.LogError("[BOAT] UI Spawned but ManifestUIController or DeploymentManager is missing!");
		}
	}
}

[HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
public static class ControlPatch
{
	[HarmonyPrefix]
	private static bool Prefix(PilotPlayerState __instance)
	{
		if (LoadoutBridge.BlockInputs)
		{
			var pps = __instance;
			pps.controlInputs.brake = 0f;
			pps.controlInputs.yaw = 0f;
			pps.controlInputs.pitch = 0f;
			pps.controlInputs.roll = 0f;
			pps.controlInputs.customAxis1 = 0.5f;
			pps.controlInputs.throttle = 0f;
			return false;
		}

		return true;
	}
}

[HarmonyPatch(typeof(Airbase), "CanSpawnAircraft")]
public static class CanSpawnAircraftPatch
{
	[HarmonyPrefix]
	private static bool Prefix(Airbase __instance, AircraftDefinition definition, ref bool __result)
	{
		var filter = __instance.GetComponent<AirbaseAIFilter>();
		if (filter == null) return true;

		System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();

		for (int i = 1; i < 5; i++)
		{
			var method = stackTrace.GetFrame(i).GetMethod();
			if (method.Name.Contains("FlyAircraftAsync")) return true;
		}

		if (filter.CanSpawnAircraft(definition.jsonKey)) return true;
		__result = false;
		return false;

	}
}
