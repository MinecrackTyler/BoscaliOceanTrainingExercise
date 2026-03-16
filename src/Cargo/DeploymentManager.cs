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
    public List<FOBUnit> availableFOBUnits;
    [SerializeField] private bool fobAvailable;
    [SerializeField] private int maxPoints;
    [SerializeField] private int fobCost;
    [SerializeField] private float spawnVelocity;
    
    public List<DeployableUnit> presetUnits;

    public readonly SyncList<int> unitManifest = new SyncList<int>();
    [SyncVar] private bool hasFOB;
    [SyncVar] private bool fobSelected;
    [SyncVar] private int selectedIndex = 0;
    
    public bool buildingFob;

    [SerializeField] private GameObject uiPrefab;
    [SerializeField] private GameObject fobUIPrefab;
    [SerializeField] private Aircraft aircraft;

    public int MaxPoints => maxPoints;
    public int FobCost => fobCost;
    public bool FobAvailable => fobAvailable;
    public bool HasFOB => hasFOB;
    public bool FobSelected => fobSelected;
    public int SelectedIndex => selectedIndex;

    private void Awake()
    {
        aircraft.onInitialize += OnLocalPlayerStart;
    }

    private void OnLocalPlayerStart()
    {
        if (!aircraft.LocalSim) return;
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
        
        var uiInstance = Instantiate(uiPrefab, canvas.transform);
        var controller = uiInstance.GetComponent<CargoUIController>();
        controller.Initialize(this);
        CursorManager.SetFlag(CursorFlags.Map, value: true);
        DynamicMap.AllowedToOpen = false;
        LoadoutBridge.BlockInputs = true;
        GameManager.flightControlsEnabled = false;
        aircraft.onDisableUnit += Disable;
        yield return new WaitUntil(() => LoadoutBridge.LoadoutSet);
        
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

    [ServerRpc(requireAuthority = false)]
    public void CmdSetManifest(int[] unitIds, bool hasFOB)
    {
        /*var active = NetworkManagerNuclearOption.i.Server.Active;
        Debug.Log($"[BOAT] Manifest Set. Active is {active}");
        if (!active) return;*/
        Debug.Log($"Received manifest request. Count: {unitIds.Length}");
        
        unitManifest.Clear();
        this.hasFOB = hasFOB;
        Array.Sort(unitIds);
        foreach (int id in unitIds)
        {
            unitManifest.Add(id);
        }
        
        selectedIndex = 0;
    }

    private void Update()
    {
        if (!aircraft.LocalSim || (IsEmpty() && !hasFOB)) return;

        if (Input.GetKeyDown(KeyCode.UpArrow) && hasFOB)
        {
            CmdRequestSelectionChange(0, true);
        } else if (Input.GetKeyDown(KeyCode.DownArrow) && hasFOB)
        {
            CmdRequestSelectionChange(0, false);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            CmdRequestSelectionChange(-1, false);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            CmdRequestSelectionChange(1, false);
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

    public void DeployUnit()
    {
        if (!aircraft.IsServer || (IsEmpty() && !hasFOB)) return;

        if (fobSelected)
        {
            DeployFOB();
            hasFOB = false;
            fobSelected = false;
            return;
        }
        
        int index = unitManifest[selectedIndex];
        DeployableUnit unit = availableUnits[index];
        
        Vector3 spawnVel = aircraft.rb.velocity + spawnPoint.forward * spawnVelocity;
        
        unit.SpawnUnit(spawnPoint.position, spawnPoint.rotation, spawnVel, aircraft);
        
        unitManifest.RemoveAt(selectedIndex);
        
        if (selectedIndex >= unitManifest.Count && unitManifest.Count > 0)
        {
           selectedIndex = unitManifest.Count - 1;
        }
    }

    [ServerRpc]
    public void ResetFOB()
    {
        hasFOB = true;
    }

    [ClientRpc(target = RpcTarget.Owner)]
    private void DeployFOB()
    {
        StartCoroutine(FOBBuilder());
    }

    private IEnumerator FOBBuilder()
    {
        var canvas = GameplayUI.i.gameplayCanvas;
        if (canvas == null) yield break;
        
        CursorManager.SetFlag(CursorFlags.Map, value: true);
        DynamicMap.AllowedToOpen = false;
        GameManager.flightControlsEnabled = false;
        LoadoutBridge.BlockInputs = true;
        aircraft.onDisableUnit += Disable;
        
        var fobUI = Instantiate(fobUIPrefab, canvas.transform);
        var manager = fobUI.GetComponent<FOBUIController>();
        manager.Initialize(this, aircraft, aircraft.rb.position, availableFOBUnits,160);
        buildingFob = this;
        
        yield return new WaitUntil(() => !buildingFob); //will be changed to check when fob is done
        
        Destroy(fobUI.gameObject);
        
        CursorManager.SetFlag(CursorFlags.Map, value: false);
        Disable(aircraft);
        aircraft.onDisableUnit -= Disable;
    }

    public void FinalizeFOB(List<PlacedFOBUnit> placedUnits)
    {
        int count = placedUnits.Count;
        
        int[] indices = new int[count];
        Vector3[] positions = new Vector3[count];
        Quaternion[] rotations = new Quaternion[count];

        for (int i = 0; i < count; i++)
        {
            var unit = placedUnits[i];
            indices[i] = availableFOBUnits.IndexOf(unit.data);
            positions[i] = unit.position.ToGlobalPosition().AsVector3();
            rotations[i] = unit.rotation;
        }
        
        CmdFinalizeFOB(indices, positions, rotations);
    }
    
    [ServerRpc]
    private void CmdFinalizeFOB(int[] indices, Vector3[] positions, Quaternion[] rotations)
    {
        if (indices.Length != positions.Length || indices.Length != rotations.Length)
        {
            //Debug.LogError("[FOB] Network array mismatch! Aborting spawn.");
            return;
        }
        
        GameObject go = Instantiate(GameAssets.i.airbasePrefab, Datum.origin);
        string uname = $"FOB_{aircraft.Player.PlayerName}_{Time.time}";
		go.name = uname; // create unique name
        var filter = go.AddComponent<AirbaseAIFilter>();
        filter.AddAllowedKey("UtilityHelo1");
        filter.AddAllowedKey("AttackHelo1");
        filter.AddAllowedKey("QuadVTOL1");
		var airbase = go.GetComponent<Airbase>();
		airbase.transform.position = transform.position;
		airbase.center.localPosition = Vector3.zero;
		airbase.airbaseSettings.CaptureRange = 100f;
        airbase.SavedAirbase.UniqueName = uname;
        airbase.SavedAirbase.DisplayName = $"FOB: {aircraft.Player.PlayerName}";
		airbase.capture.SetCapturable(true);
		airbase.CaptureFaction(aircraft.NetworkHQ);
		NetworkManagerNuclearOption.i.ServerObjectManager.Spawn(airbase.Identity);
        
        for (int i = 0; i < indices.Length; i++)
        {
            int dataIndex = indices[i];
            if (dataIndex < 0 || dataIndex >= availableFOBUnits.Count) continue;
            
            var data = availableFOBUnits[dataIndex];
            var gp = new GlobalPosition(positions[i].x,  positions[i].y, positions[i].z);
            var spawnedObj = data.SpawnUnit(gp.ToLocalPosition(), rotations[i], Vector3.zero, aircraft);
        
            if (spawnedObj != null)
            {
                var building = spawnedObj.GetComponent<Building>();
                if (building != null)
                {
                    building.SetAirbase(airbase);
                }
            }
        }
    }
}