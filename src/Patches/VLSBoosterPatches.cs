using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(VLSBooster))]
public class VLSBoosterPatches
{
	[HarmonyPatch(nameof(VLSBooster.VLSBooster_OnInitialize))]
	[HarmonyPrefix]
	private static bool VLSBooster_OnInitialize_Prefix(VLSBooster __instance)
	{
		if (!__instance.missile.owner.definition.IsShipDefinition())
			return true;
		
		__instance.missile.onInitialize -= __instance.VLSBooster_OnInitialize;
		if (__instance.missile.owner == null || (__instance.missile.owner is Aircraft aircraft && !aircraft.definition.IsShipDefinition()) || GameManager.gameState == GameState.Encyclopedia)
		{
			__instance.missile.boosterIsAttached = false;
			Object.Destroy(__instance.gameObject);
		}
		else
		{
			__instance.missile.boosterIsAttached = true;
		}

		return false;
	}
}