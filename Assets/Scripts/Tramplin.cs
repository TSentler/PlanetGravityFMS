using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tramplin : MonoBehaviour
{
    public float Speed = 20f;

    private Vector3 position;

    private void Awake()
    {
        position = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayerGravity player = collision.gameObject.GetComponentInParent<PlayerGravity>();
        if (player != null)
        {
            player.GetComponent<Rigidbody>()
                .AddForce(player.transform.up * Speed, ForceMode.Impulse);

            transform.position += player.GravityUp * 0.1f;
        }
    }

    private void Update()
    {
        transform.position = 
            Vector3.Slerp(transform.position, position, Time.deltaTime);
    }

}
