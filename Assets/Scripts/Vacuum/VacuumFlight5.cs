using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
public class VacuumFlight5 : MonoBehaviour
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

    [Header("Переход")]
    [Tooltip("Сколько секунд камера догоняет место за спиной после включения")]
    public float enterBlend = 0.6f;

    [Header("Flight")]
    public float lookSensitivity = 2f;
    public float rollSpeed = 90f;
    public float alignSpeed = 4f;
    public float thrust = 25f;
    public float brake = 40f;

    Rigidbody rb;
    Camera view;
    Quaternion camRot = Quaternion.identity;
    Quaternion startRot = Quaternion.identity;
    Vector3 startOffset;
    float currentLen;
    float blendT;
    bool cameraReady;
    bool blending;
    int poseFrame = -1;
    readonly RaycastHit[] hits = new RaycastHit[8];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints = RigidbodyConstraints.None;
        rb.maxAngularVelocity = 20f;

#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
#else
        rb.drag = 0f;
        rb.angularDrag = 0f;
#endif
        if (cameraTransform != null)
        {
            camRot = cameraTransform.rotation;
            view = cameraTransform.GetComponentInChildren<Camera>();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        Camera.onPreCull += OnAnyCameraPreCull;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        BeginBlend();
    }

    void OnDisable()
    {
        Camera.onPreCull -= OnAnyCameraPreCull;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    public void BeginBlend()
    {
        if (cameraTransform == null) return;

        camRot = cameraTransform.rotation;
        startRot = camRot;
        startOffset = cameraTransform.position - transform.position;
        if (startOffset.sqrMagnitude < 0.0001f)
            startOffset = -(camRot * Vector3.forward) * distance;

        blendT = 0f;
        blending = enterBlend > 0.01f;
        poseFrame = -1;
    }

    void OnAnyCameraPreCull(Camera cam)
    {
        if (cam == view) PlaceCamera();
    }

    void OnBeginCameraRendering(ScriptableRenderContext _, Camera cam)
    {
        if (cam == view) PlaceCamera();
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

        DriveRotation(rot);

        if (braking)
            ApplyBrake();
        else if (wish.sqrMagnitude > 0f)
        {
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            rb.AddForce(wish * thrust, ForceMode.Acceleration);
        }
    }

    void LateUpdate() => PlaceCamera();

    void PlaceCamera()
    {
        if (cameraTransform == null) return;

        bool advance = poseFrame != Time.frameCount;
        if (advance) poseFrame = Time.frameCount;

        Vector3 pivot = transform.position;
        Vector3 fullBoom = camRot * new Vector3(0f, height, -distance);
        float full = fullBoom.magnitude;
        if (full < 0.0001f) return;

        Vector3 dir = fullBoom / full;
        float allowed = BoomDistance(pivot, dir, full);

        if (!cameraReady || allowed >= full - 0.001f)
        {
            currentLen = allowed >= full - 0.001f ? full : allowed;
            cameraReady = true;
        }
        else if (allowed < currentLen)
            currentLen = allowed;
        else
            currentLen = Mathf.Lerp(currentLen, allowed, 1f - Mathf.Exp(-16f * Time.deltaTime));

        Vector3 desired = pivot + dir * currentLen;

        if (!blending)
        {
            cameraTransform.SetPositionAndRotation(desired, camRot);
            return;
        }

        if (advance)
            blendT += Time.deltaTime / enterBlend;

        if (blendT >= 1f)
        {
            blending = false;
            cameraTransform.SetPositionAndRotation(desired, camRot);
            return;
        }

        float s = blendT * blendT * (3f - 2f * blendT);
        Vector3 offset = BlendOffset(startOffset, desired - pivot, s);
        Quaternion shown = Quaternion.Slerp(startRot, camRot, s);
        cameraTransform.SetPositionAndRotation(pivot + offset, shown);
    }

    static Vector3 BlendOffset(Vector3 from, Vector3 to, float s)
    {
        if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f)
            return Vector3.Lerp(from, to, s);
        if (Vector3.Dot(from.normalized, to.normalized) < -0.5f)
            return Vector3.Lerp(from, to, s);
        return Vector3.Slerp(from, to, s);
    }

    float BoomDistance(Vector3 pivot, Vector3 dir, float full)
    {
        float castStart = 0.75f;
        Collider body = GetComponent<Collider>();
        if (body != null)
            castStart = body.bounds.extents.magnitude;

        if (castStart >= full)
            return Mathf.Max(minDistance * 0.5f, full);

        float allowed = full;
        Vector3 origin = pivot + dir * castStart;
        float remain = full - castStart;
        int n = Physics.SphereCastNonAlloc(origin, cameraRadius, dir, hits, remain, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.attachedRigidbody == rb || col.transform.IsChildOf(transform)) continue;
            float dist = castStart + hits[i].distance;
            if (dist < allowed) allowed = dist;
        }

        return Mathf.Max(minDistance * 0.5f, allowed - cameraPadding);
    }

    void DriveRotation(Quaternion target)
    {
        Quaternion delta = target * Quaternion.Inverse(rb.rotation);
        if (delta.w < 0f)
        {
            delta.x = -delta.x;
            delta.y = -delta.y;
            delta.z = -delta.z;
            delta.w = -delta.w;
        }

        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        if (Mathf.Abs(angle) < 0.5f || axis.sqrMagnitude < 1e-8f)
        {
            if (rb.angularVelocity.sqrMagnitude > 0f)
                rb.angularVelocity = Vector3.zero;
            return;
        }

        Vector3 w = axis.normalized * (angle * Mathf.Deg2Rad / Time.fixedDeltaTime);
        float maxW = rb.maxAngularVelocity;
        if (w.sqrMagnitude > maxW * maxW)
            w = w.normalized * maxW;
        rb.angularVelocity = w;
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

    float RollInput()
    {
        float roll = 0f;
        if (Input.GetKey(KeyCode.Q)) roll += 1f;
        if (Input.GetKey(KeyCode.E)) roll -= 1f;
        return roll;
    }
}