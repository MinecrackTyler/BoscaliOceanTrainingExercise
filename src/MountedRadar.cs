using UnityEngine;

namespace NOComponentWIP;

public class MountedRadar : MonoBehaviour
{
	[SerializeField] private Radar radar;
	private bool attached;
	
	private void Update()
	{
		if (attached)
		{
			enabled = false;
			return;
		}

		if (radar.attachedUnit is not Aircraft aircraft) return;
		if (aircraft.radar != null) return;
		aircraft.radar = radar;
		attached = true;
	}
}