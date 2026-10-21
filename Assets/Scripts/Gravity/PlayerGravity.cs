using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Притягивает ТОЛЬКО игрока к выбранному объекту (центру планеты)
/// и выравнивает его "верх" против направления гравитации.
/// Если цели нет, игрок в невесомости: сила не прикладывается, скорость сохраняется по инерции.
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
    private readonly List<GravityZone> activeZones = new List<GravityZone>();

    /// <summary>Направление "вверх" для персонажа (от центра планеты). В невесомости остаётся последним.</summary>
    public Vector3 GravityUp { get; private set; } = Vector3.up;

    /// <summary>Сила гравитации (м/с²). Нужна прыжку для расчёта скорости отрыва.</summary>
    public float Strength => gravityStrength;

    /// <summary>true, когда игрок не в зоне ни одной планеты.</summary>
    public bool IsWeightless => gravityTarget == null;

    /// <summary>Срабатывает при входе в невесомость (true) и при выходе из неё (false).</summary>
    public event Action<bool> WeightlessChanged;

    /// <summary>Текущая планета. null = невесомость.</summary>
    public Transform GravityTarget
    {
        get => gravityTarget;
        set
        {
            if (gravityTarget == value) return;

            bool wasWeightless = gravityTarget == null;
            gravityTarget = value;
            bool isWeightless = gravityTarget == null;

            if (wasWeightless != isWeightless) WeightlessChanged?.Invoke(isWeightless);
        }
    }

    /// <summary>Добавить поворот вокруг "верха" (применится в ближайшем шаге физики).</summary>
    public void AddYaw(float degrees) => pendingYaw += degrees;

    /// <summary>Игрок вошёл в зону. Гравитация переключается на её планету.</summary>
    public void EnterZone(GravityZone zone)
    {
        if (!activeZones.Contains(zone)) activeZones.Add(zone);
        GravityTarget = zone.Center;
    }

    /// <summary>Игрок вышел из зоны. Если это была текущая планета, переключаемся на оставшуюся или уходим в невесомость.</summary>
    public void ExitZone(GravityZone zone)
    {
        activeZones.Remove(zone);

        // Вышли из зоны, которая сейчас не главная: ничего не меняется
        if (gravityTarget != zone.Center) return;

        GravityTarget = FindNearestActiveCenter(); // null, если зон не осталось
    }

    private Transform FindNearestActiveCenter()
    {
        activeZones.RemoveAll(z => z == null); // зоны, которые успели уничтожить

        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (GravityZone z in activeZones)
        {
            float sqr = (z.Center.position - rb.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = z.Center;
            }
        }
        return best;
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
        if (gravityTarget == null)
        {
            pendingYaw = 0f; // невесомость: накопленный поворот не нужен
            return;
        }

        Vector3 toTarget = gravityTarget.position - rb.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;             // защита от деления на ноль в центре

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

    private void OnDrawGizmos()
    {
        if (gravityTarget == null) return;

        Vector3 toTarget = gravityTarget.position - transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 gravityDir = toTarget.normalized;

        // Линия к центру планеты
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, gravityTarget.position);

        // Направление гравитации (вниз)
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, gravityDir * 2f);
        Gizmos.DrawSphere(transform.position + gravityDir * 2f, 0.1f);

        // "Верх" персонажа (зелёный, должен совпадать с обратным направлением гравитации)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.up * 2f);
    }
}