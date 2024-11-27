using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class ThreadManager : MonoBehaviour
{
    Queue<MapThreadInfo<List<MeshData>>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<List<MeshData>>>();
    
    public void RequestMeshData(Action<List<MeshData>> callback, MeshData oMesh, Plane plane, int submeshIndexCut)
    {
        //ThreadStart threadStart = delegate { MeshDataThread(callback, oMesh, plane, submeshIndexCut); };
        ThreadStart threadStart = () => MeshDataThread(callback, oMesh, plane, submeshIndexCut);

        new Thread(threadStart).Start();
    }

    void MeshDataThread(Action<List<MeshData>> callback, MeshData oMesh, Plane plane, int submeshIndexCut)
    {
        List<MeshData> meshesData = MeshCuttingFunctions.CutMeshByPlane(oMesh, plane, submeshIndexCut);

        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<List<MeshData>>(callback, meshesData));
        }
    }
    private void Update()
    {
        if (meshDataThreadInfoQueue.Count > 0){
            MapThreadInfo<List<MeshData>> threadInfo = meshDataThreadInfoQueue.Dequeue();
            threadInfo.callback(threadInfo.parameter);
        }
    }


    public struct MapThreadInfo<T>
    {
        public Action<T> callback;
        public T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}
