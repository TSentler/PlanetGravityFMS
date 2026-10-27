using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerGravity player = other.GetComponentInParent<PlayerGravity>();
        if (player != null) 
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
