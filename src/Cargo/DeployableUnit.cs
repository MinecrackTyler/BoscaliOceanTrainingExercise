using System;
using Mirage;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOComponentWIP;

public abstract class DeployableUnit : ScriptableObject
{
	public string unitName;
	public int pointCost;
	public Sprite icon;
	public string description;
	public virtual UnitDefinition UnitDefinition { get; } = null;

	public abstract Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft, out bool spawned);
}

[CreateAssetMenu(fileName = "New DeployableVehicle", menuName = "Bote/DeployableVehicle")]
public class DeployableVehicle : DeployableUnit
{
	public VehicleDefinition unitDefinition;
	public Vector3 spawnOffset;
	public override UnitDefinition UnitDefinition => unitDefinition;
	
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = false;
		Vector3 worldOffset = rotation  * spawnOffset;

		Vector3 finalSpawnPos = position + worldOffset;
		
		var spawnedVehicle = NetworkSceneSingleton<Spawner>.i.SpawnVehicle(unitDefinition.unitPrefab, finalSpawnPos.ToGlobalPosition(), 
			rotation, spawnVel, aircraft.NetworkHQ, null, 1f, false, aircraft.Player);

		if (spawnedVehicle != null)
		{
			spawned = true;
		}
		else
		{
			return null;
		}
		
		spawnedVehicle.MoveFromDepot();
		if (spawnedVehicle.parachuteSystem == null) return spawnedVehicle;
		var cds = spawnedVehicle.GetComponentInChildren<CargoDeploymentSystem>()?.gameObject;
		Destroy(cds);
		return spawnedVehicle;
	}
}

[CreateAssetMenu(fileName = "New DeployableAircraft", menuName = "Bote/DeployableAircraft")]
public class DeployableAircraft : DeployableUnit
{
	public AircraftDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = false;
		var airbase = aircraft.GetComponent<Airbase>();
		if (airbase == null && !airbase.CanSpawnAircraft(unitDefinition)) return null;

		Loadout loadout = null;
		float fuelLevel = unitDefinition.aircraftParameters.DefaultFuelLevel;
		StandardLoadout randomStandardLoadout = unitDefinition.aircraftParameters.GetRandomStandardLoadout(unitDefinition, aircraft.NetworkHQ);
		if (randomStandardLoadout != null)
		{
			loadout = randomStandardLoadout.loadout;
			fuelLevel = randomStandardLoadout.FuelRatio;
		}

		int randomLivery = unitDefinition.aircraftParameters.GetRandomLiveryForFaction(aircraft.NetworkHQ.faction);
		var result = airbase.TrySpawnAircraft(null, unitDefinition, new LiveryKey(randomLivery), loadout, fuelLevel);
		if (result.Allowed)
		{
			spawned = true;
		}

		return null;
	}
}

public class FOBUnit : DeployableUnit
{
	public bool IsAirbaseCenter;
	public GameObject unitGhost;
	public int maxUnits = -1;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		throw new System.NotImplementedException();
	}
}

[CreateAssetMenu(fileName = "New FOBBuilding", menuName = "Bote/FOBBuilding")]
public class FOBBuilding : FOBUnit
{
	public BuildingDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = true;
		var spawnedBuilding = NetworkSceneSingleton<Spawner>.i.SpawnBuilding(unitDefinition.unitPrefab, position.ToGlobalPosition(), rotation, aircraft.NetworkHQ, null, null, false, null);
		if (spawnedBuilding != null) spawned = true;
		return spawnedBuilding;
	}
}

[CreateAssetMenu(fileName = "New FOBVehicle", menuName = "Bote/FOBVehicle")]
public class FOBVehicle : FOBUnit
{
	public VehicleDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = true;
		return NetworkSceneSingleton<Spawner>.i.SpawnVehicle(unitDefinition.unitPrefab, position.ToGlobalPosition(), rotation, Vector3.zero, aircraft.NetworkHQ, null, 1f, true, aircraft.Player);
	}
}

[CreateAssetMenu(fileName = "New FOBScenery", menuName = "Bote/FOBScenery")]
public class FOBScenery : FOBUnit
{
	public SceneryDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = true;
		return NetworkSceneSingleton<Spawner>.i.SpawnScenery(unitDefinition.unitPrefab, position.ToGlobalPosition(),
			rotation, null);
	}
}
