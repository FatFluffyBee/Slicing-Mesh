using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class ThreadManager : MonoBehaviour
{
    Queue<MapThreadInfo<List<MeshData>>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<List<MeshData>>>();
    
    public void RequestMeshData(Action<List<MeshData>, int> callback, MeshData oMesh, Plane plane, int submeshIndexCut, int meshIndex) {
        //ThreadStart threadStart = delegate { MeshDataThread(callback, oMesh, plane, submeshIndexCut); };
        ThreadStart threadStart = () => MeshDataThread(callback, oMesh, plane, submeshIndexCut, meshIndex);

        new Thread(threadStart).Start();
    }

    void MeshDataThread(Action<List<MeshData>, int> callback, MeshData oMesh, Plane plane, int submeshIndexCut, int meshIndex) {
        List<MeshData> meshesData = MeshCuttingFunctions.CutMeshByPlane(oMesh, plane, submeshIndexCut);

        lock (meshDataThreadInfoQueue){
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<List<MeshData>>(callback, meshesData, meshIndex));
        }
    }
    private void Update() {
        if (meshDataThreadInfoQueue.Count > 0){
            MapThreadInfo<List<MeshData>> threadInfo = meshDataThreadInfoQueue.Dequeue();
            threadInfo.callback(threadInfo.parameter, threadInfo.index);
        }
    }


    public struct MapThreadInfo<T> {
        public Action<T, int> callback;
        public T parameter;
        public int index;

        public MapThreadInfo(Action<T, int> callback, T parameter, int index) {
            this.callback = callback;
            this.parameter = parameter;
            this.index = index;
        }
    }
}
