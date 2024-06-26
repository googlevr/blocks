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
Shader "Mogwai/Shadow/Depth_ESM"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
       SubShader {		
        Tags { "RenderType"="OpaqueTransformed" }
        LOD 100
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragLinear
            
            #include "UnityCG.cginc"
            #define NUMBER_OF_MESH_XFORMS 128
            #define INV_E_20 0.00000000206115362243
            #define INV_E_10 0.00004539992976248485
            #define INV_E_40 0.000000000000000004248354255291588995329

                    
            float4x4 _RemesherMeshTransforms[NUMBER_OF_MESH_XFORMS];

            struct PNCVertexInput
            {
              float4 position : POSITION;
              float2 meshBone : TEXCOORD2;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float4 worldPosition : TEXCOORD0;
            };

            uniform float4 _ShadowInfo;
            VertexOutput vert (PNCVertexInput vertex)

            {     
              VertexOutput output = (VertexOutput)0;
              float4x4 xFormMat = mul(unity_ObjectToWorld, _RemesherMeshTransforms[vertex.meshBone.x]);
              output.worldPosition = mul(xFormMat, vertex.position);
              output.position = mul(UNITY_MATRIX_VP, output.worldPosition);
              return output;
            }
            
            float fragESM (VertexOutput fragment) : SV_Target
            {
                float depth = length(fragment.worldPosition.xyz - _WorldSpaceCameraPos.xyz)*_ShadowInfo.x;
                /// ESM, higher values increase quality, at the price of stability
                return INV_E_40 * exp(40*min(depth, 1.0));
            }


            // Stores both linear and exponential depth in the shadow texture
            float2 fragLinear (VertexOutput fragment) : SV_Target
            {
                float depth = (length(fragment.worldPosition.xyz - _WorldSpaceCameraPos.xyz))*_ShadowInfo.x;
                /// ESM, higher values increase quality, at the price of stability
                float depth2 = INV_E_40 * exp(40*min(depth - 0.002, 1.0));
                return float2(depth, depth2);
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
                LOD 100
                 Cull Back
                Pass
                {
                    CGPROGRAM
                    #pragma vertex vert
                    #pragma fragment fragLinear
                    
                    #include "UnityCG.cginc"
                    #define INV_E_20 0.00000000206115362243
                    #define INV_E_10 0.00004539992976248485
                    #define INV_E_40 0.000000000000000004248354255291588995329

                    struct VertexInput
                    {
                        float4 position : POSITION;
                    };
         
                    struct VertexOutput
                    {
                        float4 position : SV_POSITION;
                        float4 worldPosition : TEXCOORD0;
                    };
         
                    uniform float4 _ShadowInfo;
                    VertexOutput vert (VertexInput vertex)
                    {
                        VertexOutput output;
         
                        output.position = UnityObjectToClipPos(vertex.position);
                        output.worldPosition = mul(unity_ObjectToWorld, vertex.position);
                        return output;
                    }

                    float fragESM (VertexOutput fragment) : SV_Target
                    {
                        float depth = length(fragment.worldPosition.xyz - _WorldSpaceCameraPos.xyz)*_ShadowInfo.x;
                        /// ESM, higher values increase quality, at the price of stability
                        return INV_E_40 * exp(40*min(depth, 1.0));
                    }
         
                   // Stores both linear and exponential depth in the shadow texture
                   float2 fragLinear (VertexOutput fragment) : SV_Target
                   {
             
                     float depth = (length(fragment.worldPosition.xyz - _WorldSpaceCameraPos.xyz))*_ShadowInfo.x;
                     /// ESM, higher values increase quality, at the price of stability
                     float depth2 = INV_E_40 * exp(40*min(depth - 0.002, 1.0));
                     return float2(depth, depth2);
                   }
                    ENDCG
                }
         }
 
}
