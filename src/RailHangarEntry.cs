using System;
using UnityEngine;

namespace NOComponentWIP;

[CreateAssetMenu(fileName = "RailHangarAircraftEntry", menuName = "Bote/Aircraft Rail Entry")]
public class RailHangarEntry : ScriptableObject
{
	public GameObject boosterPrefab;
	public Vector3 railAttachPoint;
}