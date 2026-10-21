using UnityEngine;

/// <summary>
/// Зона гравитации планеты. Вход игрока в триггер включает притяжение к этой планете,
/// выход возвращает гравитацию другой зоне, в которой игрок ещё находится, или оставляет его в невесомости.
/// Вешается на дочерний объект планеты с SphereCollider (радиус больше самой планеты).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class GravityZone : MonoBehaviour
{
    [Tooltip("К чему притягивать игрока (центр планеты). Если пусто, берётся родитель, а если родителя нет, сам объект.")]
    [SerializeField] private Transform planetCenter;

    /// <summary>Центр притяжения этой зоны.</summary>
    public Transform Center => planetCenter;

    private void Reset()
    {
        // Срабатывает при добавлении компонента в редакторе: сразу делаем всё как надо
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        if (transform.parent != null) planetCenter = transform.parent;
    }

    private void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        if (planetCenter == null) planetCenter = transform.parent != null ? transform.parent : transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerGravity player = other.GetComponentInParent<PlayerGravity>();
        if (player != null) player.EnterZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerGravity player = other.GetComponentInParent<PlayerGravity>();
        if (player != null) player.ExitZone(this);
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<SphereCollider>();
        if (col == null) return;

        Vector3 s = transform.lossyScale;
        float radius = col.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z));

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.TransformPoint(col.center), radius);
    }
}