using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using NuclearOption.ModScripts.Impl;
using NuclearOption.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NOComponentWIP;

public class FOBUIController : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI pointsText;
	[SerializeField] private Image pointsFillBar;

	[SerializeField] private Transform scrollArea;
	[SerializeField] private GameObject rowPrefab;
	
	[SerializeField] private LayerMask terrainLayer = 1 << 6;
	
	[SerializeField] private Button finalizeButton;
	[SerializeField] private Button cancelButton;
	
	[SerializeField] private float panSpeed = 40f;
	[SerializeField] private float zoomSpeed = 100f;
	[SerializeField] private Vector2 zoomLimit = new Vector2(50f, 250f);
	[SerializeField] private Vector2 vertLimit = new Vector2(0f, 5f);
	[SerializeField] private GameObject bootScreenPrefab;
	[SerializeField] private Color selectedColor;
	[SerializeField] private Color placedColor;
	[SerializeField] private Color invalidColor;

	private Camera buildCamera;
	
	private float buildRadius = 1000f;
	private GlobalPosition centerPos;
	private DeploymentManager manager;
	private Aircraft aircraft;
	
	private GameObject activeUnit;
	private FOBUnit activeData;
	private float currentYRotation;
	
	private List<PlacedFOBUnit> placedUnits = new List<PlacedFOBUnit>();
	private int currentPoints = 0;
	private int maxPoints = 0;
	private float verticalOffset;
	private MaterialPropertyBlock propertyBlock;
	
	public void Initialize(DeploymentManager manager, Aircraft aircraft, Vector3 center, List<FOBUnit> units, int maxPoints)
	{
		this.manager = manager;
		this.maxPoints = maxPoints;
		centerPos = center.ToGlobalPosition();
		this.aircraft = aircraft;
		
		finalizeButton.onClick.AddListener(FinalizeFOB);
		cancelButton.onClick.AddListener(CancelFOB);
		
		foreach (Transform child in scrollArea) Destroy(child.gameObject);

		foreach (var unit in units)
		{
			var row = Instantiate(rowPrefab, scrollArea);
			row.GetComponent<FOBAssetRow>().Setup(unit, this);
		}
		RefreshBudget();
		StartCoroutine(BootScreen());
		buildCamera = CameraStateManager.i.mainCamera;
		CameraStateManager.i.SwitchState(CameraStateManager.i.freeState);
	}

	private IEnumerator BootScreen()
	{
		GameObject bs = Instantiate(bootScreenPrefab, transform.parent);
		yield return new WaitForSeconds(1f);
		Destroy(bs);
	}

	public void SelectUnit(FOBUnit unit)
	{
		if (activeUnit != null) Destroy(activeUnit);

		activeData = unit;
		activeUnit = Instantiate(unit.unitGhost);
		foreach (var collider in activeUnit.GetComponentsInChildren<Collider>())
		{
			collider.enabled = false;
		}
	}

	private void Update()
	{
		HandleInput();
	}
	
	private void HandleInput()
	{
		var cam = buildCamera;
		if (cam == null) return;
		var ray = cam.ScreenPointToRay(Input.mousePosition);

		if (activeUnit != null)
		{
			if (Physics.Raycast(ray, out RaycastHit hit, 2000f, terrainLayer))
			{
				float dist = Vector3.Distance(hit.point, centerPos.ToLocalPosition());
				bool inRange = dist <= buildRadius;
				bool canAfford = (currentPoints + activeData.pointCost) <= maxPoints;
				bool isValid = inRange && canAfford;
			
				activeUnit.transform.position = hit.point + Vector3.up * (verticalOffset + activeData.UnitDefinition.spawnOffset.y);

				float inputDir = 0;
				if (Input.GetKey(KeyCode.Q)) inputDir = -1f;
				if (Input.GetKey(KeyCode.E)) inputDir = 1f;

				if (Input.GetKey(KeyCode.LeftShift))
				{
					verticalOffset += inputDir * 5f * Time.deltaTime;
					verticalOffset = Mathf.Clamp(verticalOffset, vertLimit.x, vertLimit.y);
				}
				else
				{
					currentYRotation += inputDir * 100f * Time.deltaTime;
				}
				
				if (!Input.GetKey(KeyCode.LeftAlt))
				{
					activeUnit.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, currentYRotation, 0);
				}
				else
				{
					activeUnit.transform.rotation = Quaternion.Euler(0, currentYRotation, 0);
				}
				
				SetGhostColor(activeUnit, isValid ? selectedColor : invalidColor);

				if (Input.GetMouseButtonDown(0) && isValid)
				{
					PlaceUnit();
				}
			}

			if (Input.GetMouseButtonDown(1))
			{
				Destroy(activeUnit);
				activeUnit = null;
				activeData = null;
			}
		}
		else
		{
			if (Input.GetMouseButtonDown(1))
			{
				if (Physics.Raycast(ray, out RaycastHit hit))
				{
					var target = placedUnits.FirstOrDefault(unit =>
						unit.instance == hit.collider.gameObject || hit.transform.IsChildOf(unit.instance.transform));
					if (target == null) return;
					currentPoints -= target.data.pointCost;
					placedUnits.Remove(target);
					Destroy(target.instance);
					RefreshBudget();
				}
			}
		}
	}
	
	private void PlaceUnit()
	{
		var placed = new PlacedFOBUnit
		{
			data = activeData,
			instance = activeUnit,
			position = activeUnit.transform.position,
			rotation = activeUnit.transform.rotation
		};
		
		placedUnits.Add(placed);
		currentPoints += activeData.pointCost;
		
		foreach (var collider in activeUnit.GetComponentsInChildren<Collider>())
		{
			collider.enabled = true;
		}
		
		SetGhostColor(activeUnit, placedColor);
		
		activeUnit = null;
		activeData = null;
		RefreshBudget();
	}
	
	private void SetGhostColor(GameObject ghost, Color color)
	{
		propertyBlock ??= new MaterialPropertyBlock();

		var renderers = ghost.GetComponentsInChildren<MeshRenderer>();
		foreach (var renderer in renderers)
		{
			renderer.GetPropertyBlock(propertyBlock);
			
			propertyBlock.SetColor("_Color", color);
			propertyBlock.SetColor("_BaseColor", color);
			propertyBlock.SetColor("_Tint", color);
			
			renderer.SetPropertyBlock(propertyBlock);
		}
	}

	private void RefreshBudget()
	{
		pointsText.text = $"CONSTRUCTION BUDGET: {currentPoints} / {maxPoints}";
		pointsFillBar.fillAmount = (float)currentPoints / maxPoints;
	}

	private void FinalizeFOB()
	{
		finalizeButton.onClick.RemoveAllListeners();
		
		manager.FinalizeFOB(placedUnits);
		Close();
	}

	private void CancelFOB()
	{
		manager.ResetFOB();
		Close();
	}
	
	private void Close()
	{
		cancelButton.onClick.RemoveAllListeners();
		CameraStateManager.i.SwitchState(CameraStateManager.i.cockpitState);
		if (activeUnit != null) Destroy(activeUnit);
		foreach (var unit in placedUnits) Destroy(unit.instance);
		manager.buildingFob = false;
	}
}

public class PlacedFOBUnit
{
	public FOBUnit data;
	public GameObject instance;
	public Vector3 position;
	public Quaternion rotation;
}