using UnityEngine;

/// <summary>
/// Враг, который живёт на одной планете и преследует игрока по её поверхности.
/// Самодостаточный: сам считает гравитацию к планете, выравнивается по ней и бежит к игроку
/// по кратчайшей дуге. Не зависит от зон гравитации и не меняет никакие другие скрипты.
/// Повесить на врага (Rigidbody + CapsuleCollider).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyChaseV1 : MonoBehaviour
{
    [Header("Планета")]
    [Tooltip("Центр планеты, на которой живёт враг. Если пусто, берётся ближайшая зона гравитации.")]
    [SerializeField] private Transform planet;
    [SerializeField] private float gravityStrength = 25f;
    [Tooltip("Чем больше, тем быстрее враг выравнивается 'головой от планеты'.")]
    [SerializeField] private float alignSpeed = 10f;

    [Header("Цель")]
    [Tooltip("Игрок. Если пусто, ищется на сцене.")]
    [SerializeField] private PlayerGravity player;
    [Tooltip("Преследовать, только если игрок на той же планете")]
    [SerializeField] private bool onlySamePlanet = true;

    [Header("Погоня")]
    [Tooltip("С какого расстояния враг замечает игрока")]
    [SerializeField] private float detectionRange = 15f;
    [Tooltip("Дальше этого расстояния враг теряет интерес (больше радиуса обнаружения, чтобы не дёргался на границе)")]
    [SerializeField] private float loseRange = 22f;
    [Tooltip("Ближе этого расстояния враг останавливается и просто смотрит на игрока")]
    [SerializeField] private float stopDistance = 1.5f;
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Разгон и торможение, м/с²")]
    [SerializeField] private float acceleration = 25f;
    [Tooltip("Скорость разворота лицом к игроку, градусов в секунду")]
    [SerializeField] private float turnSpeed = 360f;

    private Rigidbody rb;
    private bool isChasing;
    private Vector3 faceDirection; // последнее известное направление на игрока вдоль поверхности

    /// <summary>true, пока враг преследует игрока.</summary>
    public bool IsChasing => isChasing;

    // Обёртка: в Unity 6 velocity переименовали в linearVelocity
    private Vector3 Velocity
    {
#if UNITY_6000_0_OR_NEWER
        get => rb.linearVelocity;
        set => rb.linearVelocity = value;
#else
        get => rb.velocity;
        set => rb.velocity = value;
#endif
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        // Поиск делаем в Start, чтобы все остальные объекты уже прошли Awake
        if (player == null) player = FindFirstObjectByType<PlayerGravity>();

        if (planet == null)
        {
            GravityZone[] zones = FindObjectsByType<GravityZone>(FindObjectsSortMode.None);
            float best = float.MaxValue;
            foreach (GravityZone z in zones)
            {
                if (z.Center == null) continue;
                float d = (z.Center.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    planet = z.Center;
                }
            }
        }

        if (planet == null) Debug.LogWarning("EnemyChase: планета не найдена, враг не будет двигаться.", this);
    }

    private void FixedUpdate()
    {
        if (planet == null) return;

        float dt = Time.fixedDeltaTime;

        Vector3 toPlanet = planet.position - rb.position;
        if (toPlanet.sqrMagnitude < 0.0001f) return;
        Vector3 up = -toPlanet.normalized;

        // 1. Гравитация к центру планеты
        rb.AddForce(-up * gravityStrength, ForceMode.Acceleration);

        // 2. Куда бежать
        Vector3 moveDirection = Vector3.zero;
        UpdateChase(up, out moveDirection);

        // 3. Скорость: бег по поверхности, вертикаль отдаём физике
        Vector3 velocity = Velocity;
        Vector3 vertical = Vector3.Project(velocity, up);
        Vector3 tangential = velocity - vertical;
        tangential = Vector3.MoveTowards(tangential, moveDirection * moveSpeed, acceleration * dt);
        Velocity = tangential + vertical;

        // 4. Ориентация: выравниваем по планете и поворачиваем лицом к игроку
        Vector3 currentUp = rb.rotation * Vector3.up;
        Quaternion target = Quaternion.FromToRotation(currentUp, up) * rb.rotation;
        float t = Mathf.Clamp01(alignSpeed * dt);
        Quaternion aligned = Quaternion.Slerp(rb.rotation, target, t);

        if (isChasing && faceDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = Vector3.ProjectOnPlane(aligned * Vector3.forward, up);
            if (forward.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.SignedAngle(forward, faceDirection, up);
                float maxStep = turnSpeed * dt;
                aligned = Quaternion.AngleAxis(Mathf.Clamp(angle, -maxStep, maxStep), up) * aligned;
            }
        }

        rb.MoveRotation(aligned);
    }

    /// <summary>Решает, преследовать ли игрока, и считает направление бега вдоль поверхности.</summary>
    private void UpdateChase(Vector3 up, out Vector3 moveDirection)
    {
        moveDirection = Vector3.zero;

        if (player == null)
        {
            isChasing = false;
            return;
        }

        Vector3 toPlayer = player.transform.position - rb.position;
        float distance = toPlayer.magnitude;

        // Игрок должен быть на нашей планете (если включено)
        bool reachable = !onlySamePlanet || player.GravityTarget == planet;

        // Гистерезис: заметить легче, чем потерять
        float range = isChasing ? loseRange : detectionRange;
        isChasing = reachable && distance <= range;

        if (!isChasing) return;

        // Проекция направления на касательную плоскость даёт начало кратчайшей дуги к игроку,
        // даже если он на другой стороне планеты
        Vector3 flat = Vector3.ProjectOnPlane(toPlayer, up);
        if (flat.sqrMagnitude > 0.0001f) faceDirection = flat.normalized;
        // (если игрок точно напротив через центр, оставляем прошлое направление)

        if (distance > stopDistance) moveDirection = faceDirection;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
