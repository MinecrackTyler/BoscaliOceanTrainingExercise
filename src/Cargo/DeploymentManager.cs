using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BoscaliOceanTrainingExercise.ForwardOperatingBase;
using BoscaliOceanTrainingExercise.ServerConfig;
using Mirage;
using Mirage.Collections;
using Mirage.Serialization;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.Serialization;

namespace BoscaliOceanTrainingExercise;

public class DeploymentManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Aircraft aircraft;
    [SerializeField] private FOBManager fobManager;
    [SerializeField] private Transform spawnPoint;
    
    [Header("Configuration")]
    [SerializeField] private int maxPoints;
    [SerializeField] private int fobCost;
    [SerializeField] private float spawnVelocity;
    
    [FormerlySerializedAs("availableUnits")] public List<DeployableUnit> deployableUnits;

    /*public readonly SyncIDictionary<DeployableUnit, int> unitManifest = 
        new(new SortedDictionary<DeployableUnit, int>(DeployableUnitComparer.Instance));*/
    public readonly SyncDictionary<DeployableUnit, int> unitManifest = new SyncDictionary<DeployableUnit, int>();
    [SyncVar] private int selectedIndex = 0;
    
    public bool Safety = false;

    public int MaxPoints => maxPoints;
    public int FobCost => fobCost;
    public bool FobAvailable => fobManager != null;
    public bool HasFOB => FobAvailable && fobManager.hasFob;
    public int SelectedIndex => selectedIndex;
    public IReadOnlyDictionary<DeployableUnit, int> UnitManifest => unitManifest;

    private float lastDeployTime;

    private const string INPUT_FOB    = $"{Mod_Input.ModShortName}:Deploy FOB";
    private const string INPUT_NEXT   = $"{Mod_Input.ModShortName}:Next Unit";
    private const string INPUT_PREV   = $"{Mod_Input.ModShortName}:Previous Unit";
    private const string INPUT_DEPLOY = $"{Mod_Input.ModShortName}:Deploy Unit";

    private void Awake()
    {
        aircraft.onInitialize += OnLocalPlayerStart;
    }

    private void OnLocalPlayerStart()
    {
        if (!aircraft.LocalSim) return;
        if (aircraft.Player == null) return;
        if (!GameManager.IsLocalAircraft(aircraft)) return;

        if (LoadoutBridge.LoadoutSet)
        {
            CmdSetManifest(LoadoutBridge.Manifest, LoadoutBridge.IncludeFOB);
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
        
        aircraft.onDisableUnit += Disable;
        
        var uiInstance = Instantiate(ModAssets.i.CargoEditorUI, canvas.transform);
        var controller = uiInstance.GetComponent<CargoUIController>();
        controller.Initialize(this);
        
        CursorManager.SetFlag(CursorFlags.Map, value: true);
        DynamicMap.AllowedToOpen = false;
        LoadoutBridge.BlockInputs = true;
        GameManager.flightControlsEnabled = false;
        
        yield return new WaitUntil(() => LoadoutBridge.LoadoutSet);
        
        if (controller != null) controller.Close();
        
        CursorManager.SetFlag(CursorFlags.Map, value: false);
        CmdSetManifest(LoadoutBridge.Manifest, LoadoutBridge.IncludeFOB);
        
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
        if (!aircraft?.LocalSim ?? true) return;
        DynamicMap.AllowedToOpen = true;
        LoadoutBridge.Clear();
        LoadoutBridge.BlockInputs = false;
        GameManager.flightControlsEnabled = true;
    }

    private bool IsUnitAllowed(DeployableUnit unit)
    {
        //TODO
        return unit != null;
    }

    [ServerRpc]
    public void CmdSetManifest(Dictionary<DeployableUnit, int> manifest, bool hasFOB)
    {
        Plugin.Logger.LogInfo($"Received manifest request. Count: {manifest.Count}");
        
        if (aircraft.Player == null) return;

        unitManifest.Clear();
        fobManager?.hasFob = hasFOB;
        
        float allocation = aircraft.Player.Allocation;
        float totalCost = 0f;
        bool errored = false;

        foreach (var kvp in manifest)
        {
            if (errored) break;
            if (!IsUnitAllowed(kvp.Key)) continue;
            for (int i = 0; i < kvp.Value; i++)
            {
                var cost = UnitConfig.UnitCost(kvp.Key.JsonKey);
                allocation -= cost;

                if (allocation <= 0f)
                {
                    errored = true;
                    Plugin.Logger.LogWarning(
                        $"Player: {aircraft.Player.GetDisplayName(PlayerNameContext.ChatOrLeaderboard)} sent invalid manifest, insufficient allocation");
                    break;
                }

                totalCost += cost;

                unitManifest.TryAdd(kvp.Key, kvp.Value);
            }
        }
        
        aircraft.Player.AddAllocation(0 - totalCost);

        selectedIndex = 0;
    }

    [Server]
    public void AddUnit(DeployableUnit unit, int count = 1)
    {
        if (unit == null) return;
        
        if (unitManifest.TryGetValue(unit, out int currentCount))
        {
            unitManifest[unit] = currentCount + count;
        }
        else
        {
            unitManifest[unit] = count;
        }
    }

    [Server]
    private bool UseUnit(DeployableUnit unit)
    {
        if (unit == null) return false;

        if (unitManifest.TryGetValue(unit, out int currentCount) && currentCount > 0)
        {
            int nextCount = currentCount - 1;
            if (nextCount == 0)
            {
                unitManifest.Remove(unit);
                if (selectedIndex >= unitManifest.Count && unitManifest.Count > 0)
                {
                    selectedIndex = unitManifest.Count - 1;
                }
            }
            else
            {
                unitManifest[unit] = nextCount;
            }
            return true;
        }

        return false;
    }

    public void NextUnit() =>  CmdRequestSelectionChange(1);
    public void PrevUnit() => CmdRequestSelectionChange(-1);

    private void Update()
    {
        if (!aircraft.LocalSim || (IsEmpty() && !HasFOB)) return;

        var player = aircraft.pilots[0]?.playerState?.player;
        if (player == null) return;
        if (player.GetButtonDown(INPUT_FOB) && !Safety)
        {
            if (!HasFOB) return;
            CmdDeployFOB();
        }
        if (player.GetButtonDown(INPUT_NEXT))
        {
            NextUnit();
        } 
        else if (player.GetButtonDown(INPUT_PREV))
        {
            PrevUnit();
        }

        if (player.GetButton(INPUT_DEPLOY) && !Safety)
        {
            if (Time.timeSinceLevelLoad > lastDeployTime + 1f)
            {
                lastDeployTime = Time.timeSinceLevelLoad;
                CmdDeployUnit();
            }
            
        }
    }
    
    [ServerRpc]
    private void CmdRequestSelectionChange(int direction)
    {
        if (direction == 0 || unitManifest.Count <= 1) return;
        selectedIndex = (selectedIndex + direction + unitManifest.Count) % unitManifest.Count;
    }

    public DeployableUnit GetSelectedUnit()
    {
        if (unitManifest.Count == 0 || selectedIndex < 0 || selectedIndex >= unitManifest.Count)
            return null;

        int i = 0;
        foreach (var kvp in unitManifest)
        {
            if (i == selectedIndex) return kvp.Key;
            i++;
        }

        return null;
    }

    public bool IsEmpty() => unitManifest.Count == 0;

    [ServerRpc]
    public void CmdDeployFOB()
    {
        DeployFOB();
    }

    [Server]
    public void DeployFOB()
    {
        if (!HasFOB) return;
        fobManager.hasFob = false;
        fobManager.DeployFOB();
    }
    
    [ServerRpc]
    public void CmdDeployUnit()
    {
        DeployUnit();
    }

    [Server]
    public void DeployUnit()
    {
        if (IsEmpty()) return;

        DeployableUnit unit = GetSelectedUnit();
        if (unit == null) return;
        
        Vector3 spawnVel = aircraft.rb.velocity + spawnPoint.forward * spawnVelocity;
        
        unit.SpawnUnit(spawnPoint.position, spawnPoint.rotation, spawnVel, aircraft, false, out var spawned);
        if (!spawned) return;
        UseUnit(unit);
    }

    public bool ContainsUnit(UnitDefinition unitDefinition, out DeployableUnit foundUnit)
    {
        foreach (var kvp in unitManifest)
        {
            if (kvp.Key != null && kvp.Key.UnitDefinition == unitDefinition)
            {
                foundUnit = kvp.Key;
                return true;
            }
        }

        foundUnit = null;
        return false;
    }
}