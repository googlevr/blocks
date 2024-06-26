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

Shader "Mogwai/OccSilhouette"
{
  Properties
  {
    _SelectColor("SelectColor", Color) = (0.0, 0.5, 0.5, 0.2)
    _EmissiveAmount ("Emissive Amount", Float) = 0.00
    _EmissiveColor ("Emissive Color", Color) = (0.0, 0.0, 0.0, 0.0)
    _EmissivePulseFrequencyMultiplier ("Emissive Pulse Frequency Multiplier", Float) = 1.00
    _EmissivePulseAmount ("Emissive Pulse Amount", Float) = 0.00
    _Roughness("Roughness",Float) = 0.3
    _Metallic("Metallic", Float) = 0.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
    _MultiplicitiveAlpha("Multiplicitive Alpha", Float) = 1.0
  }
  SubShader
  {
    Tags { "RenderType"="Transparent" "Queue"="Transparent+5"}
    LOD 100

    Pass
    {
      Offset -1, -1
      Blend SrcAlpha OneMinusSrcAlpha
      ZTest Greater
      CGPROGRAM
      	#pragma vertex vertWithColorAndTangents
        #pragma fragment frag
        #pragma target 5.0
        #include "UnityCG.cginc"

        #include "shaderMath.cginc"
        #define INV_PI 0.31830988618

        float _MultiplicitiveAlpha;
        float4 frag (CTVertexOutput fragment) : SV_Target
        {
          float3 ennoisenedNormal;
          float4 ennoisenedColorMult;

          float3 lightOut = 0;
          float3 specOut = 0;
          evaluateLights(
            fragment.worldPosition.xyz , // pixelPos
            fragment.normal , // pixelNormal
            fragment.color, // color
            fragment.shadowPosition, // shadowPosition
            lightOut, // inout diffuseOut
            specOut);
          return float4(lightOut, 0.4 * _MultiplicitiveAlpha);
      	}
      ENDCG
    }
  }
}
