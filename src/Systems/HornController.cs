using Mirage;
using UnityEngine;

namespace BoscaliOceanTrainingExercise.Systems;

public class HornController : NetworkBehaviour
{
	[SerializeField] private Aircraft aircraft;
	[SerializeField] private AudioSource source;
	[SerializeField] private float cooldown;
	[SerializeField] private float volume;

	private float lastHonkTime;
	
	private const string INPUT_HONK = $"{Mod_Input.ModShortName}:Horn";

	private void Awake()
	{
		source.playOnAwake = false;
	}

	private void Update()
	{
		if (!aircraft.LocalSim || source == null) return;
		var player = aircraft.pilots[0]?.playerState?.player;
		if (player == null) return;

		if (player.GetButtonDown(INPUT_HONK))
		{
			CmdHonk();
		}
	}

	[ServerRpc]
	private void CmdHonk()
	{
		Honk();
	}

	private void Honk()
	{
		if (Time.timeSinceLevelLoad > lastHonkTime + cooldown)
		{
			lastHonkTime = Time.timeSinceLevelLoad;
			RpcHonk();	
		}
	}

	[ClientRpc]
	private void RpcHonk()
	{
		if (!source.isPlaying)
		{
			source.Play();
		}
	}
}