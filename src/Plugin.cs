using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace NOComponentWIP;

[BepInPlugin("NOComponentsWIP", "NOComponentsWIP", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger;
	void Awake()
	{
		Logger = base.Logger;
		Harmony harmony = new Harmony("NOComponentWIP");
		harmony.PatchAll();
	}
}