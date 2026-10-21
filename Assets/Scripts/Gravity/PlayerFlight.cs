using UnityEngine;

/// <summary>
/// Свободный полёт в невесомости. Тяга меняет скорость, торможения нет, пока не нажата кнопка тормоза.
/// WASD - вперёд/назад/в стороны по камере, E/Q - вверх/вниз относительно камеры, X - тормоз.
/// Работает только когда PlayerGravity сообщает о невесомости. Требует PlayerGravity на том же объекте.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(PlayerGravity))]
public class PlayerFlight : MonoBehaviour
{
    [Header("Полёт")]
    [Tooltip("Ускорение от тяги, м/с²")]
    [SerializeField] private float thrust = 15f;
    [Tooltip("Максимальная скорость, которую можно набрать тягой")]
    [SerializeField] private float maxSpeed = 20f;
    [Tooltip("Торможение на кнопку, м/с²")]
    [SerializeField] private float brakeDeceleration = 30f;

    [Header("Клавиши")]
    [SerializeField] private KeyCode upKey = KeyCode.E;
    [SerializeField] private KeyCode downKey = KeyCode.Q;
    [SerializeField] private KeyCode brakeKey = KeyCode.X;

    [Header("Ориентация персонажа")]
    [Tooltip("true: цилиндр летит 'головой' по взгляду камеры; false: сохраняет вертикальное положение относительно камеры")]
    [SerializeField] private bool headFirst = true;
    [Tooltip("Чем больше, тем быстрее персонаж разворачивается за камерой")]
    [SerializeField] private float turnSharpness = 8f;

    [Header("Система отсчёта ввода")]
    [Tooltip("Камера. Если пусто, берётся Camera.main.")]
    [SerializeField] private Transform inputReference;

    private Rigidbody rb;
    private PlayerGravity gravity;
    private Vector3 input;   // x: вбок, y: вверх/вниз, z: вперёд/назад
    private bool braking;

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
        gravity = GetComponent<PlayerGravity>();

        // Для инерции сопротивление среды должно быть нулевым
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = 0f;
#else
        rb.drag = 0f;
#endif
    }

    private void Update()
    {
        input.x = Input.GetAxisRaw("Horizontal");
        input.z = Input.GetAxisRaw("Vertical");
        input.y = (Input.GetKey(upKey) ? 1f : 0f) - (Input.GetKey(downKey) ? 1f : 0f);
        braking = Input.GetKey(brakeKey);
    }

    private void FixedUpdate()
    {
        if (!gravity.IsWeightless) return;

        if (inputReference == null && Camera.main != null) inputReference = Camera.main.transform;
        if (inputReference == null) return;

        float dt = Time.fixedDeltaTime;

        // 1. Скорость: тяга или торможение
        Vector3 velocity = Velocity;

        if (braking)
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, brakeDeceleration * dt);
        }
        else
        {
            Vector3 wish = inputReference.right * input.x
                         + inputReference.up * input.y
                         + inputReference.forward * input.z;
            wish = Vector3.ClampMagnitude(wish, 1f);

            if (wish.sqrMagnitude > 0.0001f)
            {
                Vector3 next = velocity + wish * (thrust * dt);

                // Лимит не режет скорость, полученную извне (например, при вылете с планеты),
                // но тяга не даёт разогнаться выше предела
                float limit = Mathf.Max(maxSpeed, velocity.magnitude);
                if (next.magnitude > limit) next = next.normalized * limit;

                velocity = next;
            }
        }
        Velocity = velocity;

        // 2. Ориентация: следуем за камерой
        Quaternion target = inputReference.rotation;
        if (headFirst) target *= Quaternion.Euler(90f, 0f, 0f); // ось Y капсулы смотрит туда же, куда камера

        float t = 1f - Mathf.Exp(-turnSharpness * dt);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, t));
    }
}