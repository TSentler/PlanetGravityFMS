using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tramplin : MonoBehaviour
{
    public float Speed = 20f;

    private void OnCollisionEnter(Collision collision)
    {
        PlayerGravity player = collision.gameObject.GetComponentInParent<PlayerGravity>();
        if (player != null)
        {
            player.GetComponent<Rigidbody>()
                .AddForce(player.transform.up * Speed, ForceMode.Impulse);
        }
    }

}
