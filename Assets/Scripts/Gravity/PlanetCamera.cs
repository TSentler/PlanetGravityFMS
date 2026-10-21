using UnityEngine;

/// <summary>
/// Камера от третьего лица с двумя режимами и общей ориентацией:
/// - у планеты: "верх" камеры плавно следует за гравитацией, тангаж ограничен;
/// - в невесомости: свободное вращение мышью без ограничений (куда смотрит камера, туда и полёт).
/// Переходы между режимами плавные: при вылете камера просто продолжает с того же положения,
/// при посадке постепенно выравнивает горизонт и возвращает тангаж в допустимые границы.
/// Повесить на объект Camera (НЕ делать дочерним к игроку).
/// </summary>
public class PlanetCamera : MonoBehaviour
{
    [Header("Цель")]
    [SerializeField] private PlayerGravity target;

    [Header("Позиция")]
    [SerializeField] private float distance = 8f;
    [Tooltip("Насколько точка, на которую смотрит камера, выше центра игрока")]
    [SerializeField] private float height = 1.5f;

    [Header("Мышь")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private bool lockCursor = true;

    [Header("У планеты")]
    [SerializeField] private float pitchMin = 5f;
    [SerializeField] private float pitchMax = 75f;
    [SerializeField] private float startPitch = 25f;
    [Tooltip("Чем меньше, тем плавнее и ленивее заворачивается горизонт")]
    [SerializeField] private float upFollowSpeed = 5f;
    [Tooltip("Градусов в секунду: с какой скоростью тангаж возвращается в допустимые границы после полёта")]
    [SerializeField] private float pitchSettleSpeed = 120f;

    private Quaternion rotation;   // общая ориентация камеры для обоих режимов
    private Vector3 smoothedUp;    // "верх" камеры в режиме у планеты
    private bool initialized;
    private bool wasWeightless;

    private void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Transform player = target.transform;
        float dt = Time.deltaTime;
        bool weightless = target.IsWeightless;

        if (!initialized)
        {
            Initialize(player, weightless);
            initialized = true;
        }
        else if (wasWeightless && !weightless)
        {
            // Посадка: начинаем с текущего "верха" камеры, чтобы не было скачка,
            // и дальше плавно подтягиваем его к планете
            smoothedUp = rotation * Vector3.up;
        }
        wasWeightless = weightless;

        float mx = Input.GetAxis("Mouse X") * sensitivity;
        float my = Input.GetAxis("Mouse Y") * sensitivity;

        if (weightless) UpdateFlight(mx, my);
        else UpdatePlanet(mx, my, dt);

        rotation = Quaternion.Normalize(rotation);

        // Камера смотрит на точку над игроком и стоит позади по направлению взгляда
        Vector3 pivot = player.position + rotation * (Vector3.up * height);
        transform.position = pivot - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }

    private void Initialize(Transform player, bool weightless)
    {
        wasWeightless = weightless;

        if (weightless)
        {
            rotation = player.rotation;
            smoothedUp = rotation * Vector3.up;
            return;
        }

        // GravityUp на первом кадре может быть ещё не посчитан, берём напрямую
        smoothedUp = (player.position - target.GravityTarget.position).normalized;

        Vector3 heading = Vector3.ProjectOnPlane(player.forward, smoothedUp);
        if (heading.sqrMagnitude < 0.0001f) heading = Vector3.ProjectOnPlane(Vector3.right, smoothedUp);
        heading.Normalize();

        Vector3 right = Vector3.Cross(smoothedUp, heading);
        Vector3 view = Quaternion.AngleAxis(startPitch, right) * heading; // положительный тангаж = смотрим вниз
        rotation = Quaternion.LookRotation(view, smoothedUp);
    }

    private void UpdatePlanet(float mx, float my, float dt)
    {
        // 1. Плавно догоняем "верх" планеты и переносим ориентацию камеры вместе с ним
        Vector3 prevUp = smoothedUp;
        float follow = 1f - Mathf.Exp(-upFollowSpeed * dt);
        smoothedUp = Vector3.Slerp(prevUp, target.GravityUp, follow).normalized;
        rotation = Quaternion.FromToRotation(prevUp, smoothedUp) * rotation;

        // 2. Рыскание вокруг "верха"
        rotation = Quaternion.AngleAxis(mx, smoothedUp) * rotation;

        // 3. Тангаж: ограничиваем, а если мы вне границ (после полёта), возвращаем плавно
        Vector3 f = rotation * Vector3.forward;
        float pitch = Mathf.Asin(Mathf.Clamp(-Vector3.Dot(f, smoothedUp), -1f, 1f)) * Mathf.Rad2Deg;
        float desired = Mathf.Clamp(pitch - my, pitchMin, pitchMax);

        bool outside = pitch < pitchMin - 0.5f || pitch > pitchMax + 0.5f;
        float newPitch = outside ? Mathf.MoveTowards(pitch, desired, pitchSettleSpeed * dt) : desired;

        rotation = Quaternion.AngleAxis(newPitch - pitch, rotation * Vector3.right) * rotation;

        // 4. Убираем накопившийся крен, чтобы верх камеры совпадал с smoothedUp
        f = rotation * Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(f, smoothedUp)) < 0.995f)
        {
            rotation = Quaternion.LookRotation(f, smoothedUp);
        }
    }

    private void UpdateFlight(float mx, float my)
    {
        // Свободное вращение вокруг собственных осей камеры, без ограничений тангажа
        rotation = rotation
                   * Quaternion.AngleAxis(mx, Vector3.up)
                   * Quaternion.AngleAxis(-my, Vector3.right);
    }
}