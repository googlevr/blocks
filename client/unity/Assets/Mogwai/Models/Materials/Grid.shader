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
Shader "Grid" {

  Properties{
    _GridThickness("Grid Thickness", Range(0.0, 1.0)) = 0.05
    _GridSpacing("Grid Spacing", Range(0.00, 1.000)) = 0.25
    _GridColor("Grid Color", Color) = (1.0, 1.0, 1.0, 0.5)
    _BaseColor("Base Color", Color) = (1.0, 1.0, 1.0, 0.1)
    _MarkerColor("Marker Color", Color) = (0.0, 1.0, 0.1, 1.0)
    _MarkerRadius("Marker Radius", Range(0.0, 1.0)) = 1.0
    _AxisMarkerColor("Axis Marker Color", Color) = (1.0, 1.0, 1.0, 1.0)
    _AxisMarkerThickness("Axis Marker Thickness", Range(0.0, 1.0)) = 0.5

    _SelectorPosition("Selector Position", Vector) = (0,0,0,0)
    _SelectorAlphaRadius("Selector Alpha Radius", Float) = 0.50
  }

  SubShader{
    Tags{ "Queue" = "Transparent" }

    Pass{
    ZWrite Off Cull Off
    Blend SrcAlpha OneMinusSrcAlpha

    CGPROGRAM

    #pragma vertex vert
    #pragma fragment frag

    uniform float _GridThickness;
    uniform float _GridSpacing;
    uniform float4 _GridColor;
    uniform float4 _BaseColor;
    uniform float4 _MarkerColor;
    uniform float _MarkerRadius;
    uniform float4 _AxisMarkerColor;
    uniform float _AxisMarkerThickness;
    uniform float4 _SelectorPosition;
    uniform float _SelectorAlphaRadius;

    struct vertexInput {
      float4 vertex : POSITION;
    };

    struct vertexOutput {
      float4 pos : SV_POSITION;
      float4 worldPos : TEXCOORD0;
      float4 lpos : TEXCOORD2;
    };

    // VERTEX SHADER
    vertexOutput vert(vertexInput input) {
      vertexOutput output;
      output.lpos = input.vertex;
      output.pos = UnityObjectToClipPos(input.vertex);
      output.worldPos = mul(unity_ObjectToWorld, input.vertex);
      return output;
    }

    // FRAGMENT SHADER
    float4 frag(vertexOutput input) : COLOR{
      float4 outputColor = _BaseColor;
      float gridLineRadius = (_GridThickness / 2);
      float threshTestX = _GridSpacing - fmod(abs(input.lpos.x), _GridSpacing);
      float threshTestZ = _GridSpacing - fmod(abs(input.lpos.z), _GridSpacing);
      bool xTest = false;
      bool zTest = false;

      // TEST X
      if (threshTestX >= (_GridSpacing - gridLineRadius) || threshTestX <= gridLineRadius) {
        xTest = true;
      }

      // TEST Z
      if (threshTestZ >= (_GridSpacing - gridLineRadius) || threshTestZ <= gridLineRadius) {
        zTest = true;
      }

      // GRID COLOR
      if (xTest || zTest) {
        outputColor = _GridColor;
      }

      // AXIS MARKER
      if ((input.lpos.x >= -(_AxisMarkerThickness * gridLineRadius) && input.lpos.x < (_AxisMarkerThickness * gridLineRadius))
        || (input.lpos.z >= -(_AxisMarkerThickness * gridLineRadius) && input.lpos.z < (_AxisMarkerThickness * gridLineRadius))) {
        outputColor = _AxisMarkerColor;
      }

      // CROSSTHATCH COLOR
      if (xTest && zTest) {
        float deltaX;
        float deltaZ;

        if (threshTestX >= (_GridSpacing - gridLineRadius)) {
          deltaX = _GridSpacing - threshTestX;
        }
        else if (threshTestX <= gridLineRadius) {
          deltaX = threshTestX;
        }

        if (threshTestZ >= (_GridSpacing - gridLineRadius)) {
          deltaZ = _GridSpacing - threshTestZ;
        }
        else if (threshTestZ <= gridLineRadius) {
          deltaZ = threshTestZ;
        }

        float dist = sqrt((deltaX * deltaX) + (deltaZ * deltaZ));

        if (dist <= _MarkerRadius * gridLineRadius) {
          outputColor = _MarkerColor;
        }
      }

      // If we are using selector based alpha, calculate the distance from the selector point
      // for the fragment and determine new alpha factor.
      float distBasedAlpha = 1.0;
      if (_SelectorPosition.w == 1.0) {
        distBasedAlpha = 1.0 - clamp((min(distance(_SelectorPosition, input.worldPos.xyz), _SelectorAlphaRadius) / _SelectorAlphaRadius), 0.1, 1.0);
        outputColor = float4(outputColor.x, outputColor.y, outputColor.z, outputColor.w * distBasedAlpha);
      }

      return outputColor;
    }
      ENDCG
    }
  }
}