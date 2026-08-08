using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureStudy : MonoBehaviour
{
    [SerializeField] private Texture2D texture;

    private Mesh _mesh;

    [SerializeField] RenderTexture rt;

    private Vector3[] _positions = new Vector3[]
    {
        new Vector3(-1,-1,0),
        new Vector3(-1,1,0),
        new Vector3(1,-1,0),
        new Vector3(1,1,0),

        new Vector3(-0.5f,-0.5f,0),
        new Vector3(-0.5f,0.5f,0),
        new Vector3(0.5f,-0.5f,0),
        new Vector3(0.5f,0.5f,0),
    };

    private int[] _triangles = new int[] { 0, 1, 2, 2, 1, 3,   4,5,6,6,5,7 };

    private Vector2[] _uvs = new Vector2[]
    {
        new Vector2(0,0),
        new Vector2(0,1f),
        new Vector2(1f,0),
        new Vector2(1f,1f),

        new Vector2(0f,0f),
        new Vector2(0f,1f),
        new Vector2(1f,0f),
        new Vector2(1f,1f),
    };

    private void Awake()
    {
        _mesh = new Mesh();
        _mesh.vertices = _positions;
        _mesh.triangles = _triangles;

        _mesh.uv = _uvs;
    }

    void Update()
    {
        Shader textureStudy = Shader.Find("Unlit/TextureStudy");
        Material m = new Material(textureStudy);
        //Shader.SetGlobalTexture(Shader.PropertyToID("_StudyTexture"), texture);
        m.SetTexture(/*"_StudyTexture"*/Shader.PropertyToID("_StudyTexture"), texture);
        //m.SetFloat("_test", 1);
        //Shader.SetGlobalFloat(Shader.PropertyToID("_test"), 1);
        Graphics.DrawMesh(_mesh, Vector3.zero, Quaternion.identity, m, 0);
        
    }
}
