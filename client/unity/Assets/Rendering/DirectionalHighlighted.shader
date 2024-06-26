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

Shader "Mogwai/DirectionalHighlighted"
{
	Properties
	{
		_Color("Color", Color) = (1.000000,1.000000,1.000000,1.000000)
		_OutlineColor("Outline Color", Color) = (0,0,0,1)
		_Outline("Outline width", Range(0.0, 0.7)) = .005
    _EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
    _EmissiveAmount("Emissive Amount", Float) = 1
    _Roughness("Roughness",Float) = 0.3
    _Metallic("Metallic", Float) = 0.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
    _MainTex("Base (RGB)", 2D) = "white" { }

		//_MainTex("Albedo", 2D) = "white" { }
		//_Cutoff("Alpha Cutoff", Range(0.000000,1.000000)) = 0.500000
		//_Glossiness("Smoothness", Range(0.000000,1.000000)) = 0.500000
		//_GlossMapScale("Smoothness Scale", Range(0.000000,1.000000)) = 1.000000
		//[Enum(Metallic Alpha,0,Albedo Alpha,1)]  _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0.000000
		//[Gamma]  _Metallic("Metallic", Range(0.000000,1.000000)) = 0.000000
		//_MetallicGlossMap("Metallic", 2D) = "white" { }
		//[ToggleOff]  _SpecularHighlights("Specular Highlights", Float) = 1.000000
		//[ToggleOff]  _GlossyReflections("Glossy Reflections", Float) = 1.000000
		//_BumpScale("Scale", Float) = 1.000000
		//_BumpMap("Normal Map", 2D) = "bump" { }
		//_Parallax("Height Scale", Range(0.005000,0.080000)) = 0.020000
		//_ParallaxMap("Height Map", 2D) = "black" { }
		//_OcclusionStrength("Strength", Range(0.000000,1.000000)) = 1.000000
		//_OcclusionMap("Occlusion", 2D) = "white" { }
		//_EmissionColor("Color", Color) = (0.000000,0.000000,0.000000,1.000000)
		//_EmissionMap("Emission", 2D) = "white" { }
		//_DetailMask("Detail Mask", 2D) = "white" { }
		//_DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" { }
		//_DetailNormalMapScale("Scale", Float) = 1.000000
		//_DetailNormalMap("Normal Map", 2D) = "bump" { }
		//[Enum(UV0,0,UV1,1)]  _UVSec("UV Set for secondary textures", Float) = 0.000000
		//[HideInInspector]  _Mode("__mode", Float) = 0.000000
		//[HideInInspector]  _SrcBlend("__src", Float) = 1.000000
		//[HideInInspector]  _DstBlend("__dst", Float) = 0.000000
		//[HideInInspector]  _ZWrite("__zw", Float) = 1.000000
	}

	SubShader
	{
		Tags {"RenderType"="Opaque" "Queue"="Transparent"}
		LOD 100
		
		CGINCLUDE
		#include "UnityCG.cginc"
    #include "shaderMath.cginc"
		ENDCG

		Pass {
			Name "OUTLINE"
			Tags{ "LightMode" = "Always" }
			Cull Off
			ZWrite Off
			ZTest Always

			// you can choose what kind of blending mode you want for the outline
			Blend SrcAlpha OneMinusSrcAlpha // Normal
      //Blend One One // Additive
      //Blend One OneMinusDstColor // Soft Additive
      //Blend DstColor Zero // Multiplicative
      //Blend DstColor SrcColor // 2x Multiplicative

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			struct appdata {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 color : COLOR;
			};

			struct v2f {
				float4 pos : POSITION;
				float4 color : COLOR;
			};

			uniform float _Outline;
			uniform float4 _OutlineColor;

			v2f vert(appdata v) {
				// just make a copy of incoming vertex data but scaled according to normal direction
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex * 1.1);

				float3 norm = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
				float2 offset = TransformViewToProjection(norm.xy);

				o.pos.xy += offset * o.pos.z * _Outline;
				o.color = _OutlineColor;
				return o;
			}

			half4 frag(v2f i) : COLOR{
				return i.color;
			}
			ENDCG
		}

		Pass
		{
					Cull Off
    			ZWrite Off
			Blend One OneMinusDstColor
      Offset -1, -1
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag


			struct VertexInput
			{
				float4 position : POSITION;
				float3 normal : NORMAL;
				float4 color : COLOR;
			};

			struct VertexOutput
			{
				float4 position : SV_POSITION;
				float3 normal : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				float4 shadowPosition : TEXCOORD2;
				float4 color : COLOR;
			};

			VertexOutput vert (VertexInput vertex)
			{
				VertexOutput output;
				output.position = UnityObjectToClipPos(vertex.position);
				output.normal = UnityObjectToWorldNormal( vertex.normal );
				output.worldPosition = mul(unity_ObjectToWorld, vertex.position);
				output.shadowPosition = mul(_ShadowMatrix ,output.worldPosition);
				output.color = vertex.color;
				return output;
			}

			float4 _Color;
			float4 frag (VertexOutput fragment) : SV_Target
			{
	      float3 normal = normalize(fragment.normal);
        float distanceToLight = length(fragment.worldPosition.xyz - _LightPosition);
        float3 lightOut = 0;

        evaluateLights(
          fragment.worldPosition.xyz, //pixelPos
          fragment.normal, //pixelNormal
          fragment.color,
          fragment.shadowPosition, // shadowPosition
          lightOut); //inout diffuseOut

        return float4(lightOut, 1);
			}
			ENDCG
		}
		UsePass "VertexLit/SHADOWCASTER"
	}
}
