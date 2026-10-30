using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnerHealth : MonoBehaviour, IHealthTaker
{
    public float MaxHealth = 100f;
    private float Health;

    float IHealthTaker.Health => Health / MaxHealth;

    private void Awake()
    {
        Health = MaxHealth;
    }

    public void TakeDamage(float damage)
    {
        Health -= damage;
        if (Health <= 0f)
        {
            gameObject.SetActive(false);
        }
        Debug.Log(Health);
    }
}
