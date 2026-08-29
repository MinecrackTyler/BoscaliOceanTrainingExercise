namespace NOComponentWIP.Systems;

//Idea and partial code from Appulcake
//https://github.com/Appulcake/PauelsRandomFixes/blob/a7157b4a1bf89a099cb927e91a4a6d1d1c7a90f8/PRF/Fixes/BOTESpawnProtection.cs

public class SpawnProtection
{
	public static bool IsProtected(Aircraft aircraft)
	{
		if (aircraft.TryGetShipBridge(out var bridge))
		{
			return bridge.Protected;
		}

		return false;
	}
}