using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpwaner : MonoBehaviour
{
    public GameObject Enemy;
    public Transform SpawnPoint;
    public float time = 3f;
    public int maxCount = 5;

    private float currentTime;
    private float currentCount;

    private void Update()
    {
        currentCount = GetComponentsInChildren<Rigidbody>().Length;
        if (currentCount >= maxCount)
            return;

        currentTime -= Time.deltaTime;
        if (currentTime <= 0f)
        {
            GameObject enemy = Instantiate(Enemy, SpawnPoint.position, Quaternion.identity);
            enemy.transform.SetParent(transform);
            currentTime = time;
            currentCount++;
        }
    }

    
}
