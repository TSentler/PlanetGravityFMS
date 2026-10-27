using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OxyTank : MonoBehaviour
{
    public float Speed = 1f;
    public float RestoreSpeed = 1f;
    public PlayerGravity playerGravity;

    private float oxyCount = 100f;
    private float oxyMax = 100f;

    public float OxyCount => oxyCount;


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
