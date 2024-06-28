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

Shader "Mogwai/MeshInsertEffect"
{
	Properties
	{
  	_EffectColor ("Effect Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Roughness ("Roughness", Float) = 0.4
		_Metallic ("Metallic", Float) = 1.0
		_RefractiveIndex ("Index of Refraction", Float) = 1.2
    _Mirror("Mirror", Float) = 1.0
		_AnimPct ("Animation percent", Range(0.0, 1.0)) = 0.0
		_MeshShaderBounds ("Min/Max Y Coord", Vector) = (0.0, 1.0, 0.0, 0.0)
		_MaxEffectEmissive("Effect Emissive", Float) = 0.4
		_AnimNoiseScale("Animation Noise Scale", Float) = 20.0
  	_AnimNoiseAmplitude("Animation Noise Amplitude", Float) = 0.125
	}

	CGINCLUDE

  #include "UnityCG.cginc"
  #include "shaderMath.cginc"
  #pragma target 5.0

  float4 _Color;
  float4 _EffectColor;
  float4 _MeshShaderBounds;
  float _AnimPct;
  float _MaxEffectEmissive;
  sampler2D _MainTex;
  float4 _MainTex_ST;
  float _AnimNoiseScale;
  float _AnimNoiseAmplitude;

  float badNoise(float param) {
    return cos(5 * param);
  }

  fixed4 frag (CTVertexOutput i) : SV_Target
  {

    float boundsHeight = (_MeshShaderBounds.y - _MeshShaderBounds.x);
    float3 lightOut;

    // Do cheap, fake noise for animated wave.  It's good enough.
    float yScale = i.worldPosition.y/boundsHeight;
    float animNoiseShift = 10 * _AnimPct;
    float noise = sin(i.worldPosition.x * _AnimNoiseScale + animNoiseShift)
        + sin(i.worldPosition.z * _AnimNoiseScale + animNoiseShift);
    noise = noise * 0.24 * boundsHeight;

    float4 effectColor = (_EffectColor.rgba + i.color.rgba) * 0.5;

    float yPivot = _MeshShaderBounds.x + _AnimPct * (boundsHeight * 1.4) + noise * _AnimNoiseAmplitude;
    float distanceIn = max( 0, yPivot - i.worldPosition.y);
    float effectPct = saturate(i.worldPosition.y <= yPivot ? 1 - (distanceIn / (0.4 * boundsHeight)) : 0);

    float matAlpha = i.worldPosition.y <= yPivot ? 1 * i.color.a : 0.3 * i.color.a;

    float3 ennoisenedNormal;
    float4 ennoisenedColorMult;
    generatePapercraftColorNormal(i.normal, i.tangent, i.binormal, i.objectPosition, ennoisenedColorMult, ennoisenedNormal);

    float3 endMatColor =  i.worldPosition.y <= yPivot ? i.color.rgb * ennoisenedColorMult.rgb : i.color.rgb;
    float3 endNormal = i.worldPosition.y <= yPivot ? ennoisenedNormal : i.normal;
    float4 inColor = float4(lerp(endMatColor, effectColor.rgb, effectPct), 1);


    evaluateLights(i.worldPosition, endNormal, float4(endMatColor, 1) /*inColor*/, lightOut);

    lightOut = lightOut + effectColor * effectPct * _MaxEffectEmissive;

    return fixed4(lightOut.xyz, matAlpha);
  }
  ENDCG

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Transparent"}
		Blend SrcAlpha OneMinusSrcAlpha
		LOD 100

		Pass
		{
		  //Render backfaces bc transparent
  		Cull Front
  		ZWrite Off

			CGPROGRAM
			#pragma vertex vertWithColorTangentsFlippedNormal
			#pragma fragment frag

			ENDCG
		}

		Pass
    		{
    			CGPROGRAM
    			#pragma vertex vertWithColorAndTangents
    			#pragma fragment frag

    			ENDCG
    		}
 		UsePass "VertexLit/SHADOWCASTER"
	}
}
