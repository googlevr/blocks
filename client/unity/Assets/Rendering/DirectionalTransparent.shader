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

Shader "Mogwai/DirectionalTransparent"
{
  Properties
  {
    _EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
    _EmissiveAmount("Emissive Amount", Float) = 1
    _Roughness("Roughness",Float) = 0.9
    _Metallic("Metallic", Float) = 0.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.0
    _MultiplicitiveAlpha("Multiplicitive Alpha", Float) = 0.3
  }
  SubShader
  {
    Tags { "RenderType"="Opaque" "Queue"="Transparent+8" }
    LOD 100

    ZWrite Off


    Pass
    {
      Offset -1, -1
      Cull Front
      ZWrite Off
		  Blend SrcAlpha OneMinusSrcAlpha
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 5.0
      #include "UnityCG.cginc"
      #include "shaderMath.cginc"
      #define INV_PI 0.31830988618

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
        float4 grabPos : TEXCOORD3;
        float4 color : COLOR;
        float3 tangentPos : TANGENT;
        float3 tangentSpaceNormal : TEXCOORD4;
      };

      VertexOutput vert (VertexInput vertex)
      {
        VertexOutput output = (VertexOutput)0;
        output.position = UnityObjectToClipPos(vertex.position);
        output.tangentPos = vertex.position;
        output.normal = UnityObjectToWorldNormal( -vertex.normal );
        output.worldPosition = mul(unity_ObjectToWorld, vertex.position);
        output.shadowPosition = mul(_ShadowMatrix, output.worldPosition);
        output.grabPos = ComputeGrabScreenPos(output.position);
        output.color = float4(GammaToLinearSpace(vertex.color.rgb), vertex.color.a);
        return output;
      }

      float _MultiplicitiveAlpha;

      float4 frag (VertexOutput fragment) : SV_Target
      {
        float3 normal = normalize(fragment.normal);
        float distanceToLight = length(fragment.worldPosition.xyz - _LightPosition);
        float3 lightOut = 0;

        float3 rawSeed = fragment.tangentPos;
        rawSeed = floor(rawSeed * 10) / 10;

        evaluateLights(
          fragment.worldPosition.xyz /* pixelPos */,
          fragment.normal /* pixelNormal */,
          fragment.color,
          fragment.shadowPosition /* shadowPosition */,
          lightOut /* inout diffuseOut */);

        float4 finalMatColor = float4(lightOut, fragment.color.a);

        float3 totalBeforeFog = finalMatColor.rgb * finalMatColor.a * _MultiplicitiveAlpha + _EmissiveColor.rgb * _EmissiveAmount;
        float cameraDistance = length(fragment.worldPosition.xyz - _WorldSpaceCameraPos);
        float fogFactor = fogAmount(cameraDistance);
        return float4(fogFactor * totalBeforeFog + (1 - fogFactor) * _FogColor, fragment.color.a * _MultiplicitiveAlpha);
      }
      ENDCG
    }

    Pass
    {
      Offset -1, -1
      Blend SrcAlpha OneMinusSrcAlpha
      ZWrite On
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 5.0

      #include "UnityCG.cginc"
      #include "shaderMath.cginc"
      #define INV_PI 0.31830988618

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
        float4 grabPos : TEXCOORD3;
        float4 color : COLOR;
        float3 tangentPos : TANGENT;
        float3 tangentSpaceNormal : TEXCOORD4;
      };

      VertexOutput vert (VertexInput vertex)
      {
        VertexOutput output = (VertexOutput)0;
        output.position = UnityObjectToClipPos(vertex.position);
        output.tangentPos = vertex.position;
        output.normal = UnityObjectToWorldNormal( vertex.normal );
        output.worldPosition = mul(unity_ObjectToWorld, vertex.position);
        output.shadowPosition = mul(_ShadowMatrix, output.worldPosition);
        output.grabPos = ComputeGrabScreenPos(output.position);
        output.color = float4(GammaToLinearSpace(vertex.color.rgb), vertex.color.a);
        return output;
      }

      float _MultiplicitiveAlpha;

      float4 frag (VertexOutput fragment) : SV_Target
      {
        float3 normal = normalize(fragment.normal);
        float distanceToLight = length(fragment.worldPosition.xyz - _LightPosition);
        float3 lightOut = 0;

        float3 rawSeed = fragment.tangentPos;
        rawSeed = floor(rawSeed * 10) / 10;

        evaluateLights(
          fragment.worldPosition.xyz /* pixelPos */,
          fragment.normal /* pixelNormal */,
          fragment.color,
          fragment.shadowPosition /* shadowPosition */,
          lightOut /* inout diffuseOut */);

        float4 finalMatColor = float4(lightOut, fragment.color.a);

        float3 totalBeforeFog = finalMatColor.rgb * finalMatColor.a + _EmissiveColor.rgb * _EmissiveAmount;
        float cameraDistance = length(fragment.worldPosition.xyz - _WorldSpaceCameraPos);
        float fogFactor = fogAmount(cameraDistance);
        return float4(fogFactor * totalBeforeFog + (1 - fogFactor) * _FogColor, finalMatColor.a * _MultiplicitiveAlpha);
      }
      ENDCG
    }
    UsePass "VertexLit/SHADOWCASTER"
  }
}
