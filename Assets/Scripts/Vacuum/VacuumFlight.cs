using UnityEngine;

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VacuumFlight : MonoBehaviour
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

    Rigidbody rb;
    float boomLength;
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
        boomLength = Boom().magnitude;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mx = Input.GetAxis("Mouse X") * lookSensitivity;
        float my = Input.GetAxis("Mouse Y") * lookSensitivity;
        cameraTransform.Rotate(Vector3.up, mx, Space.Self);
        cameraTransform.Rotate(Vector3.right, -my, Space.Self);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
    }

    void FixedUpdate()
    {
        Quaternion rot = rb.rotation;

        float roll = 0f;
        if (Input.GetKey(KeyCode.Q)) roll += 1f;
        if (Input.GetKey(KeyCode.E)) roll -= 1f;
        if (roll != 0f)
            rot = rot * Quaternion.AngleAxis(roll * rollSpeed * Time.fixedDeltaTime, Vector3.forward);

        Vector3 forward = cameraTransform.forward;
        Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
        if (up.sqrMagnitude < 1e-6f)
            up = Vector3.ProjectOnPlane(cameraTransform.up, forward);
        if (up.sqrMagnitude > 1e-6f)
            cameraTransform.rotation = Quaternion.LookRotation(forward, up.normalized);

        float t = 1f - Mathf.Exp(-alignSpeed * Time.fixedDeltaTime);
        rot = Quaternion.Slerp(rot, cameraTransform.rotation, t);

        rb.angularVelocity = Vector3.zero;
        rb.MoveRotation(rot);

        Vector3 wish = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) wish += cameraTransform.forward;
        if (Input.GetKey(KeyCode.S)) wish -= cameraTransform.forward;
        if (Input.GetKey(KeyCode.D)) wish += cameraTransform.right;
        if (Input.GetKey(KeyCode.A)) wish -= cameraTransform.right;
        if (Input.GetKey(KeyCode.Space)) wish += cameraTransform.up;
        if (Input.GetKey(KeyCode.LeftControl)) wish -= cameraTransform.up;
        if (wish.sqrMagnitude > 1f) wish.Normalize();

        rb.AddForce(wish * thrust, ForceMode.Acceleration);
    }

    void LateUpdate()
    {
        Vector3 pivot = rb.position;
        Vector3 boom = Boom();
        float full = boom.magnitude;
        Vector3 dir = boom / full;

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

        // В стену прилипаем сразу, обратно отъезжаем плавно.
        if (allowed < boomLength)
            boomLength = allowed;
        else
            boomLength = Mathf.Lerp(boomLength, full, 1f - Mathf.Exp(-8f * Time.deltaTime));

        cameraTransform.position = pivot + dir * boomLength;
    }

    Vector3 Boom()
    {
        return -cameraTransform.forward * distance + cameraTransform.up * height;
    }
}
