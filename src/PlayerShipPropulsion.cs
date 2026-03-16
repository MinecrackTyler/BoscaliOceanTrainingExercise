using System;
using UnityEngine;
using NuclearOption;

public class PlayerShipPropulsion : MonoBehaviour, IEngine
{
    [SerializeField] private ShipPart part;
    [SerializeField] private UnitPart[] criticalParts;
    [SerializeField] private float thrust = 500000f;
    [SerializeField] private float steeringThrust = 100000f;
    [SerializeField] private float momentumFactor = 0.05f;
    [SerializeField] private float damageThreshold = 10f;
    [SerializeField] private float inputSmoothing = 0.5f;
    [SerializeField] private float steerSmoothing = 0.5f;
    [SerializeField] private ParticleSystem[] particles;
    [SerializeField] private AudioSource thrustSound;
    [SerializeField] private AudioSource engineSound;
    [SerializeField] private Transform thrustTransform;
    [SerializeField] private bool underwater = true;

    private float thrustInputSmoothed;
    private float steeringInputSmoothed;
    private float thrustSmoothSpeed;
    private float steeringSmoothSpeed;
    private IRSource irSource;
    
    private Aircraft aircraft;

    protected void Awake()
    {
        aircraft = part.parentUnit as Aircraft;
        if (aircraft == null) Destroy(this);
        
        part.onDetachFromParent += (p) => DisablePropulsion();
        foreach (var cp in criticalParts)
        {
            cp.onApplyDamage += (e) => {
                if (e.hitPoints < damageThreshold) DisablePropulsion();
            };
        }
        aircraft?.onDisableUnit += (u) => DisablePropulsion();
        irSource = new IRSource(thrustTransform, 0f, flare: false);
        aircraft?.AddIRSource(irSource);
    }

    private void DisablePropulsion()
    {
        thrust = 0f;
        foreach (var p in particles) p.Stop();
        if (thrustSound) thrustSound.Stop();
        if (engineSound) engineSound.Stop();
        this.enabled = false;
    }

    private void FixedUpdate()
    {
        if (thrust == 0f) return;

        var inputs = aircraft.GetInputs();
        
        float combinedThrust = Mathf.Clamp(inputs.throttle + inputs.pitch, -1f, 1f);
        
        float combinedSteer = Mathf.Clamp(inputs.yaw + inputs.roll, -1f, 1f);

        if (!aircraft.LocalSim) return;
        
        int num = (!underwater || thrustTransform.position.y < Datum.LocalSeaY) ? 1 : 0;
        
        float forwardSpeed = Vector3.Dot(aircraft.rb.velocity, aircraft.transform.forward);
        float steeringTarget = (1f + Mathf.Abs(forwardSpeed) * momentumFactor) * combinedSteer;
        
        thrustInputSmoothed = FastMath.SmoothDamp(thrustInputSmoothed, combinedThrust, ref thrustSmoothSpeed, inputSmoothing);
        steeringInputSmoothed = FastMath.SmoothDamp(steeringInputSmoothed, steeringTarget, ref steeringSmoothSpeed, steerSmoothing);
        
        Vector3 forwardForce = num * thrustInputSmoothed * thrust * transform.forward;
        Vector3 sideForce = num * steeringInputSmoothed * steeringThrust * -transform.right;
        
        aircraft.rb.AddForceAtPosition(forwardForce + sideForce, thrustTransform.position);
        
        UpdateEffects(combinedThrust, forwardSpeed);
    }

    private void UpdateEffects(float power, float speed)
    {
        if (engineSound != null)
        {
            float powerAbs = Mathf.Abs(power);
            engineSound.pitch = Mathf.Lerp(engineSound.pitch, Mathf.Min(0.5f + Mathf.Abs(speed) * 0.005f + powerAbs * 0.35f, 1.5f), Time.deltaTime);
            engineSound.volume = engineSound.pitch;
        }

        if (Mathf.Abs(power) > 0.1f)
        {
            if (particles.Length > 0 && !particles[0].isPlaying)
                foreach (var p in particles) p.Play();
            
            if (thrustSound && !thrustSound.isPlaying) thrustSound.Play();
            if (thrustSound && thrustSound.volume < 1f) thrustSound.volume += Time.deltaTime;
        }
        else
        {
            if (particles.Length > 0 && particles[0].isPlaying)
                foreach (var p in particles) p.Stop();
                
            if (thrustSound && thrustSound.volume > 0f) thrustSound.volume -= Time.deltaTime;
            else if (thrustSound) thrustSound.Stop();
        }
    }

    public float GetThrust()
    {
        return 0f;
    }

    public float GetMaxThrust()
    {
        return 0f;
    }

    public float GetRPM()
    {
        return 0f;
    }

    public float GetRPMRatio()
    {
        return 0f;
    }

    public void SetInteriorSounds(bool useInteriorSound)
    {
        return;
    }

    public event Action OnEngineDisable;
    public event Action OnEngineDamage;
}