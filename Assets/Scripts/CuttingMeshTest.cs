using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public static class CuttingMeshTest
{
    public static bool CutMesh(Plane plane, Mesh mesh, GameObject originalMesh)
    {
        IsCuttable isCut = originalMesh.GetComponent<IsCuttable>();

        if(isCut.isFirstCut){
            if(isCut.index <= -1) {// not set and need to be set 
                Material[] meshMat = originalMesh.GetComponent<MeshRenderer>().sharedMaterials;

                for (int i = 0; i < meshMat.Length; i++){
                    if (meshMat[i] == isCut.matCut){
                        isCut.index = i;
                        break;
                    }
                }

                if(isCut.index <= -1) { // if no mat was found
                    isCut.index = meshMat.Length;
                    List<Material> materials = originalMesh.GetComponent<MeshRenderer>().materials.ToList();
                    materials.Add(isCut.matCut);
                    originalMesh.GetComponent<MeshRenderer>().SetMaterials(materials);
                }
            }
        }

        MeshData[] meshDatas = MeshCuttingFunctions.CutMeshByPlane(mesh, plane, isCut.index).ToArray();
        Transform ogTransform = originalMesh.transform;

        foreach(MeshData meshData in meshDatas) { 
            //Set basic meshes info
            Mesh newMesh = new Mesh();
            newMesh.vertices = meshData.vertices.ToArray();
            newMesh.uv = meshData.uvs.ToArray();

            //Set Submeshes Info
            newMesh.subMeshCount = meshData.subMeshes.Length;

            for(int i = 0; i < newMesh.subMeshCount; i++) {
                newMesh.SetTriangles(meshData.subMeshes[i], i);
            }
            newMesh.RecalculateNormals();
            newMesh.RecalculateBounds();

            //Creating new Object transform
            GameObject instance = new GameObject();
            instance.transform.position = ogTransform.position;
            instance.transform.rotation = ogTransform.rotation;
            instance.transform.localScale = ogTransform.localScale;
            instance.transform.tag = ogTransform.tag;

            //Initializing material and mesh
            instance.AddComponent<MeshFilter>().mesh = newMesh;
            instance.AddComponent<MeshRenderer>().SetMaterials(originalMesh.GetComponent<MeshRenderer>().materials.ToList());
            instance.AddComponent<MeshCollider>().convex = true;
            Rigidbody newMeshRb = instance.AddComponent<Rigidbody>();
            newMeshRb.mass = ogTransform.GetComponent<Rigidbody>().mass;
            newMeshRb.velocity = ogTransform.GetComponent<Rigidbody>().velocity;

            IsCuttable newIsCut = instance.AddComponent<IsCuttable>();
            newIsCut.matCut = isCut.matCut;
            newIsCut.isFirstCut = false;
            newIsCut.index = isCut.index;
        }

        return true;
    }
}
