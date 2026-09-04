using HarmonyLib;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Hangar))]
public static class HangarPatches
{
	[HarmonyPatch(nameof(Hangar.SpawnAircraft))]
	[HarmonyPrefix]
	private static bool SpawnAircraft_Prefix(Hangar __instance, Player player, AircraftDefinition definition, Loadout loadout, float fuelLevel, LiveryKey livery)
	{
		if (!ModAssets.i.ShipDefinitions.Contains(definition)) return true;

		bool attachedUnit = __instance.attachedUnit is Ship or Aircraft;
		bool isDock = __instance.attachedUnit != null && __instance.attachedUnit.definition == ModAssets.i.dockDef;
        
		Transform spawnTransform = __instance.spawnTransform;
        
		Vector3 offset = definition.spawnOffset;
        
		if (attachedUnit)
		{
			offset.z -= 200f; 
		}
        
		GlobalPosition gp = spawnTransform.GlobalPosition() + spawnTransform.up * offset.y + spawnTransform.forward * offset.z;
        
		if (attachedUnit || isDock)
		{
			gp.y = Datum.SeaLevel.y + offset.y;
		}
        
		Quaternion spawnRotation = spawnTransform.rotation;
		if (attachedUnit)
		{
			Vector3 euler = spawnRotation.eulerAngles;
			spawnRotation = Quaternion.Euler(0f, euler.y, 0f);
		}
        
		Aircraft aircraft = NetworkSceneSingleton<Spawner>.i.SpawnAircraft(
			player, 
			definition.unitPrefab, 
			loadout, 
			fuelLevel, 
			livery, 
			gp, 
			spawnRotation,
			new Vector3(0, 0, 0), 
			__instance, 
			__instance.attachedUnit?.NetworkHQ, 
			null, 
			1f, 
			0.5f
		);
        
		if (loadout == null) 
		{
			aircraft.Networkloadout = aircraft.weaponManager.SelectAIAircraftWeapons(__instance.parentAirbase);
		}

		__instance.spawnedObject = aircraft.gameObject;
        
		return false;
	}
	
	// Postfix to track deployed planes towards limits, because prefix can exit early at
	// ModAssets.i.ShipDefinitions.Contains and thus not run tracking implemented later there
	[HarmonyPatch(nameof(Hangar.SpawnAircraft))]
	[HarmonyPostfix]
	private static void SpawnAircraft_Postfix(Hangar __instance, AircraftDefinition definition)
	{
		var spawnedAircraft = __instance.spawnedObject?.GetComponent<Aircraft>();
		if (spawnedAircraft == null) return;
		
		if (UnitCountTracker.TryConsumePendingAircraft(__instance.attachedUnit, definition, out ulong ownerID))
		{
			UnitCountTracker.RegisterUnit(spawnedAircraft, ownerID);
		}
	}
}