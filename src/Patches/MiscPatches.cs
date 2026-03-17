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
			if (lastWeapon is MissileLauncher or Deployer)
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
		if (weapon is MissileLauncher ml)
		{
			return ml.GetAmmoTotal() <= 0; 
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
			var cargoDoor = aircraft.GetComponentInChildren<CargoDoor>();
            
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
	private static List<string> allowedList = ["Destroyer1_Player", "LandingKraft"];
	
	[HarmonyPrefix]
	private static bool Prefix(Hangar __instance, Player player, AircraftDefinition definition, Loadout loadout, float fuelLevel, LiveryKey livery)
	{
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