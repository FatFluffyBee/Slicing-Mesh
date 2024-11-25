using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsCuttable : MonoBehaviour
{
    public bool isFirstCut = true;
    public Material matCut;
    public int index = -1; //index of the material which will fill the cut. Based is -1 cause not present till cut
                            // but processed meshes will have it to the index of the cut material in the renderer.
}
