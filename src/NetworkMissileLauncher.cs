using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NOComponentWIP;

public class NetworkMissileLauncher : MissileLauncher
{
	public override void Fire(Unit owner, Unit target, Vector3 inheritedVelocity, WeaponStation weaponStation, GlobalPosition aimpoint)
    {
        if (ammo <= 0 || !(Time.timeSinceLevelLoad - lastFired > reloadTime))
        {
            return;
        }
        if (launchTransforms.Length != 0)
        {
            launchTransform = launchTransforms[currentCell].transform;
            currentCell++;
            if (currentCell > launchTransforms.Length - 1)
            {
                currentCell = 0;
            }
        }
        else if (cellColumns > 0)
        {
            launchTransform.localPosition = new Vector3((float)(currentCell % cellColumns) * cellSeperation, (float)(currentCell / cellColumns % cellRows) * cellSeperation, 0f);
            currentCell++;
            if (currentCell > cellRows * cellColumns)
            {
                currentCell = 0;
            }
        }
        TrackFiringVisibility().Forget();
        lastFired = Time.timeSinceLevelLoad;
        weaponStation.UpdateLastFired(1);
        if (owner is Aircraft aircraft)
        {
            if (aircraft.IsServer)
            {
                aircraft.RpcLaunchMissile(weaponStation.Number, target, aimpoint);
            } else if (aircraft.HasAuthority)
            {
                aircraft.CmdLaunchMissile(weaponStation.Number, target, aimpoint);
            }
        }
        
        if (owner.IsServer)
        {
            Vector3 velocity = inheritedVelocity + ejectionVelocity.x * launchTransform.right + ejectionVelocity.y * launchTransform.up + ejectionVelocity.z * launchTransform.forward;
            NetworkSceneSingleton<Spawner>.i.SpawnMissile(missile, launchTransform.position, launchTransform.rotation, velocity, target, owner);
        }
        if (launchParticles != null)
        {
            launchParticles.transform.position = launchTransform.position;
            launchParticles.Play();
        }
        if (launchSound != null)
        {
            launchSound.pitch = Random.Range(0.95f, 1.05f);
            launchSound.Play();
        }
        ammo--;
        weaponStation.AccountAmmo();
        weaponStation.Updated();
        ReportReloading(reloading: true);
        if (attachedUnit.IsServer)
        {
            attachedUnit.RpcSyncAmmoCount(weaponStation.Number, weaponStation.Ammo);
        }
        if (ammo > 0)
        {
            WaitForReload().Forget();
        }
        if (attachedUnit != null && attachedUnit.NetworkHQ != null && attachedUnit.IsServer)
        {
            attachedUnit.NetworkHQ.missionStatsTracker.MunitionCost(attachedUnit, info.costPerRound);
        }
    }
}