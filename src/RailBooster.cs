namespace BPCustomComponents;
using UnityEngine;

public class RailBooster : MonoBehaviour
{
	[SerializeField] private float thrust;
	[SerializeField] private float burnTime;
	[SerializeField] private ParticleSystem[] engineParticles;
	[SerializeField] private AudioSource fireSound;
	[SerializeField] private TrailEmitter[] engineTrails;
	[SerializeField] private float ejectForce;
	
	private float ignitionTime;
	private Aircraft aircraft;
	private bool fired;
	private bool burnout;

	public void Initialize(Aircraft ac)
	{
		aircraft = ac;
		transform.SetParent(ac.transform);
	}
	
	public void Ignite()
	{
		if (fired)
		{
			return;
		}
		fired = true;
		if (aircraft == null)
		{
			burnout = true;
			return;
		}
		ignitionTime = Time.timeSinceLevelLoad;
		foreach (var engineParticle in engineParticles)
		{
			engineParticle.Play();
		}
		fireSound.Play();
	}
	
	private void FixedUpdate()
	{
		if (!fired || burnout)
		{
			return;
		}

		if (aircraft.LocalSim)
		{
			aircraft.rb.AddForceAtPosition(transform.forward * thrust, transform.position);
		}
		if (Time.timeSinceLevelLoad > ignitionTime + burnTime)
		{
			burnout = true;
			Burnout();
		}
	}

	private void Burnout()
	{
		if (!burnout) return;
		foreach (var engineParticle in engineParticles)
			engineParticle.Stop();
		foreach (var trail in engineTrails)
		{
			trail.StopTrail();
		}
		fireSound.Stop();
		Detach();
	}

	private void Detach()
	{
		transform.SetParent(null);

		Rigidbody rb = gameObject.AddComponent<Rigidbody>();
		rb.mass = 500f;
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		
		rb.velocity = aircraft.rb.velocity;

		Vector3 ejectDir = (-transform.up) + (-transform.forward * 0.5f);
		
		rb.AddRelativeForce(ejectDir * ejectForce, ForceMode.Force);
		
		Destroy(gameObject, 10f);
	}
}