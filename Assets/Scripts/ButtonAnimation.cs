using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonAnimation : MonoBehaviour
{
    public float Offset = 0.1f;

    private Vector3 position;

    private void Awake()
    {
        position = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayerGravity player = collision.gameObject.GetComponentInParent<PlayerGravity>();
        float distance = Vector3.Distance(position, transform.position);
        if (player != null 
            && Mathf.Abs(distance) < 0.01f)
        {
            transform.position -= transform.up * Offset;
        }
    }

    private void Update()
    {
        transform.position = 
            Vector3.Slerp(transform.position, position, Time.deltaTime);
    }

}
