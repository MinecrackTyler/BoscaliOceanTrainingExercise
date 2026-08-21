using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace NOComponentWIP;

public static class BlueprinterHelper
{
	private static bool checkComplete = false;
	private static Type loaderType;
	private static Type registryType;
	private static object registryInstance;
	private static object instance;

	public static bool IsPatchingComplete()
	{
		Setup();
		var completeField = loaderType?.GetProperty("PatchingComplete", BindingFlags.Public | BindingFlags.Instance);
		var complete = (bool)(completeField?.GetValue(instance) ?? false);

		return complete;
	}

	private static void Setup()
	{
		if (instance != null || checkComplete) return;
		
		if (Chainloader.PluginInfos.TryGetValue("com.nikkorap.blueprinter", out var pluginInfo))
		{
			instance = pluginInfo.Instance;
			if (instance != null)
			{
				loaderType = instance.GetType();
			}
		}
		
		var registryField = loaderType?.GetField("bundleRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
		if (instance != null)
		{
			registryInstance = registryField?.GetValue(instance);
		}
		registryType = registryInstance?.GetType();
		checkComplete = true;
	}
}