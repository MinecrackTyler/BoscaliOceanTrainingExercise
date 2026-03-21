using UnityEngine;

namespace NOComponentWIP;

public class SmokeGrenade : MonoBehaviour
{
	[SerializeField] private float smokeDuration = 15f;
	[SerializeField] private ParticleSystem smokeParticles;
	[SerializeField] private MeshRenderer grenadeRenderer;

	[SerializeField] private Rigidbody rb;

	private bool exploded;
	private float timer;
	private float detonationTime;

	private void OnEnable()
	{
		if (rb == null) rb = GetComponent<Rigidbody>();
		
		var main = smokeParticles.main;
		main.simulationSpace = ParticleSystemSimulationSpace.Custom;
		main.customSimulationSpace = Datum.origin;
		main.stopAction = ParticleSystemStopAction.Destroy;
	}

	public void LaunchGrenade(Transform launchPoint, Vector3 launchVelocity, float duration)
	{
		exploded = false;
		timer = 0f;
		detonationTime = duration;
		
		transform.position = launchPoint.position;
		transform.rotation = launchPoint.rotation;
		
		rb.isKinematic = false;
		rb.velocity = launchVelocity;

		if (grenadeRenderer != null) grenadeRenderer.enabled = true;
	}

	private void FixedUpdate()
	{
		if (exploded) return;
		timer += Time.fixedDeltaTime;

		if (timer >= detonationTime)
		{
			Explode();
		}
	}

	private void Explode()
	{
		exploded = true;

		rb.velocity = Vector3.zero;
		rb.isKinematic = true;
		
		if (grenadeRenderer != null) grenadeRenderer.enabled = false;

		if (smokeParticles != null)
		{
			var main = smokeParticles.main;
			main.duration = smokeDuration;
			
			smokeParticles.Play();
		}
		
		Destroy(gameObject, smokeDuration + 2f);
	}
}