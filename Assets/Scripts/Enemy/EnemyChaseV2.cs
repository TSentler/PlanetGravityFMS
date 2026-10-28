using UnityEngine;

/// <summary>
/// Враг, который живёт на одной планете и преследует игрока по её поверхности.
/// Самодостаточный: сам считает гравитацию к планете, выравнивается по ней и бежит к игроку
/// по кратчайшей дуге. Не зависит от зон гравитации.
/// Если враг пытается бежать, но почти не двигается, срабатывает манёвр выхода: подпрыгивание и шаг в сторону.
/// Повесить на врага (Rigidbody + CapsuleCollider).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyChaseV2 : MonoBehaviour
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

    [Header("Защита от застревания")]
    [SerializeField] private bool enableUnstuck = true;
    [Tooltip("Если скорость по поверхности меньше этой доли от moveSpeed при желании двигаться, считаем, что враг стоит")]
    [SerializeField] private float stuckSpeedFraction = 0.25f;
    [Tooltip("Сколько секунд враг должен простоять, чтобы считаться застрявшим")]
    [SerializeField] private float stuckTime = 0.5f;
    [Tooltip("Длительность шага в сторону, секунд")]
    [SerializeField] private float escapeDuration = 0.35f;
    [Tooltip("Скорость подпрыгивания при выходе, м/с. 0 = без прыжка")]
    [SerializeField] private float hopSpeed = 6f;
    [Tooltip("Если враг застрял снова в течение этого времени, манёвр усиливается и сторона меняется")]
    [SerializeField] private float attemptResetTime = 3f;

    private Rigidbody rb;
    private bool isChasing;
    private Vector3 faceDirection; // последнее известное направление на игрока вдоль поверхности

    // Состояние защиты от застревания
    private float stuckTimer;
    private float escapeTimer;
    private Vector3 escapeDirection;
    private float escapeSide = 1f;
    private int escapeAttempt;
    private float lastEscapeTime = -999f;

    /// <summary>true, пока враг преследует игрока.</summary>
    public bool IsChasing => isChasing;

    /// <summary>true, пока враг выполняет манёвр выхода из застревания.</summary>
    public bool IsEscaping => escapeTimer > 0f;

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
        UpdateChase(up, out Vector3 moveDirection);

        // 3. Текущая скорость, разложенная на вертикальную и тангенциальную
        Vector3 velocity = Velocity;
        Vector3 vertical = Vector3.Project(velocity, up);
        Vector3 tangential = velocity - vertical;

        // 4. Защита от застревания: может заменить направление бега и добавить прыжок
        moveDirection = UpdateUnstuck(up, moveDirection, tangential.magnitude, dt, out float hop);
        if (hop > 0f) vertical = up * hop;

        // 5. Бег по поверхности, вертикаль отдаём физике
        tangential = Vector3.MoveTowards(tangential, moveDirection * moveSpeed, acceleration * dt);
        Velocity = tangential + vertical;

        // 6. Ориентация: выравниваем по планете и поворачиваем лицом к игроку
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

    /// <summary>
    /// Следит за тем, что враг хочет двигаться, но стоит. Тогда на короткое время подменяет направление бега
    /// шагом в сторону и подбрасывает врага вверх. Возвращает направление, которое нужно использовать.
    /// hop > 0 означает, что нужно задать вертикальную скорость этого значения.
    /// </summary>
    private Vector3 UpdateUnstuck(Vector3 up, Vector3 moveDirection, float surfaceSpeed, float dt, out float hop)
    {
        hop = 0f;
        if (!enableUnstuck) return moveDirection;

        // Идёт манёвр выхода: бежим в сторону, игнорируя погоню
        if (escapeTimer > 0f)
        {
            escapeTimer -= dt;
            Vector3 dir = Vector3.ProjectOnPlane(escapeDirection, up);
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : moveDirection;
        }

        // Обнаружение: хотим двигаться, но скорость мала
        bool wantsToMove = moveDirection.sqrMagnitude > 0.0001f;
        bool tooSlow = surfaceSpeed < moveSpeed * stuckSpeedFraction;

        if (wantsToMove && tooSlow) stuckTimer += dt;
        else stuckTimer = 0f;

        if (stuckTimer < stuckTime) return moveDirection;

        // Застряли: запускаем манёвр
        stuckTimer = 0f;

        // Повторное застревание вскоре после предыдущего усиливает манёвр
        if (Time.time - lastEscapeTime > attemptResetTime) escapeAttempt = 0;
        else escapeAttempt++;
        lastEscapeTime = Time.time;

        // Сторона: в первый раз случайная, дальше чередуем
        escapeSide = escapeAttempt == 0 ? (Random.value < 0.5f ? -1f : 1f) : -escapeSide;

        float boost = 1f + 0.5f * Mathf.Min(escapeAttempt, 2);

        Vector3 right = Vector3.Cross(up, moveDirection).normalized;
        escapeDirection = (right * escapeSide + moveDirection * 0.3f).normalized;
        escapeTimer = escapeDuration * boost;
        hop = hopSpeed * boost;

        return escapeDirection;
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
