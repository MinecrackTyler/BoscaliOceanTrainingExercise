using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(TargetCam))]
public static class TargetCamPatches
{
	[HarmonyPrefix]
	[HarmonyPatch(nameof(TargetCam.Update))]
	private static bool Update_Prefix(TargetCam __instance)
	{
		if (__instance.aircraft == null || __instance.aircraft.Player == null || !__instance.aircraft.Player.IsLocalPlayer) return false;

		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(nameof(TargetCam.Initialize))]
	private static bool Initialize_Prefix(TargetCam __instance)
	{
		if (__instance.aircraft.Player == null) return false;
		return true;
	}
}