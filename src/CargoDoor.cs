using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Rewired;

namespace NOComponentWIP
{
    public class CargoDoor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Aircraft aircraft;
        [SerializeField] private List<BayDoor> doors;
        [SerializeField] private AirCushion cushion;
        [SerializeField] private int[] cargoSlots;
        
        [Header("Global Settings")]
        [SerializeField] private float globalOpenSpeed = 0.5f;

        private float currentOpenAmount = 0f;
        private WeaponManager wm;
        private List<HardpointSet> cargoHardpoints;
        private float initialHeight = 2.5f;
        private LandingGear.GearState targetState = LandingGear.GearState.LockedRetracted;
        private bool isProcessing = false;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponentInParent<Aircraft>();
            aircraft.onSetGear += HandleGearEvent;
            aircraft.onInitialize += CargoDoor_OnInitialize;
            
            SetDoors(0f, false);
        }

        private void CargoDoor_OnInitialize()
        {
            aircraft.SetGear(false);
            aircraft.GearStateChanged(false);
            foreach (var door in doors)
            {
                door.Initialize();
            }

            initialHeight = cushion?.maxHeight ?? 2.5f;
            wm = aircraft.weaponManager;
        }

        private void OnDestroy()
        {
            if (aircraft != null) aircraft.onSetGear -= HandleGearEvent;
        }

        private void HandleGearEvent(Aircraft.OnSetGear e)
        {
            targetState = e.gearState;

            if (targetState == LandingGear.GearState.LockedRetracted)
            {
                isProcessing = false;
                SetDoors(0f, false);
                cushion?.maxHeight = initialHeight;
            }
            else if (targetState == LandingGear.GearState.LockedExtended)
            {
                isProcessing = false;
                SetDoors(1f, false);
                cushion?.maxHeight = 1.5f;
            }
            else if (!isProcessing && (targetState == LandingGear.GearState.Extending || targetState == LandingGear.GearState.Retracting))
            {
                ProcessMovement().Forget();
            }
        }

        private void ToggleSafety(bool safe)
        {
            if (wm == null) return;
            
        }
        
        private async UniTask ProcessMovement()
        {
            isProcessing = true;

            while (targetState == LandingGear.GearState.Extending || targetState == LandingGear.GearState.Retracting)
            {
                bool opening = (targetState == LandingGear.GearState.Extending);
                float step = globalOpenSpeed * Time.deltaTime;

                if (opening) currentOpenAmount += step;
                else currentOpenAmount -= step;

                currentOpenAmount = Mathf.Clamp01(currentOpenAmount);
                SetDoors(currentOpenAmount, opening);
                
                if (opening && currentOpenAmount >= 1f)
                {
                    aircraft.SetGear(LandingGear.GearState.LockedExtended);
                    break;
                }
                else if (!opening && currentOpenAmount <= 0f)
                {
                    aircraft.SetGear(LandingGear.GearState.LockedRetracted);
                    break;
                }

                await UniTask.Yield();
            }

            isProcessing = false;
        }

        private void SetDoors(float amount, bool opening)
        {
            foreach (var door in doors)
            {
                if (door != null) door.UpdateAnimation(amount, opening);
            }
        }

        private void Update()
        {
            var pilot = aircraft?.pilots[0];
            if (pilot == null) return;
            if (pilot.currentState is PilotPlayerState pilotPlayerState)
            {
                if (pilotPlayerState.player.GetButtonDown("Gear"))
                {
                    if (pilot.aircraft.gearState == LandingGear.GearState.LockedExtended)
                    {
                        pilot.aircraft.SetGear(deployed: false);
                    }
                    if (pilot.aircraft.gearState == LandingGear.GearState.LockedRetracted)
                    {
                        pilot.aircraft.SetGear(deployed: true);
                    }
                }
            }
        }

        public bool IsOpen()
        {
            return currentOpenAmount > 0.9f;
        }
    }
    
    [RequireComponent(typeof(AudioSource))]
    public class BayDoor : MonoBehaviour
    {
        public Transform doorTransform;
        public Vector3 closedAngle;
        public Vector3 openAngle;
        public float speedMultiplier = 1f;

        [Header("Audio Clips")]
        public AudioClip openStartSound;
        public AudioClip openStopSound;
        public AudioClip closeStartSound;
        public AudioClip closeStopSound;

        public Collider[] ignoreColliders;

        private AudioSource audioSource;
        private bool isMoving = false;
        private bool lastOpeningState = false;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            
        }

        public void Initialize()
        {
            foreach (var collider in this.GetComponents<Collider>())
            {
                foreach (var thisCollider in ignoreColliders)
                {
                    Physics.IgnoreCollision(collider, thisCollider);
                    Debug.Log($"{thisCollider.gameObject.name} disabling collide with {thisCollider.gameObject.name}");
                }
            }
        }

        public void UpdateAnimation(float globalAmount, bool opening)
        {
            float doorLocalAmount = Mathf.Clamp01(globalAmount * speedMultiplier);
            
            bool atBounds = (doorLocalAmount <= 0f || doorLocalAmount >= 1f);

            if (!atBounds)
            {
                if (!isMoving || lastOpeningState != opening)
                {
                    PlayMovementSound(opening);
                }
            }
            else
            {
                if (isMoving)
                {
                    PlayStopSound(opening);
                }
            }

            if (doorTransform != null)
            {
                doorTransform.localEulerAngles = Vector3.Lerp(closedAngle, openAngle, doorLocalAmount);
            }

            isMoving = !atBounds;
            lastOpeningState = opening;
        }

        private void PlayMovementSound(bool opening)
        {
            AudioClip clip = opening ? openStartSound : closeStartSound;
            if (audioSource.clip != clip)
            {
                audioSource.Stop();
                audioSource.clip = clip;
                audioSource.loop = true;
                if (clip != null) audioSource.Play();
            }
        }

        private void PlayStopSound(bool opening)
        {
            audioSource.Stop();
            audioSource.loop = false;
            AudioClip clip = opening ? openStopSound : closeStopSound;
            if (clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
        }
    }
}