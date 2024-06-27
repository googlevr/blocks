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

﻿Shader "Mogwai/DirectionalPolyMaterial"
{
	Properties
	{
		_EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
		_EmissiveAmount("Emissive Amount", Float) = 1
    _Roughness("Roughness",Float) = 0.0
    _Metallic("Metallic", Float) = 0.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
    _MultiplicitiveAlpha("Multiplicitive Alpha", Float) = 1.0
    _Mirror("Mirror", Float) = 1.0
    _OverrideAmount("Override Amount", Float) = 0.0
    _OverrideColor("Override Color", Color) = (1, 1, 1, 1)

    _SelectorPosition("Selector Position", Vector) = (0,0,0,0)
    _SelectorAlphaRadius("Selector Alpha Radius", Float) = 0.25
	}
	SubShader
	{
		Tags {"RenderType"="OpaqueTransformed"}
		LOD 100

		Pass
		{
		    Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vertWithColorAndTangents
			#pragma fragment frag
            #pragma target 5.0
			#include "UnityCG.cginc"

            #include "shaderMath.cginc"
			#define INV_PI 0.31830988618

            float _MultiplicitiveAlpha;
            float _OverrideAmount;
            float4 _OverrideColor;
            float4 _SelectorPosition;
            float _SelectorAlphaRadius;
            
            float4 frag (CTVertexOutput fragment) : SV_Target {
                float3 ennoisenedNormal;
                float4 ennoisenedColorMult;
                float4 modelSpaceNormal = mul(fragment.meshTransform, fragment.normal);
                generatePapercraftColorNormal(fragment.normal, fragment.tangent, fragment.binormal, fragment.objectPosition, ennoisenedColorMult, ennoisenedNormal, fragment.meshTransform);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - fragment.worldPosition.xyz);
                float NDotV = dot(mul(unity_ObjectToWorld, modelSpaceNormal),  V);
                float3 ObjectNormal = mul(unity_ObjectToWorld, modelSpaceNormal);
                ennoisenedNormal = lerp(mul(unity_ObjectToWorld, modelSpaceNormal), ennoisenedNormal, smoothstep(0.1, 0.8, NDotV));
                float3 lightOut = 0;
                float3 specOut = 0;
                
                evaluateLights(
                  fragment.worldPosition.xyz , // pixelPos
                  ennoisenedNormal, // pixelNormal
                  fragment.color * ennoisenedColorMult, // color
                  fragment.shadowPosition, // shadowPosition
                  lightOut, // inout diffuseOut
                  specOut);
                // If we are using selector based alpha, calculate the distance from the selector point
                // for the fragment and determine new alpha factor.
                float distBasedAlpha = 1;
                // W component of _SelectorPosition holds a float that indicates active or inactive.
                if (_SelectorPosition.w == 1.0) { 
                  distBasedAlpha = clamp((min(distance(_SelectorPosition, fragment.worldPosition.xyz), _SelectorAlphaRadius) / _SelectorAlphaRadius), 0.1, 1.0);
                }
                float4 outColor = float4(lightOut, fragment.color.a * (_MultiplicitiveAlpha * distBasedAlpha));
                return lerp(outColor, _OverrideColor, _OverrideAmount);
			}
			ENDCG
		}
		Pass
		{
            ZWrite On ZTest LEqual Cull Off
		    Tags {"LightMode" = "ShadowCaster"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            #pragma target 5.0
			#include "UnityCG.cginc"

            #include "shaderMath.cginc"

      
              struct posOut {
                float4 pos : SV_POSITION;
              };
              
              posOut vert(struct PNCVertexInput vertex) {
                
                  posOut output = (posOut)0;
                  float4x4 xFormMat =  mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, _RemesherMeshTransforms[vertex.meshBone.x]));
                  output.pos = mul(xFormMat, vertex.position);
                  return output;
                }
              
                float4 frag (posOut fragment) : SV_Target
                {
                    return float4(1, 1, 1, 1);
                }
        ENDCG
		}
    }
    
}