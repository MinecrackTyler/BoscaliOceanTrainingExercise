using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(HUDTurretCrosshair))]
public static class HUDTurretCrosshairPatches
{
	[HarmonyPatch(nameof(HUDTurretCrosshair.Refresh))]
	[HarmonyPrefix]
	static bool Refresh_Prefix(HUDTurretCrosshair __instance, ref Camera mainCamera, out Vector3 crosshairPosition)
	{
		Vector3 direction = __instance.turret.GetDirection();
		bool isOnTarget = __instance.turret.IsOnTarget();
		crosshairPosition = Vector3.one * 10000f;

		if (Vector3.Dot(mainCamera.transform.forward, direction - mainCamera.transform.position) > 0f)
		{
			crosshairPosition = SceneSingleton<CameraStateManager>.i.mainCamera.WorldToScreenPoint(direction);
			crosshairPosition.z = 0f;
			__instance.transform.position = crosshairPosition;
			__instance.crosshair.enabled = true;
            
			float reloadProgress = 0f;
			if (__instance.gun != null)
			{
				reloadProgress = __instance.gun.GetReloadProgress();
				if (reloadProgress > 0f)
				{
					if (!__instance.readinessCircle.enabled)
					{
						__instance.readinessCircle.enabled = true;
						__instance.crosshair.color = Color.red + Color.green * 0.5f;
					}
					__instance.readinessCircle.fillAmount = reloadProgress;
				}
				else if (__instance.readinessCircle.enabled)
				{
					__instance.readinessCircle.enabled = false;
					__instance.crosshair.color = Color.green;
				}
			}
			__instance.circle.enabled = isOnTarget && reloadProgress <= 0f;
		}
		else
		{
			__instance.circle.enabled = false;
			__instance.readinessCircle.enabled = false;
			__instance.crosshair.enabled = false;
		}
		return false;
	}
}
