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
Shader "Utility/BlocksDepthNormals"
{
   	SubShader
	{
		Pass
		{
		    Name "BaseDepthNormals"
            ZWrite On ZTest LEqual Cull Off
		    Tags {"LightMode" = "ShadowCaster" "RenderType"="OpaqueTransformed"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            #pragma target 5.0
			#include "UnityCG.cginc"

            #include "shaderMath.cginc"

      
      struct scVertOut {
        float4 pos : SV_POSITION;
        float4 rawDepthNormal : TEXCOORD0;
      };
      
      scVertOut vert(struct PNCVertexInput vertex) {
        
          scVertOut output = (scVertOut)0;
          float4x4 xFormMat = mul(unity_ObjectToWorld, _RemesherMeshTransforms[vertex.meshBone.x]);
          float4 worldPos = mul(xFormMat, vertex.position);
          output.rawDepthNormal.w = length(worldPos.xyz - _WorldSpaceCameraPos);
          output.rawDepthNormal.xyz = mul(xFormMat, vertex.normal);
          output.pos = mul(UNITY_MATRIX_VP, worldPos);
          return output;
        }
      
        float4 frag (scVertOut fragment) : SV_Target
        {
            return EncodeDepthNormal(fragment.rawDepthNormal.w, fragment.rawDepthNormal.xyz);
        }
        ENDCG
		}
	}
}