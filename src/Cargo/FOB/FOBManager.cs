using System.Collections;
using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace NOComponentWIP;

public class FOBManager : NetworkBehaviour
{
    [SerializeField] private Aircraft aircraft;
    public List<FOBUnit> availableFOBUnits;
    
    public bool buildingFob;
    [SyncVar] public bool hasFob;
    
    private GameObject fobUI;
    
	[ClientRpc(target = RpcTarget.Owner)]
	public void DeployFOB()
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
        
        fobUI = Instantiate(ModAssets.i.FOBEditorUI, canvas.transform);
        var manager = fobUI.GetComponent<FOBUIController>();
        manager.Initialize(this, aircraft, aircraft.rb.position, availableFOBUnits,160);
        buildingFob = this;
        
        yield return new WaitUntil(() => !buildingFob); //will be changed to check when fob is done
        
        CursorManager.SetFlag(CursorFlags.Map, value: false);
        Disable(aircraft);
        aircraft.onDisableUnit -= Disable;
    }
    
    private void Disable(Unit unit)
    {
        Destroy(fobUI?.gameObject);
        DynamicMap.AllowedToOpen = true;
        LoadoutBridge.Clear();
        LoadoutBridge.BlockInputs = false;
        GameManager.flightControlsEnabled = true;
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
            positions[i] = unit.position.ToGlobalPosition().AsVector3();
            rotations[i] = unit.rotation;
        }
        
        CmdFinalizeFOB(indices, positions, rotations, spawnAirbase, center.ToGlobalPosition().AsVector3());
    }

    [ServerRpc]
    private void CmdFinalizeFOB(int[] indices, Vector3[] positions, Quaternion[] rotations, bool spawnAirbase, Vector3 center)

    {
        if (indices.Length != positions.Length || indices.Length != rotations.Length)
        {
            //Debug.LogError("[FOB] Network array mismatch! Aborting spawn.");
            return;
        }

        Airbase airbase = null;
        if (spawnAirbase)
        {
            GameObject go = Instantiate(GameAssets.i.airbasePrefab, Datum.origin);
            string uname = $"FOB_{aircraft.Player.PlayerName}_{Time.time}";
            go.name = uname; // create unique name
            var filter = go.AddComponent<AirbaseAIFilter>();
            filter.AddAllowedKey("UtilityHelo1");
            filter.AddAllowedKey("AttackHelo1");
            filter.AddAllowedKey("QuadVTOL1");
            airbase = go.GetComponent<Airbase>();
            airbase.transform.position = transform.position;
            airbase.center.localPosition = Vector3.zero;
            airbase.airbaseSettings.CaptureRange = 100f;
            airbase.SavedAirbase.UniqueName = uname;
            airbase.SavedAirbase.DisplayName = $"FOB: {aircraft.Player.PlayerName}";
            airbase.capture.SetCapturable(true);
            airbase.CaptureFaction(aircraft.NetworkHQ);
            NetworkManagerNuclearOption.i.ServerObjectManager.Spawn(airbase.Identity);
        }
        
        for (int i = 0; i < indices.Length; i++)
        {
            int dataIndex = indices[i];
            if (dataIndex < 0 || dataIndex >= availableFOBUnits.Count) continue;

            var data = availableFOBUnits[dataIndex];
            var gp = new GlobalPosition(positions[i].x, positions[i].y, positions[i].z);
            var spawnedObj = data.SpawnUnit(gp.ToLocalPosition(), rotations[i], Vector3.zero, aircraft, out var spawned);

            if (spawnAirbase)
            {
                airbase.transform.position = new GlobalPosition(center.x, center.y, center.z).ToLocalPosition();
                airbase.aircraftSelectionTransform = airbase.transform;
            }
            if (spawnedObj != null)
            {
                var building = spawnedObj.GetComponent<Building>();
                if (building != null && spawnAirbase && airbase != null)
                {
                    building.SetAirbase(airbase);
                }
            }
        }
    }
    
    [ServerRpc]
    public void ResetFOB()
    {
        hasFob = true;
    }
}