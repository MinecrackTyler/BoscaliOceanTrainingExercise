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

		if (!Aircraft.rb.isKinematic && TryGetSnapshot(ref visualTime, out var snapshot))
		{
			networkSmoother.SmoothRB(Aircraft.rb, snapshot);
			Aircraft.CheckSpawnedInPosition();
		}
	}
}