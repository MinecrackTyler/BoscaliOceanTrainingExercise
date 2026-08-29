using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Hardpoint))]
public class HardpointPatches
{
	[HarmonyPatch(nameof(Hardpoint.SpawnMount))]
	[HarmonyPostfix]
	private static void SpawnMount_Postfix(Aircraft aircraft, WeaponMount weaponMount, GameObject __result)
	{
		if (!weaponMount.turret) return;
		foreach (var turret in __result.GetComponentsInChildren<Turret>().Skip(1))
		{
			turret.AttachToWeaponManager(aircraft);
		}
	}
}