using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(StatusGauges))]
public static class StatusGaugesPatches
{
	[HarmonyPrefix]
	[HarmonyPatch(nameof(StatusGauges.Refresh))]
	private static bool Refresh_Prefix(StatusGauges __instance)
	{
		if (__instance.irSource != null) return true;
		if (__instance.aircraft == null) return true;

		__instance.throttleLevelDisplay.rectTransform.sizeDelta = new Vector2(__instance.gaugeThickness, 200f * __instance.inputs.throttle);
		if (Time.timeSinceLevelLoad > __instance.lastRefresh + __instance.refreshDelay)
		{
			float fuelLevel = __instance.aircraft.GetFuelLevel();
			__instance.fuelLevelDisplay.rectTransform.sizeDelta = new Vector2(__instance.gaugeThickness, 200f * fuelLevel);
			__instance.fuelLevelDisplay.color = GameAssets.i.redGreenGradient.Evaluate(fuelLevel);
			float mass = __instance.aircraft.GetMass();
			__instance.massValue.text = UnitConverter.WeightReading(mass);
			float maxThrust;
			if (__instance.aircraft.GetMaxPower(out var maxPower))
			{
				__instance.twrValue.text = UnitConverter.PowerToWeightReading(maxPower * 0.001f / mass);
			}
			else if (__instance.aircraft.GetMaxThrust(out maxThrust))
			{
				__instance.twrValue.text = $"{maxThrust / (mass * 9.81f):F2}";
			}
			__instance.lastRefresh = Time.timeSinceLevelLoad;
		}
        
		return false;
	}
}