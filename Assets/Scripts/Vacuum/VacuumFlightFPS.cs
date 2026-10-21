using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VacuumFlightFPS : MonoBehaviour
{
    public Transform cameraTransform;
    public Vector3 cockpitOffset = new Vector3(0f, 0.7f, 0.2f);

    public float lookSensitivity = 2f;
    public float rollSpeed = 90f;   // град/сек
    public float alignSpeed = 4f;   // как быстро нос догон€ет взгл€д
    public float thrust = 25f;

    Rigidbody rb;

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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mx = Input.GetAxis("Mouse X") * lookSensitivity;
        float my = Input.GetAxis("Mouse Y") * lookSensitivity;

        // ќси камеры, не мировые Ч крен не сбрасываетс€ и не копитс€.
        cameraTransform.Rotate(Vector3.up, mx, Space.Self);
        cameraTransform.Rotate(Vector3.right, -my, Space.Self);
    }

    void FixedUpdate()
    {
        Quaternion rot = rb.rotation;

        float roll = 0f;
        if (Input.GetKey(KeyCode.Q)) roll += 1f;
        if (Input.GetKey(KeyCode.E)) roll -= 1f;
        if (roll != 0f)
        {
            // Ћокальный крен вокруг носа. —читаем в переменной:
            // второй MoveRotation в том же кадре затЄр бы первый.
            rot = rot * Quaternion.AngleAxis(roll * rollSpeed * Time.fixedDeltaTime, Vector3.forward);
        }

        // ¬згл€д камеры не трогаем, подстраиваем только крен под капсулу.
        Vector3 forward = cameraTransform.forward;
        Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
        if (up.sqrMagnitude < 1e-6f)
            up = Vector3.ProjectOnPlane(cameraTransform.up, forward);
        if (up.sqrMagnitude > 1e-6f)
            cameraTransform.rotation = Quaternion.LookRotation(forward, up.normalized);

        float t = 1f - Mathf.Exp(-alignSpeed * Time.fixedDeltaTime);
        rot = Quaternion.Slerp(rot, cameraTransform.rotation, t);

        rb.angularVelocity = Vector3.zero; // иначе физика докрутит поверх MoveRotation
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
        cameraTransform.position = rb.position + rb.rotation * cockpitOffset;
    }
}