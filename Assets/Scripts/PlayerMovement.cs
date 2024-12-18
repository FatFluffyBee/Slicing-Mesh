using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    static CuttingMeshManager cuttingMeshManager;

    public bool debug = false;
    public bool cutAllMesh = false;

    Rigidbody rb;
    public Transform camera;
    public Transform aimer;

    public float speed;
    public float camSensitivity;
    public float laserSpeed;

    private void Start()
    {
        cuttingMeshManager = FindObjectOfType<CuttingMeshManager>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        //Movement related
        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        Vector3 newVelocity = -transform.forward * inputX + transform.right * inputY;
        newVelocity *= speed;

        rb.velocity = newVelocity;

        //Camera and Input related
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        transform.Rotate(Vector3.up * mouseX * camSensitivity, Space.World);
        camera.Rotate(Vector3.left * mouseY * camSensitivity, Space.Self);

        //Laser related
        if (Input.GetMouseButton(1)){
            aimer.transform.Rotate(Vector3.forward * laserSpeed * Time.deltaTime, Space.Self);
        }

        if (Input.GetMouseButtonDown(0)){
            List<RaycastHit> hitMeshes = new List<RaycastHit>();

            if (cutAllMesh) {
                RaycastHit[] hits = Physics.RaycastAll(aimer.position, aimer.forward);
                foreach (RaycastHit hit in hits) { 
                    if(hit.collider?.tag == "Cuttable")
                        hitMeshes.Add(hit);
                }
            } else {
                RaycastHit hit;
                if(Physics.Raycast(aimer.position, aimer.forward, out hit)) {
                    if (hit.collider?.tag == "Cuttable")
                        hitMeshes.Add(hit);
                }
            }
            
            if (hitMeshes.Count > 0) {
                List<Mesh> meshes = new List<Mesh>();
                List<GameObject> originalMeshes = new List<GameObject>();
                List<Plane> planes = new List<Plane>();

                for(int i = 0; i < hitMeshes.Count; i++) {
                    Mesh mesh = hitMeshes[i].collider.GetComponent<MeshFilter>().mesh;

                    Plane plane = new Plane(aimer.up, hitMeshes[i].point);

                    //Convert plane to world origin
                    Quaternion rotation = Quaternion.FromToRotation(hitMeshes[i].transform.up, plane.normal);
                    Vector3 newPlaneNormal = hitMeshes[i].transform.InverseTransformDirection(aimer.up);

                    Vector3 newPoint = hitMeshes[i].transform.InverseTransformPoint(hitMeshes[i].point);

                    Plane planeRelativeToMesh = new Plane(newPlaneNormal, newPoint);
                    planes.Add(planeRelativeToMesh);

                    if(debug){
                        Debug.DrawLine(hitMeshes[i].point, hitMeshes[i].point + plane.normal * 15, Color.blue, 30f);
                        Debug.DrawLine(newPoint, newPoint + planeRelativeToMesh.normal * 15, Color.green, 30f);

                        Debug.Log("Impact point : " + hitMeshes[i].point);
                        Debug.Log("Second point : " + newPoint);
                    }

                    meshes.Add(mesh);
                    originalMeshes.Add(hitMeshes[i].collider.gameObject);
                }

                cuttingMeshManager.CutMesh(planes, meshes, originalMeshes);
                //EditorApplication.isPaused = true;
            }
        }
    }
}
