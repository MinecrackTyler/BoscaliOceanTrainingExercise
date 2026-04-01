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
	private bool spawnAirbase;
	
	private float buildRadius = 1000f;
	private GlobalPosition centerPos;
	private Vector3 airbaseCenter;
	private FOBManager manager;
	private Aircraft aircraft;
	
	private GameObject activeUnit;
	private FOBUnit activeData;
	private float currentYRotation;
	
	private List<PlacedFOBUnit> placedUnits = new List<PlacedFOBUnit>();
	private int currentPoints = 0;
	private int maxPoints = 0;
	private float verticalOffset;
	private MaterialPropertyBlock propertyBlock;
	
	private List<FOBAssetRow> uiRows = new List<FOBAssetRow>();
	
	public void Initialize(FOBManager manager, Aircraft aircraft, Vector3 center, List<FOBUnit> units, int maxPoints)
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
			var row = Instantiate(ModAssets.i.FOBEditorRow, scrollArea);
			var rowScript = row.GetComponent<FOBAssetRow>();
			rowScript.Setup(unit, this);
			uiRows.Add(rowScript);
		}

		RefreshRows();
		RefreshBudget();
		StartCoroutine(BootScreen());
		buildCamera = CameraStateManager.i.mainCamera;
		CameraStateManager.i.SwitchState(CameraStateManager.i.freeState);
	}

	private void RefreshRows()
	{
		foreach (var row in uiRows)
		{
			FOBUnit data = row.FOBUnit;

			if (data.maxUnits == -1)
			{
				row.Disable(false);
				continue;
			}
			
			int currentCount = placedUnits.Count(p => p.data == data);
			bool max = currentCount >= data.maxUnits;
			
			row.Disable(max);
		}
	}

	private IEnumerator BootScreen()
	{
		GameObject bs = Instantiate(bootScreenPrefab, transform.parent);
		yield return new WaitForSeconds(1f);
		Destroy(bs);
	}

	public void SelectUnit(FOBUnit unit)
	{
		if (unit.maxUnits != -1)
		{
			int count = placedUnits.Count(p => p.data == unit);
			if (count >= unit.maxUnits) return;
		}
		
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
			Vector3 finalPoint = Vector3.zero;
			Vector3 finalNormal = Vector3.up;
			bool hasHit = false;

			bool hitTerrain = Physics.Raycast(ray, out RaycastHit hit, 2000f, terrainLayer);

			float seaY = Datum.LocalSeaY;
			Plane oceanPlane = new Plane(Vector3.up, new Vector3(0, seaY, 0));
			bool hitOcean = oceanPlane.Raycast(ray, out float oceanDist);

			if (hitTerrain)
			{
				if (hit.point.y > seaY)
				{
					finalPoint = hit.point;
					finalNormal = hit.normal;
				} else if (hitOcean)
				{
					finalPoint = ray.GetPoint(oceanDist);
					finalNormal = Vector3.up;
				}

				hasHit = true;
			} else if (hitOcean && oceanDist <= 2000f)
			{
				finalPoint = ray.GetPoint(oceanDist);
				finalNormal = Vector3.up;
				hasHit = true;
			}
			
			if (hasHit)
			{
				float dist = Vector3.Distance(finalPoint, centerPos.ToLocalPosition());
				bool inRange = dist <= buildRadius;
				bool canAfford = (currentPoints + activeData.pointCost) <= maxPoints;
				bool isValid = inRange && canAfford;
			
				activeUnit.transform.position = finalPoint + Vector3.up * (verticalOffset + activeData.UnitDefinition.spawnOffset.y);

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
					activeUnit.transform.rotation = Quaternion.FromToRotation(Vector3.up, finalNormal) * Quaternion.Euler(0, currentYRotation, 0);
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
					if (target.isCenter)
					{
						spawnAirbase = false;
						airbaseCenter = centerPos.ToLocalPosition();
					}
					currentPoints -= target.data.pointCost;
					placedUnits.Remove(target);
					Destroy(target.instance);
					RefreshBudget();
					RefreshRows();
				}
			}
		}
	}
	
	private void PlaceUnit()
	{
		bool makeCenter = false;
		if (activeData.IsAirbaseCenter && !spawnAirbase)
		{
			spawnAirbase = true;
			airbaseCenter = activeUnit.transform.position;
			makeCenter = true;
		}
		
		var placed = new PlacedFOBUnit
		{
			data = activeData,
			instance = activeUnit,
			position = activeUnit.transform.position,
			rotation = activeUnit.transform.rotation,
			isCenter = makeCenter
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
		RefreshRows();
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
		
		manager.FinalizeFOB(placedUnits, spawnAirbase, airbaseCenter);
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
	public bool isCenter;
}