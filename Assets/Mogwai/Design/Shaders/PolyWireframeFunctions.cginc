// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

//Algorithms and shaders based on code from this journal
//http://cgg-journal.com/2008-2/06/index.html
// Adapted from UCLA GameLab Wirefreame shaders.

#ifndef MOGWAI_POLY_WIREFRAME
#define MOGWAI_POLY_WIREFRAME

#include "UnityCG.cginc"
#define NUMBER_OF_MESH_XFORMS 128
float4x4 _RemesherMeshTransforms[NUMBER_OF_MESH_XFORMS];

// DATA STRUCTURES //
// Vertex to Geometry
struct MOG_v2g
{
	float4	pos		: POSITION;		// vertex position
	float2  uv		: TEXCOORD0;	// vertex uv coordinate
};

// Geometry to  MOG_fragment
struct MOG_g2f
{
	float4	pos		: POSITION;		// fragment position
	float2	uv		: TEXCOORD0;	// fragment uv coordinate
	float3  dist	: TEXCOORD1;	// distance of this struct to each edge of the triangle in the geometry
};

// PARAMETERS //

//float4 _Texture_ST;			// For the Main Tex UV transform
float _Thickness = 1;		// Thickness of the wireframe line rendering
float4 _Color = {1,1,1,1};	// Color of the line
float4 _MainTex_ST;			// For the Main Tex UV transform
sampler2D _MainTex;			// Texture used for the line

// SHADER PROGRAMS //
// Vertex Shader
MOG_v2g MOG_vert(appdata_full v)
{
	MOG_v2g output;
	float4 objSpace = mul(_RemesherMeshTransforms[v.texcoord2.x], v.vertex);
	output.pos =  UnityObjectToClipPos(objSpace);
	output.uv = TRANSFORM_TEX (v.texcoord, _MainTex);

	return output;
}

// Geometry Shader
[maxvertexcount(3)]
void MOG_geom(triangle MOG_v2g p[3], inout TriangleStream<MOG_g2f> triStream)
{
	//points in screen space
	float2 p0 = _ScreenParams.xy * p[0].pos.xy / p[0].pos.w;
	float2 p1 = _ScreenParams.xy * p[1].pos.xy / p[1].pos.w;
	float2 p2 = _ScreenParams.xy * p[2].pos.xy / p[2].pos.w;
	
	//edge vectors
	float2 v0 = p2 - p1;
	float2 v1 = p0 - p2;
	float2 v2 = p1 - p0;

	//area of the triangle
 	float area = abs(v1.x*v2.y - v1.y * v2.x);

	//values based on distance to the edges
	float dist0 = area / length(v0);
	float dist1 = area / length(v1);
	float dist2 = area / length(v2);
	
	MOG_g2f pIn;
	
	//add the first point
	pIn.pos = p[0].pos;
	pIn.uv = p[0].uv;
	pIn.dist = float3(dist0,0,0);
	triStream.Append(pIn);

	//add the second point
	pIn.pos =  p[1].pos;
	pIn.uv = p[1].uv;
	pIn.dist = float3(0,dist1,0);
	triStream.Append(pIn);
	
	//add the third point
	pIn.pos = p[2].pos;
	pIn.uv = p[2].uv;
	pIn.dist = float3(0,0,dist2);
	triStream.Append(pIn);
}

// Fragment Shader
float4 MOG_frag(MOG_g2f input) : COLOR
{			
	//find the smallest distance
	float val = min( input.dist.x, min( input.dist.y, input.dist.z));
	
	//calculate power to 2 to thin the line
	val = exp2( -1/_Thickness * val * val );
		
	//blend between the lines and the negative space to give illusion of anti aliasing
	float4 targetColor = _Color * tex2D( _MainTex, input.uv); // the line color.
	float4 transCol = targetColor; // the negative space color.
	transCol.a = 0.1;
	return val * targetColor + ( 1 - val ) * transCol;
}

#endif