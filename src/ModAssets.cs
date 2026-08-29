using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace NOComponentWIP;

[CreateAssetMenu(fileName = "ModAssets", menuName = "Bote/ModAssets")]
public class ModAssets : ScriptableObject
{
	public static Action<bool> OnInitialize;

	private bool firstInitialize = true;
	
	private static ModAssets _instance;
	public static ModAssets i
	{
		get
		{
			if (_instance == null)
			{
				var assets = Resources.FindObjectsOfTypeAll<ModAssets>();
				if (assets.Length > 0)
				{
					_instance = assets[0];
					_instance.Initialize();
				}
			}
			return _instance;
		}
		internal set => _instance = value;
	}

	[Header("Lists")]
	[SerializeField] private AircraftDefinition[] shipDefinitions;
	[SerializeField] private AircraftDefinition[] shipDefinitionsWithDeployer;
	[SerializeField] private List<DeployableUnit> allDeployableUnits;
	
	public RadialMenuAction[] actionsToAdd;
	
	[Header("Assets")]
	public BuildingDefinition dockDef;
	public GameObject networkModSingletons;
	public GameObject modSingletons;
	public Sprite RadarHUDIcon;
	public GameObject FOBEditorUI;
	public GameObject FOBEditorRow;
	public GameObject CargoEditorUI;
	public GameObject CargoEditorRow;
	

	//Runtime
	
	public readonly Dictionary<string, DeployableUnit> AllDeployableUnits = new();
	public readonly HashSet<AircraftDefinition> ShipDefinitions = new();
	public readonly HashSet<AircraftDefinition> ShipDefinitionsWithDeployer = new();

	private void Initialize()
	{
		InitTask().Forget();
	}

	private async UniTask InitTask()
	{
		Plugin.DebugLog("ModAssets Initialize Await");
		await new WaitUntil(BlueprinterHelper.IsPatchingComplete);
		Plugin.DebugLog("ModAssets Initialize");
		foreach (var unit in allDeployableUnits)
		{
			Plugin.DebugLog($"ModAssets Initialize: Unit: {unit?.JsonKey}");
			AllDeployableUnits.TryAdd(unit?.JsonKey, unit);
		}

		foreach (var def in shipDefinitions)
		{
			ShipDefinitions.Add(def);
		}

		foreach (var def in shipDefinitionsWithDeployer)
		{
			ShipDefinitionsWithDeployer.Add(def);
		}
		
		OnInitialize?.Invoke(firstInitialize);
		firstInitialize = false;
		
		Plugin.DebugLog($"ModAssets Initialize: AllDeployableUnits: {AllDeployableUnits.Count}");
		Plugin.DebugLog($"ModAssets Initialize: ShipDefinitions: {ShipDefinitions.Count}");
	}
	
	private void OnEnable()
	{
		Plugin.DebugLog("ModAssets OnEnable");
		hideFlags = HideFlags.DontUnloadUnusedAsset;
	}
}
