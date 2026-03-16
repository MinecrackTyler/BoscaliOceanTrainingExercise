using System.Collections.Generic;

namespace NOComponentWIP;

public static class LoadoutBridge
{
	public static List<int> SelectedUnitIDs = new List<int>();
	public static bool LoadoutSet = false;
	public static bool FobMode;
	public static bool BlockInputs = false;

	public static void SetLoadout(List<int> ids, bool fobMode)
	{
		SelectedUnitIDs = ids;
		FobMode = fobMode;
		LoadoutSet = true;
	}
	
	public static void Clear()
	{
		SelectedUnitIDs = new List<int>();
		LoadoutSet = false;
	} 
}