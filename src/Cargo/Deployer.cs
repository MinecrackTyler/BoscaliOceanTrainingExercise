using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NOComponentWIP;

public class Deployer : Weapon
{
	[SerializeField] private float deployCooldown = 1f;
	private DeploymentManager manager;
	
	public override void AttachToHardpoint(Aircraft aircraft, Hardpoint hardpoint, WeaponMount weaponMount)
	{
		base.AttachToHardpoint(aircraft, hardpoint, weaponMount);
		manager = aircraft.GetComponent<DeploymentManager>();
	}


	public override int GetAmmoTotal()
	{
		int count = 0;
		
		count += manager?.unitManifest?.Count ?? 0;
		if (manager?.HasFOB ?? false) count += 1;
		
		return count;

	} 
	public override int GetAmmoLoaded() => GetAmmoTotal();
	
	public override void Fire(Unit owner, Unit target, Vector3 inheritedVelocity, WeaponStation weaponStation, GlobalPosition aimpoint)
	{
		if (owner is not Aircraft aircraft || manager == null) return;
		
		if ((manager.IsEmpty() && !manager.HasFOB) || Time.timeSinceLevelLoad - lastFired < deployCooldown)
		{
			return;
		}

		lastFired = Time.timeSinceLevelLoad;
		weaponStation.UpdateLastFired(1);
		
		if (aircraft.IsServer)
		{
			aircraft.RpcLaunchMissile(weaponStation.Number, target, aimpoint);
		} 
		else if (aircraft.HasAuthority)
		{
			aircraft.CmdLaunchMissile(weaponStation.Number, target, aimpoint);
		}
		
		if (aircraft.IsServer) {
			manager.DeployUnit();
		}
		
		weaponStation.AccountAmmo();
		weaponStation.Updated();
		ReportReloading(true);
		WaitForReload().Forget();
	}

	private async UniTask WaitForReload()
	{
		CancellationToken cancel = base.destroyCancellationToken;
		await UniTask.Delay((int)(deployCooldown * 1000f));
		if (!cancel.IsCancellationRequested)
		{
			ReportReloading(false);
		}
	}

	private void Update()
	{
		if (weaponStation is null) return;
		if (weaponStation.Ammo != GetAmmoTotal())
		{
			weaponStation.AccountAmmo();
		}
	}
}