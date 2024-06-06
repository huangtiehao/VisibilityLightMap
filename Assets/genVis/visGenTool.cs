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
        public float4 a,b,c,d;
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
        ComputeBuffer visCoeBuffer = new ComputeBuffer(1024*1024, sizeof(float)*16);
        visCoeBuffer.SetData(visCoe);
        
        SHCoe[] lightCoe=new SHCoe[1024*1024];
        ComputeBuffer lightCoeBuffer = new ComputeBuffer(1024*1024, sizeof(float)*16);
        lightCoeBuffer.SetData(lightCoe);
            
        //Texture2D visLightMap = new Texture2D(SHtexture.width, SHtexture.height, TextureFormat.ARGB32, false);
        CommandBuffer commandBuffer = new CommandBuffer();
        commandBuffer.SetRenderTarget(renderTexture);
        commandBuffer.ClearRenderTarget(true, true, Color.black, 1f);
        commandBuffer.name = "Render My Mesh";
        //Graphics.SetRandomWriteTarget(1,visCoeBuffer);
        commandBuffer.SetRandomWriteTarget(1,visCoeBuffer);
        commandBuffer.SetGlobalBuffer("visCoe",visCoeBuffer);
        //Graphics.SetRandomWriteTarget(2,lightCoeBuffer);
        commandBuffer.SetRandomWriteTarget(2, lightCoeBuffer);
        commandBuffer.SetGlobalBuffer("lightCoe",lightCoeBuffer);
        
        commandBuffer.SetGlobalBuffer("triangles",triangleBuffer);
        commandBuffer.SetGlobalBuffer("bvhNodes",bvhNodesBuffer);
        commandBuffer.SetGlobalInt("triangleCnt",meshTriangles.Length);

        //Graphics.Blit(null, renderTexture, material, 0);
        commandBuffer.DrawMesh(mesh,Matrix4x4.identity, material);
        Graphics.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.ClearRandomWriteTargets();
        
        visCoeBuffer.GetData(visCoe);
        lightCoeBuffer.GetData(lightCoe);
        
        for (int i = 0; i < 10; ++i)
        {
            for (int j = 0; j < 10; ++j)
            {
                Debug.Log(i+" "+j+" "+visCoe[i*1024+j].a);
            }
        }

        Texture2D visCoeTex1 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        Texture2D visCoeTex2 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        Texture2D visCoeTex3 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        Texture2D visCoeTex4 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        Texture2D lightCoeTex1 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        Texture2D lightCoeTex2 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        Texture2D lightCoeTex3 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        Texture2D lightCoeTex4 = new Texture2D(1024, 1024,TextureFormat.RGBAFloat,false);
        
        float4[] visCoe1 = new float4[1024 * 1024];
        float4[] visCoe2 = new float4[1024 * 1024];
        float4[] visCoe3 = new float4[1024 * 1024];
        float4[] visCoe4 = new float4[1024 * 1024];
        
        float4[] lightCoe1 = new float4[1024 * 1024];
        float4[] lightCoe2 = new float4[1024 * 1024];
        float4[] lightCoe3 = new float4[1024 * 1024];
        float4[] lightCoe4 = new float4[1024 * 1024];
        for (int i = 0; i < 1024; ++i)
        {
            for (int j = 0; j < 1024; ++j)
            {
                visCoe1[i * 1024 + j] = visCoe[i * 1024 + j].a;
                visCoe2[i * 1024 + j] = visCoe[i * 1024 + j].b;
                visCoe3[i * 1024 + j] = visCoe[i * 1024 + j].c;
                visCoe4[i * 1024 + j] = visCoe[i * 1024 + j].d;
                lightCoe1[i * 1024 + j] = lightCoe[i * 1024 + j].a;
                lightCoe2[i * 1024 + j] = lightCoe[i * 1024 + j].b;
                lightCoe3[i * 1024 + j] = lightCoe[i * 1024 + j].c;
                lightCoe4[i * 1024 + j] = lightCoe[i * 1024 + j].d;
                if(visCoe[i * 1024 + j].a.x!=0)Debug.Log( visCoe[i * 1024 + j].a);
            }
        }
        
        RenderTexture.active = renderTexture;
        
        Texture2D visLightMap = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);
        // Prompt the user to save the file.
        visLightMap.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height),0,0);
        visLightMap.Apply();
        string visName = "visMap";
        string lightName = "lightMap";
        visCoeTex1.SetPixelData(visCoe1, 0);
        visCoeTex2.SetPixelData(visCoe2, 0);
        visCoeTex3.SetPixelData(visCoe3, 0);
        visCoeTex4.SetPixelData(visCoe4, 0);
        lightCoeTex1.SetPixelData(lightCoe1, 0);
        lightCoeTex2.SetPixelData(lightCoe2, 0);
        lightCoeTex3.SetPixelData(lightCoe3, 0);
        lightCoeTex4.SetPixelData(lightCoe4, 0);
        
        AssetDatabase.CreateAsset(visCoeTex1,savePath+visName+"1.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(visCoeTex2,savePath+visName+"2.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(visCoeTex3,savePath+visName+"3.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(visCoeTex4,savePath+visName+"4.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        AssetDatabase.CreateAsset(lightCoeTex1,savePath+lightName+"1.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(lightCoeTex2,savePath+lightName+"2.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(lightCoeTex3,savePath+lightName+"3.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(lightCoeTex4,savePath+lightName+"4.asset");
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