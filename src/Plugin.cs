using BepInEx;
using HarmonyLib;

namespace NOComponentWIP;

[BepInPlugin("NOComponentsWIP", "NOComponentsWIP", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
	void Awake()
	{
		Harmony harmony = new Harmony("NOComponentWIP");
		harmony.PatchAll();
	}
}