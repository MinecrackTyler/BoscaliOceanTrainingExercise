using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(EncyclopediaBrowser))]
public class EncyclopediaBrowserPatches
{
	[HarmonyPrefix]
	[HarmonyPatch(nameof(EncyclopediaBrowser.SpawnAircraft))]
	private static void SpawnAircraft_Prefix(EncyclopediaBrowser __instance, UnitDefinition definition)
	{
		if (definition.IsShipDefinition())
		{
			__instance.spawnTransform = __instance.spawnTransforms[2];
			__instance.waterMaterial.SetVector("_OriginOffset" ,Vector2.zero);
		}
		else
		{
			__instance.spawnTransform = __instance.spawnTransforms[0];
		}
        
	}
}