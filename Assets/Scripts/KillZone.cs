using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class KillZone : MonoBehaviour
{
    public GameObject FXPrefab;
    public AudioSource AudioSource;
    public AudioClip DashSound;

    private void OnTriggerEnter(Collider other)
    {
        PlayerGravity player = other.GetComponentInParent<PlayerGravity>();
        if (player != null) 
        {
            player.gameObject.SetActive(false);
            Instantiate(FXPrefab, player.transform.position, Quaternion.identity);
            AudioSource.PlayOneShot(DashSound);

            //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
