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

Shader "Mogwai/faceExtrudeShader"
{
  Properties
  {
    _SelectColor("SelectColor", Color) = (0.0, 0.5, 0.5, 0.2)
    _DotScale ("Dot Scale", Float) = 100.0
    _MaterialColor ("Material Color", Color) = (0.5, 0.5, 0.5, 0.5)
    _EmissiveAmount ("Emissive Amount", Float) = 0.00
    _EmissiveColor ("Emissive Color", Color) = (0.0, 0.0, 0.0, 0.0)
    _EmissivePulseFrequencyMultiplier ("Emissive Pulse Frequency Multiplier", Float) = 1.00
    _EmissivePulseAmount ("Emissive Pulse Amount", Float) = 0.00
    _Roughness("Roughness",Float) = 0.3
    _Metallic("Metallic", Float) = 0.0
    _GridScale("Grid Scale", Float) = 10.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
  }
  SubShader
  {
    Tags { "RenderType"="Transparent" "Queue"="Transparent"}
    LOD 100

    Pass
    {
      Offset -1, -1
      Blend SrcAlpha OneMinusSrcAlpha
      CGPROGRAM
      #pragma vertex vertWithColorAndTangents
      #pragma fragment frag
      #pragma target 5.0
      #include "UnityCG.cginc"
      #include "shaderMath.cginc"

      float4 _MaterialColor;
      float4 _SelectColor;
      float _GridScale;
      float _DotScale;


      fixed4 frag (CTVertexOutput i) : SV_Target
      {
        // Project current point onto the plane defined by the target face
        float3 projectedPointModel = projectPointOntoPlane(i.objectPosition, i.selectPoint.xyz, i.normal);
        // Generate a tangent space coordinate for this point.
        float3x3 objToTan = genObjToTangentMat(i.normal, i.tangent, i.binormal);
        // The center of the selection, expressed in tangent coordinates for this face.
        // x,y should be orthogonal and tangent to the face, z should be along the face normal
        float3 projectedPointTangent = mul(objToTan, projectedPointModel);


        float3 choppedCoords = floor(projectedPointTangent * _GridScale + 0.5) / _GridScale;
        float2 diff = choppedCoords.xy - projectedPointTangent.xy;
        float squaredDist = _DotScale * _GridScale * dot(diff, diff);
        float4 dotTexMult = squaredDist <= 0.01 ? float4(1, 1, 1, 1) : float4(0, 0, 0, 0);
        float4 baseColor = _SelectColor * dotTexMult;

        float4 selectPointWorld = mul(unity_ObjectToWorld, float4(i.selectPoint.xyz, 1));

        //This is the current face select effect.
        float3 dirToProjected = selectPointWorld.xyz - i.worldPosition;
        float radiusThreshhold = .2;

        // Figure out where in animation we are
        float animScale = 1.0;
        // Do this to avoid branching.  Shaders don't like branching.
        animScale = lerp(0, 1, i.selectData.r);
        animScale = animScale * animScale;

        //Force animation percent to 100% if it's close in order to deal with any imprecision
        float animDoneOverride = smoothstep(1, .99, i.selectData.r);

        //Maximum radius from center to apply the effect to.
        radiusThreshhold = radiusThreshhold * animScale;

        float3 worldNormal = normalize(UnityObjectToWorldNormal(i.normal));
        float distanceToLight = length(i.worldPosition.xyz - _LightPosition);
        float3 lightOut = 0;


        //Once we figure
        evaluateLightsNoEmissiveNoFog(
          i.worldPosition.xyz, //pixelPos
          worldNormal.xyz, //pixelNormal
          baseColor,
          i.shadowPosition, // shadowPosition
          lightOut); //inout diffuseOut

        float pulsePct = 0.5 + 0.5 * cos(_Time.y * _EmissivePulseFrequencyMultiplier);
        float emissiveAmount = dotTexMult.x * _EmissiveAmount + pulsePct * _EmissivePulseAmount;
        lightOut = lightOut + i.color * emissiveAmount;
        lightOut = applyFog(lightOut, length(i.worldPosition.xyz - _WorldSpaceCameraPos));
        // Choose whether to use selection shading based on radius and animation time.
        // If not, render full transparent, which will show whatever the base shading is.
        return dotTexMult.a * (animDoneOverride * length(dirToProjected) < radiusThreshhold ? float4(lightOut, _MaterialColor.a) : float4(0, 0, 0, 0));
      }
      ENDCG
    }
  }
}
