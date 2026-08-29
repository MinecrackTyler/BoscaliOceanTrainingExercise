using System.Collections.Generic;
using HarmonyLib;
using NuclearOption.SavedMission;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(FactionHQ))]
public static class FactionHQPatches
{
    [HarmonyPatch(nameof(FactionHQ.DeployAIAircraft))]
    [HarmonyPrefix]
    private static bool DeployAIAircraft_Prefix(FactionHQ __instance)
    {
        var hq = __instance;
        
        int count = hq.factionPlayers.Count;
        int num = 0;
        foreach (FactionHQ allHQ in FactionRegistry.GetAllHQs())
        {
            if (allHQ != hq)
            {
                num += allHQ.GetPlayers(false).Count;
            }
        }

        float num2 = (float)hq.AIAircraftLimit + (float)num * hq.addAIPerEnemyPlayer - (float)count * hq.reduceAIPerFriendlyPlayer;
        
        if ((float)hq.activeAIAircraft.Count >= num2)
        {
            return false;
        }

        List<AircraftDefinition> aircraft = Encyclopedia.i.aircraft;
        
        for (int i = 0; i < aircraft.Count; i++)
        {
            int index = UnityEngine.Random.Range(i, aircraft.Count);
            AircraftDefinition value = aircraft[i];
            aircraft[i] = aircraft[index];
            aircraft[index] = value;
        }
        
        int num3 = hq.reserveAirframes + count * hq.extraReservesPerPlayer;
        foreach (AircraftDefinition item2 in aircraft)
        {
            if (ModAssets.i.ShipDefinitions.Contains(item2)) continue;
            
            if (!hq.AircraftSupply.TryGetValue(item2, out var value2) || value2.Count <= num3)
            {
                continue;
            }

            foreach (var item3 in hq.airbasesSorted)
            {
                Airbase item = item3.airbase;
                if (item != null && item.CanSpawnAircraft(item2))
                {
                    Loadout loadout = null;
                    float fuelLevel = item2.aircraftParameters.DefaultFuelLevel;
                    
                    StandardLoadout randomStandardLoadout = item2.aircraftParameters.GetRandomStandardLoadout(item2, hq);
                    if (randomStandardLoadout != null)
                    {
                        loadout = randomStandardLoadout.loadout;
                        fuelLevel = randomStandardLoadout.FuelRatio;
                    }

                    int randomLiveryForFaction = item2.aircraftParameters.GetRandomLiveryForFaction(hq.faction);
                    
                    if (item.TrySpawnAircraft(null, item2, new LiveryKey(randomLiveryForFaction), loadout, fuelLevel).Allowed)
                    {
                        return false; 
                    }
                }
            }
        }
        
        return false;
    }  
}