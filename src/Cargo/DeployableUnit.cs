using System;
using System.Collections.Generic;
using Mirage;
using NOComponentWIP.ServerConfig;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOComponentWIP;

public class DeployableUnitComparer : IComparer<DeployableUnit>
{
	public static readonly DeployableUnitComparer Instance = new ();

	public int Compare(DeployableUnit x, DeployableUnit y)
	{
		if (ReferenceEquals(x, y)) return 0;
		if (x is null) return -1;
		if (y is null) return 1;
		
		return string.CompareOrdinal(x.JsonKey, y.JsonKey);
	}
}

public abstract class DeployableUnit : ScriptableObject
{
	public string unitName;
	public int pointCost;
	public Sprite icon;
	public string description;
	public bool eventContent;
	public virtual UnitDefinition UnitDefinition { get; } = null;
	public string JsonKey => UnitDefinition?.jsonKey ?? string.Empty;
	public float Value => UnitDefinition?.value ?? 0f;

	public Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft, bool overrideBlock, out bool spawned)
	{
		if (!overrideBlock)
		{
			if (!UnitConfig.UnitAllowed(JsonKey) || !UnitCountTracker.CanDeploy(JsonKey, aircraft))
			{
				spawned = false;
				return null;
			}
		}

		var unit = SpawnUnitInternal(position, rotation, spawnVel, aircraft, out spawned);
		
		if (spawned && unit != null)
		{
			var id = aircraft?.Player.SteamID ?? 0;
			UnitCountTracker.RegisterUnit(unit, id);
		}
		
		return unit;
	}
	
	protected abstract Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft, out bool spawned);
}

[CreateAssetMenu(fileName = "New DeployableVehicle", menuName = "Bote/DeployableVehicle")]
public class DeployableVehicle : DeployableUnit
{
	public VehicleDefinition unitDefinition;
	public Vector3 spawnOffset;
	public override UnitDefinition UnitDefinition => unitDefinition;
	
	protected override Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = false;
		Vector3 worldOffset = rotation  * spawnOffset;

		Vector3 finalSpawnPos = position + worldOffset;
		
		var spawnedVehicle = NetworkSceneSingleton<Spawner>.i.SpawnVehicle(unitDefinition.unitPrefab, finalSpawnPos.ToGlobalPosition(), 
			rotation, spawnVel, aircraft.NetworkHQ, null, 1f, false, aircraft.Player);

		if (spawnedVehicle == null) return null;

		spawned = true;
		
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
	protected override Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = false;
		var airbase = aircraft.GetComponent<Airbase>();
		if (airbase == null || !airbase.CanSpawnAircraft(unitDefinition)) return null;

		Loadout loadout = null;
		float fuelLevel = unitDefinition.aircraftParameters.DefaultFuelLevel;
		StandardLoadout randomStandardLoadout = unitDefinition.aircraftParameters.GetRandomStandardLoadout(unitDefinition, aircraft.NetworkHQ);
		if (randomStandardLoadout != null)
		{
			loadout = randomStandardLoadout.loadout;
			fuelLevel = randomStandardLoadout.FuelRatio;
		}

		int randomLivery = unitDefinition.aircraftParameters.GetRandomLiveryForFaction(aircraft.NetworkHQ.faction);
		
		var ownerID = aircraft?.Player?.SteamID ?? 0;
		UnitCountTracker.RegisterPendingAircraft(aircraft, unitDefinition, ownerID);
		
		var result = airbase.TrySpawnAircraft(null, unitDefinition, new LiveryKey(randomLivery), loadout, fuelLevel);
		if (result.Allowed)
		{
			spawned = true;
			aircraft.NetworkHQ.AddSupplyUnit(unitDefinition, 1);
		}
		else
		{
			UnitCountTracker.CancelPendingAircraft(aircraft, unitDefinition, ownerID);
		}
		
		return null;
	}
}

[CreateAssetMenu(fileName = "New DeployableMissile", menuName = "Bote/DeployableMissile")]
public class DeployableMissile : DeployableUnit
{
	public MissileDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	protected override Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = false;

		var spawnedUnit =
			NetworkSceneSingleton<Spawner>.i.SpawnMissile(unitDefinition, position, rotation, spawnVel, null, aircraft);

		if (spawnedUnit != null)
		{
			spawned = true;
			return spawnedUnit;
		}

		return null;
	}
}

public abstract class FOBUnit : DeployableUnit
{
	public bool IsAirbaseCenter;
	public GameObject unitGhost;
	public int maxUnits = -1;

	protected abstract override Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned);
}

[CreateAssetMenu(fileName = "New FOBBuilding", menuName = "Bote/FOBBuilding")]
public class FOBBuilding : FOBUnit
{
	public BuildingDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	protected override Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = false;
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
	protected override Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
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
	protected override Unit SpawnUnitInternal(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft,
		out bool spawned)
	{
		spawned = true;
		return NetworkSceneSingleton<Spawner>.i.SpawnScenery(unitDefinition.unitPrefab, position.ToGlobalPosition(),
			rotation, null);
	}
}
