using System.Collections;
using System.Collections.Generic;
using NuclearOption.Jobs;
using UnityEngine;

namespace NOComponentWIP;

public class ShipPartBridge : MonoBehaviour
{
	public List<AircraftShipPart> parts;
	public Aircraft aircraft;
	public float damageControlDeploymentThreshold;
	public float damageControlAvailable;
	public bool disabled => aircraft.disabled;

	[SerializeField] private bool handleGear;

	[SerializeField] private Ship.WakeParticles[] wakeParticles;
	private float gearTimer = 0f;
	
	public DeploymentManager deploymentManager;
	public FOBManager fobManager;
	public ResupplyController resupplyController;
	
	public void Awake()
	{
		aircraft.onDisableUnit += UnitDisabled;
		aircraft.onInitialize +=  OnInitialize;
		if (handleGear) aircraft.onSetGear += HandleGearEvent;
	}
	
	private void UnitDisabled(Unit unit)
	{
		foreach (AircraftShipPart part in parts)
		{
			part.Flood();
		}
	}
	
	private void HandleGearEvent(Aircraft.OnSetGear e)
	{
		switch (e.gearState)
		{
			case LandingGear.GearState.Extending:
				aircraft.SetGear(LandingGear.GearState.LockedExtended);
				break;
			case LandingGear.GearState.Retracting:
				aircraft.SetGear(LandingGear.GearState.LockedRetracted);
				break;
		}
	}

	private void OnDestroy()
	{
		foreach (AircraftShipPart part in parts)
		{
			if (part != null)
			{
				Destroy(part.gameObject);
			}
		}
	}

	private void ApplyPartsForce()
	{
		Vector3 force = default(Vector3);
		Vector3 torque = default(Vector3);
		Vector3 vector = base.transform.TransformPoint(aircraft.rb.centerOfMass);
		foreach (AircraftShipPart part in parts)
		{
			if (!part.IsDetached() && part.JobFields.IsCreated)
			{
				ref ShipPartFields reference = ref part.JobFields.Ref();
				Vector3 vector2 = reference.forcePosition - vector;
				Vector3 vector3 = Vector3.Cross(reference.force, -vector2);
				force += reference.force;
				torque += vector3;
			}
		}
		aircraft.rb.AddForce(force);
		aircraft.rb.AddTorque(torque);
	}

	private void UpdateParticles()
	{
		foreach (var wake in wakeParticles)
		{
			wake.Update(aircraft.speed, aircraft.rb.velocity);
		}
	}

	private void FixedUpdate()
	{
		ApplyPartsForce();
	}

	private void OnInitialize()
	{
		if (handleGear)
		{
			aircraft.SetGear(false);
			aircraft.GearStateChanged(false);
		}
		foreach (var wake in wakeParticles)
		{
			wake.Initialize(aircraft);
		}
		aircraft.StartSlowUpdate(1f, UpdateParticles);

		if (!aircraft.LocalSim)
		{
			return;
		}
		foreach (AircraftShipPart part in parts)
		{
			JobManager.Add(part.SetupJob());
		}
		
	}

	private void Update()
	{
		if (!handleGear) return;
		if (gearTimer > 0)
		{
			gearTimer -= Time.deltaTime;
			return;
		}
		var pilot = aircraft?.pilots[0];
		if (pilot == null) return;
		if (pilot.currentState is PilotPlayerState pilotPlayerState)
		{
			if (pilotPlayerState.player.GetButtonDown("Gear"))
			{
				if (pilot.aircraft.gearState == LandingGear.GearState.LockedExtended)
				{
					pilot.aircraft.SetGear(deployed: false);
				}
				else if (pilot.aircraft.gearState == LandingGear.GearState.LockedRetracted)
				{
					pilot.aircraft.SetGear(deployed: true);
				}

				gearTimer = 1f;
			}
		}
	}
	
	public void SetComplexPhysics()
	{
		var colliders = aircraft.GetComponentsInChildren<Collider>();
		foreach (var collider1 in colliders)
		{
			foreach (var collider2 in colliders)
			{
				if (collider1 == collider2) continue;
				Physics.IgnoreCollision(collider1, collider2);
			}
		}
		
		foreach (var part in parts)
		{
			part.CreateRB(aircraft.rb.GetPointVelocity(part.transform.position), Vector3.zero);
		}

		foreach (var part in parts)
		{
			part.CreateJoints();
		}
	}

	public void SetSimplePhysics()
	{
		foreach (var part in parts)
		{
			part.MergeWithParent();
		}
	}
}