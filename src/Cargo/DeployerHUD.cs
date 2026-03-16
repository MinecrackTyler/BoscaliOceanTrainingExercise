using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace NOComponentWIP;

public class DeployerHUD : HUDApp
{
    [Header("UI References")]
    [SerializeField] private RectTransform contentParent;
    [SerializeField] private Text itemTemplate; 
    [SerializeField] private float itemHeight = 31f;
    [SerializeField] private Text fobStatus;
    
    private Aircraft aircraft;
    private DeploymentManager manager;
    private List<Text> pool = new List<Text>();

    public override void Initialize(Aircraft aircraft)
    {
        this.aircraft = aircraft;
        manager = aircraft.GetComponent<DeploymentManager>();
        
        if (manager == null) 
        {
            gameObject.SetActive(false);
            return;
        }

        itemTemplate.gameObject.SetActive(false);
    }

    public override void Refresh()
    {
        if (manager == null) return;
        
        if (manager.HasFOB)
        {
            fobStatus.text = "FOB: READY";
            fobStatus.color = manager.FobSelected ? Color.green : Color.cyan;
        }
        else
        {
            fobStatus.text = "FOB: EMPTY";
            fobStatus.color = Color.red;
        }
        
        if (manager.IsEmpty())
        {
            UpdatePool(1); 
            pool[0].text = "EMPTY";
            pool[0].color = !manager.FobSelected ? Color.red : new Color(1, 1, 1, 0.4f);
            
            contentParent.anchoredPosition = Vector2.zero;
            return;
        }
        
        var uniqueTypes = GetUniqueManifestTypes();
        UpdatePool(uniqueTypes.Count);

        int visualSelectedIndex = 0;
        int currentSelectedID = manager.unitManifest[manager.SelectedIndex];

        for (int i = 0; i < uniqueTypes.Count; i++)
        {
            int typeID = uniqueTypes[i];
            int count = manager.unitManifest.Count(id => id == typeID);
        
            pool[i].text = $"{manager.availableUnits[typeID].unitName} x{count}";
        
            if (typeID == currentSelectedID && !manager.FobSelected)
            {
                pool[i].color = Color.green;
                visualSelectedIndex = i;
            }
            else
            {
                pool[i].color = new Color(1, 1, 1, 0.4f);
            }
        }
        
        float totalHeight = uniqueTypes.Count * itemHeight;
        
        float itemLocalY = (totalHeight / 2f) - (visualSelectedIndex * itemHeight) - (itemHeight / 2f);
        
        float targetY = -itemLocalY;

        Vector2 anchoredPos = contentParent.anchoredPosition;
        anchoredPos.y = Mathf.Lerp(anchoredPos.y, targetY, Time.deltaTime * 10f);
        contentParent.anchoredPosition = anchoredPos;
    }

    private List<int> GetUniqueManifestTypes()
    {
        List<int> unique = new List<int>();
        foreach (int id in manager.unitManifest)
        {
            if (!unique.Contains(id)) unique.Add(id);
        }
        return unique;
    }

    private void UpdatePool(int requiredCount)
    {
        while (pool.Count < requiredCount)
        {
            Text newItem = Instantiate(itemTemplate, contentParent);
            newItem.gameObject.SetActive(true);
            pool.Add(newItem);
        }
        while (pool.Count > requiredCount)
        {
            Destroy(pool[pool.Count - 1].gameObject);
            pool.RemoveAt(pool.Count - 1);
        }
    }
}