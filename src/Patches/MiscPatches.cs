using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Mirage;
using Mirage.Serialization;
using NuclearOption.Jobs;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(WeaponStation))]
public static class WeaponStationPatches
{
    [HarmonyPatch(nameof(WeaponStation.LaunchMount))]
    [HarmonyPostfix]
    static void LaunchMount_Postfix(WeaponStation __instance, ref int ___weaponIndex)
    {
        if (__instance.Weapons.Count == 0) return;
        
        if (___weaponIndex >= __instance.Weapons.Count)
        {
            var lastWeapon = __instance.Weapons[__instance.Weapons.Count - 1];
            if (lastWeapon is NetworkMissileLauncher)
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
        if (weapon is NetworkMissileLauncher nml)
        {
            return nml.GetAmmoTotal() <= 0 || nml.GetAmmoLoaded() <= 0 || nml.Reloading;
        }
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
        component.NetworkunitName = player != null ? $"{player.GetDisplayName(PlayerNameContext.ChatOrLeaderboard)} [{component.definition.unitName}]" : component.definition.unitName;

        if (component.TryGetComponent<Airbase>(out var airbase))
        {
            airbase.SetupAttachedAirbase(component);
            airbase.SavedAirbase.UniqueName += $"{player?.GetDisplayName(PlayerNameContext.ChatOrLeaderboard)}_{Time.time}";
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
}

[HarmonyPatch(typeof(Turret))]
public static class TurretPatches
{
    [HarmonyPatch(nameof(Turret.AimTurret), typeof(Vector3))]
    [HarmonyPostfix]
    private static void AimTurret_PostfixVector3(Turret __instance)
    {
        if (__instance.aimSafetyWeapon is not Gun) return;
        if (!ModAssets.i.ShipDefinitions.Contains(__instance.attachedUnit?.definition)) return;
        
        if (Physics.SphereCast(__instance.aimSafetyWeapon.transform.position + __instance.aimSafetyWeapon.transform.forward * 2f, 0.2f, __instance.aimSafetyWeapon.transform.forward, out _, __instance.attachedUnit?.maxRadius * 2f ?? 200f, -8193))
        {
            __instance.aimSafetyWeapon.Safety = true;
        }
    }
    
    [HarmonyPatch(nameof(Turret.AimTurret), typeof(WeaponStation))]
    [HarmonyPostfix]
    private static void AimTurret_PostfixWeaponStation(Turret __instance)
    {
        if (__instance.aimSafetyWeapon is not Gun gun) return;
        if (!ModAssets.i.ShipDefinitions.Contains(__instance.attachedUnit?.definition)) return;
        
        var targetDist = __instance.targetRange - (__instance.target.maxRadius + 50f);
        if (Physics.SphereCast(gun.transform.position + gun.transform.forward * 2f, 0.2f, gun.transform.forward, out var hit, __instance.attachedUnit?.maxRadius * 2f ?? 200f, -8193) || (hit.distance < targetDist && hit.distance > 1f))
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
        if (!ModAssets.i.ShipDefinitions.Contains(__instance.pilot.aircraft.definition)) return;

        if (__instance.player.GetButton("Countermeasures") && !__instance.pilot.aircraft.countermeasureTrigger)
        {
            __instance.pilot.aircraft.Countermeasures(true, __instance.pilot.aircraft.countermeasureManager.activeIndex);
        }

        if (__instance.player.GetButtonDown("Gear"))
        {
            if (__instance.pilot.aircraft.gearState == LandingGear.GearState.LockedExtended)
            {
                __instance.pilot.aircraft.SetGear(deployed: false);
            }
            else if (__instance.pilot.aircraft.gearState == LandingGear.GearState.LockedRetracted)
            {
                __instance.pilot.aircraft.SetGear(deployed: true);
            }
        }
    }
}

[HarmonyPatch(typeof(RadialMenuAction))]
public static class RadialMenuActionPatches
{
    [HarmonyPatch(nameof(RadialMenuAction.TriggerAction))]
    [HarmonyPostfix]
    private static void TriggerAction_Postfix(RadialMenuAction __instance, Aircraft aircraft)
    {
        if (!ModAssets.i.ShipDefinitions.Contains(aircraft.definition)) return;
        switch (__instance.actionType)
        {
            case RadialMenuAction.ActionType.Gear:
                if (aircraft.gearState == LandingGear.GearState.LockedExtended)
                {
                    aircraft.SetGear(deployed: false);
                }
                if (aircraft.gearState == LandingGear.GearState.LockedRetracted)
                {
                    aircraft.SetGear(deployed: true);
                }
                break;
            case RadialMenuAction.ActionType.Eject:
                break;
            case RadialMenuAction.ActionType.Radar:
                break;
            case RadialMenuAction.ActionType.NavLights:
                break;
            case RadialMenuAction.ActionType.FlightAssist:
                break;
            case RadialMenuAction.ActionType.AutoHover:
                break;
            case RadialMenuAction.ActionType.Engine:
                break;
            case RadialMenuAction.ActionType.Nightvis:
                break;
            case RadialMenuAction.ActionType.TurretAuto:
                break;
            case RadialMenuAction.ActionType.SelectWeapon:
                break;
            case RadialMenuAction.ActionType.LinkGuns:
                break;
            default:
                break;
        }
    }
}

[HarmonyPatch(typeof(Encyclopedia))]
public static class EncyclopediaPatches
{

}

[HarmonyPatch(typeof(FactionHQ))]
public static class FactionHQPatches
{
    [HarmonyPatch(nameof(FactionHQ.DeployAIAircraft))]
    [HarmonyPrefix]
    private static bool DeployAIAircraft_Prefix(FactionHQ __instance)
    {
        var hq = __instance;
        
        int count = hq.factionPlayers.Count;
        int num = 0;
        foreach (FactionHQ allHQ in FactionRegistry.GetAllHQs())
        {
            if (allHQ != hq)
            {
                num += allHQ.GetPlayers(false).Count;
            }
        }

        float num2 = (float)hq.AIAircraftLimit + (float)num * hq.addAIPerEnemyPlayer - (float)count * hq.reduceAIPerFriendlyPlayer;
        
        if ((float)hq.activeAIAircraft.Count >= num2)
        {
            return false;
        }

        List<AircraftDefinition> aircraft = Encyclopedia.i.aircraft;
        
        for (int i = 0; i < aircraft.Count; i++)
        {
            int index = UnityEngine.Random.Range(i, aircraft.Count);
            AircraftDefinition value = aircraft[i];
            aircraft[i] = aircraft[index];
            aircraft[index] = value;
        }
        
        int num3 = hq.reserveAirframes + count * hq.extraReservesPerPlayer;
        foreach (AircraftDefinition item2 in aircraft)
        {
            if (ModAssets.i.ShipDefinitions.Contains(item2)) continue;
            
            if (!hq.AircraftSupply.TryGetValue(item2, out var value2) || value2.Count <= num3)
            {
                continue;
            }

            foreach (var item3 in hq.airbasesSorted)
            {
                Airbase item = item3.airbase;
                if (item != null && item.CanSpawnAircraft(item2))
                {
                    Loadout loadout = null;
                    float fuelLevel = item2.aircraftParameters.DefaultFuelLevel;
                    
                    StandardLoadout randomStandardLoadout = item2.aircraftParameters.GetRandomStandardLoadout(item2, hq);
                    if (randomStandardLoadout != null)
                    {
                        loadout = randomStandardLoadout.loadout;
                        fuelLevel = randomStandardLoadout.FuelRatio;
                    }

                    int randomLiveryForFaction = item2.aircraftParameters.GetRandomLiveryForFaction(hq.faction);
                    
                    if (item.TrySpawnAircraft(null, item2, new LiveryKey(randomLiveryForFaction), loadout, fuelLevel).Allowed)
                    {
                        return false; 
                    }
                }
            }
        }
        
        return false;
    }  
}

[HarmonyPatch(typeof(Pilot))]
public static class PilotPatches
{
    [HarmonyPatch(nameof(Pilot.ApplyDamage))]
    [HarmonyPrefix]
    private static void ApplyDamage_Prefix(Pilot __instance, ref float impactDamage)
    {
        if (__instance.aircraft != null && ModAssets.i.ShipDefinitions.Contains(__instance.aircraft.definition))
            impactDamage = 0f;
    }
}

[HarmonyPatch(typeof(Missile))]
public static class MissilePatches
{
}

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

[HarmonyPatch(typeof(AIHeloTakeoffState))]
public static class AIHeloTakeoffStatePatches
{
    /*[HarmonyPatch(nameof(AIHeloTakeoffState.FixedUpdateState))]
    [HarmonyPostfix]
    private static void FixedUpdateState_Postfix(AIHeloTakeoffState __instance)
    {
        if (__instance.aircraft.radarAlt > 1f)
        {
            var ac = __instance.aircraft;
            ac?.GetControlsFilter().SetAutoHover(enabled: true);
            ac?.SetFlightAssist(enabled: false);
            var vel = ac?.hit.collider?.attachedRigidbody?.velocity ?? Vector3.zero;
            ac?.autopilot?.Hover(ac.GlobalPosition() + vel, 50f, ac.transform.forward);
        }
    }*/
}

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
            else if (faction != null && faction.TryGetNearestAircraft(position, out var nearestAircraft, out nearestDistance, tempAircraft) && nearestDistance < 250000f && ModAssets.i.ShipDefinitionsWithDeployer.Contains(nearestAircraft.definition))
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

[HarmonyPatch(typeof(TargetCam))]
public static class TargetCamPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(TargetCam.Update))]
    private static bool Update_Prefix(TargetCam __instance)
    {
        if (__instance.aircraft == null || __instance.aircraft.Player == null || !__instance.aircraft.Player.IsLocalPlayer) return false;

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(TargetCam.Initialize))]
    private static bool Initialize_Prefix(TargetCam __instance)
    {
        if (__instance.aircraft.Player == null) return false;
        return true;
    }
}

[HarmonyPatch(typeof(BulletSim.Bullet))]
public class BulletPatches
{
    /*[HarmonyPostfix]
    [HarmonyPatch(nameof(BulletSim.Bullet.TrajectoryTrace))]
    private static void TrajectoryTrace_Postfix(BulletSim.Bullet __instance, WeaponInfo info, Unit owner)
    {
        if (!ModAssets.i.ShipDefinitions.Contains(owner.definition)) return;
        if (__instance.impacted && !__instance.active && info.blastDamage > 0)
        {
            if (NetworkManagerNuclearOption.i.Server.Active)
            {
                DamageEffects.BlastFrag(info.blastDamage, __instance.position.ToLocalPosition(), owner.persistentID, PersistentID.None);
            }
        }
    }*/
}

[HarmonyPatch(typeof(EncyclopediaBrowser))]
public class EncyclopediaBrowserPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncyclopediaBrowser.SpawnAircraft))]
    private static void SpawnAircraft_Prefix(EncyclopediaBrowser __instance, UnitDefinition definition)
    {
        if (ModAssets.i.ShipDefinitions.Contains(definition))
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