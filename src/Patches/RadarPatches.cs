using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Radar))]
public static class RadarPatches
{
	[HarmonyPatch(nameof(Radar.OnDestroy))]
	[HarmonyPrefix]
	private static void OnDestroy_Prefix(Radar __instance)
	{
		foreach (var hq in FactionRegistry.HQLookup.Values)
		{
			if (hq == null) continue;
			if (hq.radars.Contains(__instance))
			{
				hq.radars.Remove(__instance);
			}
		}
	}

	[HarmonyPatch(nameof(Radar.Update))]
	[HarmonyPostfix]
	private static void Update_Postfix(Radar __instance)
	{
		if (__instance.activated) return;
		for (int i = 0; i < __instance.rotators.Length; i++)
		{
			__instance.rotators[i].transform.localEulerAngles -= __instance.rotators[i].axis * Time.deltaTime;
		}
	}
    
	[HarmonyPatch(nameof(Radar.AttachToUnit))]
	[HarmonyPostfix]
	private static void AttachToUnit_Postfix(Radar __instance, Unit unit)
	{
		if (__instance.guidedMissiles == null)
		{
			__instance.guidedMissiles = new List<Missile>();
		}
	}
}