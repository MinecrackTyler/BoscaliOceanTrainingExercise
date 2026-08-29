using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Systems;

[CreateAssetMenu(fileName = "New Radial Menu", menuName = "CustomActions/RadialMenu")]
public class RadialMenu : ScriptableObject
{
	public static List<RadialMenuAction> originalActions;
	
	[SerializeField] private CustomMenuAction[] actions;

	public void ShowMenu()
	{
		var origMenu = RadialMenuMain.i;
		
		if (origMenu == null) return;
		
		if (originalActions == null)
		{
			originalActions = origMenu.actionsMain.ToList();
		}

		var array = new RadialMenuAction[actions.Length];
		actions.CopyTo(array, 0);
		origMenu.actionsMain = array;
		origMenu.SetupMain();
		onMainMenu = false;
	}

	public static void ResetMenu()
	{
		var origMenu = RadialMenuMain.i;
		if (origMenu == null || originalActions == null) return;
		
		origMenu.actionsMain = originalActions.ToArray();
		origMenu.SetupMain();
		onMainMenu = true;
	}

	private static float lastOpenTime;
	private static bool onMainMenu;
	
	public static void Update()
	{
		if (RadialMenuMain.i == null) return;
		if (Plugin.Instance?.MenuAutoReset ?? false) return;
		
		if (RadialMenuMain.IsInUse())
		{
			lastOpenTime = Time.time; 
			return;
		}
		if (Time.time > lastOpenTime + 5f && !onMainMenu)
		{
			ResetMenu();
		}
	}
}

[HarmonyPatch]
public class CustomMenuAction : RadialMenuAction
{
	public CustomMenuAction()
	{
		this.actionType = ActionType.NavLights;
	}

	public new virtual bool AllowedOnAircraft(Aircraft aircraft) => true;
	public new virtual void TriggerAction(Aircraft aircraft) { }

	[HarmonyPatch(typeof(RadialMenuAction), nameof(RadialMenuAction.AllowedOnAircraft))]
	[HarmonyPrefix]
	private static bool AllowedOnAircraft_Prefix(RadialMenuAction __instance, Aircraft aircraft, ref bool __result)
	{
		if (__instance is not CustomMenuAction customMenuAction) return true;
		__result = customMenuAction.AllowedOnAircraft(aircraft);
		return false;
	}

	[HarmonyPatch(typeof(RadialMenuAction), nameof(RadialMenuAction.TriggerAction))]
	[HarmonyPrefix]
	private static bool TriggerAction_Prefix(RadialMenuAction __instance, Aircraft aircraft)
	{
		if (__instance is not CustomMenuAction customMenuAction) return true;
		customMenuAction.TriggerAction(aircraft);
		return false;
	}

	[HarmonyPatch(typeof(SceneSingleton<RadialMenuMain>), nameof(SceneSingleton<>.Awake))]
	[HarmonyPostfix]
	private static void SetupMain_Prefix(SceneSingleton<RadialMenuMain> __instance)
	{
		RadialMenuMain radialMenu = SceneSingleton<RadialMenuMain>.i;

		if (radialMenu == null || ModAssets.i == null || ModAssets.i.actionsToAdd == null || ModAssets.i.actionsToAdd.Length == 0) 
			return;
		
		var currentActions = radialMenu.actionsMain; 
		if (Array.IndexOf(currentActions, ModAssets.i.actionsToAdd[0]) != -1)
			return;
		
		var combinedArray = new RadialMenuAction[currentActions.Length + ModAssets.i.actionsToAdd.Length];
		currentActions.CopyTo(combinedArray, 0);
		ModAssets.i.actionsToAdd.CopyTo(combinedArray, currentActions.Length);
		
		radialMenu.actionsMain = combinedArray;
	}
}

[CreateAssetMenu(fileName = "New Switch Menu Action", menuName = "CustomActions/SwitchMenuAction")]
public class SwitchMenuAction : CustomMenuAction
{
	[SerializeField] private RadialMenu menu;
	
	public override void TriggerAction(Aircraft aircraft)
	{
		Plugin.Logger.LogInfo("Switching to menu: " + menu?.name ?? "Main");
		if (menu == null)
		{
			RadialMenu.ResetMenu();
			return;
		} 
		menu?.ShowMenu();
	}
}

[CreateAssetMenu(fileName = "New BOTE Action", menuName = "CustomActions/BOTE Action")]
public class BOTEUnitAction : CustomMenuAction
{
	private enum CustomActionType
	{
		Deploy,
		Next,
		Previous,
		FOB,
		Resupply,
		ResupplyPlayer,
		CameraSwitch
	}

	[SerializeField] private CustomActionType customActionType;

	public override bool AllowedOnAircraft(Aircraft aircraft)
	{
		
		switch (customActionType)
		{
			case CustomActionType.Deploy:
			case CustomActionType.Next:
			case CustomActionType.Previous:
				return aircraft.TryGetComponent(out DeploymentManager _);
			
			case CustomActionType.FOB:
				if (aircraft.TryGetComponent(out DeploymentManager deployer))
				{
					return deployer.FobAvailable;
				}
				return false;
			
			case CustomActionType.Resupply:
			case CustomActionType.ResupplyPlayer:
				return aircraft.TryGetComponent(out ResupplyController _);
			
			case CustomActionType.CameraSwitch:
				return aircraft.GetComponentInChildren<BridgeCamManager>() != null;
			
			default:
				return true;
		}
	}

	public override void TriggerAction(Aircraft aircraft)
	{
		aircraft.TryGetComponent(out DeploymentManager deployer);
		aircraft.TryGetComponent(out ResupplyController controller);
		var camManager = aircraft.GetComponentInChildren<BridgeCamManager>();
		
		switch (customActionType)
		{
			case CustomActionType.Deploy when deployer:
				deployer.CmdDeployUnit();
				break;
			case CustomActionType.Next when deployer:
				deployer.NextUnit();
				break;
			case CustomActionType.Previous when deployer:
				deployer.PrevUnit();
				break;
			case CustomActionType.FOB when deployer:
				deployer.CmdDeployFOB();
				break;
			case CustomActionType.Resupply when controller:
				controller.RequestResupply(false);
				break;
			case CustomActionType.ResupplyPlayer when controller:
				controller.RequestResupply(true);
				break;
			case CustomActionType.CameraSwitch when camManager:
				camManager.CycleCam(1);
				break;
			default:
				break;
		}
	}
}