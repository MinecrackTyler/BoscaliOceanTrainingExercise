using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public class AIHeloPlayerNavalResupply : AIHeloTransportState
{
	public AIHeloPlayerNavalResupply(Aircraft aircraft, Aircraft targetUnit) : base(aircraft)
	{
		this.targetUnit = targetUnit;
	}

	private Aircraft targetUnit;

	private static void UpdatePlayerLZ(ref TransportDestination destination, Aircraft aircraft, Aircraft targetUnit)
	{
		Vector3 dir = (aircraft.GlobalPosition() - targetUnit.GlobalPosition()).normalized;
		dir.y = 0f;
		GlobalPosition gp = targetUnit.GlobalPosition() + dir * (30f + targetUnit.maxRadius);
		float num = Mathf.Min(FastMath.Distance(aircraft.GlobalPosition(), targetUnit.GlobalPosition()) / Mathf.Max(aircraft.speed, 1f), 30f);
		destination.slope = 0f;
		if (targetUnit.speed * 10f > targetUnit.maxRadius)
		{
			Vector3 velocity = targetUnit.rb.velocity;
			velocity.y = 0f;
			velocity = velocity.normalized * targetUnit.maxRadius + velocity * (20f + num);
			destination.touchdownPoint = targetUnit.GlobalPosition() + velocity;
			GlobalPosition targetPosition = targetUnit.GlobalPosition() + Vector3.Project(aircraft.GlobalPosition() - targetUnit.GlobalPosition(), velocity);
			destination.dropConditionsMet = FastMath.InRange(aircraft.GlobalPosition(), targetPosition, 50f);
		}
		else
		{
			destination.touchdownPoint = gp;
			destination.dropConditionsMet = FastMath.InRange(aircraft.GlobalPosition(), destination.touchdownPoint, 50f);
		}
	}
	
	[HarmonyPatch(typeof(AIHeloTransportState), nameof(AIHeloTransportState.SearchForLandingSpot))]
	[HarmonyPrefix]
	public static bool PlayerResupply_Prefix(AIHeloTransportState __instance)
	{
		if (__instance is not AIHeloPlayerNavalResupply resupply) return true;
		if (resupply.targetUnit == null) return true;

		if (Time.timeSinceLevelLoad - resupply.lastLandingSpotCheck < 3f) return false;
		Debug.Log("Resupply Patch:");
		foreach (WeaponStation ws in resupply.aircraft.weaponStations)
		{
			if (ws.WeaponInfo.cargo)
			{
				resupply.aircraft.weaponManager.currentWeaponStation = ws;
				break;
			}
		}

		if (!resupply.aircraft.weaponManager.currentWeaponStation.WeaponInfo.rearmShip) return true; //before time set to allow base ai to handle if this is not a ship rearm somehow
		Debug.Log("Resupply Patch Complete!");
		resupply.lastLandingSpotCheck = Time.timeSinceLevelLoad;
		resupply.pilot.flightInfo.EnemyContact = true;

		resupply.transportDestination.validMission = true;
		UpdatePlayerLZ(ref resupply.transportDestination, resupply.aircraft, resupply.targetUnit);
		resupply.transportMode = TransportMode.NavalSupply;
		resupply.stateDisplayName = "Delivering Naval Supplies";
		return false;
	}
}