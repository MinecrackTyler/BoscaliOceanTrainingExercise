using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using NOComponentWIP.Patches;
using NOComponentWIP.ServerConfig;
using NOComponentWIP.Systems;
using NuclearOption.UIStyleSystem;
using TMPro;

namespace NOComponentWIP;

public class ShipHUD : HUDApp
{
    [Header("UI References")]
    [SerializeField] private RectTransform contentParent;
    [SerializeField] private TextMeshProUGUI itemTemplate; 
    [SerializeField] private float itemHeight = 31f;
    [SerializeField] private TextMeshProUGUI fobStatus;
    [SerializeField] private GameObject deploymentHud;
    [SerializeField] private TextMeshProUGUI resupplyText;
    [SerializeField] private TextMeshProUGUI disembarkText;
    [SerializeField] private TextMeshProUGUI playerLimitText;
    [SerializeField] private TextMeshProUGUI factionLimitText;
    
    private Aircraft aircraft;
    private DeploymentManager manager;
    private ShipPartBridge bridge;
    private FOBManager fobManager;
    private ResupplyController resupplyController;
    private List<TextMeshProUGUI> pool = new List<TextMeshProUGUI>();
    private float lastDisembarkRefresh;

    private Color alertColor = Color.red;
    private Color selectedColor = Color.green;
    private Color idleColor = Color.cyan;

    public override void Initialize(Aircraft aircraft)
    {
        this.aircraft = aircraft;
        if (!aircraft.TryGetShipBridge(out bridge)) return;
        
        manager = bridge.deploymentManager;
        fobManager = bridge.fobManager;
        resupplyController = bridge.resupplyController;

        itemTemplate.gameObject.SetActive(false);
        
        lastDisembarkRefresh = 0f;
        
        ThemeManager.ThemeGroupChanged += ShipHUD_OnThemeGroupChanged;
        ShipHUD_OnThemeGroupChanged();
    }

    private void ShipHUD_OnThemeGroupChanged()
    {
        alertColor = ThemeManager.Active.ColorTheme.Alert;
        selectedColor = ThemeManager.Active.ColorTheme.AllClear;
        idleColor = ThemeManager.Active.ColorTheme.Warning;
    }

    private void OnDestroy()
    {
        ThemeManager.ThemeGroupChanged -= ShipHUD_OnThemeGroupChanged;
    }

    public override void Refresh()
    {
        DisembarkCheck();
        
        bool hasFobSystem = fobManager != null;
        if (fobStatus.gameObject.activeSelf != hasFobSystem)
            fobStatus.gameObject.SetActive(hasFobSystem);

        if (hasFobSystem)
        {
            if (fobManager.hasFob)
            {
                fobStatus.text = "FOB: READY";
                fobStatus.color = selectedColor;
            }
            else
            {
                fobStatus.text = "FOB: EMPTY";
                fobStatus.color = alertColor;
            }
        }
        
        bool hasResupply = resupplyController != null;
        if (resupplyText.gameObject.activeSelf != hasResupply)
            resupplyText.gameObject.SetActive(hasResupply);

        if (hasResupply)
        {
            if (resupplyController.ResupplyDistance > 0f)
            {
                resupplyText.text = $"RESUPPLY: INBOUND - {UnitConverter.DistanceReading(resupplyController.ResupplyDistance)}";
                resupplyText.color = selectedColor;
            } else if (resupplyController.ResupplyCalled)
            {
                resupplyText.text = $"RESUPPLY: CALLED";
                resupplyText.color = idleColor;
            }
            else
            {
                resupplyText.text = $"RESUPPLY: READY";
                resupplyText.color = selectedColor;
            }
        }
        
        bool hasManager = manager != null;
        if (deploymentHud.activeSelf != hasManager)
            deploymentHud.SetActive(hasManager);
        
        if (!hasManager) 
        {
            UpdatePool(0);
            return;
        }
        
        if (manager.IsEmpty())
        {
            UpdatePool(1); 
            pool[0].text = "EMPTY";
            pool[0].color = alertColor;
        
            contentParent.anchoredPosition = Vector2.zero;
            return;
        }
        
        
        int unitCount = manager.UnitManifest.Count;
        UpdatePool(unitCount);

        int index = 0;
        int visualSelectedIndex = 0;
        DeployableUnit selectedUnit = manager.GetSelectedUnit();

        foreach (var entry in manager.UnitManifest)
        {
            if (index >= pool.Count) break;

            DeployableUnit unit = entry.Key;
            int count = entry.Value;

            pool[index].text = $"{unit.unitName} x{count}";

            if (unit == selectedUnit)
            {
                pool[index].color = selectedColor;
                visualSelectedIndex = index;
            }
            else
            {
                pool[index].color = new Color(1f, 1f, 1f, 0.4f);
            }

            index++;
        }

        if (contentParent != null)
        {
            float totalHeight = unitCount * itemHeight;
            float itemLocalY = (totalHeight / 2f) - (visualSelectedIndex * itemHeight) - (itemHeight / 2f);
            float targetY = -itemLocalY;

            Vector2 anchoredPos = contentParent.anchoredPosition;
            anchoredPos.y = Mathf.Lerp(anchoredPos.y, targetY, Time.deltaTime * 10f);
            contentParent.anchoredPosition = anchoredPos;
        }
        
        if (UnitConfig.UnitLimits() && selectedUnit != null)
        {
            var key = selectedUnit.JsonKey;
            var fCount = UnitConfig.GetCurrentFactionCount(key);
            var fMax = UnitConfig.FactionMax(key);
            
            var pCount = UnitConfig.GetCurrentPlayerCount(key);
            var pMax = UnitConfig.PlayerMax(key);

            if (pMax != -1)
            {
                if (pCount != -1)
                {
                    playerLimitText.text = $"PLYR: {pCount}/{pMax}";
                    playerLimitText.color = (pCount >= pMax) ? alertColor : selectedColor;
                }
                else
                {
                    playerLimitText.text = $"PLYR: ERR";
                    playerLimitText.color = alertColor;
                }
            }
            else
            {
                playerLimitText.text = $"PLYR: N/A";
                playerLimitText.color = selectedColor;
            }
            
            if (fMax != -1)
            {
                if (fCount != -1)
                {
                    factionLimitText.text = $"FACT: {fCount}/{fMax}";
                    factionLimitText.color = (fCount >= fMax) ? alertColor : selectedColor;
                }
                else
                {
                    factionLimitText.text = $"FACT: ERR";
                    factionLimitText.color = alertColor;
                }
            }
            else
            {
                factionLimitText.text = $"FACT: N/A";
                factionLimitText.color = selectedColor;
            }
        }
        else
        {
            playerLimitText.text = $"PLYR: N/A";
            playerLimitText.color = selectedColor;
            factionLimitText.text = $"FACT: N/A";
            factionLimitText.color = selectedColor;
        }
    }

    private void DisembarkCheck()
    {
        if (Time.timeSinceLevelLoad > lastDisembarkRefresh + 1f)
        {
            lastDisembarkRefresh = Time.timeSinceLevelLoad;
            var ab = aircraft.GetComponent<Airbase>();
            bool range = aircraft.NetworkHQ.AnyNearAirbaseInRange(aircraft.transform.position, out _, 2000f, ab);
            bool speed = aircraft.speed < 10f;
            
            if (range && speed)
            {
                disembarkText.text = "DISEMBARK: SAFE";
                disembarkText.color = selectedColor;
            } else if (range)
            {
                disembarkText.text = "DISEMBARK: SPEED";
                disembarkText.color = idleColor;
            }
            else
            {
                disembarkText.text = "DISEMBARK: RANGE";
                disembarkText.color = alertColor;
            }
        }
    }

    private void UpdatePool(int requiredCount)
    {
        while (pool.Count < requiredCount)
        {
            TextMeshProUGUI newItem = Instantiate(itemTemplate, contentParent);
            pool.Add(newItem);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null)
            {
                pool[i].gameObject.SetActive(i < requiredCount);
            }
        }
    }
}