using HarmonyLib;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(RadialMenuAction))]
public static class RadialMenuActionPatches
{
	[HarmonyPatch(nameof(RadialMenuAction.TriggerAction))]
	[HarmonyPostfix]
	private static void TriggerAction_Postfix(RadialMenuAction __instance, Aircraft aircraft)
	{
		if (!ModAssets.i.ShipDefinitions.Contains(aircraft.definition)) return;
		switch (__instance.actionType)
		{
			case RadialMenuAction.ActionType.Gear:
				if (aircraft.gearState == LandingGear.GearState.LockedExtended)
				{
					aircraft.SetGear(deployed: false);
				}
				if (aircraft.gearState == LandingGear.GearState.LockedRetracted)
				{
					aircraft.SetGear(deployed: true);
				}
				break;
			case RadialMenuAction.ActionType.Eject:
				break;
			case RadialMenuAction.ActionType.Radar:
				break;
			case RadialMenuAction.ActionType.NavLights:
				break;
			case RadialMenuAction.ActionType.FlightAssist:
				break;
			case RadialMenuAction.ActionType.AutoHover:
				break;
			case RadialMenuAction.ActionType.Engine:
				break;
			case RadialMenuAction.ActionType.Nightvis:
				break;
			case RadialMenuAction.ActionType.TurretAuto:
				break;
			case RadialMenuAction.ActionType.SelectWeapon:
				break;
			case RadialMenuAction.ActionType.LinkGuns:
				break;
			default:
				break;
		}
	}
}