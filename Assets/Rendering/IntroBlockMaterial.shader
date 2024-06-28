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

Shader "Mogwai/IntroBlockMat"
{
	Properties
	{
		_Color( "Color", Color ) = ( 1, 1, 1, 1 )
		_EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
    _EmissiveAmount("Emissive Amount", Float) = 1
    _Roughness("Roughness", Float) = 0.8
    _Metallic("Metallic", Float) = 0.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.3
    _Mirror("Mirror", Float) = 0.1
    _OverrideColor("Override Color", Color) = (0.5, 0.5, 0.5, 1)
    _OverrideAmount("Override Amount", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
		  Name "BasePassUniform"
			CGPROGRAM
			#pragma vertex vertWithTangents
			#pragma fragment frag
      #pragma target 5.0
			#include "UnityCG.cginc"
      #include "shaderMath.cginc"
			#define INV_PI 0.31830988618

			float4 _Color;
      float4 _OverrideColor;
      float _OverrideAmount;

			float4 frag (TVertexOutput fragment) : SV_Target
			{
        float3 ennoisenedNormal;
        float4 ennoisenedColorMult;
        generatePapercraftColorNormal(fragment.normal, fragment.tangent, fragment.binormal, fragment.objectPosition, ennoisenedColorMult, ennoisenedNormal);

        float3 lightOut = 0;
        _NoiseScale = 450;
        _EnviroStrength = 1.2;
        _LightColor = _LightColor * 3.7;
        _FillLightColor = _FillLightColor * 2.5;
        _LightDirection = float4(normalize(float3(-0.2, -0.5, 0)), 0);
        evaluateLightsNoFog(
          fragment.worldPosition.xyz /* pixelPos */,
          ennoisenedNormal /* pixelNormal */,
          _Color * ennoisenedColorMult,
          fragment.shadowPosition /* shadowPosition */,
          lightOut /* inout diffuseOut */);
        float4 outColor = float4(lightOut, _Color.a) + float4(_EmissiveColor * _EmissiveAmount, _EmissiveAmount);
        return lerp(outColor, _OverrideColor, _OverrideAmount);
			}
			ENDCG
		}
		UsePass "VertexLit/SHADOWCASTER"
	}
}