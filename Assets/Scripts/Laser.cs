using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour
{
    LineRenderer lR;

    private void Start()
    {
        lR = GetComponent<LineRenderer>();
    }

    void Update()
    {
        float length = 100;

        RaycastHit hit;

        if(Physics.Raycast(transform.position, transform.forward, out hit, 100))
        {
            length = hit.distance;
        }

        lR.SetPosition(1, new Vector3 (0, 0, length));
    }
}
