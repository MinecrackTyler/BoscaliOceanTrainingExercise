using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOComponentWIP;

[HarmonyPatch(typeof(HUDUnitMarker))]
public class RadarHUDUnitMarker : HUDUnitMarker
{
	private Image radarImage;
	
	public RadarHUDUnitMarker(Unit unit, Image image) : base(unit, image)
	{
		if (unit is not Aircraft) return;
		radarImage = Object.Instantiate(CombatHUD.i.unitMarker).GetComponent<Image>();
		radarImage.transform.SetParent(image.transform, false);
		radarImage.transform.localPosition = new Vector3(-1f, -0.5f, 0);
		radarImage.sprite = ModAssets.i.RadarHUDIcon;
	}

	private new void UpdateHidden(bool gearExtended)
	{
		radarImage?.enabled = hidden;
	}

	[HarmonyPatch(nameof(HUDUnitMarker.UpdateHidden))]
	private static void UpdateHidden_Postfix(HUDUnitMarker __instance, bool gearExtended)
	{
		if (__instance is not RadarHUDUnitMarker radarHUDUnitMarker) return;
		radarHUDUnitMarker.UpdateHidden(gearExtended);
	}
}