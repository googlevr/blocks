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
// limitations under the License.using UnityEngine;
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Poly/Skybox"
{
	Properties
	{
		_SkyColor1("Top Color", Color) = (0.37, 0.52, 0.73, 0)
		_SkyExponent1("Top Exponent", Float) = 2

		_SkyColor2("Horizon Color", Color) = (0.89, 0.96, 1, 0)

		_SkyIntensity("Sky Intensity", Float) = 1.75

		_SunColor("Sun Color", Color) = (1, 0.99, 0.87, 1)
		_SunIntensity("Sun Intensity", Range(0.0,20.0)) = 10.0
	}

		CGINCLUDE

#include "UnityCG.cginc"

		struct appdata
	{
		float4 position : POSITION;
		float3 texcoord : TEXCOORD0;
	};

	struct v2f
	{
		float4 position : SV_POSITION;
		float3 texcoord : TEXCOORD0;
	};

	half3 _SkyColor1;
	half _SkyExponent1;

	half3 _SkyColor2;
	half _SkyIntensity;

	half3 _MoonVector;

	half3 _SunColor;
	half _SunIntensity;

	v2f vert(appdata v)
	{
		v2f o;
		o.position = UnityObjectToClipPos(v.position);
		o.texcoord = v.texcoord;
		return o;
	}

	half4 frag(v2f i) : COLOR
	{
		float3 v = normalize(i.texcoord);

		float p = v.y;
		float p1 = 1 - pow(min(1, 1 - p), pow(0.8, v.x*v.z));
		float p2 = 1 - p1;

		half3 c_sky = _SkyColor1 * p1 + _SkyColor2 * p2;
		half3 c_sun = _SunColor * min(pow(max(0, dot(v, _WorldSpaceLightPos0.xyz)), 550), 1);

		return half4(c_sky * _SkyIntensity + c_sun * _SunIntensity , 0);
	}

		ENDCG

		SubShader
	{
		Tags{ "RenderType" = "Skybox" "Queue" = "Background" }
			Pass
		{
			ZWrite Off
			Cull Off
			Fog { Mode Off }
			CGPROGRAM
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}