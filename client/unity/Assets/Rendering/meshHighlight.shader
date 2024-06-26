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

Shader "Mogwai/meshHighlightShader"
{
  Properties
  {

    _SelectColor("SelectColor", Color) = (0.0, 0.5, 0.5, 0.2)
    _PointSphereRadius ("Sphere Radius", Float) = 0.01
    _VertexSphereRadius ("Sphere Radius", Float) = 0.005
    _MaterialColor ("Material Color", Color) = (0.5, 0.5, 0.5, 0.5)
    _EmissiveAmount ("Emissive Amount", Float) = 0.00
    _EmissiveColor ("Emissive Color", Color) = (0.0, 0.0, 0.0, 0.0)
    _EmissivePulseFrequencyMultiplier ("Emissive Pulse Frequency Multiplier", Float) = 1.00
    _EmissivePulseAmount ("Emissive Pulse Amount", Float) = 0.00
    _Roughness("Roughness",Float) = 0.3
    _Metallic("Metallic", Float) = 0.0
    _LightPercentAdjust("Light Percent", Float) = 1.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333

  }
  SubShader
  {
    Tags { "RenderType"="Opaque" "Queue"="Transparent"}
    LOD 100

    Pass
    {
      Offset -1, -1
      Blend SrcAlpha OneMinusSrcAlpha
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "shaderMath.cginc"

      float4 _MaterialColor;

      struct appdata
      {
        float4 vertex : POSITION;
        float4 color : COLOR;
        float4 normal: NORMAL;
      };

      struct v2f
      {
        //float2 selectData : TEXCOORD0;
        float4 vertex : SV_POSITION;
        float4 worldPos : TEXCOORD1;
        float4 shadowPosition : TEXCOORD2;
       // float3 selectPointWorld : TANGENT;
        float4 color : COLOR;
        float4 normal: NORMAL;
      };

      float4 _SelectColor;

      v2f vert (appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex);
        o.normal = mul(unity_ObjectToWorld, v.normal);
        o.color = float4(GammaToLinearSpace(v.color.rgb), v.color.a);
        o.shadowPosition = mul(_ShadowMatrix , o.worldPos);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {

        float3 lightOut = 0;

        float4 matColor = _SelectColor * _SelectColor.a + _MaterialColor * (1 - _SelectColor.a);

        evaluateLightsNoEmissiveNoFog(
          i.worldPos.xyz, //pixelPos
          i.normal.xyz, //pixelNormal
          matColor,
          i.shadowPosition, // shadowPosition
          lightOut); //inout diffuseOut

        float pulsePct = 0.5 + 0.5 * cos(_Time.y * _EmissivePulseFrequencyMultiplier);
        float emissiveAmount = _EmissiveAmount + pulsePct * _EmissivePulseAmount;
        lightOut = lightOut + _EmissiveColor * emissiveAmount;
        lightOut = applyFog(lightOut, length(i.worldPos.xyz - _WorldSpaceCameraPos));
        // Choose whether to use selection shading based on radius and animation time.
        return float4(lightOut, matColor.a);
      }
      ENDCG
    }
 		UsePass "VertexLit/SHADOWCASTER"
  }
}
