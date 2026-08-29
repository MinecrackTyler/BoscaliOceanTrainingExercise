using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Aircraft))]
public class AircraftPatches
{
	[HarmonyPatch(nameof(Aircraft.SetSimplePhysics))]
	[HarmonyPrefix]
	static bool SetSimplePhysics_Prefix(Aircraft __instance)
	{
		if (!__instance.TryGetShipBridge(out var bridge)) return true;
		
		ColorLog<Unit>.Info("Setting " + __instance.unitName + " physics to Simplified");
		foreach (UnitPart item in __instance.partLookup)
		{
			(item as AeroPart)?.MergeWithParent();
		}
		__instance.rb.mass = __instance.definition.mass;
		__instance.rb.ResetCenterOfMass();
		__instance.rb.ResetInertiaTensor();
		__instance.simplePhysics = true;
		
		bridge.SetSimplePhysics();
		return false;
	}
	
	[HarmonyPatch(nameof(Aircraft.SetComplexPhysics))]
	[HarmonyPrefix]
	static bool SetComplexPhysics_Prefix(Aircraft __instance)
	{
		if (!__instance.TryGetShipBridge(out var bridge)) return true;
		
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

	/*[HarmonyPatch(nameof(Aircraft.CanRearm))]
	[HarmonyPrefix]
	static bool CanRearm_Prefix(Aircraft __instance, bool aircraftRearm, bool vehicleRearm, bool shipRearm, ref bool __result)
	{
		if (!__instance.GetComponent<ShipPartBridge>()) return true;

		__result = true;
		if (!shipRearm) __result = false;
		
		return false;
	}*/

	/*[HarmonyPatch(nameof(Aircraft.Rearm))]
	[HarmonyPrefix]
	static bool Rearm_Prefix(Aircraft __instance, RearmEventArgs args)
	{
		if (!__instance.GetComponent<ShipPartBridge>()) return true;
		var ac = __instance;

		if (!(ac.Player == null))
		{
			float num = ac.sortieScore * MissionManager.CurrentMission.missionSettings.successfulSortieBonus;
			if (num > 0f && ac.Player != null)
			{
				ac.SuccessfulSortie(num);
			}
			ac.NetworkHQ.AddScore(num);
			ac.RpcRearm(args);
		}
		
		return false;
	}*/

	[HarmonyPatch(nameof(Aircraft.ReturnToInventory))]
	[HarmonyPrefix]
	static void ReturnToInventory_Prefix(Aircraft __instance, ref bool __state)
	{
		__state = false;
		if (!__instance.IsServer) return;
		var aircraft = __instance;
		if (aircraft.speed < 2f && aircraft.NetworkHQ != null && aircraft.NetworkHQ.AnyNearAirbase(aircraft.transform.position, out var airbase) && aircraft.transform.position.y > Datum.LocalSeaY)
		{
			var attachedUnit = airbase.attachedUnit;
			if (attachedUnit == null) return;
			if (aircraft.Player != null) return;
			var deployManager = attachedUnit.GetComponent<DeploymentManager>();
			if (deployManager == null) return;

			if (ModAssets.i.AllDeployableUnits.TryGetValue(aircraft.definition.jsonKey, out var unit))
			{
				if (deployManager.availableUnits.Contains(unit))
				{
					deployManager.AddUnit(unit);
					__state = true;
				}
			}
		}
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
		if (!__instance.definition.IsShipDefinition()) return;
		var ship = __instance;
		var ab = ship.GetComponent<Airbase>();
		
		if ((ship.speed < 10f && ship.NetworkHQ.AnyNearAirbaseInRange(ship.transform.position, out var airbase, 2000f, ab)) && ship.NetworkHQ != null && !(ship.NetworkHQ.AnyNearAirbase(ship.transform.position, out var _) && ship.speed < 2f))
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

		if (!__instance.IsServer)
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
}