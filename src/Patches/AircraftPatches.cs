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
		var bridge = __instance.GetComponent<ShipPartBridge>();
		if (bridge == null) return true;
		
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
		var bridge = __instance.GetComponent<ShipPartBridge>();
		if (bridge == null) return true;
		
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

	[HarmonyPatch(nameof(Aircraft.CanRearm))]
	[HarmonyPrefix]
	static bool CanRearm_Prefix(Aircraft __instance, bool aircraftRearm, bool vehicleRearm, bool shipRearm, ref bool __result)
	{
		if (!__instance.GetComponent<ShipPartBridge>()) return true;

		__result = true;
		if (!shipRearm) __result = false;
		
		return false;
	}

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
			var unitIndex = deployManager.ContainsUnit(aircraft.definition);
			if (unitIndex == -1) return;
			deployManager.CmdSetManifest(deployManager.UnitManifest.ToArray().AddItem(unitIndex).ToArray(), deployManager.HasFOB);
			__state = true;
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
		if (ac.GetComponent<ShipPartBridge>() == null) return; 
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
		if (!ModAssets.i.shipDefinitions.Contains(__instance.definition)) return;
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
		if (!ModAssets.i.shipDefinitions.Contains(__instance.definition)) return true;

		if (Physics.Linecast(__instance.transform.position, __instance.transform.position - Vector3.up * 10000f, out __instance.hit, 2112))
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
		if (!ModAssets.i.shipDefinitions.Contains(__instance.definition)) return;

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
		// Close an existing CargoUI instance when local player spawns in to prevent it from staying open
		if (__instance.Player.IsLocalPlayer)
			UnityEngine.Object.FindObjectOfType<CargoUIController>()?.Close();
	}
	

	[HarmonyPatch(nameof(Aircraft.OnStartClient))]
	[HarmonyPostfix]
	private static void OnStartClient_Postfix(Aircraft __instance, ref Vector3 __state)
	{
		if (!ModAssets.i.shipDefinitions.Contains(__instance.definition)) return;

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
	
	[HarmonyPatch(nameof(Aircraft.LocalSimFixedUpdate))]
    [HarmonyPrefix]
    public static void LocalSimFixedUpdate_Prefix(Aircraft __instance)
    {
        try
        {
            if (__instance == null)
            {
                Debug.LogError("[AircraftDebug] __instance (Aircraft) is null!");
                return;
            }


            try
            {
                var rb = __instance.CockpitRB();
                if (rb == null)
                {
                    Debug.LogError("[AircraftDebug] CockpitRB() returned null.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AircraftDebug] Exception thrown while calling CockpitRB(): {ex.Message}");
            }
            
            if (__instance.partChecker == null)
            {
                Debug.LogError("[AircraftDebug] field 'partChecker' is null.");
            }
			
            if (__instance.transform == null)
            {
                Debug.LogError("[AircraftDebug] 'base.transform' is null.");
            }
            
            if (NetworkSceneSingleton<LevelInfo>.i == null)
            {
                Debug.LogError("[AircraftDebug] 'NetworkSceneSingleton<LevelInfo>.i' instance is null.");
            }
            if (__instance.controlSurfaces == null)
            {
                Debug.LogError("[AircraftDebug] 'controlSurfaces' list/array is null.");
            }
            else
            {
                int index = 0;
                foreach (var surface in __instance.controlSurfaces)
                {
                    if (surface == null)
                    {
                        Debug.LogError($"[AircraftDebug] 'controlSurfaces' contains a null element at index {index}.");
                    }
                    index++;
                }
            }
            
            if (SceneSingleton<CombatHUD>.i != null)
            {
                try
                {
                    var hudAircraft = SceneSingleton<CombatHUD>.i.aircraft;
                    if (hudAircraft == __instance && __instance.countermeasureManager == null)
                    {
                        Debug.LogError("[AircraftDebug] HUD matches this aircraft, but 'countermeasureManager' is null.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AircraftDebug] Exception accessing CombatHUD data: {ex.Message}");
                }
            }
        }
        catch (Exception criticalEx)
        {
            Debug.LogError($"[AircraftDebug] Critical failure inside prefix patch logic: {criticalEx}");
        }
    }
}