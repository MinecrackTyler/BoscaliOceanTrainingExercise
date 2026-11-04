using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;
using BoscaliOceanTrainingExercise.Systems;

namespace BoscaliOceanTrainingExercise;

public class NetworkMissileLauncher : Weapon
{
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private float reloadTime = 5f;
    [SerializeField] private int magazineCount = 0;

    [SerializeField] private MissileDefinition missile;
    [SerializeField] private Transform[] launchTransforms;
    [SerializeField] private Vector3 ejectionVelocity;

    [SerializeField] private AudioSource launchSound;
    [SerializeField] private AudioSource reloadSound;
    [SerializeField] private ParticleSystem launchParticles;

    [SerializeField] private BayDoor[] bayDoors;
    [SerializeField] private float doorOpenDuration = 0.5f;
    private int maxAmmo;
    private int loadedMissiles;
    private int reserveMissiles;
    private int currentTube;
    private float reloadEndTime;
    private bool reloading;

    private readonly SemaphoreSlim firingSemaphore = new SemaphoreSlim(1, 1);
    private float lastLaunchFinishTime;

    public bool Reloading => reloading;

    private int magazineCapacity;
    private int ReserveMagazineCount => Mathf.Max(0, magazineCount);
    
    public float FireInterval => fireInterval;

    private void OnEnable()
    {
        magazineCapacity = ammo;
        ResetAmmoState();
    }

    private void OnDestroy()
    {
        firingSemaphore?.Dispose();
    }

    private void ResetAmmoState()
    {
        int capacity = magazineCapacity;

        loadedMissiles = capacity;
        reserveMissiles = ReserveMagazineCount * capacity;
        maxAmmo = loadedMissiles + reserveMissiles;

        ammo = maxAmmo;
        currentTube = 0;
        reloading = false;
        reloadEndTime = 0f;

        ReportReloading(false);
        if (launchTransforms != null)
        {
            foreach (Transform launchTransform in launchTransforms)
            {
                launchTransform?.gameObject.SetActive(true);
            }
        }
    }

    public override void Fire(Unit owner, Unit target, Vector3 inheritedVelocity, WeaponStation weaponStation, GlobalPosition aimpoint)
    {
        this.weaponStation = weaponStation;

        if (owner == null
            || weaponStation == null
            || Safety
            || reloading
            || magazineCapacity == 0
            || loadedMissiles <= 0
            || ammo <= 0
            || Time.timeSinceLevelLoad - lastFired < fireInterval
            || currentTube < 0)
        {
            return;
        }
        
        QueuedFire(owner, target, inheritedVelocity, weaponStation, aimpoint).Forget();
        if (owner is Aircraft aircraft)
        {
            if (aircraft.IsServer)
            {
                aircraft.RpcLaunchMissile(weaponStation.Number, target, aimpoint);
            }
            else if (aircraft.HasAuthority)
            {
                aircraft.CmdLaunchMissile(weaponStation.Number, target, aimpoint);
            }
        }
    }

    private async UniTaskVoid QueuedFire(
        Unit owner,
        Unit target,
        Vector3 inheritedVelocity,
        WeaponStation weaponStation,
        GlobalPosition aimpoint)
    {
        await firingSemaphore.WaitAsync(destroyCancellationToken);

        try
        {
            if ((bayDoors != null && bayDoors.Length > 0) || hardpoint?.bayDoors?.Length > 0)
            {
                foreach (BayDoor bayDoor in bayDoors)
                    bayDoor?.OpenDoor(doorOpenDuration);

                hardpoint?.SpringOpenBayDoors();

                await UniTask.WaitUntil(
                    AreBayDoorsFullyOpen,
                    cancellationToken: destroyCancellationToken
                );
            }

            float timeSinceLastFinish = Time.timeSinceLevelLoad - lastLaunchFinishTime;
            if (timeSinceLastFinish < fireInterval)
            {
                int delayMs = Mathf.RoundToInt((fireInterval - timeSinceLastFinish) * 1000f);
                await UniTask.Delay(delayMs, cancellationToken: destroyCancellationToken);
            }

            if (this == null
                || owner == null
                || weaponStation == null
                || Safety
                || reloading
                || magazineCapacity == 0
                || loadedMissiles <= 0
                || ammo <= 0
                || currentTube < 0)
            {
                return;
            }

            Transform launchTransform = launchTransforms[currentTube];
            if (launchTransform == null)
            {
                return;
            }

            foreach (BayDoor bayDoor in bayDoors)
                bayDoor?.OpenDoor(doorOpenDuration);

            TrackFiringVisibility().Forget();
            lastFired = Time.timeSinceLevelLoad;

            if (owner.IsServer)
            {
                Vector3 velocity =
                    inheritedVelocity +
                    ejectionVelocity.x * launchTransform.right +
                    ejectionVelocity.y * launchTransform.up +
                    ejectionVelocity.z * launchTransform.forward;

                Missile spawnedMissile = NetworkSceneSingleton<Spawner>.i.SpawnMissile(
                    missile,
                    launchTransform.position,
                    launchTransform.rotation,
                    velocity,
                    target,
                    owner
                );
                
                if (spawnedMissile != null && owner is Aircraft aircraft)
                {
                    CombatDisembark.RegisterLaunch(aircraft, missile);
                }
            }

            if (launchParticles != null)
            {
                launchParticles.transform.position = launchTransform.position;
                launchParticles.transform.rotation = launchTransform.rotation;
                launchParticles.Play();
            }

            launchSound?.Play();

            launchTransform.gameObject.SetActive(false);
            loadedMissiles--;
            ammo--;
            currentTube++;
            if (currentTube >= launchTransforms.Length)
            {
                currentTube = 0;
            }

            weaponStation.UpdateLastFired(0);
            weaponStation.AccountAmmo();
            weaponStation.Updated();

            if (attachedUnit != null && attachedUnit.IsServer)
            {
                attachedUnit.RpcSyncAmmoCount(weaponStation.Number, weaponStation.Ammo);
                attachedUnit.NetworkHQ?.missionStatsTracker.MunitionCost(attachedUnit, info.costPerRound);
            }

            lastLaunchFinishTime = Time.timeSinceLevelLoad;

            if (loadedMissiles == 0 && reserveMissiles > 0)
            {
                BeginReload().Forget();
            }
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"QueuedFire failed: {ex}");
        }
        finally
        {
            firingSemaphore.Release();
        }
    }

    private bool AreBayDoorsFullyOpen()
    {
        if ((bayDoors == null || bayDoors.Length == 0) && (hardpoint?.bayDoors == null || hardpoint?.bayDoors.Length == 0))
        {
            return true;
        }

        foreach (BayDoor bayDoor in bayDoors)
        {
            if (bayDoor != null && bayDoor.openAmount < 0.95f)
            {
                return false;
            }
        }
        
        foreach (BayDoor bayDoor in hardpoint.bayDoors)
        {
            if (bayDoor != null && bayDoor.openAmount < 0.95f)
            {
                return false;
            }
        }

        return true;
    }

    private async UniTaskVoid BeginReload()
    {
        if (reloading || reserveMissiles <= 0 || magazineCapacity <= 0)
        {
            return;
        }

        reloading = true;
        reloadEndTime = Time.timeSinceLevelLoad + reloadTime;
        ReportReloading(true);
        weaponStation?.Updated();
        reloadSound?.Play();
        try
        {
            await UniTask.Delay(
                Mathf.RoundToInt(reloadTime * 1000f),
                cancellationToken: destroyCancellationToken
            );
        }
        catch
        {
            return;
        }

        if (this == null)
        {
            return;
        }

        int amountToLoad = Mathf.Min(magazineCapacity, reserveMissiles);
        loadedMissiles = amountToLoad;
        reserveMissiles -= amountToLoad;
        currentTube = 0;
        reloading = false;
        reloadEndTime = 0f;

        ammo = loadedMissiles + reserveMissiles;

        if (launchTransforms != null)
        {
            foreach (Transform launchTransform in launchTransforms)
            {
                launchTransform?.gameObject.SetActive(true);
            }
        }

        ReportReloading(false);
        weaponStation?.Updated();
    }

    public override void Rearm(int ammoToRearm, WeaponStation weaponStation)
    {
        if (!Rearmable)
        {
            return;
        }

        ResetAmmoState();
        weaponStation?.Updated();

        if (attachedUnit != null && attachedUnit.IsServer && weaponStation != null)
        {
            attachedUnit.RpcSyncAmmoCount(weaponStation.Number, weaponStation.Ammo);
        }
    }

    public override int GetAmmoLoaded()
    {
        return loadedMissiles;
    }

    public override int GetAmmoTotal()
    {
        return ammo;
    }

    public override int GetFullAmmo()
    {
        return maxAmmo;
    }

    public override float GetReloadProgress()
    {
        if (!reloading || reloadTime <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01((reloadEndTime - Time.timeSinceLevelLoad) / reloadTime);
    }

    public override bool HasMagazines()
    {
        return magazineCapacity > 0;
    }
}
