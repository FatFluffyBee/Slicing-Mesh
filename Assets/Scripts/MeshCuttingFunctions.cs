using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Mesh;

public static class MeshCuttingFunctions 
{
    public enum MeshSide { Up = 1, Down = 0 };
    public static List<MeshData> CutMeshByPlane(Mesh oMesh, Plane plane, int submeshIndexCut)
    {
        //Calculate subMeshes index
        int subMeshCount = (submeshIndexCut + 1 > oMesh.subMeshCount)? submeshIndexCut+1 : oMesh.subMeshCount;
        int[][] subMeshTriangles = new int [oMesh.subMeshCount][];

        for(int i = 0; i < subMeshTriangles.Length; i++)
        {
            subMeshTriangles[i] = oMesh.GetTriangles(i);
        }

        List<List<MeshData>> meshesData = new List<List<MeshData>>() { new List<MeshData>(), new List<MeshData>() };
        List<Vector3> pointsAlongPlane = new List<Vector3>();
        List<Vector2> uvPointAlongPlane = new List<Vector2>();
        
        // True = top, false = bottom
        Vector3[] oVertices = oMesh.vertices;

        List<Vector2> oUvs = new List<Vector2>();
        oMesh.GetUVs(0, oUvs);

        for(int j = 0; j < subMeshTriangles.Length; j++)
        {
            int[] oTriangles = subMeshTriangles[j];

            for (int i = 0; i < oTriangles.Length; i += 3)
            {
                Vector3 vertice0 = oVertices[oTriangles[i]];
                Vector3 vertice1 = oVertices[oTriangles[i + 1]];
                Vector3 vertice2 = oVertices[oTriangles[i + 2]];

                Vector2 uv0 = oUvs[oTriangles[i]];
                Vector2 uv1 = oUvs[oTriangles[i + 1]];
                Vector2 uv2 = oUvs[oTriangles[i + 2]];

                MeshSide side0 = plane.GetSide(vertice0) ? MeshSide.Up : MeshSide.Down;
                MeshSide side1 = plane.GetSide(vertice1) ? MeshSide.Up : MeshSide.Down;
                MeshSide side2 = plane.GetSide(vertice2) ? MeshSide.Up : MeshSide.Down;


                if (side0 == side1 && side1 == side2) // 4 configurations possibles dependant de la façon dont le plan traverse ces points
                {
                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, vertice1, uv1, vertice2, uv2, side0, j, subMeshCount);
                }

                else if (side1 == side2)
                {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice0, uv0, vertice1, uv1);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice0, uv0, vertice2, uv2);

                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, interA, uvA, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, interA, uvA, vertice1, uv1, vertice2, uv2, side1, j, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, vertice2, uv2, interB, uvB, interA, uvA, side1, j, subMeshCount);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);
                }

                else if (side2 == side0)
                {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice0, uv0, vertice1, uv1);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice1, uv1, vertice2, uv2);

                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, interA, uvA, vertice2, uv2, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, vertice2, uv2, interA, uvA, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, vertice1, uv1, interB, uvB, interA, uvA, side1, j, subMeshCount);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);
                }
                else
                {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice1, uv1, vertice2, uv2);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice0, uv0, vertice2, uv2);

                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, vertice1, uv1, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, vertice1, uv1, interA, uvA, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, vertice2, uv2, interB, uvB, interA, uvA, side2, j, subMeshCount);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);
                }
            }
        }

        /// Separare the list of all point of cut edge into their respective face group (if there is multiple different face group, AKA the mesh is not simple -> (  ) () )
        List<List<Vector3>> pointsAlongPlaneConcave = RegroupPointsByFace(pointsAlongPlane);

        /// Calculate geometry for each set of faces
        for (int index = 0; index < pointsAlongPlaneConcave.Count; index++)
        {
            pointsAlongPlane = pointsAlongPlaneConcave[index];

            //Calcualte face center and uvs (tmp)
            Vector3 faceCenter = Vector3.zero;
            for (int i = 0; i < pointsAlongPlane.Count; i++)
            {
                faceCenter += pointsAlongPlane[i];
            }
            faceCenter /= pointsAlongPlane.Count;

            Vector2 uvFaceCenter = Vector2.zero;
            for (int i = 0; i < uvPointAlongPlane.Count; i++)
            {
                uvFaceCenter += uvPointAlongPlane[i];
            }
            uvFaceCenter /= uvPointAlongPlane.Count;

            //Create face for each mesh for each pair of point

            for (int i = 0; i < pointsAlongPlane.Count; i += 2)
            {
                Vector3 normalFace = ComputeNormal(faceCenter, pointsAlongPlane[i], pointsAlongPlane[i + 1]);
                float direction = Vector3.Dot(normalFace, plane.normal); // return -1 if object is behing if not 1

                //Debug.Log(faceCenter + " " + pointsAlongPlane[i] + " " + pointsAlongPlane[i+1] + " " + normalFace + " " + direction);

                if (direction > 0)
                {
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i], uvPointAlongPlane[i], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], MeshSide.Down, submeshIndexCut, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], pointsAlongPlane[i], uvPointAlongPlane[i], MeshSide.Up, submeshIndexCut, subMeshCount);
                }
                else
                {
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i], uvPointAlongPlane[i], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], MeshSide.Up, submeshIndexCut, subMeshCount);
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], pointsAlongPlane[i], uvPointAlongPlane[i], MeshSide.Down, submeshIndexCut, subMeshCount);
                }
            }
        }

        List<MeshData> finalMeshList = new List<MeshData>();

        foreach(List<MeshData> list in meshesData) 
        { 
            foreach(MeshData meshData in list)
            {
                finalMeshList.Add(meshData);
            }
        }

        return finalMeshList;
    }      

    public static Vector3 ComputeNormal(Vector3 pointA, Vector3 pointB, Vector3 pointC)
    {
        return Vector3.Cross(pointB - pointA, pointC - pointA).normalized;
    }

    //! hashset to check more efficitiently, takes up most of the performance and cause lag issue
    public static void AddTrianglesToMesh(ref List<List<MeshData>> meshesData, Vector3 pointA, Vector2 uv0, Vector3 pointB, Vector2 uv1, Vector3 pointC, 
    Vector2 uv2, MeshSide side, int subMeshIndex, int subMeshCount) // ajouter face à un MeshData 
    {
        Debug.Log("Triangle getting checked");
        int indexSide = Convert.ToInt32(side);

        // On vérifie si la face en train d'être créée est reliée à une géometrie existante (on compare les 3 pos avec les meshData existant)
        List<int> indexSeenInTab = new List<int>();
        int finalIndice = -1;

        for(int i = 0; i < meshesData[indexSide].Count; i++) { 
            for(int j = 0; j < meshesData[indexSide][i].vertices.Count; j++) {
                Vector3 currentVector = meshesData[indexSide][i].vertices[j];

                if (currentVector == pointA || currentVector == pointB || currentVector == pointC) {
                    indexSeenInTab.Add(i);
                    break;
                }
            }
        }

        if(indexSeenInTab.Count == 1) { //reliée à une seule liste, on ne fait rien
            Debug.Log("Link to only one mesh data");
            finalIndice = indexSeenInTab[0];
        }
        else if(indexSeenInTab.Count > 1) { // reliée à plusieurs liste, on concatène les listes en partant de la fin 
            Debug.Log("Link to plural mesh data");
            int firstIndex = indexSeenInTab[0];

            for (int i = indexSeenInTab.Count - 1; i > 0; i--) {//on parcourt toutes les listes à relier entre elle en partant de la fin
                int currentIndex = indexSeenInTab[i];
                MeshData tmpCurrentMeshData = meshesData[indexSide][currentIndex];
                int startVerticeNumber = meshesData[indexSide][firstIndex].vertices.Count;

                foreach (Vector3 pos in tmpCurrentMeshData.vertices){
                    meshesData[indexSide][firstIndex].vertices.Add(pos);
                }

                foreach (Vector2 uv in tmpCurrentMeshData.uvs){
                    meshesData[indexSide][firstIndex].uvs.Add(uv);
                }

                for (int x = 0; x < tmpCurrentMeshData.subMeshes.Length; x++) {
                    foreach(int tri in tmpCurrentMeshData.subMeshes[x]) {
                        meshesData[indexSide][firstIndex].subMeshes[x].Add(tri + startVerticeNumber);
                    }
                }

                meshesData[indexSide].RemoveAt(currentIndex);
            }

            finalIndice = indexSeenInTab[0];
        }
        else { // pas reliée à un MeshData existant, on crée un nouveau MeshData
            Debug.Log("Not link to existing MeshData");
            meshesData[indexSide].Add(new MeshData(subMeshCount));
            finalIndice = meshesData[indexSide].Count - 1;
        }

        meshesData[indexSide][finalIndice].vertices.Add(pointA);
        meshesData[indexSide][finalIndice].uvs.Add(uv0);
        meshesData[indexSide][finalIndice].subMeshes[subMeshIndex].Add(meshesData[indexSide][finalIndice].vertices.Count - 1);

        meshesData[indexSide][finalIndice].vertices.Add(pointB);
        meshesData[indexSide][finalIndice].uvs.Add(uv1);
        meshesData[indexSide][finalIndice].subMeshes[subMeshIndex].Add(meshesData[indexSide][finalIndice].vertices.Count - 1);

        meshesData[indexSide][finalIndice].vertices.Add(pointC);
        meshesData[indexSide][finalIndice].uvs.Add(uv2);
        meshesData[indexSide][finalIndice].subMeshes[subMeshIndex].Add(meshesData[indexSide][finalIndice].vertices.Count - 1);
    }

    public static void CalculateIntersectionPointAndUvs(out Vector3 interPoint, out Vector2 uvPoint, Plane plane, Vector3 pointA, Vector2 uvA, 
    Vector3 pointB, Vector2 uvB) {
        Ray ray = new Ray(pointA, pointB - pointA);

        plane.Raycast(ray, out float distance);
        float ratio = distance / Vector3.Distance(pointA, pointB);        
        
        interPoint = pointA + (pointB - pointA).normalized * distance;
        uvPoint = uvA + (uvB - uvA) * ratio;
    }

    static public List<List<Vector3>> RegroupPointsByFace(List<Vector3> pointList) {//sépare les points appartennant aux mêmes faces dans des liste séparées
        List<Vector3> pointListCopy = new List<Vector3>();

        foreach(Vector3 point in pointList)
            pointListCopy.Add(point);
       
        List<List<Vector3>> pointsAlongPlaneConcave = new List<List<Vector3>>();

        int count = pointListCopy.Count;
        int currentIndex = 0;

        pointsAlongPlaneConcave.Add(new List<Vector3>());
        pointsAlongPlaneConcave[0].Add(pointListCopy[0]);
        pointsAlongPlaneConcave[0].Add(pointListCopy[1]);
        Vector3 pointA = pointListCopy[0];
        Vector3 pointB = pointListCopy[1];
        pointListCopy.RemoveAt(1);
        pointListCopy.RemoveAt(0);
        count -= 2;

        while (count > 0) {
            bool endLine = true;

            for(int i = 0; i < count-1; i+=2) {
                if(pointA == pointListCopy[i] || pointA == pointListCopy[i+1] || pointB == pointListCopy[i] || pointB == pointListCopy[i+1]){
                    pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[i]);
                    pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[i + 1]);
                    pointA = pointListCopy[i];
                    pointB = pointListCopy[i + 1];
                    pointListCopy.RemoveAt(i+1);
                    pointListCopy.RemoveAt(i);
                    count -= 2;
                    endLine = false;
                }
            }

            if (endLine){
                pointsAlongPlaneConcave.Add(new List<Vector3>());
                currentIndex++;
                pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[0]);
                pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[1]);
                pointA = pointListCopy[0];
                pointB = pointListCopy[1];
                pointListCopy.RemoveAt(1);
                pointListCopy.RemoveAt(0);
                count -= 2;
            } 
        }

        return pointsAlongPlaneConcave;
    }

    public static bool CompareVector(Vector3 pointA, Vector3 pointB, float roundingError){
        return (pointA - pointB).magnitude < roundingError;
    }
}


public class MeshData
{
    public List<Vector3> vertices;
    public List<Vector2> uvs;
    public List<int> [] subMeshes;

    public MeshData(int subMeshCount)
    {
        vertices = new List<Vector3>();
        uvs = new List<Vector2>();

        subMeshes = new List<int>[subMeshCount];

        for(int i = 0; i < subMeshCount; i++){
            subMeshes[i] = new List<int>();
        }
    }
}







/*
  public static class MeshCuttingFunctions 
{
    public enum MeshSide { Up = 1, Down = 0 };
    const float roundingError = 0.000001f;
    public static MeshData[] CutMeshByPlane(Mesh oMesh, Plane plane, int submeshIndexCut)
    {
        //Calculate subMeshses index
        int subMeshCount = (submeshIndexCut + 1 > oMesh.subMeshCount)? submeshIndexCut+1 : oMesh.subMeshCount;
        int[][] subMeshTriangles = new int [oMesh.subMeshCount][];

        for(int i = 0; i < subMeshTriangles.Length; i++)
        {
            subMeshTriangles[i] = oMesh.GetTriangles(i);
        }

        MeshData[] meshesData = new MeshData[2] { new MeshData(subMeshCount), new MeshData(subMeshCount) };
        List<Vector3> pointsAlongPlane = new List<Vector3>();
        List<Vector2> uvPointAlongPlane = new List<Vector2>();
        
        // True = top, false = bottom
        Vector3[] oVertices = oMesh.vertices;

        List<Vector2> oUvs = new List<Vector2>();
        oMesh.GetUVs(0, oUvs);

        for(int j = 0; j < subMeshTriangles.Length; j++)
        {
            int[] oTriangles = subMeshTriangles[j];

            for (int i = 0; i < oTriangles.Length; i += 3)
            {
                Vector3 vertice0 = oVertices[oTriangles[i]];
                Vector3 vertice1 = oVertices[oTriangles[i + 1]];
                Vector3 vertice2 = oVertices[oTriangles[i + 2]];

                Vector2 uv0 = oUvs[oTriangles[i]];
                Vector2 uv1 = oUvs[oTriangles[i + 1]];
                Vector2 uv2 = oUvs[oTriangles[i + 2]];

                MeshSide side0 = plane.GetSide(vertice0) ? MeshSide.Up : MeshSide.Down;
                MeshSide side1 = plane.GetSide(vertice1) ? MeshSide.Up : MeshSide.Down;
                MeshSide side2 = plane.GetSide(vertice2) ? MeshSide.Up : MeshSide.Down;


                if (side0 == side1 && side1 == side2) // differentes valeurs d'intersection et vercies en fonction de la fa�on dont le plan traverse la face
                {
                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, vertice1, uv1, vertice2, uv2, side0, j);
                }

                else if (side1 == side2)
                {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice0, uv0, vertice1, uv1);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice0, uv0, vertice2, uv2);

                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, interA, uvA, interB, uvB, side0, j);
                    AddTrianglesToMesh(ref meshesData, interA, uvA, vertice1, uv1, vertice2, uv2, side1, j);
                    AddTrianglesToMesh(ref meshesData, vertice2, uv2, interB, uvB, interA, uvA, side1, j);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);
                }

                else if (side2 == side0)
                {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice0, uv0, vertice1, uv1);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice1, uv1, vertice2, uv2);

                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, interA, uvA, vertice2, uv2, side0, j);
                    AddTrianglesToMesh(ref meshesData, vertice2, uv2, interA, uvA, interB, uvB, side0, j);
                    AddTrianglesToMesh(ref meshesData, vertice1, uv1, interB, uvB, interA, uvA, side1, j);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);
                }
                else
                {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice1, uv1, vertice2, uv2);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice0, uv0, vertice2, uv2);

                    AddTrianglesToMesh(ref meshesData, vertice0, uv0, vertice1, uv1, interB, uvB, side0, j);
                    AddTrianglesToMesh(ref meshesData, vertice1, uv1, interA, uvA, interB, uvB, side0, j);
                    AddTrianglesToMesh(ref meshesData, vertice2, uv2, interB, uvB, interA, uvA, side2, j);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);
                }
            }
        }

        /// Separare the list of all point of cut edge into their respective face group (if there is multiple different face group, AKA the mesh is not simple -> (  ) () )
        List<List<Vector3>> pointsAlongPlaneConcave = RegroupPointsByFace(pointsAlongPlane);

        /// Calculate geometry for each set of faces
        for (int index = 0; index < pointsAlongPlaneConcave.Count; index++)
        {
            pointsAlongPlane = pointsAlongPlaneConcave[index];

            //Calcualte face center and uvs (tmp)
            Vector3 faceCenter = Vector3.zero;
            for (int i = 0; i < pointsAlongPlane.Count; i++)
            {
                faceCenter += pointsAlongPlane[i];
            }
            faceCenter /= pointsAlongPlane.Count;

            Vector2 uvFaceCenter = Vector2.zero;
            for (int i = 0; i < uvPointAlongPlane.Count; i++)
            {
                uvFaceCenter += uvPointAlongPlane[i];
            }
            uvFaceCenter /= uvPointAlongPlane.Count;

            //Create face for each mesh for each pair of point

            for (int i = 0; i < pointsAlongPlane.Count; i += 2)
            {
                Vector3 normalFace = ComputeNormal(faceCenter, pointsAlongPlane[i], pointsAlongPlane[i + 1]);
                float direction = Vector3.Dot(normalFace, plane.normal); // return -1 if object is behing if not 1

                //Debug.Log(faceCenter + " " + pointsAlongPlane[i] + " " + pointsAlongPlane[i+1] + " " + normalFace + " " + direction);

                if (direction > 0)
                {
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i], uvPointAlongPlane[i], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], MeshSide.Down, submeshIndexCut);
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], pointsAlongPlane[i], uvPointAlongPlane[i], MeshSide.Up, submeshIndexCut);
                }
                else
                {
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i], uvPointAlongPlane[i], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], MeshSide.Up, submeshIndexCut);
                    AddTrianglesToMesh(ref meshesData, faceCenter, uvFaceCenter, pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], pointsAlongPlane[i], uvPointAlongPlane[i], MeshSide.Down, submeshIndexCut);
                }
            }
        }

        return meshesData;
    }      

    public static Vector3 ComputeNormal(Vector3 pointA, Vector3 pointB, Vector3 pointC)
    {
        return Vector3.Cross(pointB - pointA, pointC - pointA).normalized;
    }

    public static void AddTrianglesToMesh(ref MeshData[] meshesData, Vector3 pointA, Vector2 uv0, Vector3 pointB,
    Vector2 uv1, Vector3 pointC, Vector2 uv2, MeshSide side, int subMeshIndex)
    { 
        int indexSide = Convert.ToInt32(side);

        meshesData[indexSide].vertices.Add(pointA);
        meshesData[indexSide].uvs.Add(uv0);
        meshesData[indexSide].subMeshes[subMeshIndex].Add(meshesData[indexSide].vertices.Count - 1);

        meshesData[indexSide].vertices.Add(pointB);
        meshesData[indexSide].uvs.Add(uv1);
        meshesData[indexSide].subMeshes[subMeshIndex].Add(meshesData[indexSide].vertices.Count - 1);

        meshesData[indexSide].vertices.Add(pointC);
        meshesData[indexSide].uvs.Add(uv2);
        meshesData[indexSide].subMeshes[subMeshIndex].Add(meshesData[indexSide].vertices.Count - 1);


    }

    public static void CalculateIntersectionPointAndUvs(out Vector3 interPoint, out Vector2 uvPoint, Plane plane, Vector3 pointA, Vector2 uvA, 
    Vector3 pointB, Vector2 uvB)
    {
        Ray ray = new Ray(pointA, pointB - pointA);
        float distance;

        plane.Raycast(ray, out distance);
        float ratio = distance / Vector3.Distance(pointA, pointB);        
        
        interPoint = pointA + (pointB - pointA).normalized * distance;
        uvPoint = uvA + (uvB - uvA) * ratio;
    }

    static public List<List<Vector3>> RegroupPointsByFace(List<Vector3> pointList) //sépare les points appartennant aux mêmes faces dans des liste séparées
    {
        List<Vector3> pointListCopy = new List<Vector3>();

        foreach(Vector3 point in pointList)
            pointListCopy.Add(point);
       
        List<List<Vector3>> pointsAlongPlaneConcave = new List<List<Vector3>>();

        int count = pointListCopy.Count;
        int currentIndex = 0;

        pointsAlongPlaneConcave.Add(new List<Vector3>());
        pointsAlongPlaneConcave[0].Add(pointListCopy[0]);
        pointsAlongPlaneConcave[0].Add(pointListCopy[1]);
        Vector3 pointA = pointListCopy[0];
        Vector3 pointB = pointListCopy[1];
        pointListCopy.RemoveAt(1);
        pointListCopy.RemoveAt(0);
        count -= 2;

        while (count > 0) 
        {
            bool endLine = true;

            for(int i = 0; i < count-1; i+=2) 
            {
                //Debug.Log("Iterating");
                if(pointA == pointListCopy[i] || pointA == pointListCopy[i+1] || pointB == pointListCopy[i] || pointB == pointListCopy[i+1])
                {
                    pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[i]);
                    pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[i + 1]);
                    pointA = pointListCopy[i];
                    pointB = pointListCopy[i + 1];
                    pointListCopy.RemoveAt(i+1);
                    pointListCopy.RemoveAt(i);
                    count -= 2;
                    endLine = false;
                }
            }

            if (endLine)
            {
                pointsAlongPlaneConcave.Add(new List<Vector3>());
                currentIndex++;
                pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[0]);
                pointsAlongPlaneConcave[currentIndex].Add(pointListCopy[1]);
                pointA = pointListCopy[0];
                pointB = pointListCopy[1];
                pointListCopy.RemoveAt(1);
                pointListCopy.RemoveAt(0);
                count -= 2;
            } 
        }

        return pointsAlongPlaneConcave;
    }

    public static bool CompareVector(Vector3 pointA, Vector3 pointB, float roundingError)
    {
        return ((pointA - pointB).magnitude < roundingError);
    }
}


public class MeshData
{
    public List<Vector3> vertices;
    public List<Vector2> uvs;
    public List<int> [] subMeshes;

    public MeshData(int subMeshCount)
    {
        vertices = new List<Vector3>();
        uvs = new List<Vector2>();

        subMeshes = new List<int>[subMeshCount];

        for(int i = 0; i < subMeshCount; i++)
        {
            subMeshes[i] = new List<int>();
        }
    }
}
*/