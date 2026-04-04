using System;
using System.Collections;
using System.Collections.Generic;
using Mirage;
using Mirage.Collections;
using Mirage.Serialization;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOComponentWIP;

public class DeploymentManager : NetworkBehaviour
{
    public Transform spawnPoint;
    public List<DeployableUnit> availableUnits;
    [SerializeField] private FOBManager fobManager;
    [SerializeField] private int maxPoints;
    [SerializeField] private int fobCost;
    [SerializeField] private float spawnVelocity;
    
    public List<DeployableUnit> presetUnits;

    public readonly SyncList<int> unitManifest = new SyncList<int>();
    [SyncVar] private bool fobSelected;
    [SyncVar] private int selectedIndex = 0;
    
    
    [SerializeField] private Aircraft aircraft;

    public bool Safety = false;

    public int MaxPoints => maxPoints;
    public int FobCost => fobCost;
    public bool FobAvailable => fobManager != null;
    public bool HasFOB => FobAvailable && fobManager.hasFob;
    public bool FobSelected => fobSelected;
    public int SelectedIndex => selectedIndex;

    public List<int> UnitManifest => new List<int>(unitManifest);

    private float lastDeployTime;

    private void Awake()
    {
        aircraft.onInitialize += OnLocalPlayerStart;
    }

    private void OnLocalPlayerStart()
    {
        if (!aircraft.LocalSim) return;
        if (aircraft.Player == null) return;
        Debug.Log($"[BOAT] Local Player Started. Initializing Manifest...");
        
        List<int> presetIds = new List<int>();
        foreach (var unit in presetUnits)
        {
            int id = availableUnits.IndexOf(unit);
            if (id != -1) presetIds.Add(id);
        }
        

        if (LoadoutBridge.LoadoutSet)
        {
            CmdSetManifest(LoadoutBridge.SelectedUnitIDs.ToArray(), LoadoutBridge.FobMode);
            LoadoutBridge.Clear();
        }
        else
        {
            StartCoroutine(EditorStart());
        }
    }

    private IEnumerator EditorStart()
    {
        var canvas = GameplayUI.i.gameplayCanvas;
        if (canvas == null) yield break;
        
        var uiInstance = Instantiate(ModAssets.i.CargoEditorUI, canvas.transform);
        var controller = uiInstance.GetComponent<CargoUIController>();
        controller.Initialize(this);
        CursorManager.SetFlag(CursorFlags.Map, value: true);
        DynamicMap.AllowedToOpen = false;
        LoadoutBridge.BlockInputs = true;
        GameManager.flightControlsEnabled = false;
        aircraft.onDisableUnit += Disable;
        yield return new WaitUntil(() => LoadoutBridge.LoadoutSet);
        if (controller != null) controller.Close();
        CursorManager.SetFlag(CursorFlags.Map, value: false);
        CmdSetManifest(LoadoutBridge.SelectedUnitIDs.ToArray(), LoadoutBridge.FobMode);
        Disable(aircraft);
        aircraft.onDisableUnit -= Disable;
    }

    private void Disable(Unit unit)
    {
        DynamicMap.AllowedToOpen = true;
        LoadoutBridge.Clear();
        LoadoutBridge.BlockInputs = false;
        GameManager.flightControlsEnabled = true;
    }

    private void OnDestroy()
    {
        if (!aircraft.LocalSim) return;
        DynamicMap.AllowedToOpen = true;
        LoadoutBridge.Clear();
        LoadoutBridge.BlockInputs = false;
        GameManager.flightControlsEnabled = true;
    }

    [ServerRpc(requireAuthority = false)]
    public void CmdSetManifest(int[] unitIds, bool hasFOB)
    {
        Debug.Log($"Received manifest request. Count: {unitIds.Length}");
        
        unitManifest.Clear();
        fobManager?.hasFob = hasFOB;
        Array.Sort(unitIds);
        foreach (int id in unitIds)
        {
            unitManifest.Add(id);
        }
        
        selectedIndex = 0;
    }

    private void Update()
    {
        if (!aircraft.LocalSim || (IsEmpty() && !HasFOB)) return;

        var player = aircraft.pilots[0]?.playerState?.player;
        if (player == null) return;
        if (player.GetButtonDown("Select/Deselect FOB"))
        {
            if (!HasFOB) return;
            CmdRequestSelectionChange(0, !fobSelected);
        }

        if (player.GetButtonDown("Next Unit"))
        {
            CmdRequestSelectionChange(1, fobSelected);
        } else if (player.GetButtonDown("Previous Unit"))
        {
            CmdRequestSelectionChange(-1, fobSelected);
        }

        if (player.GetButton("Deploy Unit") && !Safety)
        {
            if (Time.timeSinceLevelLoad > lastDeployTime + 1f)
            {
                lastDeployTime = Time.timeSinceLevelLoad;
                CmdDeployUnit();
            }
            
        }
    }
    

    [ServerRpc]
    private void CmdRequestSelectionChange(int direction, bool fobSelected)
    {
        this.fobSelected = fobSelected;
        if (direction == 0) return;
        
        if (unitManifest.Count <= 1) return;
        
        int currentUnitID = unitManifest[selectedIndex];
        int nextIndex = selectedIndex;
        
        for (int i = 0; i < unitManifest.Count; i++)
        {
            nextIndex = (nextIndex + direction + unitManifest.Count) % unitManifest.Count;
            if (unitManifest[nextIndex] != currentUnitID)
            {
                selectedIndex = nextIndex;
                return;
            }
        }
        selectedIndex = (selectedIndex + direction + unitManifest.Count) % unitManifest.Count;
    }

    public bool IsEmpty() => unitManifest.Count == 0;

    [ServerRpc]
    public void CmdDeployUnit()
    {
        DeployUnit();
    }

    [Server]
    public void DeployUnit()
    {
        if (IsEmpty() && !HasFOB) return;

        if (fobSelected)
        {
            fobManager.hasFob = false;
            fobManager.DeployFOB();
            fobSelected = false;
            return;
        }
        
        int index = unitManifest[selectedIndex];
        DeployableUnit unit = availableUnits[index];
        
        Vector3 spawnVel = aircraft.rb.velocity + spawnPoint.forward * spawnVelocity;
        
        unit.SpawnUnit(spawnPoint.position, spawnPoint.rotation, spawnVel, aircraft, out var spawned);
        if (!spawned) return;
        unitManifest.RemoveAt(selectedIndex);
        
        if (selectedIndex >= unitManifest.Count && unitManifest.Count > 0)
        {
            selectedIndex = unitManifest.Count - 1;
        }
    }

    public int ContainsUnit(UnitDefinition unitDefinition)
    {
        foreach (var unitIndex in unitManifest)
        {
            var du = availableUnits[unitIndex];
            if (du.UnitDefinition == unitDefinition) return unitIndex;
        }

        return -1;
    }
}