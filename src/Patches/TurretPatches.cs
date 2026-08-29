using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Turret))]
public static class TurretPatches
{
	[HarmonyPatch(nameof(Turret.AimTurret), typeof(Vector3))]
	[HarmonyPostfix]
	private static void AimTurret_PostfixVector3(Turret __instance)
	{
		if (__instance.aimSafetyWeapon is not Gun) return;
		if (!ModAssets.i.ShipDefinitions.Contains(__instance.attachedUnit?.definition)) return;
        
		if (Physics.SphereCast(__instance.aimSafetyWeapon.transform.position + __instance.aimSafetyWeapon.transform.forward * 2f, 0.2f, __instance.aimSafetyWeapon.transform.forward, out _, __instance.attachedUnit?.maxRadius * 2f ?? 200f, -8193))
		{
			__instance.aimSafetyWeapon.Safety = true;
		}
	}
    
	[HarmonyPatch(nameof(Turret.AimTurret), typeof(WeaponStation))]
	[HarmonyPostfix]
	private static void AimTurret_PostfixWeaponStation(Turret __instance)
	{
		if (__instance.aimSafetyWeapon is not Gun gun) return;
		if (!ModAssets.i.ShipDefinitions.Contains(__instance.attachedUnit?.definition)) return;
        
		var targetDist = __instance.targetRange - (__instance.target.maxRadius + 50f);
		if (Physics.SphereCast(gun.transform.position + gun.transform.forward * 2f, 0.2f, gun.transform.forward, out var hit, __instance.attachedUnit?.maxRadius * 2f ?? 200f, -8193) || (hit.distance < targetDist && hit.distance > 1f))
		{
			__instance.aimSafetyWeapon.Safety = true;
		}
	}

	[HarmonyPatch(nameof(Turret.AttachToWeaponManager))]
	[HarmonyPostfix]
	private static void AttachToWeaponManager_Postfix(Turret __instance, Aircraft aircraft)
	{
		if (__instance.targetAcquisitionMode == Turret.TargetAcquisitionMode.parentUnitTargetDetector && __instance.attachedUnit?.radar != null)
		{
			__instance.RegisterTargetDetector(__instance.attachedUnit.radar);
		}
	}
}