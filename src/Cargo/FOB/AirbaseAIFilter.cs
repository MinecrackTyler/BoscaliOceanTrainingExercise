using System.Collections.Generic;
using UnityEngine;

namespace NOComponentWIP;

public class AirbaseAIFilter : MonoBehaviour
{
	private List<string> allowedAircraftKeys = new List<string>();

	public bool CanSpawnAircraft(string jsonKey)
	{
		return allowedAircraftKeys.Contains(jsonKey);
	}

	public void AddAllowedKey(string jsonKey)
	{
		allowedAircraftKeys.Add(jsonKey);
	}
}