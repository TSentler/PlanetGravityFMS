using UnityEngine;

public class EnemyHealth : MonoBehaviour
{


    public void TakeDamage(float damage)
    {
        Destroy(gameObject);
    }
}
