using HarmonyLib;
using NuclearOption.Networking;
using NuclearOption.SavedMission;

namespace NOComponentWIP.Patches;

[HarmonyPatch]
public static class FOBNamePatches
{
    [HarmonyPatch(typeof(Airbase), nameof(Airbase.SetupCustomAirbase))]
    [HarmonyPostfix]
    private static void SetupCustomAirbase_Postfix(SavedAirbase saved)
    {
        FOBNameResolver.ResolveFromUniqueName(saved);
    }
    
    [HarmonyPatch(typeof(UnitRegistry), nameof(UnitRegistry.AddPlayer))]
    [HarmonyPostfix]
    private static void AddPlayer_Postfix(Player player)
    {
        FOBNameResolver.ResolveForPlayer(player);
    }
}