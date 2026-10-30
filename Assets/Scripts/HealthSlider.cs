using UnityEngine;
using UnityEngine.UI;

public interface IHealthTaker
{
    public float Health { get; }
}

[RequireComponent(typeof(Slider))]
public class HealthSlider : MonoBehaviour
{
    public MonoBehaviour source;

    private IHealthTaker healthTaker;
    private Slider slider;

    private void OnValidate()
    {
        if (source == null)
        {
            source = GetComponentInParent<IHealthTaker>() as MonoBehaviour;
        }
    }

    private void Awake()
    {
        healthTaker = source as IHealthTaker ?? GetComponentInParent<IHealthTaker>();
        slider = GetComponent<Slider>();
        
    }

    private void Update()
    {
        slider.value = healthTaker.Health;
    }
}
