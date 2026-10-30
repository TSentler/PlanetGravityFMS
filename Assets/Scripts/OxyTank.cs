using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OxyTank : MonoBehaviour
{
    public float Speed = 1f;
    public float RestoreSpeed = 1f;
    public PlayerGravity playerGravity;
    public float oxyMax = 100f;

    private float oxyCount = 100f;

    public float OxyCount => oxyCount;

    public static void LvlUp( float value)
    {
        FindObjectOfType<OxyTank>().oxyMax += 20f;

    }

    internal void AddOxy(float oxy)
    {
        oxyCount += oxy;
        oxyCount = Mathf.Min(oxyCount, oxyMax);
    }

    internal void DecreaseOxy(float oxy)
    {
        oxyCount -= oxy;
    }

    private void Awake()
    {
        playerGravity = FindObjectOfType<PlayerGravity>();
    }

    private void Update()
    {
        if (oxyCount <= 0f)
        {
            oxyCount = 0f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (playerGravity.GravityTarget == null)
        {
            oxyCount -= Speed * Time.deltaTime;
        }
        else
        {
            oxyCount += RestoreSpeed * Time.deltaTime;
            oxyCount = Mathf.Min(oxyCount, oxyMax);
        }

    }
}
