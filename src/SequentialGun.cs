using System;
using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using UnityEngine;

namespace NOComponentWIP;

public class SequentialGun : Gun
{
    private int _currentMuzzleIndex = 0;

    [Header("Sequential Configuration")]
    [SerializeField] private Muzzle[] muzzleArray;
    
    private void FixedUpdate()
    {
        if (queuedBullets < 1f && Time.timeSinceLevelLoad - lastFired > fireInterval)
        {
            queuedBullets = 1f;
        }
        queuedBullets = ((ticksSinceTriggerPull < 2) ? (queuedBullets + Time.fixedDeltaTime * fireRate * 0.01667f) : 0f);
        queuedBullets = Mathf.Min(queuedBullets, bulletsLoaded);
        
        int num = 0;
        while (queuedBullets >= 1f)
        {
            queuedBullets -= 1f;
            SpawnBullet(queuedBullets * fireInterval);
            num++;
            ammo--;
            bulletsLoaded--;
            weaponStation.Updated();
        }
        
        if (num > 0)
        {
            lastFired = Time.timeSinceLevelLoad;
            weaponStation.UpdateLastFired(num);
            if (spinRate > 0f && FastMath.InRange(spinTransform.position, SceneSingleton<CameraStateManager>.i.transform.position, 100f))
            {
                if (currentSpinRate <= 0f) SpinBarrels().Forget();
                currentSpinRate = 1f;
            }
        }

        if (bulletsLoaded == 0 && !reloading && magazines > 0)
        {
            timeUntilReload = reloadTime;
            reloading = true;
            ReportReloading(true);
            magazines--;
            if (reloadSound != null) reloadSound.Play();
        }

        if (sources.Length > 1) LoopSounds();
        if (heatEnabled) heat.Update(Time.deltaTime);
        
        if (recoilTravel > 0f)
        {
            foreach (var m in muzzleArray)
            {
                if (m.recoilTransform == null) continue;
                
                m.muzzleRecoilPosition += (m.muzzleRecoilEnergy > 0f) 
                    ? (recoilRate * Time.deltaTime) 
                    : (-recoilReturnRate * Time.deltaTime);

                if (m.muzzleRecoilPosition >= 1f) m.muzzleRecoilEnergy = 0f;
                m.muzzleRecoilPosition = Mathf.Clamp01(m.muzzleRecoilPosition);
                
                m.recoilTransform.localPosition = new Vector3(
                    m.initialLocalPosition.x, 
                    m.initialLocalPosition.y, 
                    m.initialLocalPosition.z - (m.muzzleRecoilPosition * recoilTravel)
                );
            }
        }
        
        if (timeUntilReload > 0f)
        {
            timeUntilReload -= Time.fixedDeltaTime;
            if (timeUntilReload <= 0f)
            {
                bulletsLoaded = magazineCapacity;
                reloading = false;
                ReportReloading(false);
                weaponStation.Updated();
            }
        }
        else if (Time.timeSinceLevelLoad - lastFired > 1f && (!heatEnabled || heat.GetNormalisedHeat() <= 0f))
        {
            base.enabled = false;
        }
    }

    private new void SpawnBullet(float timeOffset)
    {
        if (muzzleArray == null || muzzleArray.Length == 0) return;
        
        Muzzle activeMuzzle = muzzleArray[_currentMuzzleIndex];
        
        TrackFiringVisibility().Forget();
        ShotSound();
        if (ejectionTransform != null) SpawnEjection().Forget();
        if (heatEnabled) heat.GunFired();
        
        activeMuzzle.muzzleRecoilEnergy = 1f;
        
        if (attachedUnit.LocalSim && attachedUnit.rb != null)
        {
            attachedUnit.rb.AddForceAtPosition(-activeMuzzle.muzzleTransform.forward * recoilImpulse, activeMuzzle.muzzleTransform.position, ForceMode.Impulse);
        }
        
        if (activeMuzzle.muzzleParticles != null)
        {
            foreach (var p in activeMuzzle.muzzleParticles) p.Play();
        }
        
        if (attachedUnit.LocalSim && fireInterval > 0.2f)
        {
            attachedUnit.SingleRemoteFire(weaponStation.Number, weaponStation.Ammo - 1);
        }
        
        Vector3 inheritVector = (velocityInherit != null) ? velocityInherit.velocity : Vector3.zero;
        tracerSeed++;
        bool isTracer = tracerSeed > tracerRatio;
        if (isTracer) tracerSeed -= tracerRatio;

        if (hardpoint != null) hardpoint.ModifyMass(-info.massPerRound);

        bool isServer = NetworkManagerNuclearOption.i.Server.Active;
        
        if (guidedProjectile != null && isServer)
        {
            NetworkSceneSingleton<Spawner>.i.SpawnMissile(guidedProjectile, activeMuzzle.muzzleTransform.position, activeMuzzle.muzzleTransform.rotation, inheritVector + activeMuzzle.muzzleTransform.forward * muzzleVelocity, proximityFuseTarget, attachedUnit);
        }
        else
        {
            if (bulletSim == null) bulletSim = BulletSim.Create(attachedUnit, this, weaponStation.GetTurret());
            bulletSim.AddBullet(activeMuzzle.muzzleTransform, inheritVector + activeMuzzle.muzzleTransform.forward * muzzleVelocity, bulletSpread, bulletSelfDestruct, isTracer, tracerSize, tracerColor, timeOffset, proximityFuseTarget);
        }

        if (isServer && attachedUnit?.NetworkHQ != null)
        {
            attachedUnit.NetworkHQ.missionStatsTracker.MunitionCost(attachedUnit, info.costPerRound);
        }

        _currentMuzzleIndex = (_currentMuzzleIndex + 1) % muzzleArray.Length;
    }
}

public class Muzzle : MonoBehaviour
{
    public Transform muzzleTransform;
    public Transform recoilTransform;
    public ParticleSystem[] muzzleParticles;
        
    [HideInInspector] public float muzzleRecoilEnergy;
    [HideInInspector] public float muzzleRecoilPosition;
    
    // Stores the original local position from the editor
    [HideInInspector] public Vector3 initialLocalPosition;

    private void Awake()
    {
        if (recoilTransform != null)
        {
            initialLocalPosition = recoilTransform.localPosition;
        }
    }
}