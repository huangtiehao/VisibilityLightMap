#ifndef CUSTOM_VOXEL_PASS_INCLUDED
#define CUSTOM_VOXEL_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#define EPS 0.01
#define STEPS 64

struct SHCoe
{
    float4 a,b,c,d;
};
struct Triangle {
    float3 a, b, c;
};

struct BVHNode {
    int left;           // 左子树
    int right;          // 右子树
    int n;              // 包含三角形数目
    int index;          // 三角形起始索引
    float3 AA, BB;      // 碰撞盒
};



RWStructuredBuffer<SHCoe>visCoe;
RWStructuredBuffer<SHCoe>lightCoe;
StructuredBuffer<Triangle> triangles;
StructuredBuffer<BVHNode> bvhNodes;
uint triangleCnt;
// sampler3D SDF_tex;

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

// float3 _MinExtents;
// float3 _MaxExtents;
// float3 _Center;

int get_1d_uv(float2 uv)
{
    return uv.x*1024+uv.y;
}

float SHfunction(int i,in float3 s) 
{ 
    #define k01 0.2820947918    // sqrt(  1/PI)/2
    #define k02 0.4886025119    // sqrt(  3/PI)/2
    #define k03 1.0925484306    // sqrt( 15/PI)/2
    #define k04 0.3153915652    // sqrt(  5/PI)/4
    #define k05 0.5462742153    // sqrt( 15/PI)/4

    //float3 n = s.zxy;
    float x = s.x;
    float y = s.z;
    float z = s.y;
	
    //----------------------------------------------------------
    if( i==0 ) return  k01;
    //----------------------------------------------------------
    if( i==1 ) return  k02*y;
    if( i==2 ) return  k02*z;
    if( i==3 ) return  k02*x;
    //----------------------------------------------------------
    if( i==4 ) return  k03*x*y;
    if( i==5 ) return  k03*y*z;
    if( i==6 ) return  k04*(2.0*z*z-x*x-y*y);
    if( i==7 ) return  k03*x*z;
    if( i==8 ) return  k05*(x*x-y*y);

    return 0.0;
}

// float3 Remap(float3 v, float3 fromMin, float3 fromMax, float3 toMin, float3 toMax) {
//     return (v-fromMin)/(fromMax-fromMin)*(toMax-toMin)+toMin;
// }
//       
// float3 LocalPosToUVW(float3 localPos) {
//     return Remap(localPos-_Center, _MinExtents, _MaxExtents, 0, 1);
// }
      
struct Ray
{
    float3 orig;
    float3 dir;
};

struct Attributes
{
    float4 positionOS : POSITION;
    float4 normalOS: NORMAL;
    float2 baseUV : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS :SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS: VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;
};

// 光线和三角形求交 
bool hitTriangle(Triangle tri, Ray ray) {

    float3 p1 = tri.a;
    float3 p2 = tri.b;
    float3 p3 = tri.c;

    float3 S = ray.orig;    // 射线起点
    float3 d = ray.dir;     // 射线方向
    float3 N = normalize(cross(p2-p1, p3-p1));    // 法向量

    // 如果视线和三角形平行
    if (abs(dot(N, d)) < 0.00001f) return false;

    // 距离
    float t = (dot(N, p1) - dot(S, N)) / dot(d, N);
    if (t < 0.0005f) return false;    // 如果三角形在光线背面

    // 交点计算
    float3 P = S + d * t;

    // 判断交点是否在三角形中
    float3 c1 = cross(p2 - p1, P - p1);
    float3 c2 = cross(p3 - p2, P - p2);
    float3 c3 = cross(p1 - p3, P - p3);
    bool r1 = (dot(c1, N) > 0 && dot(c2, N) > 0 && dot(c3, N) > 0);
    bool r2 = (dot(c1, N) < 0 && dot(c2, N) < 0 && dot(c3, N) < 0);

    // 命中，封装返回结果
    if (r1 || r2) {
        return true;
    }

    return false;
}

// 获取第 i 下标的三角形
Triangle getTriangle(int i) {
    Triangle t=triangles[i];
    return t;
}

// 获取第 i 下标的 BVHNode 对象
BVHNode getBVHNode(int i) {
    BVHNode node=bvhNodes[i];
    return node;
}


// ray和 aabb 盒子求交，没有交点则返回 -1
bool hitAABB(Ray r, float3 AA, float3 BB) {
    float3 invdir = 1.0 / r.dir;

    float3 f = (BB - r.orig) * invdir;
    float3 n = (AA - r.orig) * invdir;

    float3 tmax = max(f, n);
    float3 tmin = min(f, n);

    float t1 = min(tmax.x, min(tmax.y, tmax.z));
    float t0 = max(tmin.x, max(tmin.y, tmin.z));
    
    return (t1 >= t0) ? true : false;
}
// ----------------------------------------------------------------------------- //

// 暴力遍历数组下标范围 [l, r] 求最近交点
bool hitArray(Ray ray, int l, int r) {
    for(int i=l; i<=r; i++) {
        Triangle tri = getTriangle(i);
        if(hitTriangle(tri, ray))return true;
    }
    return false;
}


// ----------------------------------------------------------------------------- //
// 遍历 BVH 求交
bool hitBVH(Ray ray) {
    
    // 栈
    int stack[22];
    int sp = 0;
    stack[sp++] = 1;
    while(sp>0&&sp<12) {
        int top = stack[--sp];
        BVHNode node = getBVHNode(top);
        
        // 是叶子节点，遍历三角形，求最近交点
        if(node.n>0) {
            int L = node.index;
            int R = node.index + node.n - 1;
            if( hitArray(ray, L, R))return true;
        }
        
        // 和左右盒子 AABB 求交
        BVHNode leftNode = getBVHNode(node.left);
        BVHNode rightNode = getBVHNode(node.right);
        bool hitL=hitAABB(ray, leftNode.AA, leftNode.BB);
        bool hitR=hitAABB(ray, rightNode.AA, rightNode.BB);

        if(hitL)stack[sp++] = node.left;
        if(hitR)stack[sp++] = node.right;
    }
    return false;
}

Varyings VoxelPassVertex(Attributes input)
{
    Varyings output;
    output.positionWS=TransformObjectToWorld(input.positionOS);
    output.positionCS=TransformWorldToHClip(output.positionWS);
    output.normalWS=TransformObjectToWorldNormal(input.normalOS);
    output.baseUV=input.baseUV;
    return output;
    
}

float getVisTri(Ray ray)
{
    if(hitBVH(ray))return 1; 
    return 0;
}
// float getVisSdf(float3 orig,float3 dir)
// {
//     float3 posOS=TransformWorldToObject(orig);
//     float3 dirOS=TransformWorldToObject(dir);
//     posOS+=2*EPS*dirOS;
//     [unroll(STEPS)]
//     for (uint j = 0; j < STEPS; j++)
//     {
//         float3 texcoord = LocalPosToUVW(posOS);
//         float4 samp = tex3D(SDF_tex, texcoord);
//         if (texcoord.x < 0.0 || texcoord.x > 1.0||texcoord.y < 0.0 || texcoord.y > 1.0||texcoord.z < 0.0 || texcoord.z > 1.0)
//         {
//             return 0;
//         }
//         float sdf=samp.r;
//         //rayMarch
//         posOS+=sdf*dirOS;
//         //小于某个阈值就当作击中
//         if(abs(sdf)<EPS)
//         {
//             return 1;
//         }
//     }
//     return 0;
// }

void integ(Varyings input)
{
    float3 N = normalize(input.normalWS);
    float visAns[16];
    float lightAns[16];
    for(int i=0;i<16;++i)
    {
        visAns[i]=0;
        lightAns[i]=0;
    }
    // tangent space calculation from origin point
    float3 up = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
    float3 right = normalize(cross(up, N));
    up = normalize(cross(N, right));
       
    float sampleDelta = 0.3;
    float nrSamples = 0.0;
    for(float phi = 0.0; phi < 2.0 * PI; phi += sampleDelta)
    {
        for(float theta = 0.0; theta < 0.5 * PI; theta += sampleDelta)
        {
            // spherical to cartesian (in tangent space)
            float3 tangentSample = float3(sin(theta) * cos(phi),  sin(theta) * sin(phi), cos(theta));
            // tangent space to world

            float3 worldDir=tangentSample.x * right + tangentSample.y * up + tangentSample.z * N; 
            Ray ray;
            ray.orig=input.positionWS;
            ray.dir=worldDir;
            float v=getVisTri(ray);

            float l=1;
            //float v=1.0;
            for(int i=0;i<16;++i)
            {
                visAns[i]+= v*SHfunction(i,worldDir)*2*PI;
                lightAns[i]+=l*SHfunction(i,worldDir)*2*PI*cos(theta);
            }

            nrSamples+=1;

            //irradiance += texture(cubeMap, sampleVec).rgb * cos(theta) * sin(theta);
        }
    }

    visCoe[get_1d_uv(1024*input.baseUV)].a[0]=visAns[0]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].a[1]=visAns[1]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].a[2]=visAns[2]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].a[3]=visAns[3]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].b[0]=visAns[4]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].b[1]=visAns[5]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].b[2]=visAns[6]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].b[3]=visAns[7]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].c[0]=visAns[8]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].c[1]=visAns[9]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].c[2]=visAns[10]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].c[3]=visAns[11]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].d[0]=visAns[12]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].d[1]=visAns[13]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].d[2]=visAns[14]/nrSamples;
    visCoe[get_1d_uv(1024*input.baseUV)].d[3]=visAns[15]/nrSamples;
    
    lightCoe[get_1d_uv(1024*input.baseUV)].a[0]=lightAns[0]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].a[1]=lightAns[1]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].a[2]=lightAns[2]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].a[3]=lightAns[3]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].b[0]=lightAns[4]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].b[1]=lightAns[5]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].b[2]=lightAns[6]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].b[3]=lightAns[7]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].c[0]=lightAns[8]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].c[1]=lightAns[9]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].c[2]=lightAns[10]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].c[3]=lightAns[11]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].d[0]=lightAns[12]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].d[1]=lightAns[13]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].d[2]=lightAns[14]/nrSamples;
    lightCoe[get_1d_uv(1024*input.baseUV)].d[3]=lightAns[15]/nrSamples;
}



float4 VoxelPassFragment (Varyings input):SV_TARGET
{
    //return float4(0.0f,0.0f,1.0f,1.0f);
    //SH[input.baseUV]=integ(input.normalWS,0,input.positionWS);

    integ(input);
    //visCoe[int2(0,0)].a=float4(1,1,1,0);
    //SH[input.baseUV*1024]=integ(input.normalWS,input.positionWS);
    return float4(1,1,0,1); 
    //return integ(input.normalWS,input.positionWS);
}
#endif