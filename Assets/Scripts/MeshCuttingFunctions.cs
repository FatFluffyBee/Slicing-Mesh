using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Mesh;

public static class MeshCuttingFunctions 
{
    public enum MeshSide { Up = 1, Down = 0 };
    public static List<MeshData> CutMeshByPlane(MeshData initMesh, Plane plane, int submeshIndexCut)
    {
        DateTime startTime = DateTime.Now;
        int nTrianglesProcessed = 0;

        //Check submesh index to see if we need a new submesh for cut face
        int subMeshCount = (submeshIndexCut + 1 > initMesh.subMeshes.Length)? submeshIndexCut+1 : initMesh.subMeshes.Length;
        int[][] subMeshTriangles = new int [initMesh.subMeshes.Length][];

        for(int i = 0; i < subMeshTriangles.Length; i++) {
            subMeshTriangles[i] = initMesh.subMeshes[i].ToArray();
        }

        List<MeshData> twoCutMeshesData = new List<MeshData>() { new MeshData(subMeshCount), new MeshData(subMeshCount)};
        List<Vector3> pointsAlongPlane = new List<Vector3>();
        List<Vector2> uvPointAlongPlane = new List<Vector2>();
        
        // True = top, false = bottom
        Vector3[] oVertices = initMesh.vertices.ToArray();
        List<Vector2> oUvs = initMesh.uvs;

        for(int j = 0; j < subMeshTriangles.Length; j++) {
            int[] oTriangles = subMeshTriangles[j];

            for (int i = 0; i < oTriangles.Length; i += 3) {
                nTrianglesProcessed++;

                Vector3 vertice0 = oVertices[oTriangles[i]];
                Vector3 vertice1 = oVertices[oTriangles[i + 1]];
                Vector3 vertice2 = oVertices[oTriangles[i + 2]];

                Vector2 uv0 = oUvs[oTriangles[i]];
                Vector2 uv1 = oUvs[oTriangles[i + 1]];
                Vector2 uv2 = oUvs[oTriangles[i + 2]];

                MeshSide side0 = plane.GetSide(vertice0) ? MeshSide.Up : MeshSide.Down;
                MeshSide side1 = plane.GetSide(vertice1) ? MeshSide.Up : MeshSide.Down;
                MeshSide side2 = plane.GetSide(vertice2) ? MeshSide.Up : MeshSide.Down;

                if (side0 == side1 && side1 == side2) {// 4 configurations possibles dependant de la façon dont le plan traverse ces points
                    AddTrianglesToMesh(ref twoCutMeshesData, vertice0, uv0, vertice1, uv1, vertice2, uv2, side0, j, subMeshCount);
                } else if (side1 == side2) {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice0, uv0, vertice1, uv1);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice0, uv0, vertice2, uv2);

                    AddTrianglesToMesh(ref twoCutMeshesData, vertice0, uv0, interA, uvA, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, interA, uvA, vertice1, uv1, vertice2, uv2, side1, j, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, vertice2, uv2, interB, uvB, interA, uvA, side1, j, subMeshCount);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    /*uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);*/

                } else if (side2 == side0) {
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice0, uv0, vertice1, uv1);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice1, uv1, vertice2, uv2);

                    AddTrianglesToMesh(ref twoCutMeshesData, vertice0, uv0, interA, uvA, vertice2, uv2, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, vertice2, uv2, interA, uvA, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, vertice1, uv1, interB, uvB, interA, uvA, side1, j, subMeshCount);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    /*uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);*/

                } else { //implicit side1 == side0
                    CalculateIntersectionPointAndUvs(out Vector3 interA, out Vector2 uvA, plane, vertice1, uv1, vertice2, uv2);
                    CalculateIntersectionPointAndUvs(out Vector3 interB, out Vector2 uvB, plane, vertice0, uv0, vertice2, uv2);

                    AddTrianglesToMesh(ref twoCutMeshesData, vertice0, uv0, vertice1, uv1, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, vertice1, uv1, interA, uvA, interB, uvB, side0, j, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, vertice2, uv2, interB, uvB, interA, uvA, side2, j, subMeshCount);

                    pointsAlongPlane.Add(interA);
                    pointsAlongPlane.Add(interB);
                    /*uvPointAlongPlane.Add(uvA);
                    uvPointAlongPlane.Add(uvB);*/
                }
            }
        }
            
        // Separare the list of all point of cut edge into their respective face group, if there is multiple different face group, AKA the mesh is not simple -> (  ) () 
        List<List<Vector3>> verticesAlongPlaneConcave = RegroupPointsByFace(pointsAlongPlane, uvPointAlongPlane);

        // Calculate geometry for each set of faces
        for (int index = 0; index < verticesAlongPlaneConcave.Count; index++) {
            pointsAlongPlane = verticesAlongPlaneConcave[index];
            //uvPointAlongPlane = uvPointsAlongPlaneConcave[index];

            //Calculate face center and uvs (temporaty solution cause only works for convex meshes)
            Vector3 faceCenter = Vector3.zero;
            for (int i = 0; i < pointsAlongPlane.Count; i++){
                faceCenter += pointsAlongPlane[i];
            }
            faceCenter /= pointsAlongPlane.Count;
            pointsAlongPlane.Add(faceCenter);
            
            //map the coordinates depending on the current pos of uvs
            //todo decide if its need option for different cuts, if it need to take a parameter for which mesh its taken off or if the cut is small or big for scale
           uvPointAlongPlane = ComputeUVsFromVertices(pointsAlongPlane, plane);

            //Create face for each mesh for each pair of point 
            for (int i = 0; i < pointsAlongPlane.Count - 1; i += 2){
                Vector3 normalFace = ComputeNormal(faceCenter, pointsAlongPlane[i], pointsAlongPlane[i + 1]);
                float direction = Vector3.Dot(normalFace, plane.normal); // return -1 if object is behing if not 1
                if (direction > 0) {
                    AddTrianglesToMesh(ref twoCutMeshesData, faceCenter, uvPointAlongPlane[^1], pointsAlongPlane[i], uvPointAlongPlane[i], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], MeshSide.Down, submeshIndexCut, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, faceCenter, uvPointAlongPlane[^1], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], pointsAlongPlane[i], uvPointAlongPlane[i], MeshSide.Up, submeshIndexCut, subMeshCount);
                } else {
                    AddTrianglesToMesh(ref twoCutMeshesData, faceCenter, uvPointAlongPlane[^1], pointsAlongPlane[i], uvPointAlongPlane[i], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], MeshSide.Up, submeshIndexCut, subMeshCount);
                    AddTrianglesToMesh(ref twoCutMeshesData, faceCenter, uvPointAlongPlane[^1], pointsAlongPlane[i + 1], uvPointAlongPlane[i + 1], pointsAlongPlane[i], uvPointAlongPlane[i], MeshSide.Down, submeshIndexCut, subMeshCount);
                }
            }
        }

        //Compile mesh data for return
        List<List<MeshData>> allSeparatedMeshData = new List<List<MeshData>>() {};
        foreach(MeshData meshData in twoCutMeshesData) {
            allSeparatedMeshData.Add(SeparateMeshDataByLinkedTriangles(meshData)); 
        }

        List<MeshData> finalMeshList = new List<MeshData>();
        foreach(List<MeshData> list in allSeparatedMeshData) { 
            foreach(MeshData meshData in list) {
                MeshData cleanedMeshData = RemoveDuplicatesVertices(meshData);
                finalMeshList.Add(cleanedMeshData);
            }
        }

        TimeSpan completionTime = DateTime.Now - startTime;
        Debug.Log("Processed took " + completionTime + " seconds, for a total of " + initMesh.vertices.Count + " vertices and " + nTrianglesProcessed + " triangles processed");
        return finalMeshList;
    }      

    static List<Vector2> ProjectVerticesOntoPlane(Plane plane, List<Vector3> vertices) {
        List<Vector2> projectedPoints = new List<Vector2>();
        Vector3 planeNormal = plane.normal;

        foreach (Vector3 point in vertices) {
            float distance = plane.GetDistanceToPoint(point);
            Vector2 projection = point - distance * planeNormal;
            projectedPoints.Add(projection);
        }

        return projectedPoints;
    }

    static Vector3 ComputeNormal(Vector3 pointA, Vector3 pointB, Vector3 pointC){
        return Vector3.Cross(pointB - pointA, pointC - pointA).normalized;
    }

    //separate each mesh data into differents island depending on connectivity 
    static List<MeshData> SeparateMeshDataByLinkedTriangles(MeshData meshData) { 
        List<MeshData> separatedMeshData = new List<MeshData>();
        int subMeshCount = meshData.subMeshes.Length;

        //Iterate for every set of triangles in meshData and add it to a new list of mesh data if linked to triangles in the list. If not create a new one. 
        //If present in more than one combine the list
        for(int i = 0; i < subMeshCount; i++) {
            for(int j = 0; j < meshData.subMeshes[i].Count; j+=3) {
                //get all three points of the triangles
                Vector3 pointA = meshData.vertices[meshData.subMeshes[i][j]];
                Vector3 pointB = meshData.vertices[meshData.subMeshes[i][j+1]];
                Vector3 pointC = meshData.vertices[meshData.subMeshes[i][j+2]];

                List<int> indicesSeenInTab = new List<int>();
                int finalIndice = -1;

                for(int x = 0; x < separatedMeshData.Count; x++) { //we check all the meshes data already created to see if the current triangle is linked to existing vertices
                    int indexPointA = separatedMeshData[x].vertices.IndexOf(pointA);
                    int indexPointB = separatedMeshData[x].vertices.IndexOf(pointB);
                    int indexPointC = separatedMeshData[x].vertices.IndexOf(pointC);

                    if(indexPointA != -1 || indexPointB != -1 || indexPointC != -1) {
                        indicesSeenInTab.Add(x);
                    }
                }
        
                if(indicesSeenInTab.Count == 1) { //reliée à une seule liste, on ne fait rien
                    finalIndice = indicesSeenInTab[0];
                } else if(indicesSeenInTab.Count > 1) { // reliée à plusieurs liste, on concatène les listes en partant de la fin 
                    int firstIndex = indicesSeenInTab[0];

                    for (int x = indicesSeenInTab.Count - 1; x > 0; x--) {//on parcourt toutes les listes à relier entre elle en partant de la fin
                        int currentIndex = indicesSeenInTab[x];
                        MeshData tmpCurrentMeshData = separatedMeshData[currentIndex];
                        int startVerticeNumber = separatedMeshData[firstIndex].vertices.Count;

                        foreach (Vector3 pos in tmpCurrentMeshData.vertices){
                            separatedMeshData[firstIndex].vertices.Add(pos);
                        }

                        foreach (Vector2 uv in tmpCurrentMeshData.uvs){
                            separatedMeshData[firstIndex].uvs.Add(uv);
                        }

                        for (int y = 0; y < tmpCurrentMeshData.subMeshes.Length; y++) {
                            foreach(int tri in tmpCurrentMeshData.subMeshes[y]) {
                                separatedMeshData[firstIndex].subMeshes[y].Add(tri + startVerticeNumber); //add to existing triangle without breaking continuity
                            }
                        }
                        separatedMeshData.RemoveAt(currentIndex);
                    }
                    finalIndice = indicesSeenInTab[0];
                }
                else { // not linked to an existing meshdata so we create a new ones
                    separatedMeshData.Add(new MeshData(subMeshCount));
                    finalIndice = separatedMeshData.Count - 1;
                }

                if(true) {
                    int triIndexA = meshData.subMeshes[i][j];
                    int triIndexB = meshData.subMeshes[i][j+1];
                    int triIndexC = meshData.subMeshes[i][j+2];
                    int lastTriangleIndex = separatedMeshData[finalIndice].vertices.Count;

                    separatedMeshData[finalIndice].vertices.Add(pointA);
                    separatedMeshData[finalIndice].vertices.Add(pointB);
                    separatedMeshData[finalIndice].vertices.Add(pointC);

                    separatedMeshData[finalIndice].uvs.Add(meshData.uvs[triIndexA]);
                    separatedMeshData[finalIndice].uvs.Add(meshData.uvs[triIndexB]);
                    separatedMeshData[finalIndice].uvs.Add(meshData.uvs[triIndexC]);

                    separatedMeshData[finalIndice].subMeshes[i].Add(lastTriangleIndex);
                    separatedMeshData[finalIndice].subMeshes[i].Add(lastTriangleIndex+1);
                    separatedMeshData[finalIndice].subMeshes[i].Add(lastTriangleIndex+2);
                } else {
                    int triIndexA = meshData.subMeshes[i][j];  
                    int indexA = separatedMeshData[finalIndice].vertices.IndexOf(pointA);
                    if(indexA == -1) {
                        separatedMeshData[finalIndice].vertices.Add(pointA);
                        separatedMeshData[finalIndice].uvs.Add(meshData.uvs[triIndexA]);
                        separatedMeshData[finalIndice].subMeshes[i].Add(separatedMeshData[finalIndice].vertices.Count-1);
                    } else {
                        separatedMeshData[finalIndice].subMeshes[i].Add(indexA);
                    }
                    
                    int triIndexB = meshData.subMeshes[i][j+1];  
                    int indexB = separatedMeshData[finalIndice].vertices.IndexOf(pointB);
                    if(indexB == -1) {
                        separatedMeshData[finalIndice].vertices.Add(pointB);
                        separatedMeshData[finalIndice].uvs.Add(meshData.uvs[triIndexB]);
                        separatedMeshData[finalIndice].subMeshes[i].Add(separatedMeshData[finalIndice].vertices.Count-1);
                    } else {
                        separatedMeshData[finalIndice].subMeshes[i].Add(indexB);
                    }

                    int triIndexC = meshData.subMeshes[i][j+2];  
                    int indexC = separatedMeshData[finalIndice].vertices.IndexOf(pointC);
                    if(indexC == -1) {
                        separatedMeshData[finalIndice].vertices.Add(pointC);
                        separatedMeshData[finalIndice].uvs.Add(meshData.uvs[triIndexC]);
                        separatedMeshData[finalIndice].subMeshes[i].Add(separatedMeshData[finalIndice].vertices.Count-1);
                    } else {
                        separatedMeshData[finalIndice].subMeshes[i].Add(indexC);
                    }
                }
            }
        }

        return separatedMeshData;
    }
    
    // ajouter face à un MeshData 
    static void AddTrianglesToMesh(ref List<MeshData> finalMeshesData, Vector3 pointA, Vector2 uv0, Vector3 pointB, Vector2 uv1, Vector3 pointC, 
        Vector2 uv2, MeshSide side, int subMeshIndex, int subMeshCount) {
        
        int indexSide = Convert.ToInt32(side);
        //todo check to reduce vertices probably here and make a list of common vertices not for each subamesh but one meshdata
        if(false) {
            int indexA = finalMeshesData[indexSide].vertices.IndexOf(pointA);
            if(indexA == -1) {
                finalMeshesData[indexSide].vertices.Add(pointA);
                finalMeshesData[indexSide].uvs.Add(uv0);
                finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(finalMeshesData[indexSide].vertices.Count -1);
            } else {
                finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(indexA);
            }

            int indexB = finalMeshesData[indexSide].vertices.IndexOf(pointB);
            if(indexB == -1) {
                finalMeshesData[indexSide].vertices.Add(pointB);
                finalMeshesData[indexSide].uvs.Add(uv1);
                finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(finalMeshesData[indexSide].vertices.Count -1);
            } else {
                finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(indexB);
            }

            int indexC = finalMeshesData[indexSide].vertices.IndexOf(pointC);
            if(indexC == -1) {
                finalMeshesData[indexSide].vertices.Add(pointC);
                finalMeshesData[indexSide].uvs.Add(uv2);
                finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(finalMeshesData[indexSide].vertices.Count -1);
            } else {
                finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(indexC);
            }
        } else {
            int subMeshTriangleIndex = finalMeshesData[indexSide].vertices.Count;

            finalMeshesData[indexSide].vertices.Add(pointA);
            finalMeshesData[indexSide].uvs.Add(uv0);
            finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(subMeshTriangleIndex);

            finalMeshesData[indexSide].vertices.Add(pointB);
            finalMeshesData[indexSide].uvs.Add(uv1);
            finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(subMeshTriangleIndex + 1);

            finalMeshesData[indexSide].vertices.Add(pointC);
            finalMeshesData[indexSide].uvs.Add(uv2);
            finalMeshesData[indexSide].subMeshes[subMeshIndex].Add(subMeshTriangleIndex + 2);
        }
    }

    //Calculate the intersection point of the plane and a segment AB, return the point and uvs
    static void CalculateIntersectionPointAndUvs(out Vector3 interPoint, out Vector2 uvPoint, Plane plane, Vector3 pointA, Vector2 uvA, Vector3 pointB, Vector2 uvB) { 
        Ray ray = new Ray(pointA, pointB - pointA);
        plane.Raycast(ray, out float distance);

        interPoint = pointA + (pointB - pointA).normalized * distance;
        float ratio = distance / Vector3.Distance(pointA, pointB);  
        uvPoint = uvA + (uvB - uvA) * ratio;
    }

    //sépare les points appartennant aux mêmes faces dans des liste séparées
    static List<List<Vector3>> RegroupPointsByFace(List<Vector3> verticesList, List<Vector2> uvList/*, out List<List<Vector2>> finalUvList*/) {
        if(verticesList.Count== 0)
            Debug.LogError("List of face is empty, the mesh is not supposed to be cut by the plane");

        List<Vector3> verticesListCopy = new List<Vector3>(verticesList);
        //List<Vector2> uvListCopy = new List<Vector2>(uvList);
       
        List<List<Vector3>> verticesAlongPlaneConcave = new List<List<Vector3>>() {new List<Vector3>() {verticesListCopy[0], verticesListCopy[1]}};
        //finalUvList = new List<List<Vector2>>() {new List<Vector2>() {uvListCopy[0], uvListCopy[1]}};

        //start first point group
        int remainingVertices = verticesListCopy.Count;
        int verticeGroupID = 0;
        Vector3 currentVertice = verticesListCopy[0];
        Vector3 endVertice = verticesListCopy[1]; //todo could add a check condition, longrr code but faster execution
        verticesListCopy.RemoveRange(0, 2);
        //uvListCopy.RemoveRange(0, 2);
        remainingVertices -= 2;
            
        while (remainingVertices > 0) {
            int firstIndexNextPair = -1;
            int secondIndexNextPair;

            //get indice of next pair or -1 if not
            for(int i = 0; i < verticesListCopy.Count; i++)
                if(currentVertice == verticesListCopy[i]) {
                    firstIndexNextPair = i;
                    break;
                }
            
            if(firstIndexNextPair != -1) {// other pair found, add them to list
                secondIndexNextPair = firstIndexNextPair % 2 == 0? firstIndexNextPair + 1 : firstIndexNextPair - 1;
            } else { // no other pair found, reset pair and go create next list
                firstIndexNextPair = 0;
                secondIndexNextPair = 1;
                verticesAlongPlaneConcave.Add(new List<Vector3>());
                //finalUvList.Add(new List<Vector2>());
                verticeGroupID++;
            }

            int removeIndex = firstIndexNextPair < secondIndexNextPair ? firstIndexNextPair : secondIndexNextPair;
            currentVertice = verticesListCopy[secondIndexNextPair]; 
            verticesAlongPlaneConcave[verticeGroupID].Add(verticesListCopy[firstIndexNextPair]);
            verticesAlongPlaneConcave[verticeGroupID].Add(verticesListCopy[secondIndexNextPair]);
            //finalUvList[verticeGroupID].Add(uvListCopy[firstIndexNextPair]);
            //finalUvList[verticeGroupID].Add(uvListCopy[secondIndexNextPair]);
            verticesListCopy.RemoveRange(removeIndex, 2);
            //uvListCopy.RemoveRange(removeIndex, 2);
            remainingVertices -= 2;
        }

        return verticesAlongPlaneConcave;
    }

    //remap vertices on the newly created face
    static List<Vector2> ComputeUVsFromVertices(List<Vector3> vertices, Plane plane) {
        //We remap vertices to a 2D plane, surprisingly the one wh ocut the mesh
        List<Vector2> remappedVertices = ProjectVerticesOntoPlane(plane, vertices);

        //we try to find the bounding box by taking the max and min x and y values
        float xMax = float.MinValue;
        float xMin = float.MaxValue;
        float yMax = float.MinValue;
        float yMin = float.MaxValue;

        foreach(Vector2 v in remappedVertices) {
            xMax = Mathf.Max(xMax, v.x);
            xMin = Mathf.Min(xMin, v.x);
            yMax = Mathf.Max(yMax, v.y);
            yMin = Mathf.Min(yMin, v.y);
        }

        float xRange = xMax - xMin;
        float yRange = yMax - yMin;

        //we convert the bounding box into a square 
        if(xRange < yRange) {
            xMin -= (yRange - xRange) / 2;
            xMax += (yRange - xRange) / 2;
            xRange = yRange;
        } else {
            yMin -= (xRange - yRange) / 2;
            yMax += (xRange - yRange) / 2;
            yRange = xRange;
        }

        List<Vector2> uvs = new List<Vector2>();
        foreach(Vector2 point in remappedVertices) {
            float xUV = (point.x - xMin) / xRange; 
            float yUV = (point.y - yMin) / yRange; 
            uvs.Add(new Vector2(xUV, yUV));
        }

        return uvs;
    }

    //remove duplicate vertices at the end //is not very efficient but might be necessary to avoid 1000+ vertices on small pieces without check at every steps
    static MeshData RemoveDuplicatesVertices(MeshData meshData) {
        
        var vert = meshData.vertices;
        var uvs = meshData.uvs;
        var subMs = meshData.subMeshes;
        List<Vector2Int> duplicateIndex = new List<Vector2Int>(); // (duplicate, first iteration)

        //iterate through vertices and uvs and report any correspondance
        for(int i = 0; i < vert.Count-1; i++) {
            for(int j = i+1; j < vert.Count; j++) {
                if(vert[i] == vert[j]) {
                    if(uvs[i] == uvs[j]) {
                        duplicateIndex.Add(new Vector2Int(j, i));
                        break;
                    }
                }
            }
        }

        duplicateIndex = duplicateIndex.OrderBy(duplicateIndex => duplicateIndex.x).ToList();

        //with the correspondance tables, supress duplicate vertices / uvs and change triangle value
        //foreach duplicate index line, supress vertices in question and decrement every triangles above value by 1
        for(int i = duplicateIndex.Count-1; i >= 0; i--) {
            Vector2Int key = duplicateIndex[i];
            vert.RemoveAt(key.x);
            uvs.RemoveAt(key.x);

            foreach(var triangles in subMs) {
                for(int j = 0; j < triangles.Count; j++) {
                    if(triangles[j] == key.x) triangles[j] = key.y;
                    else if(triangles[j] > key.x) triangles[j]--;
                }
            }
        }

        //return lighter meshdata
        MeshData copy = new MeshData(meshData.subMeshCount);
        copy.vertices = vert;
        copy.uvs = uvs;
        copy.subMeshes = subMs;
        return copy;
    }
}

public class MeshData
{
    public List<Vector3> vertices;
    public List<Vector2> uvs;
    public List<int> [] subMeshes;
    public int subMeshCount;

    public MeshData(int subMeshCount)
    {
        this.subMeshCount = subMeshCount;
        vertices = new List<Vector3>();
        uvs = new List<Vector2>();

        subMeshes = new List<int>[subMeshCount];

        for(int i = 0; i < subMeshCount; i++){
            subMeshes[i] = new List<int>();
        }
    }
}






/*static public List<List<Vector3>> RegroupPointsByFace(List<Vector3> verticesList, List<Vector2> uvList, out List<List<Vector2>> finalUvList) {//sépare les points appartennant aux mêmes faces dans des liste séparées
        List<Vector3> verticesListCopy = new List<Vector3>();
        List<Vector2> uvListCopy = new List<Vector2>();

        foreach(Vector3 vertice in verticesList)
            verticesListCopy.Add(vertice);

        foreach(Vector2 uv in uvList)
            uvListCopy.Add(uv);
       
        List<List<Vector3>> verticesAlongPlaneConcave = new List<List<Vector3>>();
        finalUvList = new List<List<Vector2>>();

        int count = verticesListCopy.Count;
        int currentIndex = 0;

        //start first point group
        verticesAlongPlaneConcave.Add(new List<Vector3>());
        finalUvList.Add(new List<Vector2>());
        verticesAlongPlaneConcave[0].Add(verticesListCopy[0]);
        verticesAlongPlaneConcave[0].Add(verticesListCopy[1]);
        finalUvList[0].Add(uvListCopy[0]);
        finalUvList[0].Add(uvListCopy[1]);
        Vector3 pointA = verticesListCopy[0];
        Vector3 pointB = verticesListCopy[1];
        verticesListCopy.RemoveAt(1);
        verticesListCopy.RemoveAt(0);
        uvListCopy.RemoveAt(1);
        uvListCopy.RemoveAt(0);
        count -= 2;

        while (count > 0) {
            bool endLine = true;
            //check all set of two points to check if a conformity exists
            for(int i = 0; i < count-1; i+=2) {
                if(pointA == verticesListCopy[i] || pointA == verticesListCopy[i+1] || pointB == verticesListCopy[i] || pointB == verticesListCopy[i+1]){
                    verticesAlongPlaneConcave[currentIndex].Add(verticesListCopy[i]);
                    verticesAlongPlaneConcave[currentIndex].Add(verticesListCopy[i + 1]);
                    finalUvList[currentIndex].Add(uvListCopy[i]);
                    finalUvList[currentIndex].Add(uvListCopy[i + 1]);
                    pointA = verticesListCopy[i];
                    pointB = verticesListCopy[i + 1];
                    verticesListCopy.RemoveAt(i+1);
                    verticesListCopy.RemoveAt(i);
                    uvListCopy.RemoveAt(i+1);
                    uvListCopy.RemoveAt(i);
                    count -= 2;
                    endLine = false;
                }
            }
            //end of tab so pass to next submesh
            if (endLine){
                verticesAlongPlaneConcave.Add(new List<Vector3>());
                finalUvList.Add(new List<Vector2>());
                currentIndex++;
                verticesAlongPlaneConcave[currentIndex].Add(verticesListCopy[0]);
                verticesAlongPlaneConcave[currentIndex].Add(verticesListCopy[1]);
                finalUvList[currentIndex].Add(uvListCopy[0]);
                finalUvList[currentIndex].Add(uvListCopy[1]);
                pointA = verticesListCopy[0];
                pointB = verticesListCopy[1];
                verticesListCopy.RemoveAt(1);
                verticesListCopy.RemoveAt(0);
                uvListCopy.RemoveAt(1);
                uvListCopy.RemoveAt(0);
                count -= 2;
            } 
        }

        return verticesAlongPlaneConcave;
    }*/