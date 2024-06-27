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

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Shader created with Shader Forge v1.34 
// Shader Forge (c) Neat Corporation / Joachim Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.34;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,lico:1,lgpr:1,limd:2,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:False,hqlp:False,rprd:False,enco:False,rmgx:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:0,bdst:1,dpts:2,wrdp:True,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:False,qofs:0,qpre:1,rntp:1,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:1,fgcg:0.8431373,fgcb:0.7294118,fgca:1,fgde:0.017,fgrn:0,fgrf:300,stcl:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False;n:type:ShaderForge.SFN_Final,id:9361,x:33209,y:32712,varname:node_9361,prsc:2|diff-9158-OUT,emission-1140-OUT;n:type:ShaderForge.SFN_Clamp01,id:4953,x:28737,y:31473,cmnt:FogMask,varname:node_4953,prsc:2|IN-7277-OUT;n:type:ShaderForge.SFN_Slider,id:5606,x:28180,y:31729,ptovrint:False,ptlb:FogDistance,ptin:_FogDistance,varname:_FogDepth_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:20;n:type:ShaderForge.SFN_Slider,id:3737,x:27739,y:31723,ptovrint:False,ptlb:FogCurve,ptin:_FogCurve,varname:_FogNearCuttoff_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:2,max:10;n:type:ShaderForge.SFN_Tex2d,id:4071,x:31497,y:30561,ptovrint:False,ptlb:Clouds,ptin:_Clouds,varname:_FogPattern_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:c1178dfbbc5ee1f4b82d214652566d2b,ntxv:1,isnm:False|UVIN-4496-UVOUT;n:type:ShaderForge.SFN_TexCoord,id:7535,x:30950,y:30481,varname:node_7535,prsc:2,uv:1,uaff:False;n:type:ShaderForge.SFN_Panner,id:4496,x:31293,y:30561,varname:node_4496,prsc:2,spu:0.05,spv:0|UVIN-7535-UVOUT,DIST-3894-TSL;n:type:ShaderForge.SFN_Time,id:3894,x:30950,y:30653,varname:node_3894,prsc:2;n:type:ShaderForge.SFN_Lerp,id:8446,x:31564,y:30986,varname:node_8446,prsc:2|B-4071-RGB;n:type:ShaderForge.SFN_Lerp,id:9158,x:31785,y:33792,varname:node_9158,prsc:2|A-6609-OUT,B-919-OUT,T-6283-OUT;n:type:ShaderForge.SFN_Vector1,id:919,x:31495,y:33874,varname:node_919,prsc:2,v1:0;n:type:ShaderForge.SFN_Tex2d,id:7756,x:29726,y:34124,ptovrint:False,ptlb:FGTexture,ptin:_FGTexture,varname:node_7756,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:49f8294a6ca0e8a44b975eb986aee4f2,ntxv:0,isnm:False|UVIN-4623-OUT;n:type:ShaderForge.SFN_TexCoord,id:5034,x:29181,y:34124,varname:node_5034,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_Multiply,id:4623,x:29483,y:34124,varname:node_4623,prsc:2|A-5034-UVOUT,B-5341-OUT;n:type:ShaderForge.SFN_Vector1,id:5341,x:29181,y:34292,varname:node_5341,prsc:2,v1:100;n:type:ShaderForge.SFN_Tex2d,id:9697,x:29603,y:34965,ptovrint:False,ptlb:BGTexture,ptin:_BGTexture,varname:_FGTexture_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:4c270811b2b358b4296f2c1a8573c805,ntxv:0,isnm:False|UVIN-5034-UVOUT;n:type:ShaderForge.SFN_Lerp,id:6609,x:30690,y:34295,varname:node_6609,prsc:2|A-7316-OUT,B-2663-OUT,T-9037-OUT;n:type:ShaderForge.SFN_TexCoord,id:1990,x:26947,y:31477,varname:node_1990,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_RemapRange,id:2740,x:27122,y:31477,varname:node_2740,prsc:2,frmn:0,frmx:1,tomn:-1,tomx:1|IN-1990-UVOUT;n:type:ShaderForge.SFN_Multiply,id:4958,x:27300,y:31477,varname:node_4958,prsc:2|A-2740-OUT,B-2740-OUT;n:type:ShaderForge.SFN_Add,id:7712,x:27672,y:31473,varname:node_7712,prsc:2|A-8040-R,B-8040-G;n:type:ShaderForge.SFN_ComponentMask,id:8040,x:27477,y:31473,varname:node_8040,prsc:2,cc1:0,cc2:1,cc3:-1,cc4:-1|IN-4958-OUT;n:type:ShaderForge.SFN_Multiply,id:7277,x:28538,y:31473,varname:node_7277,prsc:2|A-8069-OUT,B-5606-OUT;n:type:ShaderForge.SFN_Power,id:8069,x:28274,y:31473,varname:node_8069,prsc:2|VAL-7712-OUT,EXP-3737-OUT;n:type:ShaderForge.SFN_Vector1,id:1688,x:29242,y:31794,varname:node_1688,prsc:2,v1:0;n:type:ShaderForge.SFN_Color,id:8530,x:29596,y:31613,ptovrint:False,ptlb:FogColor,ptin:_FogColor,varname:node_8530,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.882353,c2:0.9568628,c3:0.8705883,c4:1;n:type:ShaderForge.SFN_Fresnel,id:1998,x:28298,y:32607,varname:node_1998,prsc:2|EXP-1983-OUT;n:type:ShaderForge.SFN_Color,id:313,x:29151,y:31931,ptovrint:False,ptlb:FresnelColor,ptin:_FresnelColor,varname:_FogColor_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.882353,c2:0.9568628,c3:0.8705883,c4:1;n:type:ShaderForge.SFN_Clamp01,id:4156,x:29926,y:32301,cmnt:EmissiveMask,varname:node_4156,prsc:2|IN-8109-OUT;n:type:ShaderForge.SFN_Slider,id:1983,x:27878,y:32639,ptovrint:False,ptlb:FresnelExp,ptin:_FresnelExp,varname:_FogPowerCurve_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0.5,cur:2,max:10;n:type:ShaderForge.SFN_Color,id:9902,x:29340,y:33677,ptovrint:False,ptlb:DiffuseColorA,ptin:_DiffuseColorA,varname:node_9902,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.5,c2:0.5,c3:0.5,c4:1;n:type:ShaderForge.SFN_Lerp,id:7316,x:30163,y:34076,varname:node_7316,prsc:2|A-9902-RGB,B-5920-RGB,T-8909-OUT;n:type:ShaderForge.SFN_Color,id:5920,x:29340,y:33897,ptovrint:False,ptlb:DiffuseColorB,ptin:_DiffuseColorB,varname:node_5920,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.5,c2:0.5,c3:0.5,c4:1;n:type:ShaderForge.SFN_Lerp,id:2663,x:30104,y:34604,varname:node_2663,prsc:2|A-9902-RGB,B-5920-RGB,T-8518-OUT;n:type:ShaderForge.SFN_Slider,id:5397,x:27459,y:32394,ptovrint:False,ptlb:FresnelMaskCurve,ptin:_FresnelMaskCurve,varname:node_5397,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:10;n:type:ShaderForge.SFN_Slider,id:9331,x:27910,y:32395,ptovrint:False,ptlb:FresnelDistance,ptin:_FresnelDistance,varname:node_9331,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:20;n:type:ShaderForge.SFN_Power,id:8404,x:28039,y:32215,varname:node_8404,prsc:2|VAL-7712-OUT,EXP-5397-OUT;n:type:ShaderForge.SFN_Multiply,id:1329,x:28259,y:32215,varname:node_1329,prsc:2|A-8404-OUT,B-9331-OUT;n:type:ShaderForge.SFN_Lerp,id:2991,x:29870,y:31740,varname:node_2991,prsc:2|A-8574-OUT,B-8530-RGB,T-6008-OUT;n:type:ShaderForge.SFN_Clamp01,id:5244,x:28841,y:32220,cmnt:FresnelMask,varname:node_5244,prsc:2|IN-7832-OUT;n:type:ShaderForge.SFN_Lerp,id:8574,x:29523,y:31908,varname:node_8574,prsc:2|A-1688-OUT,B-313-RGB,T-4003-OUT;n:type:ShaderForge.SFN_Add,id:8109,x:29686,y:32301,varname:node_8109,prsc:2|A-6008-OUT,B-5244-OUT;n:type:ShaderForge.SFN_Slider,id:7824,x:28646,y:31686,ptovrint:False,ptlb:FogStrength,ptin:_FogStrength,varname:node_7824,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:1;n:type:ShaderForge.SFN_Multiply,id:6008,x:28975,y:31473,varname:node_6008,prsc:2|A-4953-OUT,B-7824-OUT;n:type:ShaderForge.SFN_Lerp,id:6798,x:30231,y:31901,varname:node_6798,prsc:2|A-2991-OUT,B-313-RGB,T-6433-OUT;n:type:ShaderForge.SFN_Multiply,id:4003,x:29293,y:32155,varname:node_4003,prsc:2|A-5244-OUT,B-3122-OUT;n:type:ShaderForge.SFN_Slider,id:4430,x:28713,y:32504,ptovrint:False,ptlb:FresnelOnTopOfFog,ptin:_FresnelOnTopOfFog,varname:node_4430,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:1;n:type:ShaderForge.SFN_OneMinus,id:3122,x:29043,y:32343,varname:node_3122,prsc:2|IN-4430-OUT;n:type:ShaderForge.SFN_Multiply,id:6433,x:29957,y:32033,varname:node_6433,prsc:2|A-5244-OUT,B-4430-OUT;n:type:ShaderForge.SFN_Multiply,id:7832,x:28532,y:32301,varname:node_7832,prsc:2|A-1329-OUT,B-1998-OUT;n:type:ShaderForge.SFN_Multiply,id:6283,x:30382,y:32297,varname:node_6283,prsc:2|A-4156-OUT,B-9037-OUT;n:type:ShaderForge.SFN_Multiply,id:1140,x:30581,y:31959,varname:node_1140,prsc:2|A-6798-OUT,B-9037-OUT;n:type:ShaderForge.SFN_Clamp01,id:9037,x:30137,y:35039,cmnt:BGMask,varname:node_9037,prsc:2|IN-315-OUT;n:type:ShaderForge.SFN_RemapRangeAdvanced,id:315,x:29877,y:35142,varname:node_315,prsc:2|IN-9697-A,IMIN-9822-OUT,IMAX-4405-OUT,OMIN-2684-OUT,OMAX-2007-OUT;n:type:ShaderForge.SFN_Vector1,id:9822,x:29592,y:35155,varname:node_9822,prsc:2,v1:0;n:type:ShaderForge.SFN_Vector1,id:4405,x:29592,y:35220,varname:node_4405,prsc:2,v1:1;n:type:ShaderForge.SFN_Slider,id:2684,x:29435,y:35336,ptovrint:False,ptlb:BGMaskNear,ptin:_BGMaskNear,varname:node_2684,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:-10;n:type:ShaderForge.SFN_Slider,id:2007,x:29435,y:35449,ptovrint:False,ptlb:BGMaskFar,ptin:_BGMaskFar,varname:node_2007,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:1,cur:1,max:20;n:type:ShaderForge.SFN_Vector1,id:4087,x:29339,y:33415,varname:node_4087,prsc:2,v1:0;n:type:ShaderForge.SFN_Lerp,id:8518,x:29887,y:34757,varname:node_8518,prsc:2|A-2296-OUT,B-9697-R,T-8707-OUT;n:type:ShaderForge.SFN_Vector1,id:2296,x:29647,y:34565,varname:node_2296,prsc:2,v1:0.5;n:type:ShaderForge.SFN_Lerp,id:8909,x:29974,y:34152,varname:node_8909,prsc:2|A-2296-OUT,B-7756-R,T-8707-OUT;n:type:ShaderForge.SFN_Slider,id:8707,x:29462,y:34731,ptovrint:False,ptlb:TextureStrength,ptin:_TextureStrength,varname:node_8707,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.5,max:1;proporder:5606-3737-7824-8530-313-9331-5397-1983-4430-4071-9697-7756-9902-5920-8707-2684-2007;pass:END;sub:END;*/

Shader "VoidWorld/BarrenTerrain" {
    Properties {
        _FogDistance ("FogDistance", Range(0, 20)) = 1
        _FogCurve ("FogCurve", Range(0, 10)) = 2
        _FogStrength ("FogStrength", Range(0, 1)) = 1
        _FogColor ("FogColor", Color) = (0.882353,0.9568628,0.8705883,1)
        _FresnelColor ("FresnelColor", Color) = (0.882353,0.9568628,0.8705883,1)
        _FresnelDistance ("FresnelDistance", Range(0, 20)) = 1
        _FresnelMaskCurve ("FresnelMaskCurve", Range(0, 10)) = 1
        _FresnelExp ("FresnelExp", Range(0.5, 10)) = 2
        _FresnelOnTopOfFog ("FresnelOnTopOfFog", Range(0, 1)) = 0
        _Clouds ("Clouds", 2D) = "gray" {}
        _BGTexture ("BGTexture", 2D) = "white" {}
        _FGTexture ("FGTexture", 2D) = "white" {}
        _DiffuseColorA ("DiffuseColorA", Color) = (0.5,0.5,0.5,1)
        _DiffuseColorB ("DiffuseColorB", Color) = (0.5,0.5,0.5,1)
        _TextureStrength ("TextureStrength", Range(0, 1)) = 0.5
        _BGMaskNear ("BGMaskNear", Range(0, -10)) = 0
        _BGMaskFar ("BGMaskFar", Range(1, 20)) = 1
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDBASE
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "PenumbraShadows.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform float _FogDistance;
            uniform float _FogCurve;
            uniform sampler2D _FGTexture; uniform float4 _FGTexture_ST;
            uniform sampler2D _BGTexture; uniform float4 _BGTexture_ST;
            uniform float4 _FogColor;
            uniform float4 _FresnelColor;
            uniform float _FresnelExp;
            uniform float4 _DiffuseColorA;
            uniform float4 _DiffuseColorB;
            uniform float _FresnelMaskCurve;
            uniform float _FresnelDistance;
            uniform float _FogStrength;
            uniform float _FresnelOnTopOfFog;
            uniform float _BGMaskNear;
            uniform float _BGMaskFar;
            uniform float _TextureStrength;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float4 shadowPosition : TEXCOORD6;
                LIGHTING_COORDS(3,4)
                UNITY_FOG_COORDS(5)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos(v.vertex );
                o.shadowPosition = mul(_ShadowMatrix, o.posWorld);
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 _Tint;
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;
                float3 lightDirection = normalize(-_LightDirection);
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
/////// Diffuse:
                float NdotL = max(0.0,dot( normalDirection, lightDirection ));
                float shadows = 0.5 + 0.5 * calcShadow(i.posWorld, normalDirection, i.shadowPosition); 
                float3 directDiffuse = shadows * max( 0.0, NdotL) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                indirectDiffuse += UNITY_LIGHTMODEL_AMBIENT.rgb; // Ambient Light
                float node_2296 = 0.5;
                float2 node_4623 = (i.uv0*100.0);
                float4 _FGTexture_var = tex2D(_FGTexture,TRANSFORM_TEX(node_4623, _FGTexture));
                float4 _BGTexture_var = tex2D(_BGTexture,TRANSFORM_TEX(i.uv0, _BGTexture));
                float node_9822 = 0.0;
                float node_9037 = saturate((_BGMaskNear + ( (_BGTexture_var.a - node_9822) * (_BGMaskFar - _BGMaskNear) ) / (1.0 - node_9822))); // BGMask
                float node_919 = 0.0;
                float2 node_2740 = (i.uv0*2.0+-1.0);
                float2 node_8040 = (node_2740*node_2740).rg;
                float node_7712 = (node_8040.r+node_8040.g);
                float node_4953 = saturate((pow(node_7712,_FogCurve)*_FogDistance)); // FogMask
                float node_6008 = (node_4953*_FogStrength);
                float node_1998 = pow(1.0-max(0,dot(normalDirection, viewDirection)),_FresnelExp);
                float node_5244 = saturate(((pow(node_7712,_FresnelMaskCurve)*_FresnelDistance)*node_1998)); // FresnelMask
                float3 diffuseColor = lerp(lerp(lerp(_DiffuseColorA.rgb,_DiffuseColorB.rgb,lerp(node_2296,_FGTexture_var.r,_TextureStrength)),lerp(_DiffuseColorA.rgb,_DiffuseColorB.rgb,lerp(node_2296,_BGTexture_var.r,_TextureStrength)),node_9037),float3(node_919,node_919,node_919),(saturate((node_6008+node_5244))*node_9037));
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
////// Emissive:
                float node_1688 = 0.0;
                float3 emissive = (lerp(lerp(lerp(float3(node_1688,node_1688,node_1688),_FresnelColor.rgb,(node_5244*(1.0 - _FresnelOnTopOfFog))),_FogColor.rgb,node_6008),_FresnelColor.rgb,(node_5244*_FresnelOnTopOfFog))*node_9037);
/// Final Color:
                float3 finalColor = diffuse + emissive;
                fixed4 finalRGBA = fixed4(finalColor,1);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA * _Tint;
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDADD
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform float _FogDistance;
            uniform float _FogCurve;
            uniform sampler2D _FGTexture; uniform float4 _FGTexture_ST;
            uniform sampler2D _BGTexture; uniform float4 _BGTexture_ST;
            uniform float4 _FogColor;
            uniform float4 _FresnelColor;
            uniform float _FresnelExp;
            uniform float4 _DiffuseColorA;
            uniform float4 _DiffuseColorB;
            uniform float _FresnelMaskCurve;
            uniform float _FresnelDistance;
            uniform float _FogStrength;
            uniform float _FresnelOnTopOfFog;
            uniform float _BGMaskNear;
            uniform float _BGMaskFar;
            uniform float _TextureStrength;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                LIGHTING_COORDS(3,4)
                UNITY_FOG_COORDS(5)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos(v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 _Tint;
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
/////// Diffuse:
                float NdotL = max(0.0,dot( normalDirection, lightDirection ));
                float3 directDiffuse = max( 0.0, NdotL) * attenColor;
                float node_2296 = 0.5;
                float2 node_4623 = (i.uv0*100.0);
                float4 _FGTexture_var = tex2D(_FGTexture,TRANSFORM_TEX(node_4623, _FGTexture));
                float4 _BGTexture_var = tex2D(_BGTexture,TRANSFORM_TEX(i.uv0, _BGTexture));
                float node_9822 = 0.0;
                float node_9037 = saturate((_BGMaskNear + ( (_BGTexture_var.a - node_9822) * (_BGMaskFar - _BGMaskNear) ) / (1.0 - node_9822))); // BGMask
                float node_919 = 0.0;
                float2 node_2740 = (i.uv0*2.0+-1.0);
                float2 node_8040 = (node_2740*node_2740).rg;
                float node_7712 = (node_8040.r+node_8040.g);
                float node_4953 = saturate((pow(node_7712,_FogCurve)*_FogDistance)); // FogMask
                float node_6008 = (node_4953*_FogStrength);
                float node_1998 = pow(1.0-max(0,dot(normalDirection, viewDirection)),_FresnelExp);
                float node_5244 = saturate(((pow(node_7712,_FresnelMaskCurve)*_FresnelDistance)*node_1998)); // FresnelMask
                float3 diffuseColor = lerp(lerp(lerp(_DiffuseColorA.rgb,_DiffuseColorB.rgb,lerp(node_2296,_FGTexture_var.r,_TextureStrength)),lerp(_DiffuseColorA.rgb,_DiffuseColorB.rgb,lerp(node_2296,_BGTexture_var.r,_TextureStrength)),node_9037),float3(node_919,node_919,node_919),(saturate((node_6008+node_5244))*node_9037));
                float3 diffuse = directDiffuse * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse;
                fixed4 finalRGBA = fixed4(finalColor * 1,0);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA * _Tint;
            }
            ENDCG
        }
            UsePass "VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
    CustomEditor "ShaderForgeMaterialInspector"
    

}
