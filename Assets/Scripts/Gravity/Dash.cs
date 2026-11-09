using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dash : MonoBehaviour
{
    public float Speed = 20f;
    public float Oxy = 20f;
    public Transform FXPoint;
    public GameObject FXPrefab;
    public AudioSource AudioSource;
    public AudioClip DashSound;

    private OxyTank oxyTank;
    private Rigidbody rb;

    private void Awake()
    {
        oxyTank = GetComponent<OxyTank>();
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            rb.AddForce(transform.up * Speed, ForceMode.Impulse);
            oxyTank.DecreaseOxy(Oxy);
            FX();
        }
    }

    public void DecreaseOxy()
    {
        oxyTank.DecreaseOxy(Oxy);
    }

    private void FX()
    {
        Instantiate(FXPrefab, FXPoint.position, Quaternion.identity);
        AudioSource.PlayOneShot(DashSound);
    }
}
