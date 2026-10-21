using UnityEngine;

/// <summary>
/// Притягивает ТОЛЬКО игрока к выбранному объекту (центру планеты)
/// и выравнивает его "верх" против направления гравитации.
/// Повесить на игрока (Rigidbody + CapsuleCollider).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerGravity : MonoBehaviour
{
    [Header("Цель притяжения")]
    [SerializeField] private Transform gravityTarget;

    [Header("Гравитация")]
    [Tooltip("Ускорение в м/с². Константа, не зависит от расстояния.")]
    [SerializeField] private float gravityStrength = 25f;

    [Header("Выравнивание")]
    [Tooltip("Чем больше, тем быстрее персонаж поворачивается 'головой от планеты'.")]
    [SerializeField] private float alignSpeed = 10f;

    private Rigidbody rb;
    private float pendingYaw;

    public float Strength => gravityStrength;

    /// <summary>Направление "вверх" для персонажа (от центра планеты). Пригодится скриптам движения и камере.</summary>
    public Vector3 GravityUp { get; private set; } = Vector3.up;

    /// <summary>Текущая планета. Можно менять из других скриптов при переходе между планетами.</summary>
    public Transform GravityTarget
    {
        get => gravityTarget;
        set => gravityTarget = value;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;                                   // стандартную отключаем
        rb.freezeRotation = true;                                // физика не должна кувыркать капсулу
        rb.interpolation = RigidbodyInterpolation.Interpolate;   // плавность на любой частоте кадров
    }

    private void FixedUpdate()
    {
        if (gravityTarget == null) return;

        Vector3 toTarget = gravityTarget.position - rb.position;
        if (toTarget.sqrMagnitude < 0.0001f) 
            return;             // защита от деления на ноль в центре

        GravityUp = -toTarget.normalized;

        // Притяжение к центру. Acceleration игнорирует массу.
        rb.AddForce(-GravityUp * gravityStrength, ForceMode.Acceleration);

        // Выравнивание: поворачиваем "верх" к GravityUp, сохраняя направление взгляда.
        Vector3 currentUp = rb.rotation * Vector3.up;
        Quaternion targetRot = Quaternion.FromToRotation(currentUp, GravityUp) * rb.rotation;

        float t = Mathf.Clamp01(alignSpeed * Time.fixedDeltaTime);
        Quaternion aligned = Quaternion.Slerp(rb.rotation, targetRot, t);
        aligned = Quaternion.AngleAxis(pendingYaw, GravityUp) * aligned;
        pendingYaw = 0f;
        rb.MoveRotation(aligned);
    }

    public void AddYaw(float degrees) => pendingYaw += degrees;

    private void OnDrawGizmos()
    {
        if (gravityTarget == null) 
            return;

        // "Верх" персонажа (зелёный, должен совпадать с обратным направлением гравитации)
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.up * 3f);
    }
}
