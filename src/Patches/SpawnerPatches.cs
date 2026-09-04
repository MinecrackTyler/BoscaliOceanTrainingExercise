using HarmonyLib;
using Mirage;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(Spawner))]
public static class SpawnerPatches
{
    [HarmonyPatch(nameof(Spawner.SpawnAircraft))]
    [HarmonyPrefix]
    public static bool SpawnAircraft_Prefix(Spawner __instance, ref Aircraft __result, Player player, GameObject prefab, Loadout loadout, float fuelLevel, LiveryKey livery, GlobalPosition globalPosition, Quaternion rotation, Vector3 startingVel, Hangar spawningHangar, FactionHQ HQ, string uniqueName, float skill, float bravery)
    {
        PlayerRef networkPlayerRef = player?.PlayerRef ?? PlayerRef.Invalid;
        Vector3 position = globalPosition.ToLocalPosition();
        GameObject gameObject = Object.Instantiate(prefab, position, rotation);

        Aircraft component = gameObject.GetComponent<Aircraft>();
        component.NetworkHQ = HQ;
        component.NetworkUniqueName = uniqueName;
        component.NetworkspawningHangar = spawningHangar;
        component.NetworkstartPosition = globalPosition;
        component.NetworkstartRotation = rotation;
        component.NetworkstartingVelocity = startingVel;
        component.Networkloadout = loadout;
        component.NetworkfuelLevel = Mathf.Clamp(fuelLevel, 0f, 1f);
        component.skill = skill;
        component.bravery = bravery;
        component.SetLiveryKey(livery);
        component.NetworkplayerRef = networkPlayerRef;
        component.NetworkunitName = component.definition.unitName;

        if (component.TryGetComponent<Airbase>(out var airbase))
        {
            airbase.SetupAttachedAirbase(component);
            if (player != null)
            {
                airbase.SavedAirbase.UniqueName += $"{player?.SteamID}_{Time.time}";
            }
        }

        if (player != null) __instance.ServerObjectManager.Spawn(gameObject, player.Owner);
        else __instance.ServerObjectManager.Spawn(gameObject);

        __result = component;
        return false;
    }
}