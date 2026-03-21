using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Mirage;
using NuclearOption.Jobs;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(WeaponStation), "LaunchMount")]
public static class WeaponStationMLPatch
{
	[HarmonyPostfix]
	static void Postfix(WeaponStation __instance, ref int ___weaponIndex)
	{
		if (___weaponIndex >= __instance.Weapons.Count)
		{
			var lastWeapon = __instance.Weapons[__instance.Weapons.Count - 1];
			if (lastWeapon is MissileLauncher or Deployer or NetworkMissileLauncher)
			{
				___weaponIndex = 0;
			}
			else
			{
				return;
			}
		}
		
		int startIndex = ___weaponIndex;
		int checkedCount = 0;
		int totalWeapons = __instance.Weapons.Count;
		
		while (IsWeaponEmpty(__instance.Weapons[___weaponIndex]) && checkedCount < totalWeapons)
		{
			___weaponIndex = (___weaponIndex + 1) % totalWeapons;
			checkedCount++;
		}
	}
	
	private static bool IsWeaponEmpty(object weapon)
	{
		if (weapon is NetworkMissileLauncher nml)
		{
			return nml.GetAmmoTotal() <= 0; 
		}
		if (weapon is Deployer d)
		{
			return d.GetAmmoTotal() <= 0;
		}
		return false; 
	}
}

[HarmonyPatch(typeof(HUDTurretCrosshair), "Refresh")]
public static class HudCrosshairPatch
{
	[HarmonyPrefix]
	static bool Prefix(HUDTurretCrosshair __instance, ref Camera mainCamera, out Vector3 crosshairPosition)
	{
		HUDTurretCrosshair hc = __instance;
		Vector3 direction = hc.turret.GetDirection();
		bool flag = hc.turret.IsOnTarget();
		crosshairPosition = Vector3.one * 10000f;
		if (Vector3.Dot(mainCamera.transform.forward, direction - mainCamera.transform.position) > 0f)
		{
			crosshairPosition = SceneSingleton<CameraStateManager>.i.mainCamera.WorldToScreenPoint(direction);
			crosshairPosition.z = 0f;
			hc.transform.position = crosshairPosition;
			hc.crosshair.enabled = true;
			float reloadProgress = 0f;
			if (hc.gun != null)
			{
				reloadProgress = hc.gun.GetReloadProgress();
				if (reloadProgress > 0f)
				{
					if (!hc.readinessCircle.enabled)
					{
						hc.readinessCircle.enabled = true;
						hc.crosshair.color = Color.red + Color.green * 0.5f;
					}
					hc.readinessCircle.fillAmount = reloadProgress;
				}
				else if (hc.readinessCircle.enabled)
				{
					hc.readinessCircle.enabled = false;
					hc.crosshair.color = Color.green;
				}
			}
			
			hc.circle.enabled = flag && reloadProgress <= 0f;
		}
		else
		{
			hc.circle.enabled = false;
			hc.readinessCircle.enabled = false;
			hc.crosshair.enabled = false;
		}
		return false;
	}
}

[HarmonyPatch(typeof(WeaponStation), "SafetyIsOn")]
public static class WeaponStationPatch
{
	[HarmonyPostfix]
	public static void Postfix(WeaponStation __instance, Aircraft aircraft, ref bool __result)
	{
		if (__result) return;
		
		if (__instance.Cargo)
		{
			var cargoDoor = aircraft.GetComponentInChildren<CargoDoorController>();
            
			if (cargoDoor != null)
			{
				if (cargoDoor.IsOpen())
				{
					__result = true;
				}
			}
		}
	}
}

/*[HarmonyPatch(typeof(Spawner), "SpawnUnit")] //this is awful
public static class Spawner_OTB_Rotation_Patch
{
	[HarmonyPrefix]
	public static void Prefix(UnitDefinition unit, Vector3 spawnPosition, ref Quaternion rotation, Unit owner)
	{
		if (owner != null && owner.name.Contains("LandingKraft"))
		{
			rotation *= Quaternion.Euler(0, 180f, 0);
		}
	}
}*/


[HarmonyPatch(typeof(AeroPart), "ApplyJobFields")]
public class Patch_AeroPart_ApplyJobFields
{
	static bool Prefix(AeroPart __instance)
	{
		if (!__instance.JobFields.IsCreated)
		{
			return false;
		}
		
		ref var reference = ref __instance.JobFields.Ref();

		if (reference.splashed)
		{
			Vector3 position = __instance.xform.position;
			bool flag = false;
			bool flag2 = false;
			
			if (Physics.Linecast(position + Vector3.up * 100f, position - Vector3.up * 10f, out var hitInfo, 64))
			{
				flag2 = hitInfo.collider.sharedMaterial == GameAssets.i.WaterMaterial;
				flag = !flag2 && hitInfo.point.y > Datum.LocalSeaY;
			}

			if (!flag2)
			{
				position.y = Datum.LocalSeaY;
			}
		}
		
		if (reference.angularDragChanged)
		{
			__instance.rb.angularDrag = reference.angularDrag;
		}

		switch (reference.hasForce)
		{
			case JobForceType.Force:
				__instance.rb.AddForce(reference.force);
				break;
			case JobForceType.ForceAndTorque:
				__instance.rb.AddForce(reference.force);
				__instance.rb.AddTorque(reference.torque);
				break;
		}
		
		return false;
	}
}

[HarmonyPatch(typeof(Hangar), "SpawnAircraft")]
public class Hangar_SpawnAircraft
{
	private static List<string> allowedList = ["Destroyer1_Player", "LandingKraft", "Korvette1"];
	private static List<AircraftDefinition> processedAircraft = new List<AircraftDefinition>();
	
	[HarmonyPrefix]
	private static bool Prefix(Hangar __instance, Player player, AircraftDefinition definition, Loadout loadout, float fuelLevel, LiveryKey livery)
	{
		if (!processedAircraft.Contains(definition) && ModAssets.i.aircraftKeys.IndexOf(definition.jsonKey) >= 0)
		{
			definition.unitPrefab.AddComponent<RailHangarController>();
			var go = new GameObject("RailAttachPoint");
			go.transform.SetParent(definition.unitPrefab.transform);
			go.transform.localPosition = ModAssets.i.aircraftEntries[ModAssets.i.aircraftKeys.IndexOf(definition.jsonKey)].railAttachPoint;
			processedAircraft.Add(definition);
		}
		
		if (!allowedList.Contains(definition.jsonKey)) return true;
		var hgr = __instance;

		GlobalPosition tempgp = hgr.spawnTransform.GlobalPosition();
		tempgp.y = Datum.SeaLevel.y;
		GlobalPosition gp = tempgp + hgr.spawnTransform.up * definition.spawnOffset.y + hgr.spawnTransform.forward * definition.spawnOffset.z;
		Vector3 velocity = hgr.GetVelocity();
		Aircraft aircraft = NetworkSceneSingleton<Spawner>.i.SpawnAircraft(player, definition.unitPrefab, loadout, fuelLevel, livery, gp, hgr.spawnTransform.rotation, velocity, hgr, hgr.attachedUnit.NetworkHQ, null, 1f, 0.5f);
		if (loadout == null)
		{
			aircraft.Networkloadout = aircraft.weaponManager.SelectWeapons(preferNukes: true);
		}
		hgr.spawnedObject = aircraft.gameObject;
		return false;
	}

	
}

[HarmonyPatch(typeof(Spawner), "SpawnAircraft")]
public class Spawner_SpawnAircraft
{
    [HarmonyPrefix]
    public static bool Prefix(
        Spawner __instance, 
        ref Aircraft __result,
        Player player, 
        GameObject prefab, 
        Loadout loadout, 
        float fuelLevel, 
        LiveryKey livery, 
        GlobalPosition globalPosition, 
        Quaternion rotation, 
        Vector3 startingVel, 
        Hangar spawningHangar, 
        FactionHQ HQ, 
        string uniqueName, 
        float skill, 
        float bravery)
    {
        PlayerRef networkplayerRef = ((player != null) ? player.PlayerRef : PlayerRef.Invalid);
        Vector3 position = globalPosition.ToLocalPosition();
        GameObject gameObject = Object.Instantiate(prefab, position, rotation);
        
        Aircraft component = gameObject.GetComponent<Aircraft>();
        component.NetworkHQ = HQ;
        component.NetworkUniqueName = uniqueName;
        component.NetworkspawningHangar = spawningHangar;
        component.NetworkstartPosition = globalPosition;
        component.NetworkstartRotation = rotation;
        component.NetworkstartingVelocity = startingVel;
        component.Networkloadout = loadout;
        component.NetworkfuelLevel = Mathf.Clamp(fuelLevel, 0f, 1f);
        component.skill = skill;
        component.bravery = bravery;
        component.SetLiveryKey(livery);
        component.NetworkplayerRef = networkplayerRef;
        component.NetworkunitName = ((player != null) ? (player.GetNameOrCensored() + " [" + component.definition.unitName + "]") : component.definition.unitName);
		
        if (component.TryGetComponent<Airbase>(out var airbase))
        {
            airbase.SetupAttachedAirbase(component);
            airbase.SavedAirbase.UniqueName = airbase.SavedAirbase.UniqueName + $"{player?.GetNameOrCensored()}_{Time.time}";
        }
        
        if (player != null)
        {
            __instance.ServerObjectManager.Spawn(gameObject, player.Owner);
        }
        else
        {
            __instance.ServerObjectManager.Spawn(gameObject);
        }
        
        __result = component;
        return false; 
    }
}

[HarmonyPatch(typeof(Hardpoint), "SpawnMount")]
public class Hardpoint_SpawnMount
{
	[HarmonyPostfix]
	private static void Postfix(Hardpoint __instance, Aircraft aircraft, WeaponMount weaponMount, GameObject __result)
	{
		if (!weaponMount.turret) return;
		var turrets = __result.GetComponentsInChildren<Turret>().Skip(1);
		foreach (var turret in turrets)
		{
			turret.AttachToWeaponManager(aircraft);
		}
	}
}

[HarmonyPatch(typeof(Hangar), "TrySpawnAircraft")]
public static class Hangar_TrySpawnAircraft
{
    [HarmonyPrefix]
    public static bool Prefix(
        Hangar __instance, 
        ref Airbase.TrySpawnResult __result,
        Player player, 
        AircraftDefinition definition, 
        LiveryKey livery, 
        Loadout loadout, 
        float fuelLevel)
    {
	    if (__instance is not RailHangar railHangar) return true;
	    if (!railHangar.IsServer)
        {
	        throw new MethodInvocationException("[Server] function 'TrySpawnAircraft' called when server not active");
        }
        if (!railHangar.CanSpawnAircraft(definition))
        {
	        __result = default(Airbase.TrySpawnResult);
	        return false;
        }
        if (railHangar.waitForOpenBeforeSpawn)
        {
	        Hangar.QueuedAircraftToSpawn spawnAircraft = new Hangar.QueuedAircraftToSpawn(player, definition, livery, loadout, fuelLevel);
                
	        railHangar.DoorSequenceRailLauncher(spawnAircraft).Forget();
        }
        else
        {
	        railHangar.SpawnAircraft(player, definition, loadout, fuelLevel, livery);
	        railHangar.DoorSequenceNormal().Forget();
        }
        if (player != null)
        {
	        player.FlyOwnedAirframe(definition);
        }
        else
        {
	        railHangar.attachedUnit.NetworkHQ.AddSupplyUnit(definition, -1);
        }
        __result = new Airbase.TrySpawnResult(true, railHangar, railHangar.waitForOpenBeforeSpawn);
        return false;
    }
}

[HarmonyPatch(typeof(UnitPart), nameof(UnitPart.Awake))]
public static class UnitPart_Awake
{
	[HarmonyPrefix]
	private static void Prefix(UnitPart __instance)
	{
		if (__instance.parentUnit == null && __instance.transform.parent != null)
		{
			__instance.parentUnit = __instance.transform.parent.GetComponentInParentWithDepth<UnitPart>(6).parentUnit;
		}
	}
}


public static class TransformExtensions
{
	public static T GetComponentInParentWithDepth<T>(this Transform startTransform, int maxDepth) where T : Component
	{
		Transform currentTransform = startTransform;
		int depth = 0;
		
		while (currentTransform != null && depth <= maxDepth)
		{
			T component = currentTransform.GetComponent<T>();
			if (component != null)
			{
				return component;
			}
			
			currentTransform = currentTransform.parent;
			depth++;
		}

		return null;
	}
}
/*[HarmonyPatch(typeof(Airbase), "OnStartServer")]
internal static class Airbase_SetupAttachedAirbase_Patch
{
	private static int fobIndex = 1;
	private const string KARPREFIX = "bote_";

	private static void Prefix(Airbase __instance)
	{
		if (__instance.SavedAirbase.UniqueName.StartsWith(KARPREFIX))
		{
			//Plugin.Logger.LogWarning($"NEW AIRBASE HASHED {__instance.SavedAirbase.UniqueName += fobIndex.ToString()}");
			__instance.SavedAirbase.DisplayName += $" {fobIndex}";
			fobIndex++;
		}
	}
}*/