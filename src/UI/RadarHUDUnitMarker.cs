using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOComponentWIP;

[HarmonyPatch(typeof(HUDUnitMarker))]
public class RadarHUDUnitMarker : HUDUnitMarker
{
	private Image radarImage;
	private Radar radar;
	
	public RadarHUDUnitMarker(Unit unit, Image image) : base(unit, image)
	{
		if (unit is not (Aircraft or Missile)) return;
		radarImage = Object.Instantiate(CombatHUD.i.unitMarker).GetComponent<Image>();
		radarImage.transform.SetParent(image.transform, false);
		radarImage.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
		radarImage.sprite = ModAssets.i.RadarHUDIcon;
		radar = CombatHUD.i.aircraft?.radar as Radar;
		radarImage.enabled = false;
	}

	private void Update()
	{
		if (radar == null)
		{
			radarImage?.enabled = false;
			return;
		}
		radarImage?.enabled = !hidden && maximized && radar.detectedTargets.Contains(unit);
		radarImage?.color = image.color;
	}

	[HarmonyPatch(nameof(HUDUnitMarker.UpdatePosition))]
	[HarmonyPostfix]
	private static void UpdatePosition_Postfix(HUDUnitMarker __instance)
	{
		if (__instance is not RadarHUDUnitMarker radarHUDUnitMarker) return;
		radarHUDUnitMarker.Update();
	}
}

[HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.CreateMarker))]
public static class CombatHUD_CreateMarkerPatch
{
	private static bool Prefix(CombatHUD __instance, PersistentID id)
	{
		if (!__instance.aircraft?.definition.IsShipDefinition() ?? true) return true;
		
		
		bool valid = UnitRegistry.TryGetUnit(id, out var unit);
		if (!valid) return true;

		bool flag = unit is Aircraft && (Plugin.Instance?.RadarTargetIndicatorAircraft ?? false);
		flag |= unit is Missile && (Plugin.Instance?.RadarTargetIndicatorMissile ?? false);
		
		if (!flag) return true;
		
		if (!(__instance.aircraft == null) && valid && __instance.aircraft != unit && !unit.disabled && !__instance.markerLookup.ContainsKey(unit) && !(unit is Scenery))
		{
			Image component = Object.Instantiate(__instance.unitMarker, __instance.iconLayer).GetComponent<Image>();
			HUDUnitMarker hUDUnitMarker = new RadarHUDUnitMarker(unit, component);
			__instance.markers.Add(hUDUnitMarker);
			__instance.markerLookup.Add(unit, hUDUnitMarker);
			hUDUnitMarker.AssessThreat(__instance.aircraft);
		}

		return false;
	}
}