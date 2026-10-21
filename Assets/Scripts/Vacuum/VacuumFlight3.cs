using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VacuumFlight3 : MonoBehaviour
{
    public Transform cameraTransform;

    [Header("Third person")]
    public float distance = 7f;
    public float height = 1.8f;
    public float minDistance = 2f;
    public float maxDistance = 16f;
    public float zoomSpeed = 6f;
    public float cameraRadius = 0.25f;
    public float cameraPadding = 0.15f;

    [Header("Flight")]
    public float lookSensitivity = 2f;
    public float rollSpeed = 90f;
    public float alignSpeed = 4f;
    public float thrust = 25f;
    public float brake = 40f;

    Rigidbody rb;
    Quaternion camRot;
    float currentLen;
    bool cameraReady;
    readonly RaycastHit[] hits = new RaycastHit[16];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.None;

#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
#else
        rb.drag = 0f;
        rb.angularDrag = 0f;
#endif
        camRot = cameraTransform != null ? cameraTransform.rotation : Quaternion.identity;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mx = Input.GetAxis("Mouse X") * lookSensitivity;
        float my = Input.GetAxis("Mouse Y") * lookSensitivity;

        camRot = camRot * Quaternion.AngleAxis(mx, Vector3.up);
        camRot = camRot * Quaternion.AngleAxis(-my, Vector3.right);

        float roll = RollInput();
        if (roll != 0f)
            camRot = camRot * Quaternion.AngleAxis(roll * rollSpeed * Time.deltaTime, Vector3.forward);

        camRot = Quaternion.Normalize(camRot);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
    }

    void FixedUpdate()
    {
        Quaternion rot = rb.rotation;

        float roll = RollInput();
        if (roll != 0f)
            rot = rot * Quaternion.AngleAxis(roll * rollSpeed * Time.fixedDeltaTime, Vector3.forward);

        Vector3 camFwd = camRot * Vector3.forward;
        Vector3 camRight = camRot * Vector3.right;
        Vector3 camUp = camRot * Vector3.up;

        Vector3 wish = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) wish += camFwd;
        if (Input.GetKey(KeyCode.S)) wish -= camFwd;
        if (Input.GetKey(KeyCode.D)) wish += camRight;
        if (Input.GetKey(KeyCode.A)) wish -= camRight;
        if (Input.GetKey(KeyCode.Space)) wish += camUp;
        if (Input.GetKey(KeyCode.LeftControl)) wish -= camUp;

        bool braking = Input.GetKey(KeyCode.X);
        if (braking)
            wish = Vector3.zero;

        if (wish.sqrMagnitude > 0f)
        {
            float t = 1f - Mathf.Exp(-alignSpeed * Time.fixedDeltaTime);
            rot = Quaternion.Slerp(rot, camRot, t);
        }

        rb.angularVelocity = Vector3.zero;
        if (Quaternion.Angle(rb.rotation, rot) > 0.05f)
            rb.MoveRotation(rot);

        if (braking)
            ApplyBrake();
        else if (wish.sqrMagnitude > 0f)
        {
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            rb.AddForce(wish * thrust, ForceMode.Acceleration);
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        Vector3 pivot = transform.position;
        Vector3 fullBoom = camRot * new Vector3(0f, height, -distance);
        float full = fullBoom.magnitude;
        if (full < 0.0001f) return;

        Vector3 dir = fullBoom / full;
        float allowed = full;
        int n = Physics.SphereCastNonAlloc(pivot, cameraRadius, dir, hits, full, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.attachedRigidbody == rb || col.transform.IsChildOf(transform)) continue;
            if (hits[i].distance < allowed) allowed = hits[i].distance;
        }
        allowed = Mathf.Max(minDistance * 0.5f, allowed - cameraPadding);

        if (!cameraReady)
        {
            currentLen = allowed;
            cameraReady = true;
        }
        else if (allowed < currentLen)
            currentLen = allowed;
        else
            currentLen = Mathf.Lerp(currentLen, allowed, 1f - Mathf.Exp(-16f * Time.deltaTime));

        cameraTransform.SetPositionAndRotation(pivot + dir * currentLen, camRot);
    }

    float RollInput()
    {
        float roll = 0f;
        if (Input.GetKey(KeyCode.Q)) roll += 1f;
        if (Input.GetKey(KeyCode.E)) roll -= 1f;
        return roll;
    }

    void ApplyBrake()
    {
#if UNITY_6000_0_OR_NEWER
        Vector3 v = rb.linearVelocity;
#else
        Vector3 v = rb.velocity;
#endif
        float step = brake * Time.fixedDeltaTime;
        v = v.magnitude <= step ? Vector3.zero : v - v.normalized * step;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}