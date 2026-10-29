using System;
using UnityEngine;

/// <summary>
/// Пуля с двумя режимами полёта:
/// - AlongPlanet: летит вдоль планеты по дуге большой окружности (кратчайшему пути по поверхности);
/// - Straight: летит прямо, как обычная пуля, не привязываясь к планете.
/// Rigidbody и коллайдер на пуле не нужны: движение считается напрямую, попадания ищутся сферокастом
/// между прошлой и новой позицией, поэтому быстрая пуля не пролетает сквозь цели.
///
/// Запуск:
///   bullet.Launch(planetCenter, направление, владелец);      // вдоль планеты
///   bullet.LaunchStraight(направление, владелец);            // прямо
/// Если в Launch передать null вместо планеты (например, стрелок в невесомости), пуля полетит прямо.
/// </summary>
public class PlanetBulletV2 : MonoBehaviour
{
    public enum FlightMode
    {
        AlongPlanet,
        Straight
    }

    [Header("Режим")]
    [Tooltip("Режим по умолчанию, если пулю не запускали через Launch / LaunchStraight")]
    [SerializeField] private FlightMode mode = FlightMode.AlongPlanet;

    [Header("Планета")]
    [Tooltip("Центр планеты. Обычно задаётся через Launch. Если пусто, берётся ближайшая зона гравитации.")]
    [SerializeField] private Transform planet;

    [Header("Полёт")]
    [Tooltip("Скорость, м/с (по поверхности в режиме AlongPlanet)")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float lifetime = 3f;

    [Header("Рельеф (только AlongPlanet)")]
    [Tooltip("true: пуля повторяет неровности поверхности. false: летит на постоянном расстоянии от центра планеты.")]
    [SerializeField] private bool followTerrain = true;
    [Tooltip("Высота пули над поверхностью, м")]
    [SerializeField] private float hoverHeight = 0.6f;
    [Tooltip("Слои, которые считаются землёй для следования за рельефом (слой самой планеты)")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("С какой высоты над пулей пускается луч вниз к рельефу, м. Должна быть больше самых высоких перепадов.")]
    [SerializeField] private float terrainProbeHeight = 3f;

    [Header("Попадания")]
    [SerializeField] private float radius = 0.2f;
    [Tooltip("Слои, в которые пуля попадает (враги, игрок, препятствия). Слой самой планеты сюда лучше не включать, если включено следование за рельефом.")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float damage = 10f;
    [SerializeField] private bool destroyOnHit = true;

    /// <summary>Попадание: во что попали и в какой точке.</summary>
    public event Action<Collider, Vector3> Hit;

    /// <summary>Текущий режим полёта.</summary>
    public FlightMode Mode => mode;

    private static readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    private Vector3 heading;      // текущее направление полёта
    private float fixedRadius;    // расстояние от центра при followTerrain = false
    private Transform owner;
    private bool initialized;

    /// <summary>
    /// Запустить пулю вдоль планеты. Вызывать сразу после Instantiate.
    /// Если planetCenter равен null, пуля полетит прямо.
    /// </summary>
    /// <param name="planetCenter">Центр планеты, по которой летит пуля.</param>
    /// <param name="direction">Желаемое направление (в режиме AlongPlanet проецируется на поверхность).</param>
    /// <param name="shooter">Тот, кто стрелял: в него пуля не попадает.</param>
    public void Launch(Transform planetCenter, Vector3 direction, Transform shooter = null)
    {
        planet = planetCenter;
        mode = planetCenter != null ? FlightMode.AlongPlanet : FlightMode.Straight;
        heading = direction;
        owner = shooter;
        Initialize();
    }

    /// <summary>Запустить пулю прямо, независимо от планеты.</summary>
    public void LaunchStraight(Vector3 direction, Transform shooter = null)
    {
        mode = FlightMode.Straight;
        heading = direction;
        owner = shooter;
        Initialize();
    }

    private void Start()
    {
        if (!initialized)
        {
            heading = transform.forward;
            Initialize();
        }
        Destroy(gameObject, lifetime);
    }

    private void Initialize()
    {
        if (mode == FlightMode.Straight)
        {
            if (heading.sqrMagnitude < 1e-6f) heading = transform.forward;
            heading.Normalize();
        }
        else
        {
            if (planet == null) planet = FindNearestPlanet();
            if (planet != null) fixedRadius = (transform.position - planet.position).magnitude;
        }

        initialized = true;
    }

    private Transform FindNearestPlanet()
    {
        GravityZone[] zones = FindObjectsByType<GravityZone>(FindObjectsSortMode.None);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (GravityZone z in zones)
        {
            if (z.Center == null) continue;
            float sqr = (z.Center.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = z.Center;
            }
        }
        return best;
    }

    private void Update()
    {
        if (!initialized) return;

        if (mode == FlightMode.Straight) StepStraight();
        else StepAlongPlanet();
    }

    // ----- Режим "прямо" -----
    private void StepStraight()
    {
        Vector3 position = transform.position;
        float distance = speed * Time.deltaTime;

        if (distance > 1e-5f && TryHit(position, heading, distance)) return;

        transform.SetPositionAndRotation(position + heading * distance, Quaternion.LookRotation(heading));
    }

    // ----- Режим "вдоль планеты" -----
    private void StepAlongPlanet()
    {
        if (planet == null) return;

        Vector3 center = planet.position;
        Vector3 position = transform.position;
        Vector3 offset = position - center;
        float dist = offset.magnitude;
        if (dist < 0.0001f) return;

        Vector3 up = offset / dist;

        // Направление строго по касательной к текущей точке
        heading = Vector3.ProjectOnPlane(heading, up);
        if (heading.sqrMagnitude < 1e-6f)
        {
            Destroy(gameObject); // направление совпало с "верхом", лететь некуда
            return;
        }
        heading.Normalize();

        // 1. Шаг по дуге большой окружности: поворачиваем радиус-вектор вокруг оси,
        //    перпендикулярной плоскости (центр, пуля, направление)
        float angle = speed * Time.deltaTime / dist; // радианы
        Vector3 axis = Vector3.Cross(up, heading);
        Quaternion step = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);

        Vector3 newOffset = step * offset;
        Vector3 newHeading = step * heading;
        Vector3 newUp = newOffset.normalized;

        // 2. Высота: либо постоянная, либо по рельефу
        float newDist = followTerrain ? dist : fixedRadius;
        Vector3 newPosition = center + newUp * newDist;

        if (followTerrain)
        {
            float probeDistance = newOffset.magnitude + terrainProbeHeight;
            Vector3 probeOrigin = center + newUp * probeDistance;

            if (Physics.Raycast(probeOrigin, -newUp, out RaycastHit ground, probeDistance, groundMask,
                                QueryTriggerInteraction.Ignore))
            {
                newPosition = ground.point + newUp * hoverHeight;
            }
        }

        // 3. Попадания на пути от старой позиции к новой
        Vector3 move = newPosition - position;
        float moveLength = move.magnitude;
        if (moveLength > 1e-5f && TryHit(position, move / moveLength, moveLength)) return;

        // 4. Применяем движение
        heading = newHeading;
        transform.SetPositionAndRotation(newPosition, Quaternion.LookRotation(newHeading, newUp));
    }

    private bool TryHit(Vector3 origin, Vector3 direction, float distance)
    {
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, distance, hitMask,
                                               QueryTriggerInteraction.Ignore);

        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i].collider;
            if (owner != null && c.transform.IsChildOf(owner)) continue; // в себя не попадаем

            if (hitBuffer[i].distance < bestDistance)
            {
                bestDistance = hitBuffer[i].distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return false;

        Collider target = hitBuffer[bestIndex].collider;
        Vector3 point = origin + direction * bestDistance;

        transform.position = point;

        // Урон передаём сообщением, чтобы не привязываться к конкретному классу здоровья:
        // на цели (или её родителе) достаточно метода void TakeDamage(float amount)
        target.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        Hit?.Invoke(target, point);

        if (destroyOnHit) Destroy(gameObject);
        return true;
    }
}
