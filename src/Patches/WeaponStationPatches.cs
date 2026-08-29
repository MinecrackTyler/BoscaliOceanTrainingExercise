using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(WeaponStation))]
public static class WeaponStationPatches
{
	[HarmonyPatch(nameof(WeaponStation.LaunchMount))]
	[HarmonyPostfix]
	static void LaunchMount_Postfix(WeaponStation __instance, ref int ___weaponIndex)
	{
		if (__instance.Weapons.Count == 0) return;
        
		if (___weaponIndex >= __instance.Weapons.Count)
		{
			var lastWeapon = __instance.Weapons[__instance.Weapons.Count - 1];
			if (lastWeapon is NetworkMissileLauncher)
			{
				___weaponIndex = 0;
			}
			else return;
		}

		int startIndex = ___weaponIndex;
		int checkedCount = 0;
		int totalWeapons = __instance.Weapons.Count;

		while (IsWeaponEmpty(__instance.Weapons[___weaponIndex]) && checkedCount < totalWeapons)
		{
			___weaponIndex = (___weaponIndex + 1) % totalWeapons;
			checkedCount++;
		}
	}

	[HarmonyPatch(nameof(WeaponStation.UpdateLastFired))]
	[HarmonyPostfix]
	private static void UpdateLastFired_Postfix(WeaponStation __instance, int roundsFired)
	{
		if (__instance.Weapons[0] is NetworkMissileLauncher)
		{
			__instance.Ammo += roundsFired;
		}
	}

	private static bool IsWeaponEmpty(object weapon)
	{
		if (weapon is NetworkMissileLauncher nml)
		{
			return nml.GetAmmoTotal() <= 0 || nml.GetAmmoLoaded() <= 0 || nml.Reloading;
		}
		return false;
	}
}