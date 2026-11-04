using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace BoscaliOceanTrainingExercise;

[HarmonyPatch]
public class SmokeEjector : Countermeasure
{
	[SerializeField] private GameObject smokePrefab;
	[SerializeField] private float ejectionVelocity;
	[SerializeField] private float ejectionVelocityVariance;
	[SerializeField] private FlareEjector.EjectionPoint[] ejectionPoints;
	[SerializeField] private float ejectionInterval;
	[SerializeField] private AudioClip launchSound;
	[SerializeField] private float launchVolume;
	[SerializeField] private float grenadeDuration;
	
	private int maxAmmo;
	private float lastEjectionTime;

	public override void Awake()
	{
		maxAmmo = ammo;
		base.enabled = false;
		base.Awake();
	}

	public override List<String> GetThreatTypes()
	{
		if (threatTypes == null)
		{
			threatTypes = new List<string> { "Optical", "Laser" };
		}
		return threatTypes;
	}

	public override void Fire()
	{
		if (maxAmmo == -1)
		{
			maxAmmo = ammo;
		}
		if (aircraft.disabled || Time.timeSinceLevelLoad - lastEjectionTime < ejectionInterval)
		{
			return;
		}
		
		lastEjectionTime = Time.timeSinceLevelLoad;
		aircraft.RequestRearm();
		foreach (var ejectionPoint in ejectionPoints)
		{
			if (ammo > 0 && ejectionPoint.transform != null)
			{
				EjectSmoke(aircraft, ejectionPoint).Forget();
			}
		}
		ammo--;

		if (GameManager.IsLocalAircraft(aircraft))
		{
			UpdateHUD();
		}
	}

	public override void Rearm(Aircraft aircraft, Unit rearmer)
	{
		if (ammo != maxAmmo)
		{
			ammo = maxAmmo;
			if (GameManager.IsLocalAircraft(aircraft))
			{
				UpdateHUD();
				SceneSingleton<AircraftActionsReport>.i.ReportText("Smoke rearmed by " + rearmer.unitName, 5f);
			}
		}
	}

	private async UniTask EjectSmoke(Aircraft aircraft, FlareEjector.EjectionPoint ejectionPoint)
	{
		await UniTask.WaitForFixedUpdate();
		base.enabled = true;
		GameObject smokeObj = NetworkSceneSingleton<Spawner>.i.SpawnLocal(smokePrefab, Datum.origin);
		smokeObj.transform.position = ejectionPoint.transform.position;
		var duration = grenadeDuration + UnityEngine.Random.Range(-0.5f, 0.5f);
		var vel = ejectionVelocity + UnityEngine.Random.Range(-ejectionVelocityVariance, ejectionVelocityVariance);
		smokeObj.GetComponent<SmokeGrenade>().LaunchGrenade(ejectionPoint.transform, aircraft.rb.velocity + ejectionPoint.transform.forward * vel, duration);
		AudioSource audioSource = ejectionPoint.sound;
		if (audioSource == null)
		{
			audioSource = (ejectionPoint.sound = ejectionPoint.transform.gameObject.AddComponent<AudioSource>());
			audioSource.outputAudioMixerGroup = SoundManager.i.EffectsMixer;
			audioSource.clip = launchSound;
			audioSource.volume = launchVolume;
			audioSource.spatialBlend = 1f;
			audioSource.dopplerLevel = 0f;
			audioSource.spread = 5f;
			audioSource.maxDistance = 40f;
			audioSource.minDistance = 5f;
		}
		audioSource.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
		audioSource.PlayOneShot(launchSound);
	}

	public override void UpdateHUD()
	{
		SceneSingleton<CombatHUD>.i.DisplayCountermeasures(displayName, displayImage, ammo);
	}

	public int GetAmmo()
	{
		return ammo;
	}

	public int GetMaxAmmo()
	{
		return maxAmmo;
	}
	
	[HarmonyPatch(typeof(Unit), nameof(Unit.LineOfSight))]
	[HarmonyPrefix]
	public static bool Prefix(Unit __instance, Vector3 origin, float magnification, ref bool __result, ref RaycastHit ___hit)
	{
		if (SmokeGrenade.ActiveSmokeCount <= 0)
			return true;
		
		var targetPosition = __instance.transform.position;
		
		if (FastMath.OutOfRange(origin, targetPosition, __instance.visibility * magnification))
		{
			__result = false;
			return false;
		}
		
		if (Physics.Linecast(origin, targetPosition, out ___hit, Mask))
		{
			__result = FastMath.InRange(___hit.point, targetPosition, __instance.maxRadius * 2f);
			return false;
		}

		__result = true;
		return false;
	}

	private static readonly int Mask = PhysicsLayers.StaticsMask | PhysicsLayers.ExclusionZonesMask;
}