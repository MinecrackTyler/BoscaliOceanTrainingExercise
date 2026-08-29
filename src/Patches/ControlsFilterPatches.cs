using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(ControlsFilter.AutoHover))]
public static class ControlsFilterAutoHoverPatches
{
	private static Aircraft tempAircraft;
    
	[HarmonyPatch(nameof(ControlsFilter.AutoHover.Hover))]
	[HarmonyPrefix]
	private static void Hover_Prefix(ControlsFilter.AutoHover __instance, ControlInputs inputs, Aircraft aircraft)
	{
		tempAircraft = aircraft;
	}
    
	[HarmonyPatch(nameof(ControlsFilter.AutoHover.Hover))]
	[HarmonyPostfix]
	private static void Hover_Postfix(ControlsFilter.AutoHover __instance, ControlInputs inputs, Aircraft aircraft)
	{
		tempAircraft = null;
	}
    
    
	[HarmonyPatch(nameof(ControlsFilter.AutoHover.CheckNearbyShip))]
	[HarmonyPrefix]
	private static bool CheckNearbyShip_Prefix(ControlsFilter.AutoHover __instance, FactionHQ faction,
		GlobalPosition position)
	{
		if (!(Time.timeSinceLevelLoad - __instance.lastShipCheck < 3f))
		{
			__instance.lastShipCheck = Time.timeSinceLevelLoad;
			if (faction != null && faction.TryGetNearestShip(position, out var nearestShip, out var nearestDistance) && nearestDistance < 250000f)
			{
				__instance.surfaceVelocity = nearestShip.rb.velocity;
			}
			else if (faction != null && faction.TryGetNearestAircraft(position, out var nearestAircraft, out nearestDistance, tempAircraft) && nearestDistance < 250000f && ModAssets.i.ShipDefinitions.Contains(nearestAircraft.definition))
			{
				__instance.surfaceVelocity = nearestAircraft.rb.velocity;
			} else
			{
				__instance.surfaceVelocity = Vector3.zero;
			}
		}

		return false;
	}
}