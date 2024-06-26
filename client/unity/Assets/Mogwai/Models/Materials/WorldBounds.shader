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
Shader "Custom/WorldBounds" {
  Properties{
    _Color("Color", Color) = (0.5, 0.5, 0.5,0)
    _GridColor("Grid Color", Color) = (1,1,1,1)
    _BoundsThickness("Thickness", Float) = 0.0025

    _SelectorPosition("Selector Position", Vector) = (0,0,0,0)
    _SelectorAlphaRadius("Selector Alpha Radius", Float) = 0.35
  }


  SubShader{
    Tags{ "Queue" = "Transparent" "RenderType" = "Opaque" }
    LOD 200

    Pass{
      ZWrite Off
      Cull Off
      Blend SrcAlpha OneMinusSrcAlpha

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      uniform float4 _Color;
      uniform float4 _GridColor;
      uniform float _BoundsThickness;
      float4 _SelectorPosition;
      float _SelectorAlphaRadius;

      struct vertexInput {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float4 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 pos : SV_POSITION;
        float4 uv : TEXCOORD0;
        float3 lpos : TEXCOORD2;
        float3 wpos : TEXCOORD3;
      };


      // VERTEX SHADER
      v2f vert(vertexInput appdata) {
        v2f output;
        output.lpos = appdata.vertex;
        output.wpos = mul(unity_ObjectToWorld, appdata.vertex);
        output.pos = UnityObjectToClipPos(appdata.vertex);
        output.uv = float4(appdata.texcoord.xy, 0, 0);
        return output;
      }

      // FRAGMENT SHADER
      float4 frag(v2f input) : COLOR{
        float4 outputColor = _Color;
        if (input.uv.x < _BoundsThickness || input.uv.x > 1 - _BoundsThickness
          || input.uv.y < _BoundsThickness || input.uv.y > 1 - _BoundsThickness) {
          outputColor = _GridColor;
        }
        else {
          // If we are using selector based alpha, calculate the distance from the selector point
          // for the fragment and determine new alpha factor.
          float distBasedAlpha = 1.0 - clamp((min(distance(_SelectorPosition, input.wpos.xyz), _SelectorAlphaRadius) / _SelectorAlphaRadius), 0.1, 1.0);
          outputColor = float4(outputColor.x, outputColor.y, outputColor.z, 0.4 * distBasedAlpha);// *distBasedAlpha;
        }
        return outputColor;
      }
      ENDCG
    }
  }
  FallBack "Diffuse"
}
