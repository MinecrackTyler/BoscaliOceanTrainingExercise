using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using NOComponentWIP.ServerConfig;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOComponentWIP;

public class FOBManager : NetworkBehaviour
{
    [SerializeField] private Aircraft aircraft;
    public List<FOBUnit> availableFOBUnits;
    
    [SyncVar] public bool hasFob;
    
    public bool BuildingFob { get; private set; }
    
    private GameObject fobUI;
    private Coroutine fobCoroutine;
    
    private const int MaxConstructionPoints = 160;
    
	[ClientRpc(target = RpcTarget.Owner)]
	public void DeployFOB()
	{
        if (aircraft == null || !aircraft.LocalSim) return;

        if (fobCoroutine != null)
        {
            StopCoroutine(fobCoroutine);
        }
        
		fobCoroutine = StartCoroutine(FOBBuilder());
	}
	
	private IEnumerator FOBBuilder()
    {
        var canvas = GameplayUI.i.gameplayCanvas;
        if (canvas == null) yield break;
        
        BuildingFob = true;
        
        CursorManager.SetFlag(CursorFlags.Map, value: true);
        DynamicMap.AllowedToOpen = false;
        GameManager.flightControlsEnabled = false;
        LoadoutBridge.BlockInputs = true;
        
        aircraft.onDisableUnit += Disable;
        
        fobUI = Instantiate(ModAssets.i.FOBEditorUI, canvas.transform);
        var uiController = fobUI.GetComponent<FOBUIController>();
        
        uiController.Initialize(this, aircraft, aircraft.rb.position, availableFOBUnits, MaxConstructionPoints);
        
        yield return new WaitUntil(() => !BuildingFob || aircraft.Networkdisabled); //will be changed to check when fob is done
        
        Cleanup();
    }

    private void Cleanup()
    {
        BuildingFob = false;

        if (fobUI != null)
        {
            Destroy(fobUI);
            fobUI = null;
        }
        
        this.aircraft?.onDisableUnit -= Disable;
        if (!aircraft?.LocalSim ?? true) return;
        
        CursorManager.SetFlag(CursorFlags.Map, value: false);
        DynamicMap.AllowedToOpen = true;
        LoadoutBridge.BlockInputs = false;
        GameManager.flightControlsEnabled = true;
    }
    
    private void Disable(Unit unit)
    {
        Cleanup();
    }

    public void Close()
    {
        BuildingFob = false;
    }

    public void FinalizeFOB(List<PlacedFOBUnit> placedUnits, bool spawnAirbase, Vector3 center)
    {
        int count = placedUnits.Count;
        
        int[] indices = new int[count];
        Vector3[] positions = new Vector3[count];
        Quaternion[] rotations = new Quaternion[count];

        for (int i = 0; i < count; i++)
        {
            var unit = placedUnits[i];
            indices[i] = availableFOBUnits.IndexOf(unit.data);
            positions[i] = unit.globalPosition;
            rotations[i] = unit.rotation;
        }
        
        CmdFinalizeFOB(indices, positions, rotations, spawnAirbase, center);
    }

    [ServerRpc]
    private void CmdFinalizeFOB(int[] indices, Vector3[] positions, Quaternion[] rotations, bool spawnAirbase, Vector3 center)

    {
        if (indices == null || positions == null || rotations == null ||
            indices.Length != positions.Length || indices.Length != rotations.Length)
        {
            Plugin.Logger.LogError("Network array mismatch on CmdFinalizeFOB! Aborting spawn.");
            return;
        }
        
        var player = aircraft?.Player;
        if (player == null) return;
        
        var localCounts = new Dictionary<FOBUnit, int>();
        var spawnedBuildings = new List<Building>();
        
        bool centerSpawned = false;
        Vector3 validatedCenter = center;
        
        float remainingAllocation = player.Allocation;
        float totalAllocationCost = 0f;
        
        int usedConstructionPoints = 0;
        
        for (int i = 0; i < indices.Length; i++)
        {
            int dataIndex = indices[i];
            if (dataIndex < 0 || dataIndex >= availableFOBUnits.Count) continue;

            var data = availableFOBUnits[dataIndex];
            if (data == null) continue;
            
            var localCount = localCounts.GetValueOrDefault(data, 0);
            
            if (data.maxUnits >= 0 && localCount >= data.maxUnits) continue;
            
            if (usedConstructionPoints + data.pointCost > MaxConstructionPoints) continue;
            
            var allocationCost = 0f;
            
            if (UnitConfig.UnitEconomy())
            {
                allocationCost = Mathf.Max(0f, UnitConfig.UnitCost(data.JsonKey));
                if (allocationCost > remainingAllocation) continue;
            }
            
            var gp = new GlobalPosition(positions[i]);
            var spawnedObj = data.SpawnUnit(gp.ToLocalPosition(), rotations[i], Vector3.zero, aircraft, false, out var spawned);
            
            if (!spawned || spawnedObj == null) continue;
            
            localCounts[data] = localCount + 1;
            usedConstructionPoints += data.pointCost;
            
            if (UnitConfig.UnitEconomy())
            {
                remainingAllocation -= allocationCost;
                totalAllocationCost += allocationCost;
            }
            
            if (data.IsAirbaseCenter && !centerSpawned)
            {
                centerSpawned = true;
                validatedCenter = positions[i];
            }
            
            if (spawnedObj is Building building)
            {
                spawnedBuildings.Add(building);
            }
        }
        
        if (totalAllocationCost > 0f)
        {
            player.AddAllocation(-totalAllocationCost);
        }
        
        if (spawnAirbase && centerSpawned)
        {
            SetupAirbase(validatedCenter, out var airbase);
            
            if (airbase != null)
            {
                foreach (var building in spawnedBuildings)
                {
                    building.SetAirbase(airbase);
                }
            }
        }

        hasFob = false;
    }

    private void SetupAirbase(Vector3 center, out Airbase airbase)
    {
        GameObject go = Instantiate(GameAssets.i.airbasePrefab, Datum.origin);
        
        // Earlier airbase null check so SavedAirbase registration doesn't happen yet to not leave an invalid/unused
        // part of that in case airbase is null and thus it never completes
        airbase = go.GetComponent<Airbase>();
        if (airbase == null)
        {
            Destroy(go);
            return;
        }
        
        var player = aircraft.Player;
        if (player == null)
        {
            Destroy(go);
            airbase = null;
            return;
        }
        
        var ownerRef = player.PlayerRef;
        var ownerSteamId = player.SteamID;
        
        string uname = $"FOB_{ownerSteamId}_{Time.time}";
        const string displayName = "FOB";
        var factionName = aircraft.NetworkHQ.faction.factionName;
        
        var globalCenter = new GlobalPosition(center.x, center.y + 10f, center.z);
        go.name = uname;
        
        var filter = go.AddComponent<AirbaseAIFilter>();
        filter.AddAllowedKey("UtilityHelo1");
        filter.AddAllowedKey("AttackHelo1");
        filter.AddAllowedKey("QuadVTOL1");
        
        var saved = new SavedAirbase();
        ApplySavedAirbaseState(saved, uname, displayName, factionName, globalCenter);
        
        airbase.aircraftSelectionTransform = airbase.transform;
        airbase.SetupCustomAirbase(saved);
        
        // Add new airbase to vanilla custom airbase list in current mission
        MissionManager.CurrentMission.airbases.Add(saved);
        NetworkManagerNuclearOption.i.ServerObjectManager.Spawn(airbase.Identity);
        
        // Resolve name right away on non-dedicated server host
        if (!GameManager.IsHeadless)
        {
            FOBNameResolver.Resolve(saved, player);
        }
        
        // Serialises CurrentMission state from the new additions for future joining clients to receive
        RefreshLateJoinMission();
        
        // Sync with current live clients
        RpcFinalizeFOB(airbase, globalCenter.AsVector3(), ownerRef, factionName);
    }

    [ClientRpc(excludeHost = true)] // Host already has the new SavedAirbase from its SetupCustomAirbase()
    private void RpcFinalizeFOB(Airbase airbase, Vector3 globalCenter, PlayerRef ownerRef, string faction)
    {
        if (airbase == null) return;
        
        var mission = MissionManager.CurrentMission;
        if (mission == null) return;
        
        GlobalPosition center = new GlobalPosition(globalCenter);
        
        // Make sure this doesn't already exist in mission to prevent duplicate entry
        // This is likely not needed? But I wanted to be sure, especially if network behaviour changes later
        var saved = mission.airbases.FirstOrDefault(sA => sA.UniqueName == airbase.NetworknetworkUniqueName);
        
        if (saved == null)
        {
            saved = new SavedAirbase();
            mission.airbases.Add(saved);
        }
        
        ApplySavedAirbaseState(saved, airbase.NetworknetworkUniqueName, "FOB", faction, center);
        airbase.SetupCustomAirbase(saved);
        FOBNameResolver.Resolve(saved, ownerRef.Player);
    }
    
    private static void RefreshLateJoinMission()
    {
        // This sets up serialisation of current mission state for future clients to receive
        // Otherwise they'd just get the initial mission load state without the new FOBs added
        
        var mission = MissionManager.CurrentMission;
        var networkMission = NetworkManagerNuclearOption.i?.NetworkMission;
        
        if (mission == null || networkMission == null)
            return;
        
        networkMission.partSender = NetworkMission.PartSender.Create(new NetworkMission.SyncMission(mission));
        
        // Default to multi-send part instead of dynamically deduce <64kB vs larger mission sends, stays more simple
        networkMission.sendAsParts = true;
    }
    
    private static void ApplySavedAirbaseState(SavedAirbase saved, string uniqueName, string displayName, 
        string faction, GlobalPosition center)
    {
        saved.UniqueName = uniqueName;
        saved.DisplayName = displayName;
        saved.faction = faction;
        saved.Center = center;
        saved.SelectionPosition = center;
        saved.CaptureRange = 100f;
        saved.Capturable = true;
        saved.Disabled = false;
        
        // Vanilla also calls these
        saved.CenterWrapper.SetValue(center, saved);
        saved.SelectionPositionWrapper.SetValue(center, saved);
        
        saved.SavedInMission = true;
    }
    
    [ServerRpc]
    public void ResetFOB()
    {
        hasFob = true;
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}