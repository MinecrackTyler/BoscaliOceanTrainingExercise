using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NOComponentWIP.ServerConfig;
using NOComponentWIP.Systems;
using Rewired;
using Rewired.UI.ControlMapper;
using UnityEngine;

namespace NOComponentWIP;

public static class MyPluginInfo
{
	public const string PLUGIN_GUID = "com.minec.bote";
	public const string PLUGIN_NAME = "BoscaliOceanTrainingExercise";
	public const string PLUGIN_VERSION = "1.5.2";
}


[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.nikkorap.blueprinter")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger;
	internal static Plugin Instance;
	
	// ----- CONFIG -----
	
	private ConfigEntry<bool> menuAutoReset;
	public bool MenuAutoReset => menuAutoReset.Value;
	
	private ConfigEntry<bool> radarTargetIndicatorAircraft;
	public bool RadarTargetIndicatorAircraft => radarTargetIndicatorAircraft.Value;
	
	private ConfigEntry<bool> radarTargetIndicatorMissile;
	public bool RadarTargetIndicatorMissile => radarTargetIndicatorMissile.Value;
	
	private ConfigEntry<KeyboardShortcut> reloadUnitConfig;
	public KeyboardShortcut ReloadUnitConfig => reloadUnitConfig.Value;
	
	private ConfigEntry<KeyboardShortcut> resyncUnitCounts;
	public KeyboardShortcut ResyncUnitCounts => resyncUnitCounts.Value;

	private ConfigEntry<KeyboardShortcut> debugSwitchUnit;
	public KeyboardShortcut DebugSwitchUnit => debugSwitchUnit.Value;

	private void SetupConfig()
	{
		menuAutoReset = Config.Bind($"Radial Menu",
			"Radial Menu AutoReset",
			true,
			new ConfigDescription($"Auto reset to main radial menu."));
		
		radarTargetIndicatorAircraft = Config.Bind($"UI",
			"Radar Target Indicator (Aircraft)",
			true,
			new ConfigDescription($"Show Icon if target is illuminated by onboard radar"));
		
		radarTargetIndicatorMissile = Config.Bind($"UI",
			"Radar Target Indicator (Missile)",
			false,
			new ConfigDescription($"Show Icon if target is illuminated by onboard radar"));

		reloadUnitConfig = Config.Bind($"Debug",
			"Reload Unit Config",
			new KeyboardShortcut(KeyCode.None),
			"Keyboard shortcut to live reload UnitConfig.jsonc");
		
		resyncUnitCounts = Config.Bind($"Debug",
			"Resync Unit Counts",
			new KeyboardShortcut(KeyCode.None),
			"Keyboard shortcut to resync unit counts (only needed in conjuction with Reload Unit Config)");
		
		debugSwitchUnit = Config.Bind($"Debug",
			"Switch Unit (SINGLEPLAYER)",
			new KeyboardShortcut(KeyCode.Semicolon),
			"If friendly aircraft is first target and in singleplayer, switch control to that aircraft");
	}
	
	private void Awake()
	{
		Instance = this;
		Logger = base.Logger;
		
		BlueprinterHelper.Initialize();
		
		InitializeMirageReaderWriters(typeof(Plugin).Assembly);
		Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
		harmony.PatchAll();
		SetupConfig();

		ModAssets.OnInitialize += UnitConfig.LoadOrCreateConfig;
		
		Logger.LogInfo("Boscali Ocean Training Exercise Loaded");
		
	}

	[Conditional("DEBUG")]
	internal static void DebugLog(string msg)
	{
		Logger.LogInfo(msg);
	}

	private static void InitializeMirageReaderWriters(Assembly assembly)
	{
		foreach (var type in assembly.GetTypes())
		{
			if (type.Name != "GeneratedNetworkCode") continue;
			RuntimeHelpers.RunClassConstructor(type.TypeHandle);

			foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
			{
				if (method.Name.StartsWith("InitReadWriters"))
				{
					method.Invoke(null, null);
				}
			}
		}
	}

	private void Update()
	{
		RadialMenu.Update();
		
		if (GameManager.gameState != GameState.SinglePlayer) return;
		if (DebugSwitchUnit.IsDown())
		{
			if (AircraftSwitcher.i == null) return;
			if (!GameManager.GetLocalPlayer(out NuclearOption.Networking.Player player)) return;
			if (!GameManager.GetLocalAircraft(out var aircraft)) return;
			if (aircraft.weaponManager.targetList.Count == 0) return;
			var targetUnit = aircraft.weaponManager.targetList[0];
			if (targetUnit == null || targetUnit is not Aircraft newAircraft) return;
			AircraftSwitcher.i.SwitchAircraft(player, aircraft, newAircraft);
		}

		if (ReloadUnitConfig.IsDown())
		{
			UnitConfig.ReloadConfig();
		}

		if (ResyncUnitCounts.IsDown())
		{
			UnitCountTracker.Resync();
		}
	}
}