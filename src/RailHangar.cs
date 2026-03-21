using System.Threading;
using Cysharp.Threading.Tasks;
using Mirage;
using UnityEngine;

namespace NOComponentWIP;

public class RailHangar : Hangar
{
	[SerializeField] private LaunchRail launchRail;
	public LaunchRail LaunchRail => launchRail;
	
	[Server]
	public async UniTask DoorSequenceRailLauncher(QueuedAircraftToSpawn spawnAircraft)
	{
		CancellationToken destroyToken = this.destroyCancellationToken;

		try
		{
			await OpenDoors();
			if (destroyToken.IsCancellationRequested) return;
			await UniTask.Yield();
			
			if (!destroyToken.IsCancellationRequested && IsFunctional())
			{
				SpawnAircraft(
					spawnAircraft.player, 
					spawnAircraft.definition, 
					spawnAircraft.loadout, 
					spawnAircraft.fuelLevel, 
					spawnAircraft.livery
				);
			}
			
			await WaitForUnitToLeave(destroyToken);
			if (destroyToken.IsCancellationRequested) return;
			await CloseDoors();

			if (!destroyToken.IsCancellationRequested)
			{
				this.Networkavailable = true;
				this.RpcHangarAvailable();
			}
		}
		finally
		{
			if (spawnAircraft.player != null && this.IsServer)
			{
				spawnAircraft.player.RpcClearSpawnPending();
			}
		}
	}
}