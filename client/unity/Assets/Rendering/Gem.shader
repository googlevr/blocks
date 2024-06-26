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

Shader "FX/Gem"
{
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_BackfaceColor ("Backface Color", Color) = (0, 0, 0.2, 1)
		_ReflectionStrength ("Reflection Strength", Range(0.0,2.0)) = 1.0
		_EnvironmentLight ("Environment Light", Range(0.0,2.0)) = 1.0
		_EmissiveAmount ("Emission", Range(0.0,2.0)) = 0.0
		_Metallic ("Metallic", Float) = 0.0
		_Roughness ("Roughness", Float) = 0.01
		_FacetDeflection ("Facet Deflection", Float) = 1
		_FacetEntropy ("Facet Entropy", Float) = 10
		_FacetSize ("Facet Size", Float) = 0.05
		_Mirror ("Mirror", Float) = 1.0
		_RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
		[NoScaleOffset] _RefractTex ("Refraction Texture", Cube) = "" {}
	  [NoScaleOffset] _EnvCubeTex2 ("Env Texture", Cube) = "" {}
    _MultiplicitiveAlpha("Multiplicitive Alpha", Float) = 1.0
    _OverrideColor("Override Color", Color) = (0.5, 0.5, 0.5, 1)
    _OverrideAmount("Override Amount", Float) = 0
	}
	
	
  CGINCLUDE

  
      #pragma target 5.0
      #include "shaderMath.cginc"
      // Possibly not enough precision.  Should add more digits.
      #define TAU 6.28318530717958647692528676655900576839433879875021
      #define VORGRIDSIZE 0.05
      #define INVVORGRIDSIZE 20
      #define GRIDMAGNITUDE 10
      float _FacetDeflection;
      float _FacetEntropy;
      float _FacetSize;
      float invFacetSize;
      float entropy;
      #define NOISEMAG 30
      fixed4 _Color;   
      float4 _OverrideColor;
      float _OverrideAmount;
      float4 _BackfaceColor;
      samplerCUBE _RefractTex;

      
      #define triCellHeightScale  0.86602540378
//      #define triCellHeightScale 3.0
      
      half _EnvironmentLight;
      float _MultiplicitiveAlpha;
       
			struct v2f {
				float4 pos : SV_POSITION;
				float3 worldPos : TEXCOORD1;
				float3 normal : TEXCOORD2;
				float4 shadowPosition : TEXCOORD3;
				float3 tangent : TANGENT;
				float3 binormal : BINORMAL;
			};
      
      float proceduralNoise(float2 val) {
        val %= 1.0;
        float xy = (val.x + 4.0) * (val.y + 13.0) * 100000.0;
        return (fmod((fmod(xy, 13.0) + 1.0) * (fmod(xy, 123.0) + 1.0), .05) - 0.025);
      }

      
      float2 vorGridCoords2(float2 coords, float cellSize) {
        return floor(invFacetSize * coords + float2(0.5, 0.5) ) * cellSize;
      }

      float2 vorGridCoords(float2 coords, float cellSize) {

        float2 multCoords = coords * invFacetSize;
        multCoords.y = multCoords.y / triCellHeightScale;
        float offsetRow = floor(multCoords.y + 0.5) % 2;
        float notOffsetRow = 1 - offsetRow;
        return (floor(multCoords + float2(notOffsetRow * 0.5, 0.5)) + float2(offsetRow * 0.5, 0)) * float2(1, triCellHeightScale) * cellSize;
        
      
      }


      float2 randomOffset(float2 inUv, float cellSize) {
        return cellSize * 0.5 * saturate(entropy * float2(proceduralNoise(inUv.xy), proceduralNoise(inUv.xy + float2(0.1231256645, 0.2358789))));
      }      
            
      float2 perturbedPoint(float2 inPoint, float cellSize) {
        return inPoint + randomOffset(inPoint, cellSize);
      }

      static const float2 pointOffsets[7] = {
        {-0.5, triCellHeightScale},
        {0.5, triCellHeightScale},
        {-1.0, 0.0},
        {0.0, 0.0},
        {1.0, 0.0},
        {-0.5, -triCellHeightScale},
        {0.5, -triCellHeightScale}
      };


  



      float4 voronoiCell(float2 uv, float3 normal, float cellSize, out float3 closestPoint, 
      out float3 secondPoint,
      out float3 thirdPoint,
      out float3 fourthPoint) {
        float2 vUv = vorGridCoords(uv, cellSize);
        
        float2 normalFactor = float2(0, 0);//proceduralNoise(0.5 + 0.25 * normalize(normal.xyz).xy).xx;
        
        float4 nearbyPoints[8];
        nearbyPoints[7].z = 999999999999.0;
        int closest = 7;
        int second = 7;
        int third = 7;
        int fourth = 7;
        int i;
        int j;
        for(i = 0; i <7; i++) {
                 
          nearbyPoints[i] = float4(vUv + float2(cellSize, cellSize) * pointOffsets[i], 0, 0);
          nearbyPoints[i] = nearbyPoints[i] + float4(randomOffset(nearbyPoints[i] + normalFactor, cellSize), 0, 0);
          nearbyPoints[i].z = length(nearbyPoints[i].xy - uv);
          
          int a = nearbyPoints[i].z < nearbyPoints[closest].z;
          int b = nearbyPoints[i].z < nearbyPoints[second].z;
          int c = nearbyPoints[i].z < nearbyPoints[third].z;
          int d = nearbyPoints[i].z < nearbyPoints[fourth].z;
          
          fourth = d ? (!c ? i : third) : fourth;
          third = c ? (!b ? i : second) : third;
          second = b ? (!a ? i : closest) : second;
          closest = a ? i : closest;
        }
                
        closestPoint = nearbyPoints[closest];
        secondPoint = nearbyPoints[second];
        thirdPoint = nearbyPoints[third];
        fourthPoint = nearbyPoints[fourth];
         float2 ab = normalize(secondPoint.xy - closestPoint.xy);
         float2 mid = (closestPoint.xy + secondPoint.xy) * 0.5;
         float projection = dot(mid, ab);
         nearbyPoints[closest].w = projection;
        
        return nearbyPoints[closest];
      }
      
       float4 voronoiCell(float2 uv, float3 normal, float cellSize) {
       float3 a;
       float3 b;
       float3 c;
       float3 d;
          return voronoiCell(uv, normal, cellSize, a, b, c, d);
       }
      
      float3x3 ObjectToTangentMat(float3 tangent, float3 binormal, float3 normal) {
                return float3x3(tangent.x, tangent.y, tangent.z,
                          binormal.x, binormal.y, binormal.z,
                          normal.x, normal.y, normal.z);
      }
      
			v2f vert (float4 v : POSITION, float3 n : NORMAL)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v);
				o.worldPos = mul(unity_ObjectToWorld, v);
			  o.shadowPosition = mul(_ShadowMatrix, o.worldPos);

				o.normal = UnityObjectToWorldNormal(n);
				
				 // Looking for an arbitrary vector that isn't parallel to the normal.  Avoiding axis directions should improve our chances.
          float3 arbitraryVector = normalize(float3(0.42, -0.21, 0.15));
          float3 alternateArbitraryVector = normalize(float3(0.43, 1.5, 0.15));
          // If arbitrary vector is parallel to the normal, choose a different one.
          o.tangent = normalize(abs(dot(o.normal, arbitraryVector)) < 1 ? cross(o.normal, arbitraryVector) : cross(o.normal, alternateArbitraryVector));
          o.binormal = normalize(cross(o.normal, o.tangent));
				
				return o;
			}
			
      float3 RefractionVector(float fromIOR, float toIOR, float3 inVec, float3 N) {
        float ratio = fromIOR / toIOR;
        float cos0 = dot(inVec, N);
        float checkVal = 1 - ratio * ratio * (1 - cos0 * cos0);
        float3 outVec = checkVal < 0 ? float3(0, 0, 0) : (ratio * inVec + (ratio * cos0 - sqrt(checkVal))) * N;
        return outVec;
      }
      
      float3 RefractionVector2(float fromIOR, float toIOR, float3 inVec, float3 N) {
        float3 NXvec = cross(N, inVec);
        float ratio = fromIOR / toIOR;
        float3 rhs = N * sqrt(1.0 - ratio * ratio * dot(NXvec, NXvec));
        float3 NNXvec = cross(-N, inVec);
        NNXvec = cross(N, NNXvec);
        return ratio * NNXvec - rhs;
      }
      
      float4 SampleEnv(float3 sampleVec, float roughness) {
        half perceptualRoughness = roughness;
        perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
        half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
        return texCUBElod(_EnvCubeMap, float4(sampleVec, mip));
      }
      
      float4 SampleFacets(float3 sampleVec, float roughness) {
        half perceptualRoughness = roughness;
        perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
        half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
        return texCUBElod(_EnvCubeMap, float4(sampleVec, mip));
      }

      float getFresnel(float IOR, float VDotH) {
        float3 F0 = abs((1.0  - IOR) / (1.0 + IOR));
        F0 = F0 * F0;
        return evaluateFresnelSchlick(VDotH, F0);
      }
      
      float3 SampleRefraction(float3 N, float3 V, float baseIOR) {
               float3 refVec = RefractionVector(baseIOR, 1, V, N);
               return length(refVec) > 0 ? SampleEnv(refVec, _Roughness) : float3(0, 0, 0);
             }

      float3 SampleInternalRefraction(float3 N, float3 V, float baseIOR) {
        float3 refVec = RefractionVector(baseIOR, 1, V, N);
        return length(refVec) > 0 ? SampleFacets(refVec, _Roughness) : float3(0, 0, 0);
      }
      
  
      float3 SampleGemPoint(float3 worldPos, float3 normal, float4 shadowPosition) {
        float3 V = normalize(_WorldSpaceCameraPos - worldPos);
        float3 N = normalize(normal);
        float3 reflectDir = reflect(V, N);

        half3 refraction = SampleRefraction(N, V, 1.5) * _Color;
        float fresnel = length(refraction > 0) ? getFresnel(2.4, dot(V, N)) : 0;
       // return refraction;
        float3 lightOut = float3(0, 0, 0);
        float3 specOut = float3(0, 0, 0);
        evaluateLights(
                  worldPos , // pixelPos
                  N , // pixelNormal
                  _Color, // color
                  shadowPosition, // shadowPosition
                  lightOut, // inout diffuseOut
                  specOut);
        half3 reflection = specOut;
        float3 H = normalize(V + N);
        //float fresnel = getFresnel(2.4, dot(V, N));
        return (fresnel * reflection + (1 - fresnel) * refraction + lightOut * _Color.a);
      }
      
      float3 SampleGemPointFacet(float3 worldPos, float3 normal, float IOR, float4 shadowPosition) {
        float3 V = normalize(_WorldSpaceCameraPos - worldPos);
        float3 N = normalize(normal);
        float3 reflectDir = reflect(V, N);
        
        half3 refraction = SampleRefraction(N, V, IOR) * _Color;
        half3 facetReflection = SampleInternalRefraction(N, V, IOR) * _Color;
        float3 lightOut = float3(0, 0, 0);
        
        float3 specOut = float3(0, 0, 0);
        evaluateLights(
                  worldPos , // pixelPos
                  N , // pixelNormal
                  _Color, // color
                  shadowPosition, // shadowPosition
                  lightOut, // inout diffuseOut
                  specOut);
        half3 reflection = specOut;
        float3 H = normalize(V + N);
        float fresnel = getFresnel(IOR, dot(V, N));
        // map 
        
        return fresnel * lerp(reflection, facetReflection, fresnel) + (1 - fresnel) * refraction;
      }
      
      float3 borderInfo(float2 uv, float3 a, float3 b) {
        float2 ba = normalize(b.xy - a.xy);
        float2 bamid = (b.xy + a.xy) * 0.5;
        float baDist = abs(dot(uv - bamid, ba));
        float2 projected = uv + ba * baDist;
        return float3(projected, baDist);
      
      }
      
      

  ENDCG
	
	SubShader {
		Tags {
			"Queue" = "Transparent"
		}
		// First pass - here we render the backfaces of the diamonds. Since those diamonds are more-or-less
		// convex objects, this is effectively rendering the inside of them.
		Pass {

			Cull Front
			ZWrite On
    	    Offset -1, -1
      Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vertWithTangents
			#pragma fragment frag

			half4 frag (TVertexOutput i) : SV_Target
			{
			entropy = _FacetEntropy;
			float cellSize = _FacetSize * 2.0;

      invFacetSize = 1.0 / cellSize;
      float3 ennoisenedNormal;
      float4 ennoisenedColorMult;
      float2 uv = float2(dot(i.tangent, i.objectPosition), dot(i.binormal, i.objectPosition));
      float3 a;
      float3 b;
      float3 c;
      float3 d;
      float4 cell = voronoiCell(uv, normalize(i.normal), cellSize, a, b, c, d);
      
      float2 vUv = vorGridCoords(uv, cellSize);
      float distToPoint = length(a.xy - uv);
      float pointBorder = smoothstep(0, 0.01, distToPoint);
      
      float3 ba = borderInfo(uv, a, b);
      float3 cb = borderInfo(uv, b, c);
      float3 dc = borderInfo(uv, c, d);
      
      
      float shadeValA = 0.5 + 10 * proceduralNoise(a);
      float shadeValB = 0.5 + 10 * proceduralNoise(a * 10 + b);
               
      float border2 = pow(ba.z, 0.1) < 0.01 ? 0.0 : 1.0;
      float border3 = pow(cb.z, 0.1) < 0.01 ? 0.0 : 1.0;
      //return float4(pointBorder * shadeValB - border2, pointBorder * shadeValB, pointBorder * shadeValB - border3, 1);
      
      float deflectionMul = max(0.000000001, border2 * border3);
      
      float2 towardsEdge = normalize(b - a);// / ba.z;
      float2 towardsEdge2 = normalize(c - a);// / ca.z;
      float2 towardsEdge3 = normalize(c - b);
      towardsEdge = normalize((towardsEdge + towardsEdge2 + towardsEdge3));
        
        
      float3x3 objectToTangent = ObjectToTangentMat(i.tangent, i.binormal, i.normal);
      float3x3 tangentToObject = transpose(objectToTangent);
        
        float3 tangentSpaceNormal = normalize(float3(towardsEdge * _FacetDeflection * deflectionMul, 1 / (_FacetDeflection * deflectionMul)));
        ennoisenedNormal = mul(unity_ObjectToWorld, mul(i.meshTransform, mul(tangentToObject, tangentSpaceNormal)));

        float3 V = normalize(_WorldSpaceCameraPos - i.worldPosition);
        float3 N = normalize(ennoisenedNormal);
        float3 H = normalize(N + V);
        float3 reflectDir = reflect(V, N);
        
        half perceptualRoughness = _Roughness;
        perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
        half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
        
        float NDotH = saturate(dot(N, H));
        float4 diffraction = texCUBElod(_RefractTex, float4(reflectDir, mip));        
  
        


        half3 refraction = diffraction;
        float fresnel = length(refraction > 0) ? getFresnel(_RefractiveIndex, dot(V, N)) : 1;
        float3 lightOut = (float3)0;
        float3 specOut = (float3)0;
        
        float refractMix = 0.9;
        
        float4 reflection = SampleEnv(reflectDir, _Roughness) * _Color;
         evaluateLights(
                          i.worldPosition , // pixelPos
                          -ennoisenedNormal , // pixelNormal
                          _BackfaceColor, // color
                          i.shadowPosition, // shadowPosition
                          lightOut, // inout diffuseOut
                          specOut);

        float4 outColor = float4(lightOut * (1.0 - refractMix) + refraction * refractMix, 1);
				return lerp(outColor, _OverrideColor, _OverrideAmount);
			}
			ENDCG 
		}


		// Second pass - here we render the front faces of the diamonds.
		Pass {
  		Cull Back
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha
			Offset -1, -1
			CGPROGRAM
			#pragma vertex vertWithTangents
			#pragma fragment frag
			#include "UnityCG.cginc"

      static const float NUM_FACETS = 3;

			half4 frag (TVertexOutput i) : SV_Target
			{
			    entropy = _FacetEntropy;
			    invFacetSize = 1.0 / _FacetSize;
          float3 ennoisenedNormal;
          float4 ennoisenedColorMult;
          float2 uv = float2(dot(i.tangent, i.objectPosition), dot(i.binormal, i.objectPosition));
          float3 a;
          float3 b;
          float3 c;
          float3 d;
          float4 cell = voronoiCell(uv, normalize(i.normal), _FacetSize, a, b, c, d);
          
          float2 vUv = vorGridCoords(uv, _FacetSize);
          float distToPoint = length(a.xy - uv);
          float pointBorder = smoothstep(0, 0.01, distToPoint);
     
          float3 ba = borderInfo(uv, a, b);
          float3 ca = borderInfo(uv, b, c);
          float3 dc = borderInfo(uv, c, d);

         
          float shadeValA = 0.5 + 10 * proceduralNoise(a);
          float shadeValB = 0.5 + 10 * proceduralNoise(a * 10 + b);
                    
          float border = pow(smoothstep(0, 0.005, ba.z), .01);
          
          //return float4((border).xxx, 1);
          
          float deflectionMul = max(0.000000001, border);
          
          float2 towardsEdge = normalize(b - a);// / ba.z;
          float2 towardsEdge2 = normalize(c - a);// / ca.z;
          float2 towardsEdge3 = normalize(c - b);
          towardsEdge = normalize((towardsEdge + towardsEdge2));
                     
          float3x3 objectToTangent = ObjectToTangentMat(i.tangent, i.binormal, i.normal);
          float3x3 tangentToObject = transpose(objectToTangent);
          float3 tangentSpaceNormal = normalize(float3(towardsEdge * _FacetDeflection * deflectionMul, 1 / (_FacetDeflection * deflectionMul)));
               ennoisenedNormal = mul(unity_ObjectToWorld, mul(i.meshTransform, mul(tangentToObject, tangentSpaceNormal)));             
               float3 V = normalize(_WorldSpaceCameraPos - i.worldPosition);
               float3 N = normalize(ennoisenedNormal);
               float3 H = normalize(V + N);
               float3 reflectDir = reflect(V, N);
       

               float fresnel = getFresnel(2.4, dot(H, N));
               //return float4(fresnel.xxx, 1);
               float3 lightOut = (float3)0;
               float3 specOut = (float3)0;
                evaluateLights(
                                 i.worldPosition , // pixelPos
                                 ennoisenedNormal , // pixelNormal
                                 _Color, // color
                                 i.shadowPosition, // shadowPosition
                                 lightOut, // inout diffuseOut
                                 specOut);
                                 

       			float alpha = max(_Color.a, 0.8 * (1 - fresnel));
       			
       			  half perceptualRoughness = _Roughness;
                    perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
                    half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
                    float4 diffraction = texCUBElod(_RefractTex, float4(reflectDir, mip));       

       			float diffractionAmount = saturate(0.25 * dot(ennoisenedNormal, H));
       			
       			lightOut = lightOut * (1 - diffractionAmount) + lightOut * diffraction * (diffractionAmount) - specOut + specOut * diffraction;
       			// 0.5 factor on emissive is a tweak to make the selection visible, but not blown out.
            float4 outColor = float4(lightOut + _Color.rgb * _EmissiveAmount * 0.5, alpha);


       				return lerp(outColor, _OverrideColor, _OverrideAmount);
			}
			ENDCG
		}

		// Shadow casting & depth texture support -- so that gems can
        // cast shadows
        UsePass "VertexLit/SHADOWCASTER"
	}
}
