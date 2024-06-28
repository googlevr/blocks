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

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "MogwaiProcedural/WireframePointShader"
{
	Properties
	{
    _PointSphereRadius ("Sphere Radius", Float) = 0.01
    _MaterialColor ("Material Color", Color) = (0.5, 0.5, 0.5, 0.5)
    _EmissiveAmount ("Emissive Amount", Float) = 0.00
    _EmissiveColor ("Emissive Color", Color) = (0.0, 0.0, 0.0, 0.0)
    _EmissivePulseFrequencyMultiplier ("Emissive Pulse Frequency Multiplier", Float) = 1.00
    _EmissivePulseAmount ("Emissive Pulse Amount", Float) = 0.00
    _Roughness("Roughness",Float) = 0.05
    _Metallic("Metallic",Float) = 1.0
    _RefractiveIndex("RefractiveIndex", Float) = 1.333333
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent"}
		LOD 100
    GrabPass {"PreTransparencyTexture"}
		Pass
		{
		      Offset -2, -2
      Blend SrcAlpha OneMinusSrcAlpha
      ZWrite Off
			CGPROGRAM
			#pragma vertex vert
      #pragma geometry geom
			#pragma fragment frag
      #pragma target 5.0
			
			#include "UnityCG.cginc"

      #include "shaderMath.cginc"

      float4 _MaterialColor;
      sampler2D PreTransparencyTexture;

			float _BaseEmissiveAmount;
			float _HoverEmissiveAmount;
			float _SelectedEmissiveAmount;
      float _SelectDuration;

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct vs_out
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
        float4 worldPos : TEXCOORD2;
        float4 center : TEXCOORD3;
        float4 normal : TEXCOORD1;
        float4 grabPos : TEXCOORD5;
			};

      vs_out fakeVert (appdata v)
			{
				vs_out o = (vs_out)0;
				o.vertex = UnityObjectToClipPos(v.vertex);
        o.worldPos = mul(UNITY_MATRIX_M,v.vertex);
        o.center = float4(-1.0931, 1.68113, -8.8049, 1);
				return o;
			}

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			vs_out vert (appdata v)
			{
				vs_out o = (vs_out)0;
				o.vertex = v.vertex;
				return o;
			}

      float _PointSphereRadius = 0.001;

      [maxvertexcount(6)]
      void geom(point vs_out input[1], inout TriangleStream<vs_out> OutputStream) {
        //create quad facing camera
        float4 worldPos = mul(UNITY_MATRIX_M, input[0].vertex);
        float3 view = normalize(_WorldSpaceCameraPos - worldPos.xyz);
        // up is arbitrary - we just need to cross produce view with another vector that isn't view to get a 90 degree angle
        float3 up = normalize(cross(view, float3(1, 0, 0)));
        // third vector orthogonal to both.
        float3 right = normalize(cross(view, up));

        float sphereRadius = _PointSphereRadius;

        float4 upLeftWorldPos = float4(sphereRadius * (up - right) + worldPos.xyz, 1);
        float4 upRightWorldPos = float4(sphereRadius * (up + right) + worldPos.xyz, 1);
        float4 downLeftWorldPos = float4(sphereRadius * (-up - right) + worldPos.xyz, 1);
        float4 downRightWorldPos = float4(sphereRadius * (-up + right) + worldPos.xyz, 1);

        float4 toCameraOffset = float4(view * sphereRadius, 0);//float4(0,0,0,0);//float4(view * _PointSphereRadius, 0);
        float4 upLeftClipPos =  mul(UNITY_MATRIX_VP, upLeftWorldPos + toCameraOffset);
        float4 upRightClipPos =  mul(UNITY_MATRIX_VP, upRightWorldPos + toCameraOffset);
        float4 downLeftClipPos =  mul(UNITY_MATRIX_VP, downLeftWorldPos + toCameraOffset);
        float4 downRightClipPos = mul(UNITY_MATRIX_VP, downRightWorldPos + toCameraOffset);
        vs_out curVert = (vs_out)0;


        curVert.center = worldPos;
        curVert.worldPos = upLeftWorldPos;
        curVert.vertex = upLeftClipPos;
        curVert.grabPos = ComputeGrabScreenPos(upLeftClipPos);
        OutputStream.Append(curVert);
        curVert.worldPos = upRightWorldPos;
        curVert.vertex = upRightClipPos;
        curVert.grabPos = ComputeGrabScreenPos(upRightClipPos);
        OutputStream.Append(curVert);
        curVert.worldPos = downLeftWorldPos;
        curVert.vertex = downLeftClipPos;
        curVert.grabPos = ComputeGrabScreenPos(downLeftClipPos);
        OutputStream.Append(curVert);
        curVert.worldPos = downRightWorldPos;
        curVert.vertex = downRightClipPos;
        curVert.grabPos = ComputeGrabScreenPos(downRightClipPos);
        OutputStream.Append(curVert);
        OutputStream.RestartStrip();
      }

      float _SelectRadius;
      float3 _SelectPositionWorld;
			
			fixed4 frag (vs_out i) : SV_Target
			{
        float animOnPct = 1;

        float sphereRadius = animOnPct * _PointSphereRadius;
//			return fixed4(animOnPct, animOnPct, animOnPct, animOnPct);
        //The math here is *slightly* wrong, but correct enough for rendering (and doing it right would take more instructions)
        //ignore actual pixel position, as all we care about is procedural geometry.
        float pixelOpacity = smoothstep(1.01, 1, length(i.worldPos - i.center) / sphereRadius);
        clip(pixelOpacity - 0.1);
        float3 view = i.worldPos - _WorldSpaceCameraPos;
        float3 centerToView = normalize (_WorldSpaceCameraPos - i.center);
        /*float3 A = i.worldPos - i.center;

        float b = sqrt(dot(A, A) + sphereRadius * sphereRadius);
        //        return float4(0.4 * b.xxx / sphereRadius, 1);
        // return float4((length(A) / b).xxx, 1);
        float bOverView = b / length(view);
        float dispX = bOverView * length(A);

        float dispY = sqrt(dispX * dispX +  sphereRadius * sphereRadius);
        return float4(100000000 * dispY.xxx, 1);
        float m = length(A) - dispX;
        //float dispY = m * normalize(A);
        //return float4(dispY.xxx * 40, 1);
        return float4( normalize(dispX * normalize(A) + dispY * centerToView), 1);*/


        float3 A = i.worldPos - i.center;

        float dispY = sqrt(sphereRadius * sphereRadius - dot(A, A));
        
        float3 intersectionPos = i.worldPos + dispY * centerToView;
        float3 worldNormal = normalize(intersectionPos - i.center);

        float3 totalDiffuseExitance = 0;
        float3 totalSpecularExitance = 0;

        float3 lightOut = 0;

        evaluateLights(
          intersectionPos /* pixelPos */,
          worldNormal /* pixelNormal */,
          _MaterialColor,
          lightOut /* inout diffuseOut */);


        //Radius alpha
        float diff = length(intersectionPos - _SelectPositionWorld);
        float ratio = diff * diff / (_SelectRadius * _SelectRadius);
        float alphaMult = smoothstep(1.0, 0.0, ratio * ratio);

        //Shading
        float pulsePct = 0.5 + 0.5 * cos(_Time.y * _EmissivePulseFrequencyMultiplier);
        float emissiveAmount = _EmissiveAmount + pulsePct * _EmissivePulseAmount;
        lightOut = lightOut + emissiveAmount * _EmissiveColor.rgb;

				return animOnPct * float4((lightOut * _MaterialColor.a), alphaMult * _MaterialColor.a);
			}
			ENDCG
		}
	}
}
