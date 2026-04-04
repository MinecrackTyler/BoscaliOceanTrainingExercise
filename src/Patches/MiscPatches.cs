using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Mirage;
using Mirage.Serialization;
using NuclearOption.Jobs;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(WeaponStation))]
public static class WeaponStationPatches
{
    [HarmonyPatch(nameof(WeaponStation.LaunchMount))]
    [HarmonyPostfix]
    static void LaunchMount_Postfix(WeaponStation __instance, ref int ___weaponIndex)
    {
        
        
        if (___weaponIndex >= __instance.Weapons.Count)
        {
            var lastWeapon = __instance.Weapons[__instance.Weapons.Count - 1];
            if (lastWeapon is MissileLauncher or Deployer or NetworkMissileLauncher)
            {
                ___weaponIndex = 0;
            }
            else return;
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

    [HarmonyPatch(nameof(WeaponStation.UpdateLastFired))]
    [HarmonyPostfix]
    private static void UpdateLastFired_Postfix(WeaponStation __instance, int roundsFired)
    {
        if (__instance.Weapons[0] is NetworkMissileLauncher)
        {
            __instance.Ammo += roundsFired;
        }
    }

    private static bool IsWeaponEmpty(object weapon)
    {
        if (weapon is NetworkMissileLauncher nml) return nml.GetAmmoTotal() <= 0;
        if (weapon is Deployer d) return d.GetAmmoTotal() <= 0;
        return false;
    }
}

[HarmonyPatch(typeof(HUDTurretCrosshair))]
public static class HUDTurretCrosshairPatches
{
    [HarmonyPatch("Refresh")]
    [HarmonyPrefix]
    static bool Refresh_Prefix(HUDTurretCrosshair __instance, ref Camera mainCamera, out Vector3 crosshairPosition)
    {
        Vector3 direction = __instance.turret.GetDirection();
        bool isOnTarget = __instance.turret.IsOnTarget();
        crosshairPosition = Vector3.one * 10000f;

        if (Vector3.Dot(mainCamera.transform.forward, direction - mainCamera.transform.position) > 0f)
        {
            crosshairPosition = SceneSingleton<CameraStateManager>.i.mainCamera.WorldToScreenPoint(direction);
            crosshairPosition.z = 0f;
            __instance.transform.position = crosshairPosition;
            __instance.crosshair.enabled = true;
            
            float reloadProgress = 0f;
            if (__instance.gun != null)
            {
                reloadProgress = __instance.gun.GetReloadProgress();
                if (reloadProgress > 0f)
                {
                    if (!__instance.readinessCircle.enabled)
                    {
                        __instance.readinessCircle.enabled = true;
                        __instance.crosshair.color = Color.red + Color.green * 0.5f;
                    }
                    __instance.readinessCircle.fillAmount = reloadProgress;
                }
                else if (__instance.readinessCircle.enabled)
                {
                    __instance.readinessCircle.enabled = false;
                    __instance.crosshair.color = Color.green;
                }
            }
            __instance.circle.enabled = isOnTarget && reloadProgress <= 0f;
        }
        else
        {
            __instance.circle.enabled = false;
            __instance.readinessCircle.enabled = false;
            __instance.crosshair.enabled = false;
        }
        return false;
    }
}

[HarmonyPatch(typeof(Spawner))]
public static class SpawnerPatches
{
    [HarmonyPatch(nameof(Spawner.SpawnAircraft))]
    [HarmonyPrefix]
    public static bool SpawnAircraft_Prefix(Spawner __instance, ref Aircraft __result, Player player, GameObject prefab, Loadout loadout, float fuelLevel, LiveryKey livery, GlobalPosition globalPosition, Quaternion rotation, Vector3 startingVel, Hangar spawningHangar, FactionHQ HQ, string uniqueName, float skill, float bravery)
    {
        PlayerRef networkplayerRef = player?.PlayerRef ?? PlayerRef.Invalid;
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
        component.NetworkunitName = player != null ? $"{player.GetNameOrCensored()} [{component.definition.unitName}]" : component.definition.unitName;

        if (component.TryGetComponent<Airbase>(out var airbase))
        {
            airbase.SetupAttachedAirbase(component);
            airbase.SavedAirbase.UniqueName += $"{player?.GetNameOrCensored()}_{Time.time}";
        }

        if (player != null) __instance.ServerObjectManager.Spawn(gameObject, player.Owner);
        else __instance.ServerObjectManager.Spawn(gameObject);

        __result = component;
        return false;
    }
}

[HarmonyPatch(typeof(Hangar))]
public static class HangarPatches
{

    [HarmonyPatch("SpawnAircraft")]
    [HarmonyPrefix]
    private static bool SpawnAircraft_Prefix(Hangar __instance, Player player, AircraftDefinition definition, Loadout loadout, float fuelLevel, LiveryKey livery)
    {
        if (!ModAssets.i.shipDefinitions.Contains(definition)) return true;

        GlobalPosition tempgp = __instance.spawnTransform.GlobalPosition();
        tempgp.y = Datum.SeaLevel.y;
        GlobalPosition gp = tempgp + __instance.spawnTransform.up * definition.spawnOffset.y + __instance.spawnTransform.forward * definition.spawnOffset.z;
        
        Aircraft aircraft = NetworkSceneSingleton<Spawner>.i.SpawnAircraft(player, definition.unitPrefab, loadout, fuelLevel, livery, gp, __instance.spawnTransform.rotation, __instance.GetVelocity(), __instance, __instance.attachedUnit.NetworkHQ, null, 1f, 0.5f);
        
        if (loadout == null) aircraft.Networkloadout = aircraft.weaponManager.SelectWeapons(preferNukes: true);
        __instance.spawnedObject = aircraft.gameObject;
        return false;
    }

    [HarmonyPatch("TrySpawnAircraft")]
    [HarmonyPrefix]
    public static bool TrySpawnAircraft_Prefix(Hangar __instance, ref Airbase.TrySpawnResult __result, Player player, AircraftDefinition definition, LiveryKey livery, Loadout loadout, float fuelLevel)
    {
        if (__instance is not RailHangar railHangar) return true;
        if (!railHangar.IsServer) throw new MethodInvocationException("[Server] function 'TrySpawnAircraft' called when server not active");

        if (!railHangar.CanSpawnAircraft(definition))
        {
            __result = default;
            return false;
        }

        if (railHangar.waitForOpenBeforeSpawn)
        {
            var spawnQueue = new Hangar.QueuedAircraftToSpawn(player, definition, livery, loadout, fuelLevel);
            railHangar.DoorSequenceRailLauncher(spawnQueue).Forget();
        }
        else
        {
            railHangar.SpawnAircraft(player, definition, loadout, fuelLevel, livery);
            railHangar.DoorSequenceNormal().Forget();
        }

        if (player != null) player.FlyOwnedAirframe(definition);
        else railHangar.attachedUnit.NetworkHQ.AddSupplyUnit(definition, -1);

        __result = new Airbase.TrySpawnResult(true, railHangar, railHangar.waitForOpenBeforeSpawn);
        return false;
    }
}

[HarmonyPatch(typeof(Turret))]
public static class TurretPatches
{
    [HarmonyPatch(nameof(Turret.AimTurret), typeof(Vector3))]
    [HarmonyPatch(nameof(Turret.AimTurret), typeof(WeaponStation))]
    [HarmonyPostfix]
    private static void AimTurret_Postfix(Turret __instance)
    {
        if (__instance.aimSafetyWeapon is not Gun gun) return;

        if (Physics.SphereCast(gun.transform.position + gun.transform.forward * 2f, 0.2f, gun.transform.forward, out _, 20f, -8193))
        {
            __instance.aimSafetyWeapon.Safety = true;
        }
    }

    [HarmonyPatch(nameof(Turret.AttachToWeaponManager))]
    [HarmonyPostfix]
    private static void AttachToWeaponManager_Postfix(Turret __instance, Aircraft aircraft)
    {
        if (__instance.targetAcquisitionMode == Turret.TargetAcquisitionMode.parentUnitTargetDetector && __instance.attachedUnit?.radar != null)
        {
            __instance.RegisterTargetDetector(__instance.attachedUnit.radar);
        }
    }

    [HarmonyPatch(nameof(Turret.SetTarget), typeof(PersistentID), typeof(byte))]
    [HarmonyPostfix]
    private static void SetTarget_Postfix(Turret __instance, PersistentID id)
    {
        if (__instance.attachedUnit.disabled || !__instance.aimSafetyWeapon) return;
        if (!UnitRegistry.TryGetUnit(id, out var target)) return;

        __instance.aimSafetyWeapon.SetTarget(target);
        __instance.aimSolver.SetTarget(__instance.attachedUnit, target, __instance.aimSafetyWeapon.transform, __instance.aimSafetyWeapon.info);
    }
}

[HarmonyPatch(typeof(AeroPart))]
public class AeroPartPatches
{
    [HarmonyPatch(nameof(AeroPart.ApplyJobFields))]
    [HarmonyPrefix]
    static bool ApplyJobFields_Prefix(AeroPart __instance)
    {
        if (!__instance.JobFields.IsCreated) return false;
        ref var reference = ref __instance.JobFields.Ref();

        if (reference.splashed)
        {
            Vector3 pos = __instance.xform.position;
            if (Physics.Linecast(pos + Vector3.up * 100f, pos - Vector3.up * 10f, out var hit, 64))
            {
                bool isWater = hit.collider.sharedMaterial == GameAssets.i.WaterMaterial;
                if (!isWater) pos.y = Datum.LocalSeaY;
            }
        }

        if (reference.angularDragChanged) __instance.rb.angularDrag = reference.angularDrag;

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

[HarmonyPatch(typeof(Hardpoint))]
public class HardpointPatches
{
    [HarmonyPatch(nameof(Hardpoint.SpawnMount))]
    [HarmonyPostfix]
    private static void SpawnMount_Postfix(Aircraft aircraft, WeaponMount weaponMount, GameObject __result)
    {
        if (!weaponMount.turret) return;
        foreach (var turret in __result.GetComponentsInChildren<Turret>().Skip(1))
        {
            turret.AttachToWeaponManager(aircraft);
        }
    }
}

[HarmonyPatch(typeof(UnitPart), nameof(UnitPart.Awake))]
public static class UnitPartPatches
{
    [HarmonyPrefix]
    private static void Awake_Prefix(UnitPart __instance)
    {
        if (__instance is AeroPart part && part.joints.Length > 0) return;
        if (__instance.parentUnit == null && __instance.transform.parent != null)
        {
            __instance.parentUnit = __instance.transform.parent.GetComponentInParentWithDepth<UnitPart>(6)?.parentUnit;
        }
    }
}

[HarmonyPatch(typeof(PilotPlayerState))]
public static class PilotPlayerStatePatches
{
    [HarmonyPatch(nameof(PilotPlayerState.PlayerControls))]
    [HarmonyPostfix]
    private static void PlayerControls_Postfix(PilotPlayerState __instance)
    {
        if (!GameManager.flightControlsEnabled || __instance.pilotStrength < 0.2f) return;
        if (!ModAssets.i.shipDefinitions.Contains(__instance.pilot.aircraft.definition)) return;

        if (__instance.player.GetButton("Countermeasures") && !__instance.pilot.aircraft.countermeasureTrigger)
        {
            __instance.pilot.aircraft.Countermeasures(true, __instance.pilot.aircraft.countermeasureManager.activeIndex);
        }
    }
}

[HarmonyPatch(typeof(Encyclopedia))]
public static class EncyclopediaPatches
{
    private static bool triggered = false;
    [HarmonyPatch(nameof(Encyclopedia.AfterLoad), new Type[0])]
    [HarmonyPostfix]
    private static void AfterLoad_Postfix()
    {
        if (triggered) return;
        for (int i = 0; i < ModAssets.i.aircraftDefs.Count; i++) 
        {
            var def = ModAssets.i.aircraftDefs[i];
            def.unitPrefab.AddComponent<RailHangarController>();
            var go = new GameObject("RailAttachPoint");
            go.transform.SetParent(def.unitPrefab.transform);
            go.transform.localPosition = ModAssets.i.aircraftEntries[i].railAttachPoint;
        }
        triggered = true;
    }
}

public static class TransformExtensions
{
    public static T GetComponentInParentWithDepth<T>(this Transform startTransform, int maxDepth) where T : Component
    {
        Transform current = startTransform;
        for (int i = 0; i <= maxDepth && current != null; i++)
        {
            if (current.TryGetComponent<T>(out var component)) return component;
            current = current.parent;
        }
        return null;
    }
}