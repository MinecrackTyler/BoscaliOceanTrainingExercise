using System.Collections;
using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace NOComponentWIP;

public class RailHangarController : NetworkBehaviour
{
    public Aircraft aircraft;
    private GameObject boosterPrefab;
    private Transform railAttachPoint;
    
    private LaunchRail launchRail;
    private List<RailBooster> railBoosters = new List<RailBooster>();
    private bool isLaunched;

    private void Awake()
    {
        aircraft = GetComponentInParent<Aircraft>();
        if (aircraft == null) Destroy(this);
        if (GameManager.gameState == GameState.Editor) return;

        boosterPrefab = ModAssets.i.aircraftEntries[ModAssets.i.aircraftDefs.IndexOf(aircraft.definition)].boosterPrefab;
        railAttachPoint = aircraft.transform.Find("RailAttachPoint");
        aircraft.onInitialize += OnAircraftInitialize;
        
    }

    private void OnAircraftInitialize()
    {
        if (aircraft.NetworkspawningHangar is not RailHangar hangar || railAttachPoint == null)
        {
            aircraft.onInitialize -= OnAircraftInitialize;
            this.enabled = false;
            //Destroy(this);
            return;
        }

        launchRail = hangar.LaunchRail;
        if (aircraft.LocalSim)
        {
            Attach();
        }
        
        SpawnBoosters();
    }

    private void SpawnBoosters()
    {
        if (boosterPrefab == null) return;
        var boosterObj = Instantiate(boosterPrefab, aircraft.transform.position, aircraft.transform.rotation);
        RailBooster boosterLogic = boosterObj.GetComponent<RailBooster>();
        
        // This handles parenting and passing the aircraft reference
        boosterLogic.Initialize(aircraft);
        railBoosters.Add(boosterLogic);

    }
    

    private void Attach()
    {
        if (launchRail == null)
        {
            Debug.LogError("[RailHangarController] No LaunchRail found!");
            return;
        }
        
        launchRail.AttachAircraft(aircraft, railAttachPoint);
    }

    private void Update()
    {
        if (!aircraft.LocalSim) return;
        if (launchRail == null || !launchRail.IsReady) return;

        if (!isLaunched)
        {
            if (aircraft.GetInputs().throttle >= 0.95f && GetRPMRatio() > 0.5f)
            {
                Launch();
            }
        }
    }

    private void Launch()
    {
        isLaunched = true;
        
        launchRail.Launch();
        
        foreach (var booster in railBoosters)
        {
            booster.Ignite();
        }

        Debug.Log($"{aircraft.name} initiated rail launch.");
    }

    private float GetRPMRatio()
    {
        if (aircraft.engines == null || aircraft.engines.Count == 0) return 0f;

        float total = 0f;
        foreach (var engine in aircraft.engines)
            total += engine.GetRPMRatio();

        return total / aircraft.engines.Count;
    }
}