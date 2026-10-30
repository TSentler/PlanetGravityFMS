using UnityEngine;
using UnityEngine.Events;

public class Lever : MonoBehaviour
{
    public UnityEvent OnPressed;

    private void OnCollisionEnter(Collision collision)
    {
        OxyTank player = collision.gameObject.GetComponentInParent<OxyTank>();
        if (player != null)
        {
            OnPressed.Invoke();
        }
    }
}
