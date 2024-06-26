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

Shader "Mogwai/DTUWithOccluded"
{
  Properties
  {
 		_Color( "Color", Color ) = ( 1, 1, 1, 0 )
    _EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
    _EmissiveAmount("Emissive Amount", Float) = 1
    _Roughness("Roughness",Float) = 0.3
    _Metallic("Metallic", Float) = 0.0
    _Mirror("Mirror", Float) = 0.3
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
    _MultiplicitiveAlpha("Multiplicitive Alpha", Float) = 1.0
  }
  SubShader
  {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha
    ZTest Greater
    Pass
    {
      CGPROGRAM
      #pragma vertex vertWithTangents
      #pragma fragment frag
      #pragma target 5.0
      #include "UnityCG.cginc"
      #include "shaderMath.cginc"
      #define INV_PI 0.31830988618

      float4 _Color;

      float4 frag (TVertexOutput fragment) : SV_Target
      {
        float3 ennoisenedNormal;
        float4 ennoisenedColorMult;
        generatePapercraftColorNormal(fragment.normal, fragment.tangent, fragment.binormal, fragment.objectPosition, ennoisenedColorMult, ennoisenedNormal);

        float3 lightOut = 0;

        evaluateLights(
          fragment.worldPosition.xyz /* pixelPos */,
          ennoisenedNormal /* pixelNormal */,
          _Color * ennoisenedColorMult,
          fragment.shadowPosition /* shadowPosition */,
          lightOut /* inout diffuseOut */);
        return float4(lightOut * 0.5, _Color.a * 0.5);
      }
      ENDCG
    }

    ZTest LEqual
    Pass
    {
      CGPROGRAM
			#pragma vertex vertWithColorAndTangents
			#pragma fragment frag
      #pragma target 5.0
			#include "UnityCG.cginc"

      #include "shaderMath.cginc"
			#define INV_PI 0.31830988618

      float4 _Color;
      float _MultiplicitiveAlpha;
			float4 frag (CTVertexOutput fragment) : SV_Target
			{
        float3 ennoisenedNormal;
        float4 ennoisenedColorMult;
        generatePapercraftColorNormal(fragment.normal, fragment.tangent, fragment.binormal, fragment.objectPosition, ennoisenedColorMult, ennoisenedNormal);

        float3 lightOut = 0;
        float3 specOut = 0;
        evaluateLights(
          fragment.worldPosition.xyz , // pixelPos
          ennoisenedNormal , // pixelNormal
          _Color * ennoisenedColorMult, // color
          fragment.shadowPosition, // shadowPosition
          lightOut, // inout diffuseOut
          specOut);
        return float4(lightOut, _Color.a);
			}
      ENDCG
    }
  }
}

