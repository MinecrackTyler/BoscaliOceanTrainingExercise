using UnityEngine;

namespace BPCustomComponents;

public class CatapultHook : MonoBehaviour
{
	[SerializeField] private Aircraft aircraft;
	[SerializeField] private float scanRadius;
	
	public Transform hookPoint;

	private bool isHooked;
	private CarrierCatapult currentCat;

	private void Update()
	{
		if (aircraft == null|| !aircraft.LocalSim) return;

		if (Input.GetKeyDown(KeyCode.U))
		{
			if (!isHooked)
			{
				TrySearchAndHook();
			}
		}

		if (aircraft.GetInputs().throttle > 0.95f && currentCat != null)
		{
			currentCat.Launch();
		}
	}

	private void TrySearchAndHook()
	{
		Collider[] hits = Physics.OverlapSphere(hookPoint.position, scanRadius);

		foreach (var hit in hits)
		{
			var catapult = hit.gameObject.GetComponentInChildren<CarrierCatapult>();

			if (catapult != null && !catapult.IsOccupied)
			{
				isHooked = true;
				currentCat = catapult;
				catapult.Hook(aircraft, this);
			}
		}
	}

	public void Unhook()
	{
		if (currentCat != null)
		{
			currentCat.Release();
		}
		isHooked = false;
		currentCat = null;
	}
}