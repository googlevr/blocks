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

// This shader renders the effect for face snapping - a projected dot on the snap surface, a point light, and an
// animated burst when the snap occurs.
Shader "Mogwai/ProceduralImpact" {
  Properties {
    // Color to use for both the selection effect and the point light it casts.
    _SelectColor("SelectColor", Color) = (0.0, 0.5, 0.5, 0.2)
    // Specular color to use when evaluating the point light.
    // Length in seconds of the animation played on surface snap.
    _AnimLength("AnimLength", Float) = 0.5
    // Radius in world space of the dot that is projected onto the snap surface.
    _TargetDotRadius("TargetDotRadius", Float) = 0.005
    _Roughness("Roughness",Float) = 0.3
    _Metallic("Metallic", Float) = 0.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
  }
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent-1"}
    LOD 100
    Pass {
      Offset -1, -1
      Blend SrcAlpha OneMinusSrcAlpha
      // Geometry already rendered, don't write to zbuffer
      ZWrite Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma enable_d3d11_debug_symbols
      #include "UnityCG.cginc"
      #include "shaderMath.cginc"

      struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
      };

      struct v2f {
        float4 uv : TEXCOORD0;
        UNITY_FOG_COORDS(1)
        float4 vertex : SV_POSITION;
        float3 worldPos : POSITION1;
        float4 color : COLOR;
      };

      float _TargetDotRadius;
      float4 _ImpactObjectPosWorld;
      float4 _ImpactPointWorld;
      float4 _ImpactNormalWorld;
      
      // Standard vertex shader, with a slight push towards the camera to avoid z fighting.
      v2f vert(appdata v) {
        v2f o = (v2f)0;
        o.vertex = UnityObjectToClipPos(v.vertex);
        float4x4 worldTransform = unity_ObjectToWorld;
        o.worldPos = mul(worldTransform, v.vertex).xyz;
        // Push towards camera slightly to avoid z-fighting
        o.vertex.z = o.vertex.z + 0.001;
        o.color = v.color;
        return o;
      }

      float4 _SelectColor;
      float _EffectStartTime;
      float _AnimLength;
      float _SquishFactor;

      fixed4 frag(v2f i) : SV_Target {
        // Project current point onto the plane defined by the target face
        float3 projectedPoint = projectPointOntoPlane(i.worldPos, _ImpactPointWorld.xyz, -_ImpactNormalWorld.xyz);
        float3 planarDisplacement = projectedPoint - _ImpactPointWorld.xyz;
        float3 dirToProjected = projectedPoint - i.worldPos;

        float3 adjustedPoint = planarDisplacement + dirToProjected * _SquishFactor;
        float radiusThreshhold = _TargetDotRadius;

        // Figure out where in animation we are
        float timeInAnim = _Time.y - _EffectStartTime;
        float animScale = 1.0;
        float animAlpha = 1.0;

        // Do this to avoid branching.  Shaders don't like branching.
        float turnOffIfDone = smoothstep(0.00000001, 0, timeInAnim - _AnimLength);
        float animPct = (timeInAnim / _AnimLength);
        animScale = lerp(1.0, 10, clamp(animPct, 0, 1));
        animScale = animScale * animScale;
        animAlpha = turnOffIfDone * lerp(1.0, 0.0, clamp(animPct, 0, 1));
        radiusThreshhold = radiusThreshhold * animScale;

        // Choose whether to use selection shading based on radius and animation time.
        float threshholdSelector = smoothstep(0, 0.001, length(adjustedPoint) - radiusThreshhold);
        float innerThreshholdSelector = smoothstep(0, 0.001, length(adjustedPoint) - _TargetDotRadius);
        float4 shadingForAnimation = lerp(float4(_SelectColor.rgb, animAlpha), float4(0, 0, 0, 0), threshholdSelector);
        float4 shadingForTargetDot = lerp(float4(_SelectColor.rgb, 1), float4(0, 0, 0, 0), innerThreshholdSelector);
        return max(shadingForAnimation, shadingForTargetDot);
      }
      ENDCG
    }
  }
}
