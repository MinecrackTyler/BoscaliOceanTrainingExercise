using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(RearmMissionController))]
public static class RearmMissionControllerPatches
{
	[HarmonyPatch(nameof(RearmMissionController.TryGetRearmer))]
	[HarmonyPrefix]
	public static bool TryGetRearmer_Prefix(RearmMissionController __instance, Unit requestingUnit, out Rearmer bestRearmer, ref bool __result)
	{
		bestRearmer = null;
		if (requestingUnit.radarAlt > 1f || (requestingUnit.speed > 1f && !(requestingUnit is Ship || ModAssets.i.ShipDefinitions.Contains(requestingUnit.definition as AircraftDefinition))))
		{
			__result = false;
			return false;
		}
		GlobalPosition a = requestingUnit.GlobalPosition();
		RearmMissionController.availableRearmersCache.Clear();
		foreach (Rearmer rearmer in __instance.Rearmers)
		{
			if (rearmer.Unit != requestingUnit && rearmer.Capacity > 0f && FastMath.InRange(a, rearmer.GetPosition(), rearmer.Range))
			{
				RearmMissionController.availableRearmersCache.Add(rearmer);
			}
		}
		if (RearmMissionController.availableRearmersCache.Count == 0)
		{
			__result = false;
			return false;
		}
		RearmMissionController.availableRearmersCache.Sort((Rearmer rearmer, Rearmer b) => b.Capacity.CompareTo(rearmer.Capacity));
		bestRearmer = RearmMissionController.availableRearmersCache[0];
		__result = true;
		return false;
	}
}