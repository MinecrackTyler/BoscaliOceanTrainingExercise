using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP.Patches;

[HarmonyPatch(typeof(ShipPart))]
public class ShipPartPatches
{
    [HarmonyPatch("Leak")]
    [HarmonyPrefix]
    static bool Leak(ShipPart __instance)
    {
        if (__instance.GetType() != typeof(AircraftShipPart))
        {
            return true;
        }
        var bridge = __instance.parentUnit?.GetComponent<ShipPartBridge>();
        if (bridge == null) return true;

        // Check if below sea level (using Datum if available, else 0)
        if (__instance.transform.position.y - __instance.height < Datum.SeaLevel.y) 
        {
            __instance.displacement -= __instance.leakRate * Time.deltaTime;
        }

        // Check if we need to trigger emergency compartmentalization
        if (__instance.compartmentalized || !(__instance.displacement < __instance.originalDisplacement * bridge.damageControlDeploymentThreshold))
        {
            return false;
        }

        bridge.damageControlAvailable -= __instance.originalDisplacement;
        __instance.compartmentalized = true;

        // Cascade flood if resources are dry
        if (bridge.damageControlAvailable <= 0f)
        {
            foreach (var part in __instance.connectedCompartments)
            {
                part.Flood();
            }
        }
        return false;
    }

    [HarmonyPatch("DamageControl")]
    [HarmonyPrefix]
    static bool DamageControl(ShipPart __instance)
    {
        if (__instance.GetType() != typeof(AircraftShipPart))
        {
            return true;
        }
        var bridge = __instance.parentUnit?.GetComponent<ShipPartBridge>();
        if (bridge == null) return true;
        
        if (__instance.detachedFromUnit || bridge.disabled || __instance.submerged || bridge.damageControlAvailable <= 0f || __instance.compartmentalized)
        {
            return false;
        }

        if (!__instance.DamageControlActive)
        {
            __instance.DamageControlActive = true;
            __instance.DamageControlStart = Time.timeSinceLevelLoad;
        }
        else if (Time.timeSinceLevelLoad >= __instance.DamageControlStart + __instance.DamageControlDelay)
        {
            float skillFactor = Mathf.Clamp(bridge.aircraft.skill, 0.1f, 1f);
            
            if (__instance.leakRate < 0.1f)
            {
                __instance.leakRate = 0f;
            }
            else
            {
                float leakRepair = 0.02f * __instance.leakRateMin * Time.deltaTime;
                __instance.leakRate -= leakRepair * skillFactor;
                __instance.leakRate = Mathf.Max(__instance.leakRate, 0f);
            }
            
            float pumpingRate = 0f;
            if (__instance.displacement < __instance.originalDisplacement)
            {
                pumpingRate = 0.001f * __instance.originalDisplacement * Time.deltaTime;
                __instance.displacement += pumpingRate * skillFactor;
                __instance.displacement = Mathf.Min(__instance.displacement, __instance.originalDisplacement);
            }
            
            float cost = (10f * (0.02f * __instance.leakRateMin * Time.deltaTime) + pumpingRate) * Time.deltaTime / skillFactor;
            bridge.damageControlAvailable -= cost;

            if (bridge.damageControlAvailable < 0f) bridge.damageControlAvailable = 0f;
            
            if (__instance.leakRate < 0.1f && __instance.displacement >= __instance.originalDisplacement)
            {
                __instance.leakRate = 0f;
                __instance.displacement = __instance.originalDisplacement;
                __instance.leakToDisplacement = __instance.displacement;
                __instance.DamageControlActive = false;
            }
        }

        return false;
    }
}