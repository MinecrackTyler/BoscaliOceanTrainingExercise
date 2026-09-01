using System.Collections.Generic;
using HarmonyLib;
using Mirage;
using NOComponentWIP;
using NOComponentWIP.ServerConfig;
using NuclearOption.Networking;
using UnityEngine;

[NetworkMessage]
public struct NetworkPlayerCount
{
    public string JsonKey;
    public int PlayerCount;
    public int FactionCount;
}

public class DeployedUnit
{
    public string JsonKey;
    public uint NetID;
    public ulong PlayerID;
    public string FactionID;
}

[HarmonyPatch]
public static class UnitCountTracker
{
    private static NetworkServer server;
    private static NetworkClient client;
	
    private static readonly Dictionary<uint, DeployedUnit> DeployedUnits = new();
	
    private static readonly Dictionary<(ulong SteamID, string JsonKey), int> PlayerCounts = new();
    private static readonly Dictionary<(string Faction, string JsonKey), int> FactionCounts = new();
    
    private static readonly Dictionary<(ulong SteamID, string JsonKey), int> PendingPlayerCounts = new();
    private static readonly Dictionary<(string Faction, string JsonKey), int> PendingFactionCounts = new();
    
    private static readonly Dictionary<(Unit Carrier, AircraftDefinition Definition), Queue<PendingAircraftDeployment>>
        PendingAircraftDeployments = new();

    public static void RegisterListeners(NetworkServer Server, NetworkClient Client)
    {
        server ??= Server;
        client ??= Client;
        server?.Stopped.AddListener(Reset);
        Reset();
    }
    
    public static void RegisterHandlers(NetworkServer Server, NetworkClient Client)
    {
        server ??= Server;
        client ??= Client;
        Client?.MessageHandler.RegisterHandler<NetworkPlayerCount>(OnReceiveCountUpdate);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ServerApplyFaction))]
    [HarmonyPostfix]
    private static void ServerApplyFaction_Postfix(Player __instance)
    {
        SyncPlayerState(__instance.Owner);
    }

    public static void Reset()
    {
        DeployedUnits.Clear();
        PlayerCounts.Clear();
        FactionCounts.Clear();
        
        PendingPlayerCounts.Clear();
        PendingFactionCounts.Clear();
        PendingAircraftDeployments.Clear();
    }

    public static void RegisterUnit(Unit unit, ulong ownerID)
    {
        if (unit == null || ownerID == 0 || unit.persistentID.NotValid) return;

        uint netId = unit.persistentID.Id;
        string jsonKey = unit.definition.jsonKey;
        string factionName = unit.NetworkHQ?.faction?.factionName ?? "Unassigned";
        
        if (DeployedUnits.ContainsKey(netId)) return;
        
        Plugin.DebugLog($"Tracking Unit: {jsonKey} : {netId}");

        var record = new DeployedUnit
        {
            JsonKey = jsonKey,
            NetID = netId,
            PlayerID = ownerID,
            FactionID = factionName
        };

        DeployedUnits[netId] = record;
        
        IncrementCount(ownerID, factionName, jsonKey);
        
        unit.onDisableUnit += UnregisterSpawn;
        
        SendUnitCount(jsonKey);
    }

    private static void UnregisterSpawn(Unit unit)
    {
        if (unit == null || unit.persistentID.NotValid) return;

        uint netId = unit.persistentID.Id;
        unit.onDisableUnit -= UnregisterSpawn;
        

        if (DeployedUnits.Remove(netId, out var record))
        {
            Plugin.DebugLog($"No longer tracking Unit: {record.JsonKey} : {netId}");
            DecrementCount(record.PlayerID, record.FactionID, record.JsonKey);
            
            SendUnitCount(record.JsonKey);
        }
    }
	
    public static void SendUnitCount(string jsonKey)
    {
        if (server == null || string.IsNullOrEmpty(jsonKey)) return;

        foreach (var networkPlayer in server.AuthenticatedPlayers)
        {
            if (!networkPlayer.TryGetPlayer(out Player player)) continue;

            string playerFaction = player.HQ?.faction?.factionName ?? string.Empty;

            var data = new NetworkPlayerCount
            {
                JsonKey = jsonKey,
                PlayerCount = GetPlayerCount(jsonKey, player.SteamID),
                FactionCount = GetFactionCount(jsonKey, playerFaction)
            };

            networkPlayer.Send(data);
        }
    }

    public static void Resync()
    {
        foreach (var player in server.AuthenticatedPlayers)
        {
            SyncPlayerState(player);
        }
    }
	
    public static void SyncPlayerState(INetworkPlayer networkPlayer)
    {
	    Player player = null;
	    networkPlayer?.TryGetPlayer(out player);
	    
        if (networkPlayer == null || player == null) return;

        string playerFaction = player.HQ?.faction?.factionName ?? string.Empty;
        
        HashSet<string> activeKeys = new();
        foreach (var unit in DeployedUnits.Values)
        {
            activeKeys.Add(unit.JsonKey);
        }

        foreach (var key in activeKeys)
        {
            var data = new NetworkPlayerCount
            {
                JsonKey = key,
                PlayerCount = GetPlayerCount(key, player.SteamID),
                FactionCount = GetFactionCount(key, playerFaction)
            };

            networkPlayer.Send(data);
        }
    }

    private static void OnReceiveCountUpdate(NetworkPlayerCount msg)
    {
        UnitConfig.UpdateCounts(msg.JsonKey, msg.PlayerCount, msg.FactionCount);
    }

    private static void IncrementCount(ulong steamId, string faction, string jsonKey)
    {
        var pKey = (steamId, jsonKey);
        PlayerCounts[pKey] = PlayerCounts.GetValueOrDefault(pKey, 0) + 1;

        var fKey = (faction, jsonKey);
        FactionCounts[fKey] = FactionCounts.GetValueOrDefault(fKey, 0) + 1;
    }

    private static void DecrementCount(ulong steamId, string faction, string jsonKey)
    {
        var pKey = (steamId, jsonKey);
        if (PlayerCounts.ContainsKey(pKey))
        {
            PlayerCounts[pKey] = Mathf.Max(0, PlayerCounts[pKey] - 1);
        }

        var fKey = (faction, jsonKey);
        if (FactionCounts.ContainsKey(fKey))
        {
            FactionCounts[fKey] = Mathf.Max(0, FactionCounts[fKey] - 1);
        }
    }
    
    private static void IncrementPendingCount(ulong steamId, string faction, string jsonKey)
    {
        var pKey = (steamId, jsonKey);
        PendingPlayerCounts[pKey] = PendingPlayerCounts.GetValueOrDefault(pKey, 0) + 1;
        
        var fKey = (faction, jsonKey);
        PendingFactionCounts[fKey] = PendingFactionCounts.GetValueOrDefault(fKey, 0) + 1;
    }
    
    private static void DecrementPendingCount(ulong steamId, string faction, string jsonKey)
    {
        var pKey = (steamId, jsonKey);
        
        if (PendingPlayerCounts.TryGetValue(pKey, out int playerCount))
        {
            playerCount--;
            
            if (playerCount <= 0)
                PendingPlayerCounts.Remove(pKey);
            else
                PendingPlayerCounts[pKey] = playerCount;
        }
        
        var fKey = (faction, jsonKey);
        
        if (PendingFactionCounts.TryGetValue(fKey, out int factionCount))
        {
            factionCount--;
            
            if (factionCount <= 0)
                PendingFactionCounts.Remove(fKey);
            else
                PendingFactionCounts[fKey] = factionCount;
        }
    }
    
    public static bool CanDeploy(string jsonKey, Aircraft aircraft)
    {
        if (!UnitConfig.UnitLimits()) return true;
        
        var playerMax = UnitConfig.PlayerMax(jsonKey);
        var factionMax = UnitConfig.FactionMax(jsonKey);
        
        if (playerMax < 0 && factionMax < 0) return true;
        
        var steamId = aircraft?.Player?.SteamID ?? 0UL;
        var factionName = aircraft?.NetworkHQ?.faction?.factionName;
        
        if (playerMax >= 0)
        {
            if (steamId == 0UL) return false;
            var active = GetPlayerCount(jsonKey, steamId);
            var pending = GetPendingPlayerCount(jsonKey, steamId);
            if (active + pending >= playerMax) return false;
        }
        
        if (factionMax >= 0)
        {
            if (string.IsNullOrEmpty(factionName)) return false;
            var active = GetFactionCount(jsonKey, factionName);
            var pending = GetPendingFactionCount(jsonKey, factionName);
            if (active + pending >= factionMax) return false;
        }
        
        return true;
    }
    
    public static void RegisterPendingAircraft(Unit carrier, AircraftDefinition definition, ulong ownerID)
    {
        if (carrier == null || definition == null || ownerID == 0) return;
        
        var faction = carrier.NetworkHQ?.faction?.factionName ?? "Unassigned";
        var jsonKey = definition.jsonKey;
        var key = (carrier, definition);
        
        if (!PendingAircraftDeployments.TryGetValue(key, out var queue))
        {
            queue = new Queue<PendingAircraftDeployment>();
            PendingAircraftDeployments.Add(key, queue);
        }
        queue.Enqueue(new PendingAircraftDeployment(ownerID, faction));
        IncrementPendingCount(ownerID, faction, jsonKey);
    }
    
    public static void CancelPendingAircraft(Unit carrier, AircraftDefinition definition, ulong ownerID)
    {
        if (carrier == null || definition == null || ownerID == 0) return;
        
        var key = (carrier, definition);
        
        if (!PendingAircraftDeployments.TryGetValue(key, out var queue) || queue.Count == 0) return;
        
        var entries = queue.ToArray();
        var removeIndex = -1;
        
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            if (entries[i].OwnerID == ownerID)
            {
                removeIndex = i;
                break;
            }
        }
        
        if (removeIndex < 0) return;
        PendingAircraftDeployment removed = entries[removeIndex];
        DecrementPendingCount(removed.OwnerID, removed.Faction, definition.jsonKey);
        queue.Clear();
        
        for (int i = 0; i < entries.Length; i++)
        {
            if (i != removeIndex)
            {
                queue.Enqueue(entries[i]);
            }
        }
        
        if (queue.Count == 0)
        {
            PendingAircraftDeployments.Remove(key);
        }
    }
    
    public static bool TryConsumePendingAircraft(Unit carrier, AircraftDefinition definition, out ulong ownerID)
    {
        ownerID = 0;
        
        if (carrier == null || definition == null) return false;
        
        var key = (carrier, definition);
        
        if (!PendingAircraftDeployments.TryGetValue(key, out var queue) || queue.Count == 0) return false;
        
        PendingAircraftDeployment pending = queue.Dequeue();
        ownerID = pending.OwnerID;
        DecrementPendingCount(pending.OwnerID, pending.Faction, definition.jsonKey);
        
        if (queue.Count == 0)
        {
            PendingAircraftDeployments.Remove(key);
        }
        
        return true;
    }
    
    private readonly struct PendingAircraftDeployment
    {
        public readonly ulong OwnerID;
        public readonly string Faction;
        
        public PendingAircraftDeployment(ulong ownerID, string faction)
        {
            OwnerID = ownerID;
            Faction = faction;
        }
    }

    public static int GetPlayerCount(string jsonKey, ulong steamID)
    {
        return PlayerCounts.GetValueOrDefault((steamID, jsonKey), 0);
    }

    public static int GetFactionCount(string jsonKey, string factionName)
    {
        if (string.IsNullOrEmpty(factionName)) return 0;
        return FactionCounts.GetValueOrDefault((factionName, jsonKey), 0);
    }
    
    private static int GetPendingPlayerCount(string jsonKey, ulong steamID)
    {
        return PendingPlayerCounts.GetValueOrDefault((steamID, jsonKey), 0);
    }
    
    private static int GetPendingFactionCount(string jsonKey, string factionName)
    {
        if (string.IsNullOrEmpty(factionName))
            return 0;
        
        return PendingFactionCounts.GetValueOrDefault((factionName, jsonKey), 0);
    }
}