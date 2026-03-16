using System;

namespace NOComponentWIP;

public class VLSBoosterActive : VLSBooster
{
	private void Awake()
	{
		missile.onInitialize += VLSBoosterActive_OnInitialize;
		burnRate = fuelMass / burnTime;
	}

	private void VLSBoosterActive_OnInitialize()
	{
		missile.onInitialize -= VLSBoosterActive_OnInitialize;
		if (missile.owner == null || GameManager.gameState == GameState.Encyclopedia)
		{
			Destroy(gameObject);
		}
		else
		{
			Activate();
		}
	}
}