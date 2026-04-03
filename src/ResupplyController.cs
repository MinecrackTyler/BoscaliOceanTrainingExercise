using System;
using System.Collections;
using System.Linq;
using Mirage;
using UnityEngine;

namespace NOComponentWIP;

public class ResupplyController : NetworkBehaviour
{
	[SerializeField] private Aircraft aircraft;
	[SerializeField] private AircraftDefinition[] resupplyAircrafts;
	private bool resupplyCalled;
	[SyncVar] private Aircraft resupplyAircraft;

	public float ResupplyDistance => (resupplyAircraft != null && !resupplyAircraft.Networkdisabled)
		? Vector3.Distance(aircraft.GlobalPosition().AsVector3(), resupplyAircraft.GlobalPosition().AsVector3()) 
		: -1f;
	
	public bool ResupplyCalled => resupplyCalled;

	private void Update()
	{
		if (!aircraft.LocalSim || aircraft.Player == null) return;
		var player = aircraft.pilots[0]?.playerState?.player;
		if (player == null) return;

		if (resupplyCalled) return;
		if (player.GetButtonDown("Call Resupply"))
		{
			CmdRequestResupply();
			resupplyCalled = true;
		}
	}
	
	[ServerRpc]
	private void CmdRequestResupply()
	{
		if (resupplyCalled) return;
		StartCoroutine(ResupplyCoroutine());
	}

	private IEnumerator ResupplyCoroutine()
	{
		if (!aircraft.NetworkHQ.GetNearestAircraftCapableAirbase(aircraft.transform.position, resupplyAircrafts, out var airbase)) yield break;

		var def = resupplyAircrafts[0];
		var livery = new LiveryKey(def.aircraftParameters.GetRandomLiveryForFaction(aircraft.NetworkHQ.faction));
		var loadout = def.aircraftParameters.StandardLoadouts[7];
		var result = airbase.TrySpawnAircraft(null, resupplyAircrafts[0], livery, loadout.loadout, loadout.FuelRatio);
		yield return new WaitForFixedUpdate();
		if (!result.Allowed || result.Hangar == null) yield break;
		if (result.DelayedSpawn)
		{
			var currentObj = result.Hangar.spawnedObject;

			var spawnWait = new WaitUntilOrTimeout(() => result.Hangar.spawnedObject != currentObj, 10f);
			yield return spawnWait;
			
			if (spawnWait.IsTimeout) yield break;
		}
		SoundManager.PlayInterfaceOneShot(GameAssets.i.radioStatic);

		var spawnedAircraft = result.Hangar.spawnedObject.GetComponent<Aircraft>();
		var pilot = spawnedAircraft?.pilots[0];
		if (spawnedAircraft == null || pilot == null) yield break;
		resupplyAircraft = spawnedAircraft;
		
		var takeoffWait = new WaitUntilOrTimeout(() => pilot.currentState is AIHeloTransportState, 120f);
		yield return takeoffWait;
		if (takeoffWait.IsTimeout) yield break;
		
		var playerTransportState = new AIHeloPlayerNavalResupply(spawnedAircraft, aircraft);
		pilot.SwitchState(playerTransportState);
	}
}

public class WaitUntilOrTimeout(System.Func<bool> predicate, float timeout) : CustomYieldInstruction
{
	private readonly float _timeoutTime = Time.timeSinceLevelLoad + timeout;

	public bool IsTimeout { get; private set; }

	public override bool keepWaiting
	{
		get
		{
			if (predicate())
			{
				return false;
			}
			
			if (Time.timeSinceLevelLoad >= _timeoutTime)
			{
				IsTimeout = true;
				return false;
			}

			return true;
		}
	}
}

public static class Extensions
{
	public static bool GetNearestAircraftCapableAirbase(this FactionHQ hq, Vector3 position, AircraftDefinition[] definitions, out Airbase validAirbase)
	{
		validAirbase = null;
		if (hq == null) return false;
		
		var sortedBases = hq.airbasesUnsorted
			.Select(item => item.Value)
			.Where(ab => ab != null && !ab.disabled && ab.CurrentHQ == hq)
			.OrderBy(ab => Vector3.Distance(position, ab.transform.position));

		foreach (Airbase airbase in sortedBases)
		{
			foreach (var hangar in airbase.hangars)
			{
				if (hangar != null && !hangar.Disabled && hangar.availableAircraft.Any(definitions.Contains))
				{
					validAirbase = airbase;
					return true; 
				}
			}
		}

		return false;
	}
}