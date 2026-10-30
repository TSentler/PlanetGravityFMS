using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Управляет заражением планеты через материал с шейдером Custom/URP/InfectedPlanet.
/// Infect() растит заражение до 1, Liberate() откатывает до 0, и планета возвращается к исходному виду.
/// Значения передаются через MaterialPropertyBlock, материал-ассет не меняется.
/// Повесить на планету (пивот в центре планеты).
/// </summary>
public class PlanetInfection : MonoBehaviour
{
    public Transform LiberationWorldPoint;

    [Header("Рендереры планеты")]
    [Tooltip("Если пусто, берутся все Renderer на этом объекте и в дочерних")]
    [SerializeField] private Renderer[] renderers;

    [Header("Состояние")]
    [Tooltip("0 = чистая планета, 1 = заражена полностью. Это главный источник правды: слайдер материала в рантайме игнорируется.")]
    [SerializeField, Range(0f, 1f)] private float infection = 1f;

    [Header("Анимация")]
    [SerializeField] private float liberateDuration = 3f;
    [SerializeField] private float infectDuration = 3f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>Срабатывает, когда планета полностью освобождена.</summary>
    public event Action Liberated;

    /// <summary>Срабатывает, когда планета заражена полностью.</summary>
    public event Action Infected;

    /// <summary>Текущая степень заражения, 0..1.</summary>
    public float Infection => infection;

    public bool IsLiberated => infection <= 0f;
    public bool IsInfected => infection >= 1f;

    private static readonly int InfectionId = Shader.PropertyToID("_Infection");
    private static readonly int OriginId = Shader.PropertyToID("_Origin");

    private MaterialPropertyBlock block;
    private Coroutine routine;
    private Vector3 originLocal = Vector3.up; // направление источника в локальных координатах планеты
    private bool hasOrigin;

    private void Awake()
    {
        EnsureRenderers();

        Apply();
    }

    private void OnValidate()
    {
        // Позволяет двигать слайдер в инспекторе и сразу видеть результат
        EnsureRenderers();
        Apply();
    }

    /// <summary>Мгновенно задать степень заражения.</summary>
    public void SetInfection(float value)
    {
        infection = Mathf.Clamp01(value);
        Apply();
    }

    /// <summary>Задать точку, от которой расходится заражение и к которой оно сжимается при освобождении (мировые координаты).</summary>
    public void SetOrigin(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-6f) return; // точка в самом центре, направление не определено

        originLocal = local.normalized;
        hasOrigin = true;
        Apply();
    }

    /// <summary>Освободить планету: заражение плавно сжимается к источнику.</summary>
    [ContextMenu("Liberate")]
    public void Liberate() => StartAnimation(0f, liberateDuration);

    /// <summary>Освободить планету, начав очищение от указанной точки (например, от позиции игрока).</summary>
    public void Liberate(Vector3 worldPoint)
    {
        SetOrigin(worldPoint);
        Liberate();
    }

    public void LiberateAtWorldPoint()
    {
        if (LiberationWorldPoint == null) 
            return;

        Liberate(LiberationWorldPoint.position);
    }

    /// <summary>Заразить планету: заражение плавно растёт от источника.</summary>
    [ContextMenu("Infect")]
    public void Infect() => StartAnimation(1f, infectDuration);

    /// <summary>Заразить планету, начав от указанной точки.</summary>
    public void Infect(Vector3 worldPoint)
    {
        SetOrigin(worldPoint);
        Infect();
    }

    private void StartAnimation(float target, float duration)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(target, duration));
    }

    private IEnumerator Animate(float target, float duration)
    {
        float start = infection;

        if (Mathf.Approximately(start, target))
        {
            routine = null;
            yield break; // уже в нужном состоянии, событие не повторяем
        }

        if (duration > 0f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = curve.Evaluate(Mathf.Clamp01(t));
                SetInfection(Mathf.LerpUnclamped(start, target, k));
                yield return null;
            }
        }

        SetInfection(target);
        routine = null;

        if (target <= 0f) Liberated?.Invoke();
        else if (target >= 1f) Infected?.Invoke();
    }

    private void EnsureRenderers()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>();
        }
    }

    private void Apply()
    {
        if (renderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        Vector3 worldDir = transform.TransformDirection(originLocal);

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(block);
            block.SetFloat(InfectionId, infection);

            if (hasOrigin)
            {
                // Шейдеру нужно направление в локальных координатах самого рендерера
                Vector3 dir = r.transform.InverseTransformDirection(worldDir);
                block.SetVector(OriginId, new Vector4(dir.x, dir.y, dir.z, 0f));
            }

            r.SetPropertyBlock(block);
        }
    }
}
