using UnityEngine;

/// <summary>
/// Зона гравитации планеты. Когда игрок входит в триггер, его гравитация переключается на эту планету.
/// Вешается на дочерний объект планеты с SphereCollider (радиус больше самой планеты).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class GravityZone : MonoBehaviour
{
    [Tooltip("К чему притягивать игрока (центр планеты). Если пусто, берётся родитель, а если родителя нет, сам объект.")]
    [SerializeField] private Transform planetCenter;

    private SphereCollider zone;

    private void Reset()
    {
        // Срабатывает при добавлении компонента в редакторе: сразу делаем всё как надо
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        if (transform.parent != null)
            planetCenter = transform.parent;
    }

    private void Awake()
    {
        zone = GetComponent<SphereCollider>();
        zone.isTrigger = true;
        if (planetCenter == null) 
            planetCenter = transform.parent != null ? transform.parent : transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerGravity player = other.GetComponentInParent<PlayerGravity>();
        if (player == null || player.GravityTarget == planetCenter) 
            return;

        player.GravityTarget = planetCenter;
    }
}
