using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOComponentWIP;

public class UnitRowController : MonoBehaviour
{
	[Header("Display")]
	[SerializeField] private TextMeshProUGUI unitNameText;
	[SerializeField] private TextMeshProUGUI unitCostText;
	[SerializeField] private TextMeshProUGUI countText;
	[SerializeField] private Image unitIcon;

	[Header("Controls")]
	[SerializeField] private Button plusButton;
	[SerializeField] private Button minusButton;

	private int _unitId;
	private int _currentCount;
	private int _unitCost;
	private CargoUIController _mainController;

	public void Setup(int id, DeployableUnit unit, int initialCount, CargoUIController main)
	{
		_unitId = id;
		_mainController = main;
		_currentCount = initialCount;
		_unitCost = unit.pointCost;
        
		unitNameText.text = unit.unitName;
		unitCostText.text = $"[{_unitCost}]";
		if (unitIcon != null) unitIcon.sprite = unit.icon;

		UpdateLocalDisplay();
        
		plusButton.onClick.AddListener(() => OnButtonClick(1));
		minusButton.onClick.AddListener(() => OnButtonClick(-1));
	}

	private void OnButtonClick(int delta)
	{
		_mainController.ChangeUnitCount(_unitId, delta, _unitCost);
        
		_currentCount = Mathf.Max(0, _currentCount + delta); 
        
		UpdateLocalDisplay();
	}

	public void UpdateLocalDisplay()
	{
		countText.text = _currentCount.ToString("D2"); 
        
		minusButton.interactable = _currentCount > 0;
	}
	
	public void UpdateAbilityToIncrement(int totalPoints, int maxPoints)
	{
		bool canAfford = (totalPoints + _unitCost) <= maxPoints;
		plusButton.interactable = canAfford;
        
		plusButton.GetComponentInChildren<TextMeshProUGUI>().color = canAfford ? Color.white : new Color(1,1,1,0.2f);
	}
}