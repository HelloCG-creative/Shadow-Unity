using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugFrustom : MonoBehaviour
{
    [SerializeField] private Camera camera;
    
    [SerializeField]  private float shadowDistance;

    [SerializeField] private Vector3[] debugs = new Vector3[8];
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var frustum8 = new Vector3[8];
        GetCameraFrustumCornersWS(camera, shadowDistance, frustum8);
        DrawFrustum(frustum8, Color.red, 10);
        debugs= frustum8;
    }
    
    public static void DrawFrustum(Vector3[] c, Color col, float duration = 0)
    {
        //if (c == null || c.Length != 8) return;

        // Near plane
        Debug.DrawLine(c[0], c[1], col, duration);
        Debug.DrawLine(c[1], c[2], col, duration);
        Debug.DrawLine(c[2], c[3], col, duration);
        Debug.DrawLine(c[3], c[0], col, duration);

        // Far plane
        Debug.DrawLine(c[4], c[5], col, duration);
        Debug.DrawLine(c[5], c[6], col, duration);
        Debug.DrawLine(c[6], c[7], col, duration);
        Debug.DrawLine(c[7], c[4], col, duration);

        // Sides
        Debug.DrawLine(c[0], c[4], col, duration);
        Debug.DrawLine(c[1], c[5], col, duration);
        Debug.DrawLine(c[2], c[6], col, duration);
        Debug.DrawLine(c[3], c[7], col, duration);
    }
    
    private void GetCameraFrustumCornersWS(Camera cam, float maxDistance, Vector3[] out8)
    {
        // out8: 0-3 near, 4-7 far の順（任意でOK、統一する）
        float near = cam.nearClipPlane;
        float far = Mathf.Min(cam.farClipPlane, maxDistance);

        var t = cam.transform;
        float fov = cam.fieldOfView * Mathf.Deg2Rad;
        float aspect = cam.aspect;

        float nearH = Mathf.Tan(fov * 0.5f) * near;
        float nearW = nearH * aspect;
        float farH  = Mathf.Tan(fov * 0.5f) * far;
        float farW  = farH * aspect;

        Vector3 Cn = t.position + t.forward * near;
        Vector3 Cf = t.position + t.forward * far;

        Vector3 up = t.up;
        Vector3 right = t.right;

        // near
        out8[0] = Cn - right * nearW - up * nearH;
        out8[1] = Cn + right * nearW - up * nearH;
        out8[2] = Cn + right * nearW + up * nearH;
        out8[3] = Cn - right * nearW + up * nearH;
        // far
        out8[4] = Cf - right * farW - up * farH;
        out8[5] = Cf + right * farW - up * farH;
        out8[6] = Cf + right * farW + up * farH;
        out8[7] = Cf - right * farW + up * farH;
    }
}
