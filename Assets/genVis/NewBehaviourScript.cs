// using System;
// using System.Runtime.InteropServices;
// using System.Threading;
// using System.Threading.Tasks;
// using DefaultNamespace;
// using Unity.Mathematics;
// using Unity.VisualScripting;
// using UnityEngine;
// using UnityEditor;
// using UnityEngine.Rendering;
// using VoxelSystem;
//
//
// public class MeshInfoExporter : EditorWindow
// {
//     private MeshFilter selectedMesh;
//     private GameObject obj;
//     private string savePath = "";
//     [MenuItem("Tools/Voxelization")]
//     private static void ShowWindow()
//     {
//         GetWindow<MeshInfoExporter>("Voxelization");
//     }
//     
//     private void OnGUI()
//     {
//         
//         GUILayout.Label("Export Mesh Info", EditorStyles.boldLabel);
//         selectedMesh = (MeshFilter)EditorGUILayout.ObjectField("Mesh", selectedMesh, typeof(MeshFilter), true);
//         obj = EditorGUILayout.ObjectField("obj", obj,typeof(GameObject),true)as GameObject;
//         savePath = EditorGUILayout.TextField("Save Path", savePath);
//         
//         if (GUILayout.Button("choose path"))
//         {
//             string defaultName = selectedMesh != null ? selectedMesh.name : "MeshInfo";
//             string directory = string.IsNullOrEmpty(savePath) ? Application.dataPath : System.IO.Path.GetDirectoryName(savePath);
//             string filePath = EditorUtility.SaveFilePanel("Save Mesh Info", directory, defaultName, "txt");
//
//             if (!string.IsNullOrEmpty(filePath))
//             {
//                 savePath = filePath; // 更新保存路径
//             }
//         }
//
//         if (GUILayout.Button("Export"))
//         {
//             // if (string.IsNullOrEmpty(savePath))
//             // {
//             //     EditorUtility.DisplayDialog("Error", "Please enter a valid save path.", "OK");
//             //     return;
//             // }
//             
//             Mesh mesh = selectedMesh.sharedMesh;
//             if (selectedMesh == null)
//             {
//                 EditorUtility.DisplayDialog("Error", "Please select a mesh to export.", "OK");
//                 return;
//             }
//             ExportMeshInfo(); // 导出 Mesh 信息
//
//         }
//     }
//     
//     private void ExportMeshInfo()
//     {
//
//         int resolution = 32;
//         // 应用材质
//         MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
//         Mesh mesh = selectedMesh.sharedMesh;
//         mesh.RecalculateBounds();
//         
//         Bounds bounds = mesh.bounds;
//         
//         //得到最长轴
//         var maxLength = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
//         var unit = maxLength / resolution;
//         var hunit = unit * 0.5f;
//         
//         Vector3 border = new Vector3(1f,1f,1f);
//         //扩展包围盒边界使得体素化的表面更加正确
//         var st = bounds.min - new Vector3(hunit, hunit, hunit);
//         var ed = bounds.max + new Vector3(hunit, hunit, hunit);
//         var sz = ed - st;
//         var center = (ed + st) / 2f;
//         var ext = (ed - center);
//         
//         int w, h, d;
//         w = Mathf.CeilToInt(sz.x / unit);
//         h = Mathf.CeilToInt(sz.y / unit);
//         d = Mathf.CeilToInt(sz.z / unit);
//         
//         Material material = meshRenderer.sharedMaterial;
//         
//         //Matrix4x4 PX = Matrix4x4.Ortho(-1,1,-1.5f,1.5f,0.01f,10f);
//         Matrix4x4 PX = Matrix4x4.Ortho(-ext.z,ext.z,-ext.y,ext.y,0.8f,1.2f+2*ext.x);
//         Matrix4x4 PY = Matrix4x4.Ortho(-ext.x,ext.x,-ext.z,ext.z,0.8f,1.2f+2*ext.y);
//         Matrix4x4 PZ = Matrix4x4.Ortho(-ext.x,ext.x,-ext.y,ext.y,0.8f,1.2f+2*ext.z);
//         PX = GL.GetGPUProjectionMatrix(PX, true);
//         PY = GL.GetGPUProjectionMatrix(PY, true);
//         PZ = GL.GetGPUProjectionMatrix(PZ, true);
//         //Matrix4x4 VX = Matrix4x4.LookAt(new Vector3(3,0,0), new Vector3(0,0,0),new Vector3(0, 1, 0));
//         Matrix4x4 VX = Matrix4x4.LookAt(new Vector3(ed.x+1,center.y,center.z), center,new Vector3(0, 1, 0));
//         Matrix4x4 VY = Matrix4x4.LookAt(new Vector3(center.x,ed.y+1,center.z), center,new Vector3(-1, 0, 0));
//         Matrix4x4 VZ = Matrix4x4.LookAt(new Vector3(center.x,center.y,ed.z+1), center,new Vector3(0, 1, 0));
//         Debug.Log(VX);
//         var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
//         VX = scaleMatrix * VX.inverse;
//         VY = scaleMatrix * VY.inverse;
//         VZ = scaleMatrix * VZ.inverse;
//         
//         Debug.Log( st+" "+ed+" "+sz);
//         Voxel[] voxels = new Voxel[w*h*d];
//         ComputeBuffer voxelBuffer = new ComputeBuffer(w*h*d, Marshal.SizeOf(typeof(Voxel)));
//         voxelBuffer.SetData(voxels);
//         
//         CommandBuffer commandBuffer = new CommandBuffer();
//         commandBuffer.ClearRenderTarget(true, true, Color.red, 1f);
//         commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
//         
//         
//         commandBuffer.name = "Render My Mesh";
//         //commandBuffer.SetViewport();
//         commandBuffer.SetGlobalBuffer("voxels",voxelBuffer);
//         commandBuffer.SetRandomWriteTarget(1,voxelBuffer);
//         commandBuffer.SetGlobalMatrix("PX",PX);
//         commandBuffer.SetGlobalMatrix("PY",PY);
//         commandBuffer.SetGlobalMatrix("PZ",PZ);
//         commandBuffer.SetGlobalMatrix("VX",VX);
//         commandBuffer.SetGlobalMatrix("VY",VY);
//         commandBuffer.SetGlobalMatrix("VZ",VZ);
//         commandBuffer.SetGlobalInt("w",w);
//         commandBuffer.SetGlobalInt("h",h);
//         commandBuffer.SetGlobalInt("d",d);
//         commandBuffer.SetGlobalVector("start",st);
//         commandBuffer.SetGlobalVector("end",ed);
//         commandBuffer.SetGlobalFloat("unit",unit);
//         commandBuffer.DrawMesh(mesh,Matrix4x4.identity, material);
//         Graphics.ExecuteCommandBuffer(commandBuffer);
//         
//         
//         int cnt = 0;
//         voxelBuffer.GetData(voxels);
//         for (int i = 0; i < w; ++i)
//         {
//             for (int j = 0; j < h; ++j)
//             {
//                 for (int k = 0; k < d; ++k)
//                 {
//                     int index = i*h*d+j*d+k;
//                     if (voxels[index].IsFill())
//                     {
//                         Debug.Log("index:"+index);
//                         cnt++;
//                     }
//                 }
//             }
//         }
//         
//         Debug.Log(cnt);
//         selectedMesh = obj.GetComponent<MeshFilter>();
//         mesh = selectedMesh.sharedMesh;
//         VoxelData data = new VoxelData(voxels, w, h, d, unit,cnt);
//
//         bool useUV = false;
//         // build voxel cubes integrated mesh    
//         selectedMesh.sharedMesh = VoxelMesh.Build(data.GetData(), data.unitLength, useUV);
//         voxelBuffer.Release();
//         meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//
//         //voxelBuffer.Release();
//         // // Generate mesh info
//         // string meshInfo = GenerateMeshInfo(selectedMesh.sharedMesh);
//         //
//         // // Save mesh info to the specified path
//         // System.IO.File.WriteAllText(savePath, meshInfo);
//         //
//         // EditorUtility.DisplayDialog("Success", "Mesh info exported successfully!", "OK");
//     }
//     
//     private string GenerateMeshInfo(Mesh mesh)
//     {
//         return $"Mesh name: {mesh.name}\nVertices: {mesh.vertexCount}\nTriangles: {mesh.triangles.Length / 3}";
//     }
// }