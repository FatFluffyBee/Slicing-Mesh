using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dissolution : MonoBehaviour
{
    MeshRenderer mR;
    Material material;
    public float min = -1.5f, max = 1.5f, current = 0f, timeToSwitch = 2f;
    bool goUp = true;

    void Start()
    {
        mR = GetComponent<MeshRenderer>();
        material = mR.material;
    }

    // Update is called once per frame
    void Update()
    {
        if (goUp && current >= max)
            goUp = false;
        else if(!goUp && current < min)
            goUp = true;

        if (goUp)
            current += (max - min) / (timeToSwitch / Time.deltaTime);
        else
            current -= (max - min) / (timeToSwitch / Time.deltaTime);

        material.SetFloat("CutOffHeight", current);
        Debug.Log(material.GetFloat("CutOffHeight"));

        mR.material = material;
    }
}
