using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawMesh : MonoBehaviour
{
    [SerializeField]
    private Material _material;

    private Mesh mesh;

    private Vector3[] _positions = new Vector3[]
    {
        new Vector3(-1,1,0),
        new Vector3(1,-1,0),
        new Vector3(-1, -1, 0),
        new Vector3(1,1,0),
    };

    private int[] _triangles = new int[] { 0,1,2,0,3,1};

    private Vector3[] _normals = new Vector3[]
    {
        new Vector3(0,0,-1),
        new Vector3(0,0,-1),
        new Vector3(0,0,-1),
        new Vector3(0,0,-1),
    };

    private Vector2[] _uvs = new Vector2[]
    {
        new Vector2(0,1),
        new Vector2(1,0),
        new Vector2(0,0),
        new Vector2(1,1)
    };

    // Start is called before the first frame update
    void Start()
    {
        mesh = new Mesh();

        mesh.vertices = _positions;
        mesh.triangles = _triangles;
        mesh.normals = _normals;
        mesh.uv = _uvs;

        mesh.RecalculateBounds();
    }

    // Update is called once per frame
    void Update()
    {
        Graphics.DrawMesh(mesh, Vector3.zero, Quaternion.identity, _material, 0);
    }
}
