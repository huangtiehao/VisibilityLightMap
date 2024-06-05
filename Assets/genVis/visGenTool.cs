using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using genVis;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;


public class VisGenTool : EditorWindow
{
    
    private Texture3D sdf;
    private MeshFilter selectedMesh;
    private GameObject obj;
    private string savePath = "Assets/genVis/tex/";
    [MenuItem("Tools/visGen")]
    private static void ShowWindow()
    {
        GetWindow<VisGenTool>("visGen");
    }
    public struct SHCoe
    {
        public float3 a,b,c;
    }
    private void OnGUI()
    {
        GUILayout.Label("Export Mesh Info", EditorStyles.boldLabel);
        selectedMesh = (MeshFilter)EditorGUILayout.ObjectField("Mesh", selectedMesh, typeof(MeshFilter), true);
        obj = EditorGUILayout.ObjectField("obj", obj,typeof(GameObject),true)as GameObject;
        savePath = EditorGUILayout.TextField("Save Path", savePath);
        
        if (GUILayout.Button("choose path"))
        {
            string defaultName = selectedMesh != null ? selectedMesh.name : "MeshInfo";
            string filePath = EditorUtility.SaveFilePanel("Save Mesh Info","", defaultName, "");

            if (!string.IsNullOrEmpty(filePath))
            {
                savePath = filePath; // 更新保存路径
            }
        }

        if (GUILayout.Button("Export"))
        {
            // if (string.IsNullOrEmpty(savePath))
            // {
            //     EditorUtility.DisplayDialog("Error", "Please enter a valid save path.", "OK");
            //     return;
            // }
            
            Mesh mesh = selectedMesh.sharedMesh;
            if (selectedMesh == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a mesh to export.", "OK");
                return;
            }
            ExportVisLightMap(); // 导出 Mesh 的vis信息

        }
    }
    
    private void ExportVisLightMap()
    {
        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        Mesh mesh = selectedMesh.sharedMesh;
        mesh.RecalculateBounds();
        
        Vector3[] meshVertices = mesh.vertices;
        int[] meshTriangles = mesh.GetTriangles(0);
        Triangle[] triangleArray = new Triangle[meshTriangles.Length / 3];
        for (int t = 0; t < triangleArray.Length; t++) {
            triangleArray[t].a = meshVertices[meshTriangles[3 * t + 0]];  // - mesh.bounds.center;
            triangleArray[t].b = meshVertices[meshTriangles[3 * t + 1]];  // - mesh.bounds.center;
            triangleArray[t].c = meshVertices[meshTriangles[3 * t + 2]];  // - mesh.bounds.center;
        }

        List<BVHNode> nodes=new List<BVHNode>();
        BVHNode emptyNode;
        emptyNode.left = 0;
        emptyNode.right = 0;
        emptyNode.index = 0;
        emptyNode.n = 0;
        emptyNode.AA = new float3(0, 0, 0);
        emptyNode.BB = new float3(0, 0, 0);
        nodes.Add(emptyNode);
        //Debug.Log(nodes.Count);
        Debug.Log(triangleArray.Length);
        BVH.buildBVH(triangleArray, nodes, 0, triangleArray.Length-1, 8);
        //Debug.Log(nodes.Count);
        // for (int i = 0; i < nodes.Count; ++i)
        // {
        //     Debug.Log(i+" "+ nodes[i].left+" "+nodes[i].right+" "+nodes[i].n); 
        // }
        
        // Create a buffer to hold the triangles, and upload them to the buffer.
        ComputeBuffer triangleBuffer = new ComputeBuffer(triangleArray.Length, sizeof(float) * 3 * 3);
        triangleBuffer.SetData(triangleArray);

        BVHNode[] bvhNodes = nodes.ToArray();
        
        ComputeBuffer bvhNodesBuffer = new ComputeBuffer(bvhNodes.Length, sizeof(float) * 10);
        bvhNodesBuffer.SetData(bvhNodes);
        
        // float3 _MinExtents;
        // float3 _MaxExtents;
        // float3 _Center;
        Material material = meshRenderer.sharedMaterial;
        
        RenderTexture renderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true, // 如果你需要在Compute Shader中写入纹理
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        SHCoe[] visCoe=new SHCoe[1024*1024];
        ComputeBuffer visCoeBuffer = new ComputeBuffer(1024*1024, sizeof(float)*9);
        visCoeBuffer.SetData(visCoe);
            
        //Texture2D visLightMap = new Texture2D(SHtexture.width, SHtexture.height, TextureFormat.ARGB32, false);
        CommandBuffer commandBuffer = new CommandBuffer();
        commandBuffer.SetRenderTarget(renderTexture);
        commandBuffer.ClearRenderTarget(true, true, Color.black, 1f);
        commandBuffer.name = "Render My Mesh";
        commandBuffer.SetRandomWriteTarget(1,visCoeBuffer);
        commandBuffer.SetGlobalBuffer("visCoe",visCoeBuffer);
        commandBuffer.SetGlobalBuffer("triangles",triangleBuffer);
        commandBuffer.SetGlobalBuffer("bvhNodes",bvhNodesBuffer);
        commandBuffer.SetGlobalInt("triangleCnt",meshTriangles.Length);

        //Graphics.Blit(null, renderTexture, material, 0);
        commandBuffer.DrawMesh(mesh,Matrix4x4.identity, material);
        //Graphics.ExecuteCommandBuffer(commandBuffer);
        
        visCoeBuffer.GetData(visCoe);
        
        for (int i = 0; i < 1024; ++i)
        {
            for (int j = 0; j < 1024; ++j)
            {
                if(visCoe[i*1024+j].a.x!=0)Debug.Log(i+" "+j+" "+visCoe[i*1024+j].a);
            }
        }

        Texture2D SHCoeTex = new Texture2D(1024, 1024);

        
        
        RenderTexture.active = renderTexture;
        
        Texture2D visLightMap = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
        // Prompt the user to save the file.
        visLightMap.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height),0,0);
        visLightMap.Apply();
        string fileName = "visMap";
        SHCoeTex.SetPixelData(visCoe, 0);
        
        AssetDatabase.CreateAsset(SHCoeTex,savePath+fileName+".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        //Debug.Log("generate "+savePath+"/"+fileName+" sdf success");
        triangleBuffer.Release();
        bvhNodesBuffer.Release();
        visCoeBuffer.Release();
        nodes.Clear();
        Debug.Log(1);
    }
    
    private string GenerateMeshInfo(Mesh mesh)
    {
        return $"Mesh name: {mesh.name}\nVertices: {mesh.vertexCount}\nTriangles: {mesh.triangles.Length / 3}";
    }
}