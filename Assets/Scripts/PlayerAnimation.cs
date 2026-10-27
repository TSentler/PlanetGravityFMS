using UnityEngine;

/// <summary>
/// Переключает анимации через триггеры Animator: ToIdle, ToRun, ToJump, ToFloat.
/// Ничего не меняет в других скриптах, только читает их состояние:
/// - PlayerGravity.IsWeightless -> ToFloat (невесомость)
/// - PlayerJump.IsGrounded      -> ToJump (в воздухе у планеты)
/// - скорость вдоль поверхности -> ToRun / ToIdle
/// Повесить на игрока. Animator берётся из поля или из дочерних объектов (модели).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(PlayerGravity), typeof(PlayerJump))]
public class PlayerAnimation : MonoBehaviour
{
    private enum State { None, Idle, Run, Jump, Float }

    [Header("Animator")]
    [Tooltip("Если пусто, ищется в дочерних объектах")]
    [SerializeField] private Animator animator;

    [Header("Имена триггеров")]
    [SerializeField] private string toIdleTrigger = "ToIdle";
    [SerializeField] private string toRunTrigger = "ToRun";
    [SerializeField] private string toJumpTrigger = "ToJump";
    [SerializeField] private string toFloatTrigger = "ToFloat";

    [Header("Пороги")]
    [Tooltip("Скорость по поверхности, с которой включается бег, м/с")]
    [SerializeField] private float runStartSpeed = 0.6f;
    [Tooltip("Скорость, ниже которой бег выключается (меньше стартовой, чтобы не дребезжало), м/с")]
    [SerializeField] private float runStopSpeed = 0.3f;
    [Tooltip("Сколько секунд без земли под ногами нужно, чтобы считать, что мы в воздухе. Гасит мелкие отрывы на неровностях")]
    [SerializeField] private float airborneDelay = 0.1f;
    [Tooltip("Если скорость вверх больше этого значения, прыжок включается сразу, без задержки, м/с")]
    [SerializeField] private float jumpSpeedThreshold = 1.5f;

    private Rigidbody rb;
    private PlayerGravity gravity;
    private PlayerJump jump;

    private int idleHash, runHash, jumpHash, floatHash;
    private State current = State.None;
    private float airTime;

    // Обёртка: в Unity 6 velocity переименовали в linearVelocity
    private Vector3 Velocity
    {
#if UNITY_6000_0_OR_NEWER
        get => rb.linearVelocity;
#else
        get => rb.velocity;
#endif
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        gravity = GetComponent<PlayerGravity>();
        jump = GetComponent<PlayerJump>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("PlayerAnimation: Animator не найден, скрипт отключён.", this);
            enabled = false;
            return;
        }

        idleHash = Animator.StringToHash(toIdleTrigger);
        runHash = Animator.StringToHash(toRunTrigger);
        jumpHash = Animator.StringToHash(toJumpTrigger);
        floatHash = Animator.StringToHash(toFloatTrigger);

        ValidateParameters();
    }

    private void Update()
    {
        State next = Evaluate();
        if (next == current) return;

        current = next;
        SetState(next);
    }

    private State Evaluate()
    {
        // 1. Невесомость
        if (gravity.IsWeightless)
        {
            airTime = 0f;
            return State.Float;
        }

        // 2. Разложение скорости относительно "верха" планеты
        Vector3 up = gravity.GravityUp;
        Vector3 velocity = Velocity;
        float vertical = Vector3.Dot(velocity, up);
        float surfaceSpeed = (velocity - up * vertical).magnitude;

        // 3. Воздух: нет земли дольше задержки или резкий взлёт
        if (jump.IsGrounded) airTime = 0f;
        else airTime += Time.deltaTime;

        bool airborne = airTime >= airborneDelay
                        || (!jump.IsGrounded && vertical > jumpSpeedThreshold);
        if (airborne) return State.Jump;

        // 4. Земля: бег или покой (с гистерезисом)
        bool running = current == State.Run ? surfaceSpeed > runStopSpeed
                                            : surfaceSpeed > runStartSpeed;
        return running ? State.Run : State.Idle;
    }

    private void SetState(State state)
    {
        // Сбрасываем все триггеры, чтобы не накапливалась очередь устаревших переходов
        animator.ResetTrigger(idleHash);
        animator.ResetTrigger(runHash);
        animator.ResetTrigger(jumpHash);
        animator.ResetTrigger(floatHash);

        switch (state)
        {
            case State.Idle:  animator.SetTrigger(idleHash);  break;
            case State.Run:   animator.SetTrigger(runHash);   break;
            case State.Jump:  animator.SetTrigger(jumpHash);  break;
            case State.Float: animator.SetTrigger(floatHash); break;
        }
    }

    private void ValidateParameters()
    {
        CheckTrigger(toIdleTrigger);
        CheckTrigger(toRunTrigger);
        CheckTrigger(toJumpTrigger);
        CheckTrigger(toFloatTrigger);
    }

    private void CheckTrigger(string triggerName)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == triggerName && p.type == AnimatorControllerParameterType.Trigger) return;
        }
        Debug.LogWarning($"PlayerAnimation: в Animator нет триггера \"{triggerName}\".", this);
    }
}
