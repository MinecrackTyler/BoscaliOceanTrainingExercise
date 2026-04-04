using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using Rewired.UI.ControlMapper;
using UnityEngine;

namespace NOComponentWIP;

[BepInPlugin("NOComponentsWIP", "NOComponentsWIP", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger;
	void Awake()
	{
		Logger = base.Logger;
		Harmony harmony = new Harmony("NOComponentWIP");
		harmony.PatchAll();
		Logger.LogInfo("Boscali Ocean Training Exercise Loaded");
	}
}

[HarmonyPatch]
public static class Mod_Input
{
	private static List<int> catIDs = new List<int>();
	
	private static List<string> customActions =
	[
		"Deploy Unit",
		"Next Unit",
		"Previous Unit",
		"Call Resupply",
		"Select/Deselect FOB"
	];
	
	[HarmonyPatch(typeof(InputManager_Base), nameof(InputManager_Base.Awake))]
	[HarmonyPrefix]
	private static void Prefix(InputManager_Base __instance)
	{
		SetupActions(__instance);
	}
	
	private static void SetupActions(InputManager_Base manager)
	{
		var actions = manager?.userData?.actions;
		if (actions == null) return;
		var categories = manager?.userData?.actionCategories;
		if (categories == null) return;
		var newCat = new InputCategory
		{
			descriptiveName = "Boscali Ocean Training Exercise",
			id = GetNewCategoryID(categories),
			name = "Boscali Ocean Training Exercise",
			userAssignable = true
		};
		manager.userData.actionCategories.Add(newCat);
		manager.userData.actionCategoryMap.AddCategory(newCat.id);

		foreach (var action in customActions)
		{
			var newAction = new InputAction()
			{
				id = GetNewActionID(actions),
				name = action,
				type = InputActionType.Button,
				descriptiveName = action,
				categoryId = newCat.id,
				userAssignable = true
			};
			actions.Add(newAction);
			manager.userData.actionCategoryMap.AddAction(newCat.id, newAction.id);
		}
		catIDs.Add(newCat.id);
	}

	[HarmonyPatch(typeof(ControlMapper), nameof(ControlMapper.Awake))]
	private static void Prefix(ControlMapper __instance)
	{
		foreach (var cat in catIDs)
		{
			var actionCategoryIds = __instance._mappingSets[0]?._actionCategoryIds;
			if (actionCategoryIds == null) return;
			if (actionCategoryIds.Contains(cat)) continue;
			__instance._mappingSets[0]._actionCategoryIds = actionCategoryIds.AddToArray(cat);
		}
		
	}

	private static int GetNewCategoryID(List<InputCategory> categories)
	{
		if (categories == null || !categories.Any()) return 1;
		return categories.Max(c => c.id) + 1;
	}
	
	private static int GetNewActionID(List<InputAction> actions)
	{
		if (actions == null || !actions.Any()) return 1;
		return actions.Max(a => a.id) + 1;
	}
}