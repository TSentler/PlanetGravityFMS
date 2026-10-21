using UnityEngine;

/// <summary>
/// Камера от третьего лица для сферических планет.
/// Мышь крутит камеру вокруг игрока, а "верх" камеры плавно следует за гравитацией,
/// поэтому горизонт заворачивается постепенно, без рывков.
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
    [SerializeField] private float pitchMin = 5f;
    [SerializeField] private float pitchMax = 75f;
    [SerializeField] private float startPitch = 25f;
    [SerializeField] private bool lockCursor = true;

    [Header("Следование за гравитацией")]
    [Tooltip("Чем меньше, тем плавнее и ленивее заворачивается горизонт")]
    [SerializeField] private float upFollowSpeed = 5f;

    private Vector3 smoothedUp;
    private Vector3 heading;      // направление взгляда в касательной плоскости
    private float pitch;
    private bool initialized;

    private void Start()
    {
        pitch = startPitch;
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null 
            //|| target.GravityTarget == null
            )
            return;

        Transform player = target.transform;

        if (!initialized)
        {
            // GravityUp на первом кадре может быть ещё не посчитан, берём напрямую
            smoothedUp = (player.position - target.GravityTarget?.position ?? Vector3.zero).normalized;
            heading = Vector3.ProjectOnPlane(transform.forward, smoothedUp);
            if (heading.sqrMagnitude < 0.0001f) 
                heading = Vector3.ProjectOnPlane(player.forward, smoothedUp);
            heading.Normalize();
            initialized = true;
        }

        // 1. Плавно догоняем "верх" планеты и переносим направление взгляда вместе с ним
        Vector3 prevUp = smoothedUp;
        float follow = 1f - Mathf.Exp(-upFollowSpeed * Time.deltaTime);
        smoothedUp = Vector3.Slerp(smoothedUp, target.GravityUp, follow).normalized;

        heading = Quaternion.FromToRotation(prevUp, smoothedUp) * heading;
        heading = Vector3.ProjectOnPlane(heading, smoothedUp).normalized;

        // 2. Мышь: рыскание вокруг "верха", тангаж ограничен
        float mx = Input.GetAxis("Mouse X") * sensitivity;
        float my = Input.GetAxis("Mouse Y") * sensitivity;

        heading = Quaternion.AngleAxis(mx, smoothedUp) * heading;
        pitch = Mathf.Clamp(pitch - my, pitchMin, pitchMax);

        // 3. Направление взгляда с учётом тангажа (положительный тангаж = смотрим вниз)
        Vector3 right = Vector3.Cross(smoothedUp, heading);
        Vector3 viewDir = Quaternion.AngleAxis(pitch, right) * heading;

        // 4. Ставим камеру
        Vector3 pivot = player.position + smoothedUp * height;
        transform.position = pivot - viewDir * distance;
        transform.rotation = Quaternion.LookRotation(viewDir, smoothedUp);
    }
}
