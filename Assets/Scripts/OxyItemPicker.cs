using UnityEngine;
using UnityEngine.SceneManagement;

public class OxyItemPicker : MonoBehaviour
{
    public float Oxy = 15f;

    private void OnTriggerEnter(Collider other)
    {
        OxyTank player = other.GetComponentInParent<OxyTank>();
        if (player != null)
        {
            player.AddOxy(Oxy);
            //gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    float time = 1;
    float currentTime;
    private void Update()
    {
        if (gameObject.activeSelf)
        {
            currentTime = time;
            return;
        }

        currentTime -= Time.deltaTime;
        if (currentTime <= 0)
        {
            gameObject.SetActive(true);
        }
    }
}
