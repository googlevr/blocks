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

Shader "Mogwai/DirectionalTransparentUniform"
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
    _OverrideColor("Override Color", Color) = (0.5, 0.5, 0.5, 1)
    _OverrideAmount("Override Amount", Float) = 0
  }
  SubShader
  {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" }
    LOD 100
    Pass
    {
      Cull Front
    	Blend DstColor Zero
    	ZWrite Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 5.0
      #include "UnityCG.cginc"
      #include "UnityImageBasedLighting.cginc"
      #include "shaderMath.cginc"
      #define INV_PI 0.31830988618

      struct VertexInput
      {
        float4 position : POSITION;
        float3 normal : NORMAL;
      };

      struct VertexOutput
      {
        float4 position : SV_POSITION;
        float3 normal : TEXCOORD0;
        float4 worldPosition : TEXCOORD1;

        float4 shadowPosition : TEXCOORD2;
        float4 grabPos : TEXCOORD3;
        float3 uv : TEXCOORD4;
      };

      VertexOutput vert (VertexInput vertex)
      {
        VertexOutput output;
        output.position = UnityObjectToClipPos(vertex.position);
        output.normal = UnityObjectToWorldNormal( vertex.normal );
        output.worldPosition = mul(unity_ObjectToWorld, vertex.position);
        output.shadowPosition = mul(_ShadowMatrix, output.worldPosition);
        output.grabPos = ComputeGrabScreenPos(output.position);
        return output;
      }

      float4 _Color;
      sampler2D PreTransparencyTexture;
      float _MultiplicitiveAlpha;
      float4 _OverrideColor;
      float _OverrideAmount;

      float4 frag (VertexOutput fragment) : SV_Target
      {
        float3 lightOut = 0;
        float3 specOut = 0;
        evaluateLights(
          fragment.worldPosition.xyz , // pixelPos
          -fragment.normal , // pixelNormal
          _Color, // color
          fragment.shadowPosition, // shadowPosition
          lightOut, // inout diffuseOut
          specOut);
        float4 dst =  float4(lightOut * _Color.a + specOut * max(length(specOut * specOut), _Color.a) * _MultiplicitiveAlpha + _EmissiveColor.rgb * _EmissiveAmount, 1);
        return lerp(dst, float4(0, 0, 0, 0), _OverrideAmount);
      }
      ENDCG
    }

    Pass
    {
      Cull Back
    	Blend DstColor Zero
    	ZWrite Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 5.0
      #include "UnityCG.cginc"
      #include "UnityImageBasedLighting.cginc"
      #include "shaderMath.cginc"
      #define INV_PI 0.31830988618

      struct VertexInput
      {
        float4 position : POSITION;
        float3 normal : NORMAL;
      };

      struct VertexOutput
      {
        float4 position : SV_POSITION;
        float3 normal : TEXCOORD0;
        float4 worldPosition : TEXCOORD1;

        float4 shadowPosition : TEXCOORD2;
        float4 grabPos : TEXCOORD3;
        float3 uv : TEXCOORD4;
      };

      VertexOutput vert (VertexInput vertex)
      {
        VertexOutput output;
        output.position = UnityObjectToClipPos(vertex.position);
        output.normal = UnityObjectToWorldNormal( vertex.normal );
        output.worldPosition = mul(unity_ObjectToWorld, vertex.position);
        output.shadowPosition = mul(_ShadowMatrix, output.worldPosition);
        output.grabPos = ComputeGrabScreenPos(output.position);
        return output;
      }

      float4 _Color;
      sampler2D PreTransparencyTexture;
      float _MultiplicitiveAlpha;
      float4 _OverrideColor;
      float _OverrideAmount;
      float4 frag (VertexOutput fragment) : SV_Target
      {
        float3 lightOut = 0;
        float3 specOut = 0;
        evaluateLights(
          fragment.worldPosition.xyz , // pixelPos
          fragment.normal , // pixelNormal
          _Color, // color
          fragment.shadowPosition, // shadowPosition
          lightOut, // inout diffuseOut
          specOut);
        float4 dst =  float4(lightOut * _Color.a + specOut * max(length(specOut * specOut), _Color.a) * _MultiplicitiveAlpha + _EmissiveColor.rgb * _EmissiveAmount, 1);
        return lerp(dst, _OverrideColor, _OverrideAmount);
      }
      ENDCG
    }
  }
}
