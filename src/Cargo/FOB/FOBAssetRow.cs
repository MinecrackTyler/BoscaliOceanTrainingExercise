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
		costText.text = $"[{unit.pointCost}]";
		if (icon != null) icon.sprite = unit.icon;

		selectButton.onClick.AddListener(() => controller.SelectUnit(unit));
	}

	public void Disable(bool disabled)
	{
		selectButton.interactable = !disabled;
	}
}