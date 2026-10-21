using UnityEngine;

/// <summary>
/// Бег по поверхности планеты. WASD считается относительно "системы отсчёта ввода":
/// пока камеры нет, это виртуальное направление, которое само следует за кривизной планеты.
/// Когда появится камера, достаточно перетащить её в поле Input Reference.
/// Персонаж поворачивается лицом в сторону движения.
/// </summary>
[DefaultExecutionOrder(10)] // после PlayerGravity, чтобы GravityUp был уже актуален
[RequireComponent(typeof(Rigidbody), typeof(PlayerGravity))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] private float moveSpeed = 8f;
    [Tooltip("Разгон, м/с²")]
    [SerializeField] private float acceleration = 40f;
    [Tooltip("Торможение, когда клавиши отпущены, м/с²")]
    [SerializeField] private float deceleration = 60f;

    [Header("Поворот персонажа")]
    [Tooltip("Как быстро персонаж разворачивается лицом по ходу движения, градусов в секунду")]
    [SerializeField] private float turnSpeed = 720f;

    [Header("Система отсчёта ввода")]
    [Tooltip("Камера (когда появится). Если пусто, используется виртуальная 'камера'.")]
    [SerializeField] private Transform inputReference;

    private Rigidbody rb;
    private PlayerGravity gravity;
    private Vector2 input;

    // Виртуальное "направление камеры" в касательной плоскости
    private Vector3 virtualForward;
    private Vector3 lastUp;
    private bool initialized;

    // Обёртка: в Unity 6 velocity переименовали в linearVelocity
    private Vector3 Velocity
    {
        get => rb.linearVelocity;
        set => rb.linearVelocity = value;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        gravity = GetComponent<PlayerGravity>();
    }

    private void Update()
    {
        // Ввод читаем в Update, применяем в FixedUpdate
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
    }

    private void FixedUpdate()
    {
        if (gravity.GravityTarget == null) 
            return;

        float dt = Time.fixedDeltaTime;
        Vector3 up = gravity.GravityUp;

        if (!initialized)
        {
            virtualForward = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, up).normalized;
            lastUp = up;
            initialized = true;
        }

        // Виртуальная камера: переносим вместе со сменой "верха", чтобы она не вращалась относительно планеты
        virtualForward = Quaternion.FromToRotation(lastUp, up) * virtualForward;
        virtualForward = Vector3.ProjectOnPlane(virtualForward, up).normalized;
        lastUp = up;

        // Выбираем систему отсчёта: настоящая камера или виртуальная
        Vector3 refForward = virtualForward;
        if (inputReference != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(inputReference.forward, up);
            if (f.sqrMagnitude > 0.0001f)
                refForward = f.normalized;
        }
        Vector3 refRight = Vector3.Cross(up, refForward);

        // Желаемое направление бега
        Vector3 wish = Vector3.ClampMagnitude(refForward * input.y + refRight * input.x, 1f);

        // Поворачиваем персонажа лицом по ходу движения
        if (wish.sqrMagnitude > 0.01f)
        {
            Vector3 charForward = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, up);
            float angle = Vector3.SignedAngle(charForward, wish, up);
            float maxStep = turnSpeed * dt;
            gravity.AddYaw(Mathf.Clamp(angle, -maxStep, maxStep));
        }

        // Раскладываем текущую скорость на вертикальную и тангенциальную
        Vector3 velocity = Velocity;
        Vector3 vertical = Vector3.Project(velocity, up);   // падение/подъём - не трогаем
        Vector3 tangential = velocity - vertical;           // бег - управляем

        Vector3 desired = wish * moveSpeed;
        float rate = desired.sqrMagnitude > 0.001f ? acceleration : deceleration;
        tangential = Vector3.MoveTowards(tangential, desired, rate * dt);

        Velocity = tangential + vertical;
    }
}
