using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Aircraft))]
public class AircraftPatches
{
	[HarmonyPatch("SetSimplePhysics")]
	[HarmonyPrefix]
	static bool SetSimplePhysics(Aircraft __instance)
	{
		var bridge = __instance.GetComponent<ShipPartBridge>();
		if (bridge == null) return true;
		bridge.SetSimplePhysics();
		
		ColorLog<Unit>.Info("Setting " + __instance.unitName + " physics to Simplified");
		foreach (UnitPart item in __instance.partLookup)
		{
			(item as AeroPart)?.MergeWithParent();
		}
		__instance.rb.mass = __instance.definition.mass;
		__instance.rb.ResetCenterOfMass();
		__instance.rb.ResetInertiaTensor();
		__instance.simplePhysics = true;
		return false;
	}
	
	[HarmonyPatch("SetComplexPhysics")]
	[HarmonyPrefix]
	static bool SetComplexPhysics(Aircraft __instance)
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
	static bool CanRearm(Aircraft __instance, bool aircraftRearm, bool vehicleRearm, bool shipRearm, ref bool __result)
	{
		if (!__instance.GetComponent<ShipPartBridge>()) return true;

		__result = true;
		if (!shipRearm) __result = false;
		
		return false;
	}

	[HarmonyPatch(nameof(Aircraft.Rearm))]
	[HarmonyPrefix]
	static bool Rearm(Aircraft __instance, RearmEventArgs args)
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
	}

	[HarmonyPatch(nameof(Aircraft.ReturnToInventory))]
	[HarmonyPrefix]
	static void ReturnToInventory(Aircraft __instance, ref bool __state)
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
	static void ReturnToInventory_Postfix(Aircraft __instance, ref bool __state)
	{
		if (!__state) return;
		__instance.NetworkHQ.AddSupplyUnit(__instance.definition, -1);
	}

	[HarmonyPatch(nameof(Aircraft.FixedUpdate))]
	[HarmonyPostfix]
	static void FixedUpdate(Aircraft __instance)
	{
		var ac = __instance;
		if (ac.GetComponent<ShipPartBridge>() == null) return; 
		if (ac.hit.collider != null && ac.hit.collider.attachedRigidbody != null)
		{
			var velocity = ac.cockpit.rb.velocity;
			ac.speed = velocity.magnitude;
		}
	}
}