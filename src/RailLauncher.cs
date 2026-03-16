using Mirage;
using UnityEngine;

namespace BPCustomComponents;

public class RailLauncher : NetworkBehaviour
{
	public Transform attachPoint;
	[SerializeField] private Rigidbody rb;

	public void SetTangible(bool tangible)
	{
		foreach (var collider in GetComponentsInChildren<Collider>())
		{
			collider.enabled = tangible;
		}
		rb.isKinematic = !tangible;
	}
}