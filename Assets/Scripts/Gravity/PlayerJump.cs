using UnityEngine;

/// <summary>
/// Простой прыжок вдоль "верха" планеты. Высота задаётся в метрах,
/// скорость отрыва считается из силы гравитации, поэтому прыжок одинаков на любой планете.
/// Требует PlayerGravity и CapsuleCollider на том же объекте.
/// </summary>
[DefaultExecutionOrder(20)] // после PlayerMovement
[RequireComponent(typeof(Rigidbody), typeof(PlayerGravity), typeof(CapsuleCollider))]
public class PlayerJump : MonoBehaviour
{
    [Header("Прыжок")]
    [Tooltip("Желаемая высота прыжка в метрах")]
    [SerializeField] private float jumpHeight = 2f;

    [Header("Проверка земли")]
    [Tooltip("Запас под ногами, на котором ещё считается, что стоим на земле")]
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody rb;
    private PlayerGravity gravity;
    private CapsuleCollider capsule;
    private bool jumpRequested;

    public bool IsGrounded { get; private set; }

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
        capsule = GetComponent<CapsuleCollider>();
    }

    private void Update()
    {
        if (Input.GetButtonDown("Jump")) 
            jumpRequested = true;
    }

    private void FixedUpdate()
    {
        if (gravity.IsWeightless)
        {
            IsGrounded = false;
            jumpRequested = false;
            return;
        }

        Vector3 up = gravity.GravityUp;
        IsGrounded = CheckGround(up);

        if (jumpRequested)
        {
            jumpRequested = false;

            Vector3 velocity = Velocity;
            float verticalSpeed = Vector3.Dot(velocity, up);

            // Прыгаем только с земли и только если не летим вверх (защита от двойного прыжка)
            if (IsGrounded && verticalSpeed <= 0.5f)
            {
                float jumpSpeed = Mathf.Sqrt(2f * gravity.Strength * jumpHeight);

                // Заменяем вертикальную составляющую, тангенциальную (бег) оставляем
                Vector3 tangential = velocity - up * verticalSpeed;
                Velocity = tangential + up * jumpSpeed;
            }
        }
    }

    private bool CheckGround(Vector3 up)
    {
        Vector3 scale = transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(scale.x, scale.z);
        float halfHeight = capsule.height * 0.5f * scale.y;

        // Каст из центра капсулы вниз (относительно планеты). Сфера чуть меньше капсулы,
        // стартует внутри собственного коллайдера, поэтому себя не задевает.
        Vector3 origin = capsule.bounds.center;
        float castRadius = radius * 0.9f;
        float distance = Mathf.Max(0f, halfHeight - castRadius) + groundCheckDistance;

        return Physics.SphereCast(origin, castRadius, -up, out _, distance, groundMask,
                                  QueryTriggerInteraction.Ignore);
    }
}
