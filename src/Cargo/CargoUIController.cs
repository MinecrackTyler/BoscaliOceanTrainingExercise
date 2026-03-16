using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOComponentWIP;

public class CargoUIController : MonoBehaviour
{
	[Header("Top Bar")]
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private Image pointsFillBar;

    [Header("List Area")]
    [SerializeField] private Transform scrollContent;
    [SerializeField] private GameObject rowPrefab;
    
    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Toggle fobToggle;

    private DeploymentManager _manager;
    
    private Dictionary<int, int> _workingManifest = new Dictionary<int, int>();
    
    private int _currentTotalPoints = 0;

    public void Initialize(DeploymentManager manager)
    {
        _manager = manager;
        
        applyButton.onClick.AddListener(OnApplyClicked);
        closeButton.onClick.AddListener(Close);
        fobToggle.onValueChanged.AddListener(ToggleFOB);
        fobToggle.interactable = manager.FobAvailable;
        
        //foreach (Transform child in scrollContent) Destroy(child.gameObject);
        
        foreach (int unitId in _manager.unitManifest)
        {
            if (_workingManifest.ContainsKey(unitId)) _workingManifest[unitId]++;
            else _workingManifest[unitId] = 1;
        }
        
        for (int i = 0; i < _manager.availableUnits.Count; i++)
        {
            var unit = _manager.availableUnits[i];
            var rowObj = Instantiate(this.rowPrefab, scrollContent);
            
            var rowController = rowObj.GetComponent<UnitRowController>();
            
            _workingManifest.TryGetValue(i, out int currentCount);
            rowController.Setup(i, unit, currentCount, this);
        }

        RefreshTotalPoints();
    }

    private void ToggleFOB(bool toggle)
    {
        RefreshTotalPoints();
        NotifyRowsOfPointChange();
    }

    public void ChangeUnitCount(int unitId, int delta, int unitCost)
    {
        _workingManifest.TryGetValue(unitId, out int current);
        int nextCount = Mathf.Max(0, current + delta);
        
        if (delta > 0 && (_currentTotalPoints + unitCost > _manager.MaxPoints))
        {
            return;
        }

        _workingManifest[unitId] = nextCount;
        RefreshTotalPoints();
        NotifyRowsOfPointChange();
    }

    private void RefreshTotalPoints()
    {
        _currentTotalPoints = fobToggle.isOn ? _manager.FobCost : 0;
        foreach (var entry in _workingManifest)
        {
            var unit = _manager.availableUnits[entry.Key];
            _currentTotalPoints += entry.Value * unit.pointCost;
        }
        
        pointsText.text = $"CAPACITY: {_currentTotalPoints} / {_manager.MaxPoints}";
        pointsFillBar.fillAmount = (float)_currentTotalPoints / _manager.MaxPoints;
        
        pointsFillBar.color = _currentTotalPoints > (_manager.MaxPoints * 0.9f) ? Color.red : Color.green;
    }

    private void NotifyRowsOfPointChange()
    {
        var rows = scrollContent.GetComponentsInChildren<UnitRowController>();
        foreach (var row in rows)
        {
            row.UpdateAbilityToIncrement(_currentTotalPoints, _manager.MaxPoints);
        }
    }

    private void OnApplyClicked()
    {
        List<int> finalManifest = new List<int>();
        foreach (var entry in _workingManifest)
        {
            for (int i = 0; i < entry.Value; i++)
            {
                finalManifest.Add(entry.Key);
            }
        }
        
        LoadoutBridge.SetLoadout(finalManifest, fobToggle.isOn);
        
        Close();
    }
    
    public void Close()
    {
	    LoadoutBridge.LoadoutSet = true;
        Destroy(this.gameObject);
    }
}