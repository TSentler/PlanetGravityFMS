using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnerHealth : MonoBehaviour
{
    public float Health = 100f;

    public void TakeDamage(float damage)
    {
        Health -= damage;
        if (Health <= 0f)
        {
            gameObject.SetActive(false);
        }
    }
}
