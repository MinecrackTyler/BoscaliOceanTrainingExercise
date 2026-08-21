using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using NOComponentWIP.ServerConfig;
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

	private static Transform cargoValue;

	private static TMP_Text cargoCostText;

	private static Image cargoUnaffordable;
	
	[HarmonyPatch(nameof(AircraftSelectionMenu.Initialize))]
	[HarmonyPrefix]
	static void Initialize_Prefix(AircraftSelectionMenu __instance)
	{
		// Cargo Button Setup

		var rightPanel = __instance.transform.Find("LowRow")?.Find("RightPanel");
		if (rightPanel == null) return;
		var infoPanel = rightPanel.Find("InfoPanel");
		if (infoPanel == null) return;
		var container = infoPanel.Find("Container");
		if (container == null) return;
		container.GetComponent<VerticalLayoutGroup>()?.spacing = 5f;
		container.GetComponent<VerticalLayoutGroup>()?.padding = new RectOffset(10, 10, 5, 5);
		var sDelta = rightPanel.GetComponent<RectTransform>()?.sizeDelta ?? new  Vector2(300f, 300f);
		rightPanel.GetComponent<RectTransform>()?.sizeDelta = new Vector2(sDelta.x, sDelta.y + 25f);
		if (!infoPanel.TryGetComponent<VerticalLayoutGroup>(out var vls))
		{
			vls = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
		}
		vls.childControlWidth = true;
		vls.childControlHeight = true;
		vls.padding = new RectOffset(5, 5, 5, 5);
		
		var flyButton = infoPanel.Find("FlyButton")?.GetComponent<Button>();
		if (flyButton == null) return;

		flyButton.onClick.SetPersistentListenerState(0,  UnityEventCallState.Off);
		flyButton.onClick.RemoveAllListeners();
		flyButton.onClick.AddListener(() => OnFlyButtonClicked(__instance));
		
		if (flyButton.TryGetComponent<LayoutElement>(out var layoutElement))
		{
			layoutElement.ignoreLayout = false;
		}
		
		if (newButton == null)
		{
			newButton = Object.Instantiate(flyButton.transform, infoPanel);
			newButton.SetSiblingIndex(1);

			var text = newButton.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
			if (text != null) text.text = "Cargo Options >";
			if (text != null) text.enableWordWrapping = false;

			var cargoBtn = newButton.GetComponent<Button>();
			cargoBtn.onClick.RemoveAllListeners();
			cargoBtn.onClick.SetPersistentListenerState(0,  UnityEventCallState.Off);
			cargoBtn.onClick.AddListener(() => SpawnUI(__instance));
		}

		newButton?.gameObject.SetActive(false);

		LoadoutBridge.onLoadoutChange += __instance.AircraftSelectionMenu_OnChange;

		// Cargo Cost Setup
		
		var loadoutValue = container.Find("LoadoutValue");
		
		if (cargoValue == null && loadoutValue != null)
		{
		
			cargoValue = Object.Instantiate(loadoutValue.transform, container);
			cargoValue.SetSiblingIndex(2);
		
			var cargoText = cargoValue.GetComponent<TMP_Text>();
			cargoText?.text = "Cargo Value :";
		
			cargoCostText = cargoValue.Find("LoadoutValue").GetComponent<TMP_Text>();
			cargoUnaffordable = cargoValue.Find("LoadoutUnaffordable").GetComponent<Image>();
		}
		
		cargoValue?.gameObject.SetActive(false);
	}

	[HarmonyPatch(nameof(AircraftSelectionMenu.SetSelectedType))]
	[HarmonyPostfix]
	private static void SetSelectedType_Postfix(AircraftSelectionMenu __instance)
	{
		LoadoutBridge.Clear();
	}

	[HarmonyPatch(nameof(AircraftSelectionMenu.UpdateReadouts))]
	[HarmonyPrefix]
	private static bool UpdateReadouts_Prefix(AircraftSelectionMenu __instance)
	{
		if (__instance.previewAircraft == null || !selected || !UnitConfig.UnitEconomy()) return true;

		var menu = __instance;
		
		if (menu.previewAircraft == null || CameraStateManager.cameraMode != CameraMode.selection)
		{
			menu.flyButton.interactable = false;
			return false;
		}
		menu.RCS.text = $"{menu.previewAircraft.RCS:F4}";
		menu.loadoutCost = menu.weaponManager.GetCurrentValue(includeCargo: true);
		menu.loadoutValue.text = UnitConverter.ValueReading(menu.loadoutCost);
		var cargoCost = LoadoutBridge.CalculateCost(LoadoutBridge.Manifest);
		cargoCostText.text = UnitConverter.ValueReading(cargoCost);
		
		menu.loadoutWeight.text = UnitConverter.WeightReading(menu.weaponManager.GetCurrentMass());
		menu.fuelWeight.text = UnitConverter.WeightReading(menu.previewAircraft.GetFuelQuantity()) ?? "";
		if (menu.selectedType != null)
		{
			menu.playerAllocation.text = UnitConverter.ValueReading(menu.localPlayer.Allocation);
			menu.insufficientFunds.SetActive(menu.localPlayer.Allocation < menu.selectedType.value);

			bool isLoadoutUnaffordable = menu.localPlayer.Allocation < menu.loadoutCost;
			bool isCargoUnaffordable = menu.localPlayer.Allocation - menu.loadoutCost < cargoCost;

			if (isLoadoutUnaffordable)
			{
				menu.loadoutUnaffordable.enabled = true;
				if (cargoUnaffordable != null) cargoUnaffordable.enabled = true;
			}
			else if (isCargoUnaffordable)
			{
				menu.loadoutUnaffordable.enabled = false;
				if (cargoUnaffordable != null) cargoUnaffordable.enabled = true;
			}
			else
			{
				menu.loadoutUnaffordable.enabled = false;
				if (cargoUnaffordable != null) cargoUnaffordable.enabled = false;
			}
			
			menu.insufficientRank.enabled = menu.localPlayer.PlayerRank < menu.selectedType.aircraftParameters.rankRequired;
			menu.CheckWarheads();
			menu.cachedGrossWeight = 0f;
			foreach (UnitPart allPart in menu.previewAircraft.GetAllParts())
			{
				menu.cachedGrossWeight += allPart.mass;
			}
			menu.grossWeight.text = UnitConverter.WeightReading(menu.cachedGrossWeight) + " / " + UnitConverter.WeightReading(menu.aircraftInfo.maxWeight);
			menu.aircraftName.text = ((menu.selectionIndex < menu.aircraftSelection.Count) ? menu.aircraftSelection[menu.selectionIndex].unitName : "None");
			menu.TWR.text = "0";
			float maxThrust;
			if (menu.previewAircraft.GetMaxPower(out var maxPower))
			{
				menu.ShowPWR(maxPower, menu.cachedGrossWeight);
			}
			else if (menu.previewAircraft.GetMaxThrust(out maxThrust))
			{
				menu.ShowTWR(maxThrust, menu.cachedGrossWeight);
			}
		}
		else
		{
			menu.aircraftName.text = "No aircraft available";
			menu.loadoutUnaffordable.enabled = false;
			menu.overWeight.enabled = false;
			menu.insufficientRank.enabled = false;
		}
		menu.selectionLight.enabled = NetworkSceneSingleton<LevelInfo>.i.timeOfDay < 6f || NetworkSceneSingleton<LevelInfo>.i.timeOfDay > 18f;
		menu.selectionLight.transform.position = menu.previewAircraft.transform.position + Vector3.up * 20f + Vector3.forward * 10f;
		menu.selectionLight.transform.LookAt(menu.previewAircraft.transform.position);
		
		return false;
	}

	[HarmonyPatch(nameof(AircraftSelectionMenu.Update))]
	[HarmonyPrefix]
	private static bool Update_Prefix(AircraftSelectionMenu __instance)
	{
		if (!selected || !UnitConfig.UnitEconomy()) return true;

		var menu = __instance;
		
		if (menu.airbase.UnitDestroyed())
		{
			menu.ReturnToMap();
			return false;
		}
		menu.playerAllocation.text = UnitConverter.ValueReading(menu.localPlayer.Allocation);
		menu.contributeButton.interactable = menu.localPlayer.Allocation > 0f;
		menu.previewAircraft.GetInputs().brake = 1f;
		int num = menu.airbase.GetWarheads();
		bool active = NetworkSceneSingleton<MissionManager>.i.currentEscalation >= NetworkSceneSingleton<MissionManager>.i.tacticalThreshold;
		menu.warheads.text = $"{menu.airbase.CurrentHQ.GetWarheadStockpile()}";
		menu.warheadsAtAirbasePanel.SetActive(active);
		menu.warheadsAtAirbase.text = $"   x {num}";
		menu.factionFunds.text = UnitConverter.ValueReading(menu.airbase.CurrentHQ.factionFunds) ?? "";
		menu.factionScore.text = $"{menu.airbase.CurrentHQ.factionScore:F1}";
		menu.overWeight.enabled = menu.cachedGrossWeight > menu.aircraftInfo.maxWeight;
		if (menu.localPlayer.OwnsAirframe(menu.selectedType, includeReserved: true))
		{
			menu.flyButton.interactable = !menu.loadoutUnaffordable.enabled && !menu.insufficientWarheads.enabled && !cargoUnaffordable.enabled;;
		}
		else
		{
			menu.flyButton.interactable = false;
		}
		menu.DisplayEscalationPointer();
		if (GameManager.playerInput.GetButton("Pause") || Input.GetKeyDown(KeyCode.Escape) || menu.localPlayer.HQ != menu.airbase.CurrentHQ)
		{
			menu.ReturnToMap();
		}

		return false;
	}

	private static void OnFlyButtonClicked(AircraftSelectionMenu menu)
	{
		if (selected && !LoadoutBridge.LoadoutSet)
		{
			LoadoutBridge.SetLoadout(new(), false);
		}

		if (uiInstance != null)
		{
			var controller = uiInstance.GetComponent<CargoUIController>();
			controller?.Close();
		}
		menu?.FlyAircraft();
	}
	
	private static bool selected = false;

	[HarmonyPatch(nameof(AircraftSelectionMenu.SpawnPreview))]
	[HarmonyPostfix]
	static void SpawnPreview_Postfix(AircraftSelectionMenu __instance)
	{
		if (ModAssets.i.ShipDefinitionsWithDeployer.Contains(__instance.previewAircraft?.definition))
		{
			
			newButton?.gameObject.SetActive(true);
			cargoValue?.gameObject.SetActive(UnitConfig.UnitEconomy());
			selected = true;
		}
		else
		{
			newButton?.gameObject.SetActive(false);
			cargoValue?.gameObject.SetActive(false);
			selected = false;
		}
		LoadoutBridge.Clear();
	}

	private static void SpawnUI(AircraftSelectionMenu menu)
	{
		if (uiInstance != null) return;
		
		Canvas rootCanvas = menu.GetComponentInParent<Canvas>();
		if (rootCanvas == null)
		{
			Plugin.Logger.LogError("Could not find a Canvas to spawn the UI on.");
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
			Plugin.Logger.LogError("UI Spawned but CargoUIController or DeploymentManager is missing!");
		}
	}
}

[HarmonyPatch(typeof(PilotPlayerState), nameof(PilotPlayerState.PlayerAxisControls))]
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

[HarmonyPatch(typeof(Airbase), nameof(Airbase.CanSpawnAircraft))]
public static class CanSpawnAircraftPatch
{
	[HarmonyPrefix]
	private static bool Prefix(Airbase __instance, AircraftDefinition definition, ref bool __result)
	{
		var filter = __instance.GetComponent<AirbaseAIFilter>();
		if (filter == null) return true;
		
		for (int i = 1; i <= 4; i++)
		{
			var frame = new StackFrame(i, false);
			var method = frame.GetMethod();
			if (method != null && method.Name.Contains("FlyAircraftAsync"))
			{
				return true;
			}
		}

		if (filter.CanSpawnAircraft(definition.jsonKey)) return true;

		__result = false;
		return false;
	}
}
