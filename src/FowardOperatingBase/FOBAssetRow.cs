using NOComponentWIP.ServerConfig;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOComponentWIP;

public class FOBAssetRow : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI nameText;
	[SerializeField] private TextMeshProUGUI costText;
	[SerializeField] private Button selectButton;
	[SerializeField] private Image icon;
	
	private FOBUnit fobUnit;
	public FOBUnit FOBUnit => fobUnit;

	public void Setup(FOBUnit unit, FOBUIController controller)
	{
		this.fobUnit = unit;
		nameText.text = unit.unitName;
		
		costText.alignment = TextAlignmentOptions.MidlineLeft;
		costText.enableWordWrapping = false;
		costText.overflowMode = TextOverflowModes.Overflow;
		
		if (icon != null) icon.sprite = unit.icon;

		selectButton.onClick.RemoveAllListeners();
		selectButton.onClick.AddListener(() => controller.SelectUnit(unit));
	}
	
	public void Refresh(int plannedCount, bool canSelect)
	{
		if (fobUnit == null) return;
		
		selectButton.interactable = canSelect;
		var key = fobUnit.JsonKey;
		
		if (!UnitConfig.UnitAllowed(key))
		{
			costText.text = $"[{fobUnit.pointCost}] DISABLED";
			return;
		}
		
		var text = $"[{fobUnit.pointCost}]";
		
		if (UnitConfig.UnitEconomy())
		{
			var allocationCost = UnitConfig.UnitCost(key);
			var spacing = fobUnit.pointCost < 10
				? 4
				: fobUnit.pointCost < 100
					? 3
					: 2;
			
			text += new string(' ', spacing);
			text += UnitConverter.ValueReading(allocationCost);
		}
		
		if (UnitConfig.UnitLimits())
		{
			var playerMax = UnitConfig.PlayerMax(key);
			var factionMax = UnitConfig.FactionMax(key);
			var playerCount = Mathf.Max(0, UnitConfig.GetCurrentPlayerCount(key)) + plannedCount;
			var factionCount = Mathf.Max(0, UnitConfig.GetCurrentFactionCount(key)) + plannedCount;
			if (playerMax >= 0)
				text += $"  P: {playerCount}/{playerMax}";
			if (factionMax >= 0)
				text += $"  F: {factionCount}/{factionMax}";
		}
		
		costText.text = text;
	}

	public void Disable(bool disabled)
	{
		selectButton.interactable = !disabled;
	}

	private void OnDestroy()
	{
		selectButton?.onClick.RemoveAllListeners();
	}
}