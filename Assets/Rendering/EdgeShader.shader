// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

Shader "MogwaiProcedural/EdgeShader"
{
	Properties
	{
    _PointSphereRadius ("Sphere Radius", Float) = 0.01
    _VertexSphereRadius ("Vertex Sphere Radius", Float) = 0.005
    _MaterialColor ("Material Color", Color) = (0.5, 0.5, 0.5, 0.5)
    _EmissiveAmount ("Emissive Amount", Float) = 0.00
    _EmissiveColor ("Emissive Color", Color) = (0.0, 0.0, 0.0, 0.0)
    _EmissivePulseFrequencyMultiplier ("Emissive Pulse Frequency Multiplier", Float) = 1.00
    _EmissivePulseAmount ("Emissive Pulse Amount", Float) = 0.00
    _RefractiveIndex("RefractiveIndex", Float) = 1.333333
    _Roughness("Roughness",Float) = 0.05
    _Metallic("Metallic",Float) = 1.0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent+5"}
		LOD 100
    GrabPass {"PreTransparencyTexture"}
		Pass
		{
      Blend SrcAlpha OneMinusSrcAlpha
      ZTest Off
      ZWrite On
      Offset -2, -2
			CGPROGRAM
      //#pragma vertex fakeVert
			#pragma vertex vert
      #pragma geometry geom
			#pragma fragment frag
      #pragma target 5.0
      #pragma enable_d3d11_debug_symbols 
			
			#include "UnityCG.cginc"
      #include "shaderMath.cginc"

      float4 _MaterialColor;
      sampler2D PreTransparencyTexture;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 selectData : TEXCOORD0;
        float4 normal : NORMAL;
			};

			struct vs_out
			{
				float2 selectData : TEXCOORD0;
				float4 vertex : SV_POSITION;
        float4 p0World : TEXCOORD2;
        float4 p1World : TEXCOORD4;
        float4 worldPos : TEXCOORD3;
        float4 normal : NORMAL;
        float4 grabPos : TEXCOORD5;
        float4 color : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			vs_out vert (appdata v)
			{
				vs_out o = (vs_out)0;
				o.vertex = v.vertex;
        o.normal = v.normal;
        o.selectData = v.selectData;
				return o;
			}

      float _PointSphereRadius = 0.001;

      // Used to ensure edge leaves enough space to render vertices without intersection
      float _VertexSphereRadius = 0.005;

      [maxvertexcount(24)]
      void geom(line vs_out input[2], inout TriangleStream<vs_out> OutputStream) {


        float4 v0View = mul(UNITY_MATRIX_MV, input[0].vertex);
        float4 v1View = mul(UNITY_MATRIX_MV, input[1].vertex);



        //p0 is the lower point when we enter view space.
        //float4 p0World = v0View.y < v1View.y ? mul(UNITY_MATRIX_M, input[0].vertex) : mul(UNITY_MATRIX_M, input[1].vertex);
        //float4 p1World =  v0View.y < v1View.y ? mul(UNITY_MATRIX_M, input[1].vertex) : mul(UNITY_MATRIX_M, input[0].vertex);

        float4 p0World = mul(UNITY_MATRIX_M, input[0].vertex);
        float4 p1World = mul(UNITY_MATRIX_M, input[1].vertex);

        float4 centroidWorld = (p0World + p1World) / 2;
        float3 baseAxisRay = p1World - p0World;
        float3 axisRay = normalize(baseAxisRay);

        float animatedSphereRadius = _PointSphereRadius * input[0].selectData;

        // Don't use animatedSphereRadius as we're not animating across length
        float4 p0WorldShortened = float4(p0World.xyz, 1);
        float4 p1WorldShortened = float4(p1World.xyz, 1);

        float3 viewToCentroid = normalize(_WorldSpaceCameraPos - centroidWorld.xyz);
        float3 right = normalize(p0World.xyz - p1World.xyz);
        float3 up = normalize(cross(viewToCentroid, right));
        float3 cameraUp = normalize(cross(viewToCentroid, up));

        float horRad = saturate(length(p0World - p1World)/2 + 1.5 * _VertexSphereRadius);
        float clipHorRad = length(p0World - p1World)/2;


        float axisLength = length(p0WorldShortened - p1WorldShortened);
        float3 avgNormal = normalize((input[0].normal + input[1].normal) / 2); 
        float3 viewVec = normalize(p0WorldShortened - _WorldSpaceCameraPos);
        float3 rHat = normalize(cross(axisRay, avgNormal));
        float3 hHat = -normalize(cross(axisRay, rHat));
        float3 aHat = normalize(axisRay);

        float3 scaledRHat = animatedSphereRadius * rHat;
        float3 scaledHHat = animatedSphereRadius * hHat;
        float3 scaledAHat = axisRay * axisLength;
      
      

        float4 toCameraOffset = float4(viewToCentroid * _PointSphereRadius, 0);
        vs_out curVert;  
        //create quad facing camera

        // Capsule body
       


        float4 frontUpLeft = float4(p0WorldShortened + scaledRHat + scaledHHat - axisRay * _PointSphereRadius, 1);
        float4 frontUpRight = float4(p0WorldShortened - scaledRHat + scaledHHat - axisRay * _PointSphereRadius, 1);
        float4 frontDownLeft = float4(p0WorldShortened + scaledRHat - scaledHHat - axisRay * _PointSphereRadius, 1);
        float4 frontDownRight = float4(p0WorldShortened - scaledRHat - scaledHHat - axisRay * _PointSphereRadius, 1);

        float4 backUpLeft = float4(p0WorldShortened + scaledRHat + scaledHHat + scaledAHat + axisRay * _PointSphereRadius, 1);
        float4 backUpRight = float4(p0WorldShortened - scaledRHat + scaledHHat + scaledAHat + axisRay * _PointSphereRadius, 1);
        float4 backDownLeft = float4(p0WorldShortened + scaledRHat - scaledHHat + scaledAHat + axisRay * _PointSphereRadius, 1);
        float4 backDownRight = float4(p0WorldShortened - scaledRHat - scaledHHat + scaledAHat + axisRay * _PointSphereRadius, 1);

        float4 frontUpLeftClip = mul(UNITY_MATRIX_VP, frontUpLeft);
        float4 frontUpRightClip = mul(UNITY_MATRIX_VP, frontUpRight);
        float4 frontDownLeftClip = mul(UNITY_MATRIX_VP, frontDownLeft);
        float4 frontDownRightClip = mul(UNITY_MATRIX_VP, frontDownRight);

        float4 backUpLeftClip = mul(UNITY_MATRIX_VP, backUpLeft);
        float4 backUpRightClip = mul(UNITY_MATRIX_VP, backUpRight);
        float4 backDownLeftClip = mul(UNITY_MATRIX_VP, backDownLeft);
        float4 backDownRightClip = mul(UNITY_MATRIX_VP, backDownRight);

        float4 upNormal = float4(hHat, 0);
        float4 leftNormal = float4(rHat, 0);
        float4 frontNormal = float4(-aHat, 0);

        curVert = (vs_out)0;
        curVert.selectData = input[0].selectData;
        curVert.p0World = p0WorldShortened;
        curVert.p1World = p1WorldShortened;
        curVert.color = float4(avgNormal, 1);
        
        //Front Face
        curVert.normal = float4(cross(normalize(frontUpLeft.xyz - frontUpRight.xyz), 
          normalize(frontUpLeft.xyz - frontDownLeft.xyz)), 0);
        //0
        curVert.worldPos = frontUpLeft;
        curVert.vertex = frontUpLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        //1
        curVert.worldPos = frontUpRight;
        curVert.vertex = frontUpRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        //2
        curVert.worldPos = frontDownLeft;
        curVert.vertex = frontDownLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        //3
        curVert.worldPos = frontDownRight;
        curVert.vertex = frontDownRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        OutputStream.RestartStrip();

        //Back Face
        curVert.normal =  float4(cross(normalize(backUpRight.xyz - backUpLeft.xyz), 
          normalize(backUpRight.xyz - backDownRight.xyz)), 0);
        //0
        curVert.worldPos = backUpRight;
        curVert.vertex = backUpRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
       OutputStream.Append(curVert);
        //1
        curVert.worldPos = backUpLeft;
        curVert.vertex = backUpLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //2
        curVert.worldPos = backDownRight;
        curVert.vertex = backDownRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //3
        curVert.worldPos = backDownLeft;
        curVert.vertex = backDownLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        OutputStream.RestartStrip();

        //Left Face
        curVert.normal = float4(cross(normalize(backUpLeft.xyz - frontUpLeft.xyz), 
          normalize(backUpLeft.xyz - backDownLeft.xyz)), 0);
        //0
        curVert.worldPos = backUpLeft;
        curVert.vertex = backUpLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //1
        curVert.worldPos = frontUpLeft;
        curVert.vertex = frontUpLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        //2
        curVert.worldPos = backDownLeft;
        curVert.vertex = backDownLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //3
        curVert.worldPos = frontDownLeft;
        curVert.vertex = frontDownLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        OutputStream.RestartStrip();

        //Right Face
        curVert.normal = float4(cross(normalize(backUpRight.xyz - backDownRight.xyz), 
          normalize(backUpRight.xyz - frontUpRight.xyz)), 0);
        //0
        curVert.worldPos = backUpRight;
        curVert.vertex = backUpRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //1
        curVert.worldPos = backDownRight;
        curVert.vertex = backDownRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //2
        curVert.worldPos = frontUpRight;
        curVert.vertex = frontUpRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        //3
        curVert.worldPos = frontDownRight;
        curVert.vertex = frontDownRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        OutputStream.RestartStrip();

        //Top Face
        curVert.normal = float4(cross(normalize(backUpLeft.xyz - backUpRight.xyz), 
          normalize(backUpLeft.xyz - frontUpLeft.xyz)), 0);
        //0
        curVert.worldPos = backUpLeft;
        curVert.vertex = backUpLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //1
        curVert.worldPos = backUpRight;
        curVert.vertex = backUpRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //2
        curVert.worldPos = frontUpLeft;
        curVert.vertex = frontUpLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        //3
        curVert.worldPos = frontUpRight;
        curVert.vertex = frontUpRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        OutputStream.RestartStrip();

        //Bottom Face
        curVert.normal = float4(cross(normalize(backDownLeft.xyz - frontDownLeft.xyz), 
          normalize(backDownLeft.xyz - backDownRight.xyz)), 0);
        //0
        curVert.worldPos = backDownLeft;
        curVert.vertex = backDownLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //1
        curVert.worldPos = frontDownLeft;
        curVert.vertex = frontDownLeftClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        //1
        curVert.worldPos = backDownRight;
        curVert.vertex = backDownRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[1].selectData;
        OutputStream.Append(curVert);
        //3
        curVert.worldPos = frontDownRight;
        curVert.vertex = frontDownRightClip;
        curVert.grabPos = ComputeGrabScreenPos(curVert.vertex);
        curVert.selectData = input[0].selectData;
        OutputStream.Append(curVert);
        OutputStream.RestartStrip();
        
      }


			
			fixed4 frag (vs_out i) : SV_Target
			{
			  float animPct = i.selectData.r;
        float3 worldPos = i.worldPos;
        float3 worldNormal = normalize(i.normal);

        float3 lightOut = (float3)0;

        evaluateLights(
          worldPos, //pixelPos
          worldNormal, //pixel worldspace normal
          _MaterialColor, //color
          lightOut); //inout lighting output


        //Shading
        float pulsePct = 0.5 + 0.5 * cos(_Time.y * _EmissivePulseFrequencyMultiplier);
        float emissiveAmount = _EmissiveAmount + pulsePct * _EmissivePulseAmount;
        lightOut = lightOut + emissiveAmount * _EmissiveColor.rgb;
			  //return float4(i.selectData.r, i.selectData.g, 0, 1);
        return animPct * float4(lightOut, i.selectData.g * _MaterialColor.a);
      }
			ENDCG
		}
	}
}