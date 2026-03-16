using NOComponentWIP.Patches;
using NuclearOption.Jobs;
using UnityEngine;

namespace NOComponentWIP;

public class AircraftShipPart : ShipPart
{
	[SerializeField]
	private PartJoint[] joints;
	
	private ShipPartBridge bridge;
	private bool simplePhysics;
	
	public override void Awake()
	{
        BaseReversePatches.UnitPartAwake(this);
        enabled = false;
        
        var collider = GetComponent<Collider>();
        bounds = collider.bounds;
        leakRate = 0f;
        surfaceArea = bounds.extents.x * bounds.extents.z;
        height = bounds.extents.y;
        rb = parentUnit.rb;
        
        originalDisplacement = displacement;
        leakToDisplacement = displacement;
        
		bridge = parentUnit.GetComponent<ShipPartBridge>();
        if (bridge != null)
        {
            bridge.parts.Add(this);
            DamageControlActive = false;
            DamageControlDelay /= Mathf.Clamp(bridge.aircraft.skill, 0.1f, 1f);
        }
        
        if (forceTransform == null) forceTransform = transform;

        if (transform.parent == null)
        {
            parentUnit.rb.mass = CalcMassWithChildren();
        }
	}

	public override void UnitPart_OnParentDetached(UnitPart parentPart)
	{
		rb = parentPart.rb;
		if (parentUnit.remoteSim && !detachedFromUnit)
		{
			JobManager.Add(SetupJob());
		}
		BaseReversePatches.UnitPartOnParentDetached(this, parentPart);
	}

	public override void Detach(Vector3 velocity, Vector3 relativePos)
	{
	    Rigidbody connectedBody = attachInfo.parentPart.rb;
	    bridge.damageControlAvailable -= originalDisplacement;
	    if (simplePhysics)
	    {
		    base.xform.SetParent(null);
		    rb = base.gameObject.AddComponent<Rigidbody>() ?? rb;
		    rb.mass = CalcMassWithChildren();
		    attachInfo.parentPart.rb.mass -= rb.mass;
		    rb.interpolation = RigidbodyInterpolation.Interpolate;
		    rb.velocity = velocity;
		    rb.angularVelocity = attachInfo.parentPart.rb.angularVelocity;
		    rb.maxLinearVelocity = 60f;
	    }
	    if (breakJointStrength > 0f)
	    {
	        ConfigurableJoint configurableJoint = base.gameObject.AddComponent<ConfigurableJoint>();
	        configurableJoint.connectedBody = connectedBody;
	        configurableJoint.xMotion = ConfigurableJointMotion.Locked;
	        configurableJoint.yMotion = ConfigurableJointMotion.Locked;
	        configurableJoint.zMotion = ConfigurableJointMotion.Locked;
	        configurableJoint.angularXMotion = ConfigurableJointMotion.Limited;
	        configurableJoint.angularYMotion = ConfigurableJointMotion.Limited;
	        configurableJoint.angularZMotion = ConfigurableJointMotion.Limited;
	        SoftJointLimit softJointLimit = new SoftJointLimit
	        {
	            limit = 20f
	        };
	        configurableJoint.highAngularXLimit = softJointLimit;
	        configurableJoint.lowAngularXLimit = softJointLimit;
	        configurableJoint.angularYLimit = softJointLimit;
	        configurableJoint.angularZLimit = softJointLimit;
	        configurableJoint.anchor = (base.xform.InverseTransformPoint(attachInfo.parentPart.xform.position) + attachInfo.localPosition) / 2f;
	        float breakForce = rb.mass * breakJointStrength;
	        configurableJoint.breakForce = breakForce;
	    }
	    displacement *= 0f;
	    Flood();
	    ShipPart shipPart = attachInfo.parentPart as ShipPart;
	    if (shipPart)
	    {
		    shipPart.displacement *= 0.5f;
		    shipPart.Flood();
	    }
	    ShipPart[] array = connectedCompartments;
	    foreach (ShipPart shipPart2 in array)
	    {
	        if (!(shipPart2 == shipPart))
	        {
	            shipPart2.displacement *= 0.5f;
	            shipPart2.Flood();
	        }
	    }
	    BaseReversePatches.UnitPartDetach(this, velocity, relativePos);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		JobManager.Remove(ref JobPart);
		DisposeJobFields(ref JobFields);
	}
	
	public void CreateRB(Vector3 velocity, Vector3 position)
	{
		if (mass == 0f || attachInfo == null || (rb != null && rb != parentUnit.rb))
		{
			if (rb == null)
			{
				Debug.LogError($"Couldn't find rb for part {base.gameObject}, attachInfo.parentPart = {attachInfo.parentPart}");
			}
			rb.mass = mass;
			return;
		}
		if (joints.Length != 0 && joints[0].connectedPart == null)
		{
			joints[0].connectedPart = attachInfo.parentPart;
		}
		base.xform.SetParent(null, worldPositionStays: true);
		rb = base.gameObject.AddComponent<Rigidbody>();
		rb.mass = mass;
		rb.drag = 0f;
		rb.angularDrag = 0f;
		rb.sleepThreshold = 0f;
		rb.velocity = velocity;
		rb.angularVelocity = parentUnit.rb.angularVelocity;
		rb.useGravity = true;
		if (position != Vector3.zero)
		{
			rb.position = position;
		}
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		simplePhysics = false;
	}

	public void CreateJoints()
	{
		for (int i = 0; i < joints.Length; i++)
		{
			PartJoint partJoint = joints[i];
			FixedJoint fixedJoint = base.gameObject.AddComponent<FixedJoint>();
			if (partJoint.anchor != null)
			{
				fixedJoint.anchor = partJoint.anchor.position - rb.position;
			}
			fixedJoint.connectedBody = partJoint.connectedPart.rb;
			fixedJoint.enableCollision = false;
			fixedJoint.breakForce = partJoint.breakForce * 10f;
			fixedJoint.breakTorque = partJoint.breakTorque * 10f;
			partJoint.joint = fixedJoint;
			if (partJoint.solverIterations != 6)
			{
				rb.solverIterations = partJoint.solverIterations;
			}
		}
	}
	
	public void MergeWithParent()
	{
		if (attachInfo == null || attachInfo.detachedFromParentPart)
		{
			return;
		}
		base.xform.SetParent(attachInfo.parentPart.xform);
		base.xform.localPosition = attachInfo.localPosition;
		base.xform.localRotation = attachInfo.localRotation;
		for (int i = 0; i < joints.Length; i++)
		{
			if (joints[i].joint != null)
			{
				Object.Destroy(joints[i].joint);
			}
		}
		if (rb != parentUnit.rb)
		{
			Object.Destroy(rb);
		}
		rb = parentUnit.rb;
		simplePhysics = true;
	}
}