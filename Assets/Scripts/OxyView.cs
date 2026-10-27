using System;
using TMPro;
using UnityEngine;

public class OxyView : MonoBehaviour
{
    public TMP_Text text;
    public OxyTank oxyTank;

    private void Awake()
    {
        oxyTank = FindObjectOfType<OxyTank>();
    }

    public void Update()
    {
        text.text = Convert.ToInt32(oxyTank.OxyCount).ToString();
    }
}
