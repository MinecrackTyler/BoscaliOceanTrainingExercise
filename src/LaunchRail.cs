using System;
using System.Collections;
using System.Collections.Generic;
using Mirage;
using UnityEngine;

namespace BPCustomComponents;

public class LaunchRail : NetworkBehaviour
{
	[SerializeField] private Transform attachPoint;
	[SerializeField] private float railLength = 20f;
	[SerializeField] private float releaseDistance = 1f;
	[SerializeField] private bool isCatapult;
	[SerializeField] private AnimationCurve accelCurve;
	[SerializeField] private float maxVelocity;
	[SerializeField] private Rigidbody rb;
	[SerializeField] private float maxForce = float.MaxValue;

	public bool IsReady => setupComplete;
	public bool IsOccupied => ac != null;
	public event Action onRelease;

	private bool isLaunched;
	private bool isReleased;
	private bool setupComplete;
    private Aircraft ac;
    private ConfigurableJoint launchJoint;
    private FixedJoint holdbackJoint;
    private Transform acHook;
    private Vector3 startPos;
    private float distance;
    private float progress;
    

    public bool AttachAircraft(Aircraft ac, Transform acHook)
    {
	    if (IsOccupied) return false;
	    Reset();
        this.ac = ac;
        this.acHook = acHook;
        SetTangible(false);
        ac.SetSimplePhysics();
        TeleportToRail();
        StartCoroutine(SetupJoint());
        return true;
    }

    private void Reset()
    {
	    distance = 0f;
	    progress = 0f;
	    isReleased = false;
	    isLaunched = false;
	    setupComplete = false;
	    ac = null;
	    launchJoint = null;
	    holdbackJoint = null;
	    acHook = null;
	    startPos = new Vector3();
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

    private IEnumerator SetupJoint()
    {
	    if (ac == null || ac.rb == null) yield break;
	    ac.rb.isKinematic = true;

	    yield return new WaitForFixedUpdate();

	    launchJoint = ac.gameObject.AddComponent<ConfigurableJoint>();
	    launchJoint.connectedBody = rb;
	    launchJoint.autoConfigureConnectedAnchor = false;
        
	    Vector3 railForward = attachPoint.forward;
	    Vector3 railUp = attachPoint.up;
        
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
        
	    Vector3 railLocalAttach = rb.transform.InverseTransformPoint(attachPoint.position);
	    Vector3 railLocalForward = rb.transform.InverseTransformDirection(railForward);

	    launchJoint.connectedAnchor =
		    railLocalAttach + railLocalForward * (railLength / 2f);
        
	    launchJoint.linearLimit = new SoftJointLimit
	    {
		    limit = railLength / 2f,
		    bounciness = 0f
	    };

	    startPos = transform.InverseTransformPoint(acHook.position);

	    yield return new WaitForFixedUpdate();
	    
	    ac.rb.isKinematic = false;
	    SetTangible(true);
	    ac.SetComplexPhysics();

	    holdbackJoint = ac.gameObject.AddComponent<FixedJoint>();
	    holdbackJoint.connectedBody = rb;
	    setupComplete = true;
    }

    public void Launch()
    {
	    if (isLaunched) return;
	    if (!IsReady) return;
	    isLaunched = true;
	    Destroy(holdbackJoint);
	    if (isCatapult)
	    {
		    StartCoroutine(LaunchRoutine());
	    }
    }

    public void Release()
    {
	    if (isReleased) return;
	    isReleased = true;
	    onRelease?.Invoke();
	    if (launchJoint != null) Destroy(launchJoint);
	    if (holdbackJoint != null) Destroy(holdbackJoint);
	    ac = null;
	    distance = 0f;

    }

    private IEnumerator LaunchRoutine()
    {
	    if (isCatapult)
	    {
		    JointDrive drive = new JointDrive()
		    {
			    positionSpring = 0f,
			    positionDamper = maxForce,
			    maximumForce = 500000000f
		    };
		    launchJoint.xDrive = drive;
	    }

	    Vector3 railStartWorld = transform.TransformPoint(startPos);
	    Vector3 railForward = attachPoint.forward;
	    
	    accelCurve.postWrapMode = WrapMode.Clamp;
	    accelCurve.preWrapMode = WrapMode.Clamp;
	    
	    while (!isReleased)
	    {
		    Vector3 offset = acHook.position - railStartWorld;
			distance = Vector3.Dot(offset, railForward);

		    progress = Mathf.Clamp01(distance / railLength);

		    float targetVel = accelCurve.Evaluate(progress);
		    
		    launchJoint?.targetVelocity = new Vector3(-targetVel, 0f, 0f);
		    Debug.Log(progress);
		    
		    yield return new WaitForFixedUpdate();
	    }
    }

    private void SetTangible(bool tangible)
    {
	    foreach (var collider in GetComponentsInChildren<Collider>())
	    {
		    collider.enabled = tangible;
	    }
	    rb.isKinematic = !tangible;
	    IgnoreRailCollisions(!tangible);
    }
    
    private void IgnoreRailCollisions(bool ignored)
    {
	    Collider[] aCols = ac.GetComponentsInChildren<Collider>();
	    Collider[] rCols = GetComponentsInChildren<Collider>();

	    foreach (var a in aCols)
		    foreach (var r in rCols)
			    Physics.IgnoreCollision(a, r, ignored);
    }

    private void FixedUpdate()
    {
	    if (ac == null || isReleased) return;
	    if (distance >= railLength - releaseDistance)
	    {
		    Release();
	    }
    }
}