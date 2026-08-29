using Cysharp.Threading.Tasks;
using HarmonyLib;
using Mirage;
using NuclearOption.Networking;
using NuclearOption.SceneLoading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NOComponentWIP.Systems;

public class AircraftSwitcher : NetworkSceneSingleton<AircraftSwitcher>
{
	public void SwitchAircraft(Player player, Aircraft oldAircraft, Aircraft newAircraft)
	{
		if (GameManager.gameState != GameState.SinglePlayer) return;
		CmdSwitchAircraft(player, oldAircraft, newAircraft);
	}
	
	[ServerRpc(requireAuthority = false)]
	private void CmdSwitchAircraft(Player player, Aircraft oldAircraft, Aircraft newAircraft)
	{
		if (GameManager.gameState != GameState.SinglePlayer) return;
		if (oldAircraft == null || newAircraft == null) return;
		if (player.Identity.Owner != oldAircraft.Identity.Owner) return;
		if (newAircraft.Player != null) return;
		if (newAircraft.NetworkHQ != player.HQ) return;

		oldAircraft.playerRef = new PlayerRef();
		player.RemoveAircraft(oldAircraft);
		player.RemoveAircraftAuthority(oldAircraft);
		if (oldAircraft.autopilot != null)
		{
			oldAircraft.pilots[0].SetStartingAiState();
		}
		else
		{
			oldAircraft.pilots[0].SwitchState(oldAircraft.pilots[0].parkedState);
		}
		oldAircraft.pilots[0].player = null;
		oldAircraft.SetLocalSim(oldAircraft.CheckIfLocalSim());
		if (oldAircraft.LocalSim)
		{
			oldAircraft.partChecker = new Aircraft.PartChecker(oldAircraft);
		}
		oldAircraft.weaponManager.ClearTargetList();

		if (UnitRegistry.TryGetPersistentUnit(oldAircraft.persistentID, out var persistentUnit))
		{
			persistentUnit.player = null;
		}

		newAircraft.playerRef = new PlayerRef(player);
		newAircraft.Identity.AssignClientAuthority(player.Owner);
		newAircraft.SetLocalSim(newAircraft.CheckIfLocalSim());
		player.SetAircraft(newAircraft);
		newAircraft.pilots[0].SwitchState(null);
		/*newAircraft.NetworkHQ = player.HQ; //funny mode*/
		oldAircraft.pilots[0].aircraft = oldAircraft;
		
		RpcSwitchAircraft(player, oldAircraft, newAircraft);
	}

	[ClientRpc]
	private void RpcSwitchAircraft(Player player, Aircraft oldAircraft, Aircraft newAircraft)
	{
		newAircraft?.SetLocalSim(newAircraft.CheckIfLocalSim());
		if (newAircraft.LocalSim)
		{
			newAircraft.partChecker = new Aircraft.PartChecker(newAircraft);
		}
		
		if (GameManager.IsLocalPlayer(player))
		{
			if (oldAircraft != null)
			{
				if (oldAircraft.statusDisplay != null)
				{
					oldAircraft.onDisableUnit -= oldAircraft.statusDisplay.StatusDisplay_OnDisable;
					Destroy(oldAircraft.statusDisplay.gameObject);
				}
				oldAircraft.weaponManager.currentWeaponStation.SetStationActive(oldAircraft, false);
				
				foreach (Transform cam in oldAircraft.targetCam?.currentMount.transform)
				{
					Destroy(cam.gameObject);
				}
				Destroy(oldAircraft.targetCam?.targetScreenUI);
				Destroy(oldAircraft.targetCam?.landingScreenUI);
				oldAircraft.onDisableUnit -= CombatHUD.i.threatList.ThreatList_OnAircraftDisable;
				CombatHUD.i.threatList.ThreatList_OnAircraftDisable(oldAircraft);

				if (HUDAppManager.i != null && MFDAppManager.i != null)
				{
					oldAircraft.onDisableUnit -= HUDAppManager.i.HUDAppManager_OnUnitDisable;
					oldAircraft.onDisableUnit -= MFDAppManager.i.HUDAppManager_OnUnitDisable;
					PlayerSettings.OnApplyOptions -= HUDAppManager.i.RefreshSettings;
					PlayerSettings.OnApplyOptions -= MFDAppManager.i.RefreshSettings;
					Destroy(HUDAppManager.i.gameObject);
					Destroy(MFDAppManager.i.gameObject);
				}
				
				var oldCockpit = oldAircraft.cockpit?.GetComponentInChildren<Cockpit>();
				if (oldCockpit != null)
				{
					oldCockpit.enabled = false;
					oldAircraft.onDisableUnit -=
						oldCockpit.Cockpit_OnAircraftDisable;
					Destroy(oldCockpit.tacScreen);
				}
			}

			CombatHUD.i.RemoveAircraft();
			CombatHUD.i.SetAircraft(newAircraft);
			DynamicMap.i.DeselectAllIcons();
			
			if (newAircraft != null)
			{
				newAircraft.weaponManager.currentWeaponStation.SetStationActive(newAircraft, true);
				CombatHUD.i.ShowWeaponStation(newAircraft.weaponManager.currentWeaponStation);
				
				foreach (var missile in newAircraft.GetMissileWarningSystem().knownMissiles)
				{
					CombatHUD.i.threatList.ThreatList_OnMissileWarning(new MissileWarning.OnMissileWarning
					{
						missile = missile
					});
				}
				
				newAircraft.pilots[0].SwitchState(newAircraft.pilots[0].playerState);
				newAircraft.SetupLocalPlayerAndUI();
				
				var newCockpit = newAircraft.cockpit?.GetComponentInChildren<Cockpit>();
				
				if (newCockpit != null)
				{
					newCockpit.Cockpit_OnAircraftInitialize();
				}
			
				newAircraft.targetCam?.Initialize();
				newAircraft.weaponManager.ClearTargetList();
			}
		}
		else
		{
			oldAircraft?.SetLocalSim(oldAircraft.CheckIfLocalSim());
		}
	}
}

[HarmonyPatch(typeof(MapLoader))]
public class AircraftSwitcherSpawnPatch
{
	[HarmonyPatch(nameof(MapLoader.LoadScene))]
	[HarmonyPostfix]
	private static async UniTask<MapLoader.LoadResult> Postfix(UniTask<MapLoader.LoadResult> __result, MapLoader.SceneKey key)
	{
		
		MapLoader.LoadResult status = await __result;

		if (status == MapLoader.LoadResult.ChangedScene && key.Path.Contains("GameWorld") && NetworkManagerNuclearOption.i.Server.Active)
		{
			SetupScene();
		}
		return status;
	}

	private static void SetupScene()
	{
		if (!NetworkManagerNuclearOption.i.Server.Active) return;
		if (GameManager.gameState != GameState.SinglePlayer) return;
		
		var target = GameObject.Find("SceneEssentials");

		if (ModAssets.i.networkModSingletons != null)
		{
			var networkSingletons = Object.Instantiate(ModAssets.i.networkModSingletons, target.transform, true);
			NetworkManagerNuclearOption.i.ServerObjectManager.Spawn(networkSingletons.GetNetworkIdentity());
		}

		if (ModAssets.i.modSingletons != null)
		{
			var singletons = Object.Instantiate(ModAssets.i.modSingletons, target.transform, true);
		}
	}
}

[HarmonyPatch(typeof(NetworkManagerNuclearOption))]
public class RegisterPatch
{
	[HarmonyPatch(nameof(NetworkManagerNuclearOption.RegisterPrefabs))]
	[HarmonyPostfix]
	private static void RegisterPrefabs_Postfix(NetworkManagerNuclearOption __instance)
	{
		__instance.ClientObjectManager.RegisterPrefab(ModAssets.i.networkModSingletons.GetNetworkIdentity());
	}
}