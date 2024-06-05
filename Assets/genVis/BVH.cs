using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace genVis
{
    
    public struct BVHNode
    {
        public int left, right;    // 左右子树索引
        public int n, index;       // 叶子节点信息               
        public float3 AA, BB;        // 碰撞盒
    }

    public struct Triangle
    {
        public float3 a, b, c;
    }
    
    public class TriangleComparerX : IComparer<Triangle>
    {
        public int Compare(Triangle t1, Triangle t2)
        {
            float3 center1 = (t1.a + t1.b + t1.c) / new float3(3, 3, 3);
            float3 center2 = (t2.a + t2.b + t2.c) / new float3(3, 3, 3);
            if (center1.x < center2.x) return -1;
            else if (center1.x > center2.x) return 1;
            return 0;
            // 如果x应该在y前面，则返回负数
            // 如果x和y相等，则返回0
            // 如果x应该在y后面，则返回正数
        }
    }
    public class TriangleComparerY : IComparer<Triangle>
    {
        public int Compare(Triangle t1, Triangle t2)
        {
            float3 center1 = (t1.a + t1.b + t1.c) / new float3(3, 3, 3);
            float3 center2 = (t2.a + t2.b + t2.c) / new float3(3, 3, 3);
            if (center1.y < center2.y) return -1;
            else if (center1.y > center2.y) return 1;
            return 0;
            // 如果x应该在y前面，则返回负数
            // 如果x和y相等，则返回0
            // 如果x应该在y后面，则返回正数
        }

    }
    public class TriangleComparerZ : IComparer<Triangle>
    {
        public int Compare(Triangle t1, Triangle t2)
        {
            float3 center1 = (t1.a + t1.b + t1.c) / new float3(3, 3, 3);
            float3 center2 = (t2.a + t2.b + t2.c) / new float3(3, 3, 3);
            if (center1.z < center2.z) return -1;
            else if (center1.z > center2.z) return 1;
            return 0;
            // 如果x应该在y前面，则返回负数
            // 如果x和y相等，则返回0
            // 如果x应该在y后面，则返回正数
        }
    }
    //根节点为1
    public class BVH
    {
        public static int cnt=0;
        // 构建 BVH
        public static int buildBVH(Triangle[] triangles, List<BVHNode> nodes, int l, int r, int n) {
            if (l > r) return 0;
            
            BVHNode node;
            int id = nodes.Count;
            node.left = node.right = node.n = node.index = 0;
            node.AA = new float3(1145141919, 1145141919, 1145141919);
            node.BB = new float3(-1145141919, -1145141919, -1145141919);
            nodes.Add(node);
            // 计算 AABB
            for (int i = l; i <= r; i++) {
                // 最小点 AA
                float minx = Math.Min(triangles[i].a.x, Math.Min(triangles[i].b.x, triangles[i].c.x));
                float miny = Math.Min(triangles[i].a.y, Math.Min(triangles[i].b.y, triangles[i].c.y));
                float minz = Math.Min(triangles[i].a.z, Math.Min(triangles[i].b.z, triangles[i].c.z));
                node.AA.x = Math.Min(node.AA.x, minx);
                node.AA.y = Math.Min(node.AA.y, miny);
                node.AA.z = Math.Min(node.AA.z, minz);

                // 最大点 BB
                float maxx = Math.Max(triangles[i].a.x, Math.Max(triangles[i].b.x, triangles[i].c.x));
                float maxy = Math.Max(triangles[i].a.y, Math.Max(triangles[i].b.y, triangles[i].c.y));
                float maxz = Math.Max(triangles[i].a.z, Math.Max(triangles[i].b.z, triangles[i].c.z));
                node.BB.x = Math.Max(node.BB.x, maxx);
                node.BB.y = Math.Max(node.BB.y, maxy);
                node.BB.z = Math.Max(node.BB.z, maxz);
            }
        
            // 不多于 n 个三角形 返回叶子节点
            if ((r - l + 1) <= n) {
                node.n = r - l + 1;
                node.index = l;
                nodes[id] = node;
                return id;
            }
            // 否则递归建树
            float lenx = node.BB.x - node.AA.x;
            float leny = node.BB.y - node.AA.y;
            float lenz = node.BB.z - node.AA.z;
            // 按 x 划分
            if (lenx >= leny && lenx >= lenz)
                Array.Sort(triangles,l,r-l+1,new TriangleComparerX());
            // 按 y 划分
            if (leny >= lenx && leny >= lenz)
                Array.Sort(triangles,l,r-l+1,new TriangleComparerY());
            // 按 z 划分
            if (lenz >= lenx && lenz >= leny)
                Array.Sort(triangles,l,r-l+1,new TriangleComparerZ());
            // 递归
            int mid = (l + r) / 2;
            int left = buildBVH(triangles, nodes, l, mid, n);
            int right = buildBVH(triangles, nodes, mid + 1, r, n);
            
            node.left = left;
            node.right = right;
            nodes[id] = node;
            return id;
            
        }
    }
}