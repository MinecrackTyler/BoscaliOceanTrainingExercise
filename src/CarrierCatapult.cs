using Mirage;
using UnityEngine;

namespace NOComponentWIP;

public class CarrierCatapult : NetworkBehaviour
{
	public bool IsOccupied => isOccupied;

	[SyncVar] private bool isOccupied;
	
	[SerializeField] private LaunchRail launchRail;

	private CatapultHook hook;

	public void Hook(Aircraft aircraft, CatapultHook hook)
	{
		if (isOccupied) return;

		if (launchRail.AttachAircraft(aircraft, hook.hookPoint))
		{
			isOccupied = true;
			this.hook = hook;
			launchRail.onRelease += OnRelease;
		}
	}

	public void Launch()
	{
		if (launchRail.IsReady)
		{
			launchRail.Launch();
		}
	}

	public void Release()
	{
		launchRail.Release();
		launchRail.onRelease -= OnRelease;
	}

	private void OnRelease()
	{
		isOccupied = false;
		hook?.Unhook();
	}
}