using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using Rewired.UI.ControlMapper;
using UnityEngine;

namespace NOComponentWIP;

[HarmonyPatch]
[BepInPlugin($"{MyPluginInfo.PLUGIN_GUID}.inputs",  $"{MyPluginInfo.PLUGIN_NAME}_Inputs",  MyPluginInfo.PLUGIN_VERSION)]
public class Mod_Input : BaseUnityPlugin
{
	internal static new ManualLogSource Logger;
	private void Awake()
	{
		Logger = base.Logger;

		resetButton = Config.Bind($"{ModShortName} Input Map Reset",
			"Reset Input Maps",
			"",
			new ConfigDescription($"Reenable input maps for {ModShortName} (does NOT wipe bindings)",
				null,
				new ConfigurationManagerAttributes { CustomDrawer = DrawResetButton }));
		
		// ---- Uncomment if your mod does not do this itself ----
		
		/*
		Harmony harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}.inputs");
		harmony.PatchAll();
	*/
	}
	
	// --- INPUT CONFIG ---
	
	public const string ModShortName = "BOTE";
	public const string ModLongName = "Boscali Ocean Training Exercise";
	
	private static readonly List<(string, KeyboardKeyCode)> customActions =
	[
		("Deploy Unit", KeyboardKeyCode.None),
		("Next Unit", KeyboardKeyCode.None),
		("Previous Unit", KeyboardKeyCode.None),
		("Call Resupply", KeyboardKeyCode.None),
		("Call Resupply - Player", KeyboardKeyCode.None),
		("Deploy FOB", KeyboardKeyCode.None),
		("Next Camera", KeyboardKeyCode.None),
		("Previous Camera", KeyboardKeyCode.None),
	];

	private static readonly List<string> customAxis =
	[
		
	];
	
	// --- CONFIG HANDLING ---

	private ConfigEntry<string> resetButton;

	private class ConfigurationManagerAttributes
	{
		public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
	}

	private static void DrawResetButton(ConfigEntryBase entry)
	{
		if (GUILayout.Button($"Reset Maps", GUILayout.ExpandWidth(true)))
		{
			ResetMaps();
		}
	}

	private static void ResetMaps()
	{
		int id = newMapID;
		if (newMapID == -1) return;

		if (Rewired.ReInput.isReady)
		{
			Logger.LogInfo($"Resetting input maps...");

			var player = GameManager.playerInput;

			player.controllers.maps.SetMapsEnabled(true, id);
			
			if (Rewired.ReInput.userDataStore != null)
			{
				Rewired.ReInput.userDataStore.Save();
				Logger.LogInfo("Input maps reset!");
			}
		}
		else
		{
			Logger.LogWarning("Error resetting input maps");
		}
	}
	
	// --- INPUT HANDLING ---

	private static bool init = false;
	
	private static HashSet<int> catIDs = new HashSet<int>();
	private static int newMapID = -1;

	private static readonly HashSet<int> ValidIDs = new();

	private static int GetID(string category, string element)
	{
		unchecked
		{
			int hash = 52;
			string input = $"{category}_{element}";
			foreach (char c in input)
			{
				hash = (hash * 31) + c;
			}
			return Math.Abs(hash) % 50000 + 10000;
		}
	}
	
	[HarmonyPatch(typeof(InputManager_Base), nameof(InputManager_Base.Awake))]
	[HarmonyPrefix]
	private static void Prefix(InputManager_Base __instance)
	{
		SetupActions(__instance);
	}
	
	private static void SetupActions(InputManager_Base manager)
	{
		if (init) return;
		init = true;
		var actions = manager?.userData?.actions;
		if (actions == null) return;
		var categories = manager?.userData?.actionCategories;
		if (categories == null) return;
		var mapCategories = manager?.userData?.mapCategories;
		if (mapCategories  == null) return;
		var newCat = new InputActionCategory()
		{
			descriptiveName = ModLongName,
			id = GetID(ModLongName, "Category"),
			name = ModLongName,
			userAssignable = true
		};
		manager.userData.actionCategories.Add(newCat);
		manager.userData.actionCategoryMap.AddCategory(newCat.id);

		ValidIDs.Clear();
		
		foreach (var (action, keycode) in customActions)
		{
			int id = GetID(ModLongName, $"{ModShortName}:{action}");
			ValidIDs.Add(id);
			var newAction = new InputAction()
			{
				id = id,
				name = $"{ModShortName}:{action}",
				type = InputActionType.Button,
				descriptiveName = action,
				categoryId = newCat.id,
				userAssignable = true
			};
			actions.Add(newAction);
			manager.userData.actionCategoryMap.AddAction(newCat.id, newAction.id);
		}
		
		foreach (var axis in customAxis)
		{
			int id = GetID(ModLongName, axis);
			ValidIDs.Add(id);
			var newAction = new InputAction()
			{
				id = id,
				name = axis,
				type = InputActionType.Axis,
				descriptiveName = axis,
				categoryId = newCat.id,
				userAssignable = true
			};
			actions.Add(newAction);
			manager.userData.actionCategoryMap.AddAction(newCat.id, newAction.id);
		}
		
		catIDs.Add(newCat.id);

		var newMapCat = new InputMapCategory
		{
			name = ModShortName,
			id = GetID(ModLongName, "MapCategory"),
			descriptiveName = ModShortName,
			userAssignable = true,
			checkConflictsWithAllCategories = true
		};
		mapCategories.Add(newMapCat);
		newMapID = newMapCat.id;

	}

	[HarmonyPatch(typeof(ControlMapper), nameof(ControlMapper.Initialize))]
	[HarmonyPrefix]
	private static void ControlMapper_Init_Prefix(ControlMapper __instance)
	{
		if (newMapID == -1) return;
		
		if (__instance._mappingSets.Any(ms => ms.mapCategoryId == newMapID)) return;
		var newMappingSet = new ControlMapper.MappingSet(newMapID,
			ControlMapper.MappingSet.ActionListMode.ActionCategory,
			catIDs.ToArray(),
			[]);
		
		__instance._mappingSets = __instance._mappingSets.AddToArray(newMappingSet);
		GameManager.playerInput.controllers.maps.SetMapsEnabled(true, newMapID);

		ValidateBindings();
	}

	private static void ValidateBindings()
	{
		bool changed = false;
		
		var player = GameManager.playerInput;

		var allMaps = player.controllers.maps.GetAllMaps();
		if (allMaps == null) return;
		foreach (var map in allMaps)
		{
			if (map.categoryId != newMapID) continue;
			var elements = map.AllMaps;

			foreach (var action in elements)
			{
				if (!ValidIDs.Contains(action.actionId))
				{
					Logger.LogWarning($"Removing old binding (ID: {action.actionId}) from map {map.categoryId} ");
					map.DeleteElementMap(action.id);
					changed = true;
				}
			}
		}

		if (changed && ReInput.userDataStore != null)
		{
			Logger.LogInfo("Saving Bindings");
			ReInput.userDataStore.Save();
		}
	}
}