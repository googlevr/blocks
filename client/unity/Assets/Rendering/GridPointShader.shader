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

Shader "MogwaiProcedural/GridPointShader"
{
	Properties
	{
    _PointSphereRadius ("Sphere Radius", Float) = 0.01
    _MaterialColor ("Material Color", Color) = (0.5, 0.5, 0.5, 0.5)
    _EmissiveAmount ("Emissive Amount", Float) = 0.00
    _EmissiveColor ("Emissive Color", Color) = (0.0, 0.0, 0.0, 0.0)
    _EmissivePulseFrequencyMultiplier ("Emissive Pulse Frequency Multiplier", Float) = 1.00
    _EmissivePulseAmount ("Emissive Pulse Amount", Float) = 0.00
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent"}
		Pass
		{
      Blend One One
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
      float3 _GridCenterWorld;
      float _GridRenderRadius;

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


			
			fixed4 frag (vs_out i) : SV_Target
			{
        float animOnPct = 1;

        float sphereRadius = animOnPct * _PointSphereRadius;
//			return fixed4(animOnPct, animOnPct, animOnPct, animOnPct);
        //The math here is *slightly* wrong, but correct enough for rendering (and doing it right would take more instructions)
        //ignore actual pixel position, as all we care about is procedural geometry.
        float pixelOpacity = smoothstep(1.01, 1, length(i.worldPos - i.center) / sphereRadius);
        clip (pixelOpacity);
        float distToCenter = length(i.worldPos - _GridCenterWorld);
        float pctToEdge = saturate(distToCenter / _GridRenderRadius);
        float adjustedOpacity = 1 - pctToEdge;
        return _MaterialColor * pixelOpacity * adjustedOpacity;

			}
			ENDCG
		}
	}
}
