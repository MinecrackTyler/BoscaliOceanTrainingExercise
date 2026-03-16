using UnityEngine;

public class Gyro : MonoBehaviour
{
	[Header("Dependencies")]
	[SerializeField] private Aircraft aircraft;
    
	[Header("Axis Toggles")]
	public bool usePitch = true;
	public bool useRoll = true;
	public bool useYaw = true;

	[Header("Strength Settings")]
	[SerializeField] private float pitchStrength = 50f;
	[SerializeField] private float rollStrength = 50f;
	[SerializeField] private float yawStrength = 30f;

	private Rigidbody rb;
	private ControlInputs inputs;

	private void Awake()
	{
		if (aircraft == null)
			aircraft = GetComponentInParent<Aircraft>();

		if (aircraft == null) return;
		rb = aircraft.rb;
		inputs = aircraft.GetInputs();
	}

	private void FixedUpdate()
	{
		if (rb == null || inputs == null) return;
		
		Vector3 torque = new Vector3(
			usePitch ? inputs.pitch * pitchStrength : 0f,
			useYaw   ? inputs.yaw   * yawStrength   : 0f,
			useRoll  ? -inputs.roll * rollStrength  : 0f
		);
		
		rb.AddRelativeTorque(torque, ForceMode.Acceleration);
	}
}