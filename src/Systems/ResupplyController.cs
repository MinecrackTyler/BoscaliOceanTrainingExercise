using System.Collections;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace NOComponentWIP.Systems;

public class ResupplyController : NetworkBehaviour
{
	[SerializeField] private Aircraft aircraft;
	[SerializeField] private AircraftDefinition[] resupplyAircrafts;
	[SerializeField] private Airbase attachedAirbase;
	private bool resupplyCalled;
	[SyncVar] private Aircraft resupplyAircraft;

	public float ResupplyDistance => (resupplyAircraft != null && !resupplyAircraft.Networkdisabled)
		? Vector3.Distance(aircraft.GlobalPosition().AsVector3(), resupplyAircraft.GlobalPosition().AsVector3()) 
		: -1f;
	
	public bool ResupplyCalled => resupplyCalled;

	private Player player;

	private void Update()
	{
		if (!aircraft.LocalSim || aircraft.Player == null) return;
		player = aircraft.Player;
		if (player == null) return;
		if (!GameManager.IsLocalAircraft(aircraft)) return;
		
		if (GameManager.playerInput.GetButtonDown($"{Mod_Input.ModShortName}:Call Resupply") && !resupplyCalled)
		{
			RequestResupply(false);
		} else if (GameManager.playerInput.GetButtonDown($"{Mod_Input.ModShortName}:Call Resupply - Player") && GameManager.gameState == GameState.SinglePlayer)
		{
			RequestResupply(true);
		}
	}

	public void RequestResupply(bool player)
	{
		CmdRequestResupply(player);
	}
	
	[ServerRpc]
	private void CmdRequestResupply(bool player)
	{
		if (resupplyCalled && !player) return;
		if (player && GameManager.gameState != GameState.SinglePlayer) return;
		StartCoroutine(ResupplyCoroutine(player));
		if (!player)
		{
			resupplyCalled = true;
		}
	}

	private IEnumerator ResupplyCoroutine(bool player)
	{
		if (!aircraft.NetworkHQ.GetNearestAircraftCapableAirbase(aircraft.transform.position, resupplyAircrafts, out var airbase, attachedAirbase)) yield break;

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

		if (player)
		{
			AircraftSwitcher.i.SwitchAircraft(this.player, aircraft, resupplyAircraft);
			resupplyAircraft.onDisableUnit += ReturnToAircraft;
		}
		else
		{
			var playerTransportState = new AIHeloPlayerNavalResupply(spawnedAircraft, aircraft);
			pilot.SwitchState(playerTransportState);
		}
		
		
	}

	private void ReturnToAircraft(Unit unit)
	{
		AircraftSwitcher.i.SwitchAircraft(this.player, resupplyAircraft, aircraft);
		resupplyAircraft.onDisableUnit -= ReturnToAircraft;
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