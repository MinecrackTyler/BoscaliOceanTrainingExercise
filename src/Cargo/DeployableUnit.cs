using System;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace NOComponentWIP;

public abstract class DeployableUnit : ScriptableObject
{
	public string unitName;
	public int pointCost;
	public Sprite icon;
	public string description;

	public abstract Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft);
}

[CreateAssetMenu(fileName = "New DeployableVehicle", menuName = "Bote/DeployableVehicle")]
public class DeployableVehicle : DeployableUnit
{
	public VehicleDefinition unitDefinition;
	public Vector3 spawnOffset;
	
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft)
	{
		Vector3 worldOffset = rotation  * spawnOffset;

		Vector3 finalSpawnPos = position + worldOffset;
		
		var spawnedVehicle = NetworkSceneSingleton<Spawner>.i.SpawnVehicle(unitDefinition.unitPrefab, finalSpawnPos.ToGlobalPosition(), 
			rotation, spawnVel, aircraft.NetworkHQ, null, 1f, false, aircraft.Player);
		
		spawnedVehicle.MoveFromDepot();
		if (spawnedVehicle.parachuteSystem == null) return spawnedVehicle;
		var cds = spawnedVehicle.GetComponentInChildren<CargoDeploymentSystem>()?.gameObject;
		Destroy(cds);
		return spawnedVehicle;
	}
}

public class FOBUnit : DeployableUnit
{
	public GameObject unitGhost;
	public virtual UnitDefinition UnitDefinition { get; } = null;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft)
	{
		throw new System.NotImplementedException();
	}
}

[CreateAssetMenu(fileName = "New FOBBuilding", menuName = "Bote/FOBBuilding")]
public class FOBBuilding : FOBUnit
{
	public BuildingDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft)
	{
		var spawnedBuilding = NetworkSceneSingleton<Spawner>.i.SpawnBuilding(unitDefinition.unitPrefab, position.ToGlobalPosition(), rotation, aircraft.NetworkHQ, null, null, false, null);
		return spawnedBuilding;
	}
}

[CreateAssetMenu(fileName = "New FOBVehicle", menuName = "Bote/FOBVehicle")]
public class FOBVehicle : FOBUnit
{
	public VehicleDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft)
	{
		return NetworkSceneSingleton<Spawner>.i.SpawnVehicle(unitDefinition.unitPrefab, position.ToGlobalPosition(), rotation, Vector3.zero, aircraft.NetworkHQ, null, 1f, true, aircraft.Player);
	}
}

[CreateAssetMenu(fileName = "New FOBScenery", menuName = "Bote/FOBScenery")]
public class FOBScenery : FOBUnit
{
	public SceneryDefinition unitDefinition;
	public override UnitDefinition UnitDefinition => unitDefinition;
	public override Unit SpawnUnit(Vector3 position, Quaternion rotation, Vector3 spawnVel, Aircraft aircraft)
	{
		return NetworkSceneSingleton<Spawner>.i.SpawnScenery(unitDefinition.unitPrefab, position.ToGlobalPosition(),
			rotation, null);
	}
}
