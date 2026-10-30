using UnityEngine;

public class ItemTimer : MonoBehaviour
{
    public float time = 30;
    public GameObject item;
    
    private float cerrentTime;

    private void Update()
    {
        if (item.activeSelf)
        {
            cerrentTime = time;
            return;
        }
        cerrentTime -= Time.deltaTime;
        if (cerrentTime < 0)
        {
            item.SetActive(true);
        }
    }
}
