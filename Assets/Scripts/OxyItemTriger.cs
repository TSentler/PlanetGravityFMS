using UnityEngine;

public class OxyItemTriger : MonoBehaviour
{
    public float OxyAdd = 10f;

    private void OnTriggerEnter(Collider other)
    {
        OxyTank player = other.GetComponentInParent<OxyTank>();
        if (player != null)
        {
            player.AddOxy(OxyAdd);
            gameObject.SetActive(false);
        }
    }
}
