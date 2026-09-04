using System;
using System.Linq;
using NuclearOption.Networking;
using NuclearOption.SavedMission;

namespace NOComponentWIP;

public static class FOBNameResolver
{
    private const string Prefix = "FOB_";
    
    public static void Resolve(SavedAirbase saved, Player player)
    {
        if (saved == null || player == null) return;
        if (!string.Equals(saved.DisplayName, "FOB", StringComparison.Ordinal)) return;
        
        void NameResolved(PlayerName playerName)
        {
            player.OnNameResolved.RemoveListener(NameResolved);
            saved.DisplayName = $"FOB: {playerName.GetDisplayName(PlayerNameContext.Other)}";
        }
        
        player.OnNameResolved.AddListener(NameResolved);
    }
    
    public static void ResolveFromUniqueName(SavedAirbase saved)
    {
        if (saved == null || !TryGetOwnerSteamId(saved.UniqueName, out var steamId)) return;
        var player = UnitRegistry.playerLookup.Values.FirstOrDefault(p => p != null && p.SteamID == steamId);
        Resolve(saved, player);
    }
    
    public static void ResolveForPlayer(Player player)
    {
        if (player == null || player.SteamID == 0) return;
        
        var mission = MissionManager.CurrentMission;
        if (mission?.airbases == null) return;
        
        foreach (var saved in mission.airbases)
        {
            if (saved == null) continue;
            
            if (TryGetOwnerSteamId(saved.UniqueName, out var ownerSteamId) && ownerSteamId == player.SteamID)
                Resolve(saved, player);
        }
    }
    
    private static bool TryGetOwnerSteamId(string uniqueName, out ulong steamId)
    {
        steamId = 0;
        
        if (string.IsNullOrEmpty(uniqueName) || !uniqueName.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        
        var start = Prefix.Length;
        var end = uniqueName.IndexOf('_', start);
        
        if (end <= start) return false;
        
        return ulong.TryParse(uniqueName.Substring(start, end - start), out steamId);
    }
}