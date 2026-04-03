using System;
using System.Collections.Generic;
using UnityEngine;

namespace NOComponentWIP;

[CreateAssetMenu(fileName = "ModAssets", menuName = "Bote/ModAssets")]
public class ModAssets : ScriptableObject
{
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
				}
			}
			return _instance;
		}
		internal set => _instance = value;
	}

	public GameObject FOBEditorUI;
	public GameObject FOBEditorRow;
	public GameObject CargoEditorUI;
	public GameObject CargoEditorRow;

	[SerializeField] public List<string> aircraftKeys;
	[SerializeField] public List<RailHangarEntry> aircraftEntries;

	[SerializeField] public AircraftDefinition[] shipDefinitions;
	[SerializeField] public AircraftDefinition[] shipDefinitionsWithDeployer;

	

	private void OnEnable()
	{
		hideFlags = HideFlags.DontUnloadUnusedAsset;
	}
}
