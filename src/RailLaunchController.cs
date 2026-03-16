using System.Collections;
using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace BPCustomComponents;

public class RailLaunchController : MonoBehaviour
{
    [SerializeField] private Aircraft aircraft;
    [SerializeField] private GameObject railPrefab;
    [SerializeField] private GameObject boosterPrefab;
    [SerializeField] private Transform railAttachPoint;
    [SerializeField] private Transform[] boosterAttachPoints;

    [SerializeField] private float launchRPMThreshold;
    [SerializeField] private float railLength;
    [SerializeField] private float releaseThresholdBuffer;

    private FixedJoint holdBackJoint;
    private Vector3 startLocalPos;
    private ConfigurableJoint launchJoint;
    private bool isLaunched;
    private Rigidbody rb;
    private GameObject railInstance;
    private RailLauncher railLauncher;
    private List<RailBooster> railBoosters = new List<RailBooster>();
    private bool setupComplete;

    private void Awake()
    {
        if (aircraft == null) Destroy(this);
        if (GameManager.gameState == GameState.Editor) return;

        aircraft.onInitialize += OnAircraftInitialize;
    }

    private void OnDestroy()
    {
        if (railInstance != null && aircraft != null && aircraft.IsServer)
            NetworkManagerNuclearOption.i.ServerObjectManager.Destroy(railInstance);
    }

    private void OnAircraftInitialize()
    {
        aircraft.SetSimplePhysics();

        if (aircraft.IsServer)
        {
            SpawnRail();
            TeleportToRail();
        }

        if (aircraft.LocalSim)
            StartCoroutine(SetupJoint());
        
        SpawnBoosters();
    }

    private void SpawnBoosters()
    {
        foreach (var point in boosterAttachPoints)
        {
            var booster = Instantiate(boosterPrefab, point.position, point.rotation);
            RailBooster railBooster = booster.GetComponent<RailBooster>();
            railBooster.Initialize(aircraft);
            railBoosters.Add(railBooster);
        }
    }

    private void SpawnRail()
    {
        railInstance = NetworkManagerNuclearOption.i.ServerObjectManager
            .SpawnInstantiate(railPrefab, railPrefab.GetNetworkIdentity().PrefabHash, aircraft.Owner);

        railLauncher = railInstance.GetComponent<RailLauncher>();

        railInstance.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

    private void TeleportToRail()
    {
        rb = aircraft.rb;
        rb.isKinematic = true;

        // Save hook local offset BEFORE rotation
        Vector3 localHookOffset = railAttachPoint.localPosition;

        Quaternion targetRot = railLauncher.attachPoint.rotation;

        // Rotate the local offset into world space
        Vector3 rotatedOffset = targetRot * localHookOffset;

        // Set final position so hook aligns
        Vector3 targetPos = railLauncher.attachPoint.position - rotatedOffset;

        rb.position = targetPos;
        rb.rotation = targetRot;

        Physics.SyncTransforms();
    }

    private IEnumerator SetupJoint()
    {
        while (railInstance == null)
            yield return null;

        yield return new WaitForSeconds(0.05f);

        railLauncher = railInstance.GetComponent<RailLauncher>();
        rb = aircraft.rb;
        rb.isKinematic = true;

        IgnoreRailCollisions();

        Rigidbody railRb = railInstance.GetComponentInChildren<Rigidbody>();

        launchJoint = gameObject.AddComponent<ConfigurableJoint>();
        launchJoint.connectedBody = railRb;
        launchJoint.autoConfigureConnectedAnchor = false;

        // ---- AXIS SETUP (in aircraft local space) ----
        Vector3 railForwardWorld = railLauncher.attachPoint.forward;
        Vector3 railUpWorld = railLauncher.attachPoint.up;

        launchJoint.axis =
            transform.InverseTransformDirection(railForwardWorld);

        launchJoint.secondaryAxis =
            transform.InverseTransformDirection(railUpWorld);

        // ---- MOTION ----
        launchJoint.xMotion = ConfigurableJointMotion.Limited;
        launchJoint.yMotion = ConfigurableJointMotion.Locked;
        launchJoint.zMotion = ConfigurableJointMotion.Locked;

        launchJoint.angularXMotion = ConfigurableJointMotion.Locked;
        launchJoint.angularYMotion = ConfigurableJointMotion.Locked;
        launchJoint.angularZMotion = ConfigurableJointMotion.Locked;

        // ---- AIRCRAFT ANCHOR (NO OFFSET) ----
        launchJoint.anchor =
            transform.InverseTransformPoint(railAttachPoint.position);

        // ---- RAIL ANCHOR (OFFSET BACK HALF RAIL LENGTH) ----
        Vector3 railLocalAttach =
            railInstance.transform.InverseTransformPoint(
                railLauncher.attachPoint.position);

        Vector3 railLocalForward =
            railInstance.transform.InverseTransformDirection(
                railForwardWorld);

        launchJoint.connectedAnchor =
            railLocalAttach + railLocalForward * (railLength / 2f);

        // ---- LIMIT ----
        launchJoint.linearLimit = new SoftJointLimit
        {
            limit = railLength / 2f,
            bounciness = 0f
        };

        startLocalPos =
            railInstance.transform.InverseTransformPoint(
                railAttachPoint.position);

        railLauncher.SetTangible(true);

        yield return new WaitForFixedUpdate();

        rb.isKinematic = false;
        aircraft.SetComplexPhysics();

        holdBackJoint = gameObject.AddComponent<FixedJoint>();
        holdBackJoint.connectedBody = railRb;

        setupComplete = true;
    }

    private void IgnoreRailCollisions()
    {
        Collider[] aCols = GetComponentsInChildren<Collider>();
        Collider[] rCols = railInstance.GetComponentsInChildren<Collider>();

        foreach (var a in aCols)
            foreach (var r in rCols)
                Physics.IgnoreCollision(a, r);
    }

    private void Update()
    {
        if (!aircraft.LocalSim) return;
        if (!setupComplete) return;

        if (!isLaunched)
        {
            if (aircraft.GetInputs().throttle >= 0.95f &&
                GetRPMRatio() > launchRPMThreshold)
            {
                isLaunched = true;
                Destroy(holdBackJoint);
                foreach (var booster in railBoosters)
                {
                    booster.Ignite();
                }
            }
               
        }
        else
        {
            CheckForRelease();
        }
    }

    private void CheckForRelease()
    {
        if (launchJoint == null || railLauncher == null) return;
        
        Vector3 railForward = railLauncher.attachPoint.forward;
        Vector3 railStartWorldPos = railInstance.transform.TransformPoint(startLocalPos);
        Vector3 offsetFromStart = railAttachPoint.position - railStartWorldPos;
        float distanceTraveled = Vector3.Dot(offsetFromStart, railForward);
        
        if (distanceTraveled >= railLength - releaseThresholdBuffer)
        {
            Destroy(launchJoint);
            if (holdBackJoint != null) Destroy(holdBackJoint);
        }
    }



    private float GetRPMRatio()
    {
        float total = 0f;
        foreach (var engine in aircraft.engines)
            total += engine.GetRPMRatio();

        return total / aircraft.engines.Count;
    }
}
