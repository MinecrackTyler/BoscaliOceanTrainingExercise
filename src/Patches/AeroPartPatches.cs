using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(AeroPart))]
public class AeroPartPatches
{
	/*[HarmonyPatch(nameof(AeroPart.ApplyJobFields))]
	[HarmonyPrefix]
	static bool ApplyJobFields_Prefix(AeroPart __instance)
	{
		if (!ModAssets.i.ShipDefinitions.Contains(__instance.parentUnit.definition as AircraftDefinition)) return false;
		if (!__instance.JobFields.IsCreated) return false;
		ref var reference = ref __instance.JobFields.Ref();

		if (reference.splashed)
		{
			Vector3 pos = __instance.xform.position;
			if (Physics.Linecast(pos + Vector3.up * 100f, pos - Vector3.up * 10f, out var hit, PhysicsLayers.StaticsMask))
			{
				bool isWater = hit.collider.sharedMaterial == GameAssets.i.WaterMaterial;
				if (!isWater) pos.y = Datum.LocalSeaY;
			}
		}

		if (reference.angularDragChanged) __instance.rb.angularDrag = reference.angularDrag;

		switch (reference.hasForce)
		{
			case JobForceType.Force:
				__instance.rb.AddForce(reference.force);
				break;
			case JobForceType.ForceAndTorque:
				__instance.rb.AddForce(reference.force);
				__instance.rb.AddTorque(reference.torque);
				break;
		}
		return false;
	}*/
}