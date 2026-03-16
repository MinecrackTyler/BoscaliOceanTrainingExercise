using UnityEngine;

namespace BPCustomComponents;

public class LaunchRail : MonoBehaviour
{
	[SerializeField] private Transform attachPoint;
	[SerializeField] private float railLength = 20f;
	[SerializeField] private float releaseDistance = 1f;
	[SerializeField] private Rigidbody rb;

	private bool isLaunched;
	private bool setupComplete;
    private Aircraft ac;
    private ConfigurableJoint launchJoint;
    private FixedJoint holdbackJoint;
    private Transform acHook;
    private Vector3 startPos;

    public void AttachAircraft(Aircraft ac, Transform acHook)
    {
        this.ac = ac;
        this.acHook = acHook;
        
        TeleportToRail();
        SetupJoint();
    }
    
    private void TeleportToRail()
    {
	    ac.rb.isKinematic = true;

        Vector3 localHookOffset = acHook.localPosition;

        Quaternion targetRot = attachPoint.rotation;

        Vector3 rotatedOffset = targetRot * localHookOffset;

        Vector3 targetPos = attachPoint.position - rotatedOffset;

        ac.rb.position = targetPos;
        ac.rb.rotation = targetRot;

        Physics.SyncTransforms();
    }

    private void SetupJoint()
    {
	    if (ac == null || ac.rb == null) return;
	    ac.rb.isKinematic = true;

	    launchJoint = ac.gameObject.AddComponent<ConfigurableJoint>();
	    launchJoint.connectedBody = rb;
	    launchJoint.autoConfigureConnectedAnchor = false;
        
	    Vector3 railForward = transform.forward;
	    Vector3 railUp = transform.up;
        
	    launchJoint.axis = ac.transform.InverseTransformDirection(railForward);
	    launchJoint.secondaryAxis = ac.transform.InverseTransformDirection(railUp);
        
	    launchJoint.xMotion = ConfigurableJointMotion.Limited;
	    launchJoint.yMotion = ConfigurableJointMotion.Locked;
	    launchJoint.zMotion = ConfigurableJointMotion.Locked;

	    launchJoint.angularXMotion = ConfigurableJointMotion.Locked;
	    launchJoint.angularYMotion = ConfigurableJointMotion.Locked;
	    launchJoint.angularZMotion = ConfigurableJointMotion.Locked;
        
	    launchJoint.anchor =
		    ac.transform.InverseTransformPoint(acHook.position);
        
	    Vector3 railLocalAttach =
		    transform.InverseTransformPoint(
			    attachPoint.position);

	    Vector3 railLocalForward =
		    transform.InverseTransformDirection(
			    railForward);

	    launchJoint.connectedAnchor =
		    railLocalAttach + railLocalForward * (railLength / 2f);
        
	    launchJoint.linearLimit = new SoftJointLimit
	    {
		    limit = railLength / 2f,
		    bounciness = 0f
	    };

	    startPos = transform.InverseTransformPoint(acHook.position);
        
	    ac.rb.isKinematic = false;
	    ac.SetComplexPhysics();

	    holdbackJoint = ac.gameObject.AddComponent<FixedJoint>();
	    holdbackJoint.connectedBody = rb;
	    setupComplete = true;
    }

    public void Launch()
    {
	    isLaunched = true;
	    Destroy(holdbackJoint);
    }
    
    private void Update()
    {
	    if (!ac.LocalSim) return;
	    if (!setupComplete) return;

	    if (isLaunched)
	    {
		    CheckForRelease();
	    }
    }
    
    private void CheckForRelease()
    {
	    if (launchJoint == null || ac == null) return;
        
	    Vector3 railForward = attachPoint.forward;
	    Vector3 railStartWorldPos = transform.TransformPoint(startPos);
	    Vector3 offsetFromStart = acHook.position - railStartWorldPos;
	    float distanceTraveled = Vector3.Dot(offsetFromStart, railForward);
        
	    if (distanceTraveled >= railLength - releaseDistance)
	    {
		    Destroy(launchJoint);
		    if (holdbackJoint != null) Destroy(holdbackJoint);
	    }
    }
}