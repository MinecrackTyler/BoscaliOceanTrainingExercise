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
}