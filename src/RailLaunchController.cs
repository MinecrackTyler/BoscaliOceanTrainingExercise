using System.Collections;
using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace BPCustomComponents;

public class RailLaunchController : NetworkBehaviour
{
    [SerializeField] private Aircraft aircraft;
    [SerializeField] private GameObject railPrefab;
    [SerializeField] private GameObject boosterPrefab;
    [SerializeField] private Transform railAttachPoint;
    [SerializeField] private Transform[] boosterAttachPoints;

    [SerializeField] private float launchRPMThreshold;
    
    [SyncVar] private GameObject railInstance;
    private LaunchRail launchRail;
    private List<RailBooster> railBoosters = new List<RailBooster>();
    private bool isLaunched;

    private void Awake()
    {
        if (aircraft == null) Destroy(this);
        if (GameManager.gameState == GameState.Editor) return;

        aircraft.onInitialize += OnAircraftInitialize;
    }

    private void OnDestroy()
    {
        if (railInstance != null && aircraft != null && aircraft.IsServer)
            NetworkManagerNuclearOption.i.ServerObjectManager.Destroy(railInstance);
    }

    private void OnAircraftInitialize()
    {
        if (aircraft.IsServer)
        {
            SpawnRail();
        }

        if (aircraft.LocalSim)
        {
            StartCoroutine(WaitAndAttach());
        }
        
        SpawnBoosters();
    }

    private void SpawnBoosters()
    {
        foreach (var point in boosterAttachPoints)
        {
            var boosterObj = Instantiate(boosterPrefab, point.position, point.rotation);
            RailBooster boosterLogic = boosterObj.GetComponent<RailBooster>();
            
            // This handles parenting and passing the aircraft reference
            boosterLogic.Initialize(aircraft);
            railBoosters.Add(boosterLogic);
        }
    }

    private void SpawnRail()
    {
        railInstance = NetworkManagerNuclearOption.i.ServerObjectManager
            .SpawnInstantiate(railPrefab, railPrefab.GetNetworkIdentity().PrefabHash, aircraft.Owner);
        
        // Match the initial spawn position/rotation to the aircraft
        railInstance.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

    private IEnumerator WaitAndAttach()
    {
        // Wait for Mirage to sync the railInstance to the client
        while (railInstance == null)
            yield return null;

        launchRail = railInstance.GetComponent<LaunchRail>();
        
        if (launchRail == null)
        {
            Debug.LogError("RailLaunchController: Spawned prefab is missing LaunchRail component!");
            yield break;
        }

        // The LaunchRail now handles TeleportToRail and SetupJoint internally
        launchRail.AttachAircraft(aircraft, railAttachPoint);
    }

    private void Update()
    {
        if (!aircraft.LocalSim) return;
        if (launchRail == null || !launchRail.IsReady) return;

        if (!isLaunched)
        {
            // Check for launch conditions (Throttle + Engine RPM)
            if (aircraft.GetInputs().throttle >= 0.95f && GetRPMRatio() > launchRPMThreshold)
            {
                Launch();
            }
        }
    }

    private void Launch()
    {
        isLaunched = true;
        
        // Trigger the rail physics (Catapult and Joint release)
        launchRail.Launch();
        
        // Ignite all attached boosters
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