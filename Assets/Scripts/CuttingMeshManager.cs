using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CuttingMeshManager : MonoBehaviour
{
    static ThreadManager threadManager;

    private List<IsCuttable> isCuttables = new List<IsCuttable>(); 
    private List<GameObject> originalGO = new List<GameObject>();
    private List<List<MeshData>> allMeshesData = new List<List<MeshData>>();

    [SerializeField] private bool processingCut;
    private int remainingProcess;

    void Awake ()
    {
        threadManager = FindObjectOfType<ThreadManager>();
    }

    void Update() 
    {
        if(remainingProcess == 0 && allMeshesData.Count > 0) {
            Debug.LogWarning("Updating mesh with sliced one");
            UpdateSliceMeshed();
        }
    }

    public bool CutMesh(List<Plane> planes, List<Mesh> meshes, List<GameObject> gameObjects)
    {
        if(processingCut) //if the previous cut is still being processed, cancel
            return false;

        remainingProcess = meshes.Count;
        originalGO = gameObjects;

        for(int i = 0; i < meshes.Count; i++) {
            isCuttables.Add(originalGO[i].GetComponent<IsCuttable>());

            if(isCuttables[i].isFirstCut){
                if(isCuttables[i].index <= -1) {// not set and need to be set 
                    Material[] meshMat = originalGO[i].GetComponent<MeshRenderer>().sharedMaterials;

                    for (int j = 0; j < meshMat.Length; j++){
                        if (meshMat[j] == isCuttables[i].matCut){
                            isCuttables[i].index = j;
                            break;
                        }
                    }

                    //if mesh as not the cut material on it, add a new empty submesh containing it 
                    if(isCuttables[i].index <= -1) { // if no mat was found
                        meshes[i].subMeshCount += 1;
                        meshes[i].SetTriangles(new int[0], meshes[i].subMeshCount-1);
                        isCuttables[i].index = meshMat.Length;
                        List<Material> materials = originalGO[i].GetComponent<MeshRenderer>().materials.ToList();
                        materials.Add(isCuttables[i].matCut);
                        originalGO[i].GetComponent<MeshRenderer>().SetMaterials(materials);
                    }
                }
            }

            //Initialize mesh data to pass to cut function
            MeshData initMeshData = new MeshData(meshes[i].subMeshCount);
            initMeshData.vertices = meshes[i].vertices.ToList();
            initMeshData.uvs = meshes[i].uv.ToList();
            for(int j = 0; j < meshes[i].subMeshCount; j++)
                initMeshData.subMeshes[j] = meshes[i].GetTriangles(j).ToList();

            //Start threading for each individual meshes
            threadManager.RequestMeshData(OnMeshesDataReceived, initMeshData, planes[i], isCuttables[i].index);
        }
        processingCut = true;
        return true;
    }

    void UpdateSliceMeshed() {
        for(int i = 0; i < allMeshesData.Count; i++) {
            for(int j = 0; j < allMeshesData[i].Count; j++) {
                Transform ogTransform = originalGO[i].transform;
                //Set basic meshes info
                Mesh newMesh = new Mesh();
                newMesh.vertices = allMeshesData[i][j].vertices.ToArray();
                newMesh.uv = allMeshesData[i][j].uvs.ToArray();

                //Set Submeshes Info
                newMesh.subMeshCount = allMeshesData[i][j].subMeshes.Length;

                for(int x = 0; x < newMesh.subMeshCount; x++) {
                    newMesh.SetTriangles(allMeshesData[i][j].subMeshes[x], x);
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
                instance.AddComponent<MeshRenderer>().SetMaterials(originalGO[i].GetComponent<MeshRenderer>().materials.ToList());
                instance.AddComponent<MeshCollider>().convex = true;
                Rigidbody newMeshRb = instance.AddComponent<Rigidbody>();
                newMeshRb.mass = ogTransform.GetComponent<Rigidbody>().mass;
                newMeshRb.velocity = ogTransform.GetComponent<Rigidbody>().velocity;

                IsCuttable newIsCut = instance.AddComponent<IsCuttable>();
                newIsCut.matCut = isCuttables[i].matCut;
                newIsCut.isFirstCut = false;
                newIsCut.index = isCuttables[i].index;
                
            }
            //Destroy old mesh
            Destroy(originalGO[i]);
        }     
        allMeshesData.Clear();
        isCuttables.Clear();
        originalGO.Clear();
        processingCut = false; 
    }

    void OnMeshesDataReceived(List<MeshData> meshesData) {
        allMeshesData.Add(meshesData);
        remainingProcess--;
    }
}
