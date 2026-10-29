using UnityEngine;
using UnityEngine.UIElements;

public class PlayerGun : MonoBehaviour
{
    public GameObject Bullet;
    public Transform FirePointGravity;
    public Transform FirePointFlight;
    public float oxyCount = 5;

    private PlayerGravity playerGravity;
    private Transform FirePoint;

    private void Awake()
    {
        playerGravity = FindObjectOfType<PlayerGravity>();
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1") == false)
            return;

        GetComponent<OxyTank>().DecreaseOxy(oxyCount);

        if (playerGravity.IsWeightless)
        {
            FirePoint = FirePointFlight;
        }
        else
        {
            FirePoint = FirePointGravity;
        }

        PlanetBulletV2 bullet = Instantiate(Bullet, FirePoint.position, 
            Quaternion.LookRotation(FirePoint.forward)).GetComponent<PlanetBulletV2>();

        if (playerGravity.IsWeightless)
        {
            bullet.LaunchStraight(FirePoint.forward, transform);
        }
        else
        {
            bullet.Launch(playerGravity.GravityTarget, FirePoint.forward, transform);
        }
    }
}
