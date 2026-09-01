using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace NOComponentWIP;

public static class BlueprinterHelper
{
    private static bool initialized = false;
    
    private static bool patchingComplete = false;

    public static bool PatchingComplete
    {
        get
        {
            if (!initialized)
                InitializeHarmonyHooks();
            
            return patchingComplete;
        }
    }
    
    public static void Initialize()
    {
        InitializeHarmonyHooks();
    }
    
    private static void InitializeHarmonyHooks()
{
    if (initialized)
    {
        return;
    }

    if (!Chainloader.PluginInfos.TryGetValue("com.nikkorap.blueprinter", out var pluginInfo) || pluginInfo?.Instance == null)
    {
        Plugin.DebugLog("Blueprinter instance not available yet");
        return;
    }
    
    Assembly blueprinterAssembly = pluginInfo.Instance.GetType().Assembly;

    Harmony harmony = new Harmony("com.nocomponentwip.blueprinterhelper");
    
    bool patchedAny = false;

    Type loadingScreenType = blueprinterAssembly.GetType("Blueprinter.BlueprinterLoadingScreen");

    if (loadingScreenType != null)
    {
        MethodInfo destroyInstanceMethod = loadingScreenType.GetMethod("DestroyInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (destroyInstanceMethod != null)
        {
            MethodInfo postfix = typeof(BlueprinterHelper).GetMethod(nameof(PatchingCompleted), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (postfix != null)
            {
                harmony.Patch(destroyInstanceMethod, postfix: new HarmonyMethod(postfix));
                patchedAny = true;
                Plugin.DebugLog("Successfully patched DestroyInstance");
            }
        }
    }

    Type issuePopupType = blueprinterAssembly.GetType("Blueprinter.BlueprinterIssuePopup");

    if (issuePopupType != null)
    {
        MethodInfo showMethod = issuePopupType.GetMethod("Show", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (showMethod != null)
        {
            MethodInfo postfix = typeof(BlueprinterHelper).GetMethod(nameof(PatchingCompleted), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (postfix != null)
            {
                harmony.Patch(showMethod, postfix: new HarmonyMethod(postfix));
                patchedAny = true;
                Plugin.DebugLog("Successfully patched Show");
            }
        }
    }
    
    initialized = patchedAny;
}
    
    private static void PatchingCompleted()
    {
        if (patchingComplete)
            return;
        
        patchingComplete = true;
        Plugin.DebugLog("Blueprinter patching completed");
    }
}