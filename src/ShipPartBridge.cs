using System;
using System.Collections;
using System.Collections.Generic;
using NOComponentWIP.Systems;
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
	public bool Protected => Time.timeSinceLevelLoad < protectEnd;

	[SerializeField] private bool handleGear;
	[SerializeField] private float spawnProtectTime = 10f;

	[SerializeField] private Ship.WakeParticles[] wakeParticles;
	[SerializeField] private AudioSource[] waterSounds;
	[SerializeField] private AudioSource[] hullSounds;
	
	private float gearTimer = 0f;
	
	public DeploymentManager deploymentManager;
	public FOBManager fobManager;
	public ResupplyController resupplyController;
	private bool musicStarted = false;
	private float protectEnd;
	
	public void Awake()
	{
		aircraft.onDisableUnit += UnitDisabled;
		aircraft.onInitialize +=  OnInitialize;
		if (handleGear) aircraft.onSetGear += HandleGearEvent; 
	}

	private void Update()
	{
		if (GameManager.ShowEffects)
		{
			Animate();
		}
	}

	private void Animate()
	{
		foreach (var source in waterSounds)
		{
			float sqrMagnitude = aircraft.rb.GetPointVelocity(source.transform.position).sqrMagnitude;
			source.volume = Mathf.Clamp01(sqrMagnitude * 0.005f);
		}

		foreach (var source in hullSounds)
		{
			source.volume = 0.25f + Mathf.Clamp01(aircraft.speed * 0.05f);
		}
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
		protectEnd = Time.timeSinceLevelLoad + spawnProtectTime;
		
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
		
		this.StartSlowUpdateDelayed(1f, MusicCheck);
	}

	private void MusicCheck()
	{
		if (aircraft.speed > 6 && !musicStarted) //~20 km/h 
		{
			musicStarted = true;
			MusicManager.i.CrossFadeMusic(aircraft.GetAircraftParameters().takeoffMusic, 2f, 0f, repeat: false, allowReplay: true, replacePlaying: true); //replay because botes are cool (it uses the same clips and will break stuff otherwise)
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
			
			if (!JobManager.i.shipParts.Contains(part.JobPart))
			{
				JobManager.Add(part.SetupJob());
			}
		}
	}

	public void SetSimplePhysics()
	{
		foreach (var part in parts)
		{
			part.MergeWithParent();
			JobManager.Remove(ref part.JobPart);
			ShipPart.DisposeJobFields(ref part.JobFields);
		}
	}
}