using NuclearOption.NetworkTransforms;

namespace NOComponentWIP;

public class SmoothAircraftNetworkTransform : AircraftNetworkTransform
{
	public NetworkPIDSmoother networkSmoother;

	public override void Awake()
	{
		base.Awake();

		Aircraft.onInitialize += () =>
		{
			if (!base.HasAuthority)
			{
				networkSmoother.Initialize(Aircraft.rb);
			}
		};
	}

	public override void VisualUpdate(ref VisualUpdateTime visualTime)
	{
		if (base.HasAuthority || Aircraft.LocalSim)
		{
			return;
		}

		using (visualUpdateMarker.Auto())
		{
			if (!Aircraft.rb.isKinematic && TryGetSnapshot(ref visualTime, out var snapshot))
			{
				if (Aircraft.rb == null) return; //idk maybe will fix issue?
				networkSmoother.SmoothRB(Aircraft.rb, snapshot);
				Aircraft.CheckSpawnedInPosition();
			}
		}
	}
}