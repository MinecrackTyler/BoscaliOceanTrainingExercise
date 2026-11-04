using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace BoscaliOceanTrainingExercise.Patches;

[HarmonyPatch(typeof(Aircraft))]
public class AircraftPatches
{
	[HarmonyPatch(nameof(Aircraft.SetSimplePhysics))]
	[HarmonyPrefix]
	static bool SetSimplePhysics_Prefix(Aircraft __instance)
	{
		if (!__instance.TryGetShipBridge(out var bridge)) return true;

		var rb = __instance.rb;
		
		ColorLog<Unit>.Info("Setting " + __instance.unitName + " physics to Simplified");
		foreach (UnitPart item in __instance.partLookup)
		{
			(item as AeroPart)?.MergeWithParent();
		}
		rb.mass = __instance.definition.mass;
		rb.ResetCenterOfMass();
		rb.ResetInertiaTensor();
		__instance.simplePhysics = true;
		rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
		
		bridge.SetSimplePhysics();
		return false;
	}
	
	[HarmonyPatch(nameof(Aircraft.SetComplexPhysics))]
	[HarmonyPrefix]
	static bool SetComplexPhysics_Prefix(Aircraft __instance)
	{
		if (!__instance.TryGetShipBridge(out var bridge)) return true;
		
		bridge.RaiseLcacToSurface();
		
		ColorLog<Unit>.Info("Setting " + __instance.unitName + " physics to Complex");
		foreach (UnitPart item in __instance.partLookup)
		{
			(item as AeroPart)?.CreateRB(__instance.rb.GetPointVelocity(item.transform.position), Vector3.zero);
		}
		foreach (UnitPart item2 in __instance.partLookup)
		{
			(item2 as AeroPart)?.CreateJoints();
		}
		__instance.simplePhysics = false;
		
		bridge.SetComplexPhysics();
		
		__instance.rb.ResetCenterOfMass();
		return false;
	}

	[HarmonyPatch(nameof(Aircraft.ReturnToInventory))]
	[HarmonyPrefix]
	static bool ReturnToInventory_Prefix(Aircraft __instance, ref bool __state)
	{
		__state = false;
		if (!__instance.IsServer) return true;
		var aircraft = __instance;
		
		if (aircraft.TryGetShipBridge(out var bridge) && bridge.UnsafeCombatDisembarkCommitted)
		{
			if (!aircraft.disabled)
				aircraft.DisableUnit();
			return false;
		}
		
		if (aircraft.speed < 2f && aircraft.NetworkHQ != null && aircraft.NetworkHQ.AnyNearAirbase(aircraft.transform.position, out var airbase) && aircraft.transform.position.y > Datum.LocalSeaY)
		{
			var attachedUnit = airbase.attachedUnit;
			if (attachedUnit == null) return true;
			if (aircraft.Player != null) return true;
			var deployManager = attachedUnit.GetComponent<DeploymentManager>();
			if (deployManager == null) return true;

			if (ModAssets.i.AllDeployableUnits.TryGetValue(aircraft.definition.jsonKey, out var unit))
			{
				if (deployManager.deployableUnits.Contains(unit))
				{
					deployManager.AddUnit(unit);
					__state = true;
				}
			}
		}
		
		return true;
	}

	[HarmonyPatch(nameof(Aircraft.ReturnToInventory))]
	[HarmonyPostfix]
	static void ReturnToInventory_Postfix(Aircraft __instance, bool __state)
	{
		if (!__state) return;
		__instance.NetworkHQ.AddSupplyUnit(__instance.definition, -1);
	}

	[HarmonyPatch(nameof(Aircraft.FixedUpdate))]
	[HarmonyPostfix]
	static void FixedUpdate_Postfix(Aircraft __instance)
	{
		var ac = __instance;
		if (!__instance.definition.IsShipDefinition()) return; 
		if (ac.hit.collider != null && ac.hit.collider.attachedRigidbody != null)
		{
			var velocity = ac.cockpit.rb.velocity;
			ac.speed = velocity.magnitude;
		}
	}
	
	[HarmonyPatch(nameof(Aircraft.EjectionSequence))]
	[HarmonyPrefix]
	private static void EjectionSequence_Prefix(Aircraft __instance)
	{
		if (!__instance.TryGetShipBridge(out var bridge)) return;
		var ship = __instance;
		bridge.UnsafeCombatDisembarkCommitted = bridge.CombatDisembarkLocked;
		if (bridge.UnsafeCombatDisembarkCommitted || ship.NetworkHQ == null) return;
		
		var ab = ship.GetComponent<Airbase>();
		
		bool extendedSafeRecovery = ship.speed < 10f && ship.NetworkHQ.AnyNearAirbaseInRange(ship.transform.position, out _, 2000f, ab);
		bool vanillaSafeRecovery = ship.speed < 2f && ship.NetworkHQ.AnyNearAirbase(ship.transform.position, out _);
		if (extendedSafeRecovery && !vanillaSafeRecovery)
		{
			ship.ReturnToInventory();
		}
	}

	[HarmonyPatch(nameof(Aircraft.CheckRadarAlt))]
	[HarmonyPrefix]
	private static bool CheckRadarAlt_Prefix(Aircraft __instance)
	{
		if (!__instance.definition.IsShipDefinition()) return true;
		
		if (Physics.Linecast(__instance.transform.position, __instance.transform.position - Vector3.up * 10000f, out __instance.hit,
			    (int)PhysicsLayers.StaticsMask | (int)PhysicsLayers.ShipsMask))
		{
			__instance.radarAlt = __instance.hit.distance;
		}
		else
		{
			__instance.radarAlt = __instance.transform.position.GlobalY();
		}
		__instance.radarAlt -= __instance.definition.spawnOffset.y;
		__instance.radarAlt = Mathf.Clamp(__instance.radarAlt, 0f, __instance.transform.position.GlobalY() - __instance.definition.spawnOffset.y);
		
		return false;
	}
	
	[HarmonyPatch(nameof(Aircraft.OnStartClient))]
	[HarmonyPrefix]
	private static void OnStartClient_Prefix(Aircraft __instance, ref Vector3 __state)
	{
		if (!__instance.definition.IsShipDefinition()) return;

		__state = __instance.definition.spawnOffset;

		if (!__instance.IsServer && __instance.NetworkspawningHangar != null)
		{
			bool attachedUnit = __instance.NetworkspawningHangar.attachedUnit is Ship or Aircraft;
			bool isDock = __instance.NetworkspawningHangar.attachedUnit != null && __instance.NetworkspawningHangar.attachedUnit.definition == ModAssets.i.dockDef;

			Transform spawnTransform = __instance.NetworkspawningHangar.spawnTransform;
		
			if (attachedUnit)
			{
				__instance.definition.spawnOffset.z -= 200f; 
			}

			if (attachedUnit || isDock)
			{
				var difference = spawnTransform.GlobalPosition().y - Datum.SeaLevel.y;
				__instance.definition.spawnOffset.y -= difference;
			}
		}
	}
	

	[HarmonyPatch(nameof(Aircraft.OnStartClient))]
	[HarmonyPostfix]
	private static void OnStartClient_Postfix(Aircraft __instance, ref Vector3 __state)
	{
		if (!__instance.definition.IsShipDefinition()) return;

		if (__instance.LocalSim)
		{
			__instance.controlInputs.throttle = 0f;
			__instance.SetGear(false);
			__instance.GearStateChanged(false);
		}

		if (!__instance.IsServer)
		{
			__instance.definition.spawnOffset = __state;
		}
	}
	
	// Currently every turret on bote sends its own manual aim RPC message to server when player is in free aim mode
	// (no target selected), which quickly saturates the RPC rate limit and the turret aiming stops with logs flooded
	// with RPC rate limit exceeded for 'Aircraft.CmdSetTurretVector', dropping call
	// This de-duplicates it where if a turret in a weapon station already sent an RPC this tick, the others don't need
	// to, as on server it iterates through all turrets in a station anyway to follow this aim even if one sends it
	private sealed class AircraftState
	{
		public readonly Dictionary<byte, float> LastSendFixedTime = new();
	}
	
	private static readonly ConditionalWeakTable<Aircraft, AircraftState> States = new();
	
	[HarmonyPatch(nameof(Aircraft.SetTurretVector))]
	[HarmonyPrefix]
	private static bool SetTurretVector_Prefix(Aircraft __instance, byte weaponStationIndex)
	{
		if (__instance.IsServer || !__instance.HasAuthority || !__instance.TryGetShipBridge(out _)) return true;
		var state = States.GetOrCreateValue(__instance);
		var fixedTime = Time.fixedTime;
		if (state.LastSendFixedTime.TryGetValue(weaponStationIndex, out float lastTime) && lastTime == fixedTime)
		{
			return false;
		}
		
		state.LastSendFixedTime[weaponStationIndex] = fixedTime;
		return true;
	}
}