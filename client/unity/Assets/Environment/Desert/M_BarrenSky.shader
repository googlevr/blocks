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

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Shader created with Shader Forge v1.34 
// Shader Forge (c) Neat Corporation / Joachim Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.34;sub:START;pass:START;ps:flbk:,iptp:2,cusa:False,bamd:0,lico:0,lgpr:1,limd:0,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:False,hqlp:False,rprd:False,enco:False,rmgx:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:0,bdst:1,dpts:2,wrdp:True,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:False,aust:False,igpj:True,qofs:0,qpre:0,rntp:1,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:1,fgcg:0.8431373,fgcb:0.7294118,fgca:1,fgde:0.017,fgrn:0,fgrf:300,stcl:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:True,fnfb:True;n:type:ShaderForge.SFN_Final,id:3554,x:32480,y:32959,varname:node_3554,prsc:2|emission-8792-OUT;n:type:ShaderForge.SFN_Color,id:8306,x:31088,y:32017,ptovrint:False,ptlb:Sky Color,ptin:_SkyColor,varname:node_8306,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.02553246,c2:0.03709318,c3:0.1827586,c4:1;n:type:ShaderForge.SFN_ViewVector,id:2265,x:30477,y:32203,varname:node_2265,prsc:2;n:type:ShaderForge.SFN_Dot,id:7606,x:30734,y:32284,varname:node_7606,prsc:2,dt:1|A-2265-OUT,B-3211-OUT;n:type:ShaderForge.SFN_Vector3,id:3211,x:30477,y:32328,varname:node_3211,prsc:2,v1:0,v2:-1,v3:0;n:type:ShaderForge.SFN_Color,id:3839,x:31088,y:32179,ptovrint:False,ptlb:Horizon Color,ptin:_HorizonColor,varname:_GroundColor_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.06617647,c2:0.5468207,c3:1,c4:1;n:type:ShaderForge.SFN_Power,id:4050,x:31088,y:32326,varname:node_4050,prsc:2|VAL-6125-OUT,EXP-7609-OUT;n:type:ShaderForge.SFN_Vector1,id:7609,x:30903,y:32426,varname:node_7609,prsc:2,v1:8;n:type:ShaderForge.SFN_OneMinus,id:6125,x:30903,y:32284,varname:node_6125,prsc:2|IN-7606-OUT;n:type:ShaderForge.SFN_Lerp,id:2737,x:31315,y:32200,cmnt:Sky,varname:node_2737,prsc:2|A-8306-RGB,B-3839-RGB,T-4050-OUT;n:type:ShaderForge.SFN_LightVector,id:3559,x:30039,y:32371,cmnt:Auto-adapts to your directional light,varname:node_3559,prsc:2;n:type:ShaderForge.SFN_Dot,id:1472,x:30398,y:32481,cmnt:Linear falloff to sun angle,varname:node_1472,prsc:2,dt:1|A-8269-OUT,B-8750-OUT;n:type:ShaderForge.SFN_ViewVector,id:8750,x:30211,y:32491,varname:node_8750,prsc:2;n:type:ShaderForge.SFN_Add,id:7568,x:31578,y:32390,cmnt:Sky plus Sun,varname:node_7568,prsc:2|A-2737-OUT,B-5855-OUT;n:type:ShaderForge.SFN_Negate,id:8269,x:30211,y:32371,varname:node_8269,prsc:2|IN-3559-OUT;n:type:ShaderForge.SFN_RemapRangeAdvanced,id:3001,x:30699,y:32613,cmnt:Modify radius of falloff,varname:node_3001,prsc:2|IN-1472-OUT,IMIN-1476-OUT,IMAX-1574-OUT,OMIN-9430-OUT,OMAX-6262-OUT;n:type:ShaderForge.SFN_Slider,id:2435,x:29636,y:32797,ptovrint:False,ptlb:Sun Radius B,ptin:_SunRadiusB,varname:node_2435,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.1,max:0.1;n:type:ShaderForge.SFN_Slider,id:3144,x:29636,y:32691,ptovrint:False,ptlb:Sun Radius A,ptin:_SunRadiusA,varname:_SunOuterRadius_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:0.1;n:type:ShaderForge.SFN_Vector1,id:9430,x:30398,y:32941,varname:node_9430,prsc:2,v1:1;n:type:ShaderForge.SFN_Vector1,id:6262,x:30398,y:32999,varname:node_6262,prsc:2,v1:0;n:type:ShaderForge.SFN_Clamp01,id:7022,x:30872,y:32613,varname:node_7022,prsc:2|IN-3001-OUT;n:type:ShaderForge.SFN_OneMinus,id:1574,x:30398,y:32795,varname:node_1574,prsc:2|IN-8889-OUT;n:type:ShaderForge.SFN_OneMinus,id:1476,x:30398,y:32646,varname:node_1476,prsc:2|IN-3432-OUT;n:type:ShaderForge.SFN_Multiply,id:8889,x:30209,y:32795,varname:node_8889,prsc:2|A-9367-OUT,B-9367-OUT;n:type:ShaderForge.SFN_Multiply,id:3432,x:30209,y:32646,varname:node_3432,prsc:2|A-7933-OUT,B-7933-OUT;n:type:ShaderForge.SFN_Max,id:9367,x:29997,y:32795,varname:node_9367,prsc:2|A-3144-OUT,B-2435-OUT;n:type:ShaderForge.SFN_Min,id:7933,x:29997,y:32646,varname:node_7933,prsc:2|A-3144-OUT,B-2435-OUT;n:type:ShaderForge.SFN_Power,id:754,x:31088,y:32667,varname:node_754,prsc:2|VAL-7022-OUT,EXP-5929-OUT;n:type:ShaderForge.SFN_Vector1,id:5929,x:30872,y:32743,varname:node_5929,prsc:2,v1:5;n:type:ShaderForge.SFN_Multiply,id:5855,x:31273,y:32588,cmnt:Sun,varname:node_5855,prsc:2|A-2359-RGB,B-754-OUT,C-7055-OUT;n:type:ShaderForge.SFN_ValueProperty,id:7055,x:31088,y:32815,ptovrint:False,ptlb:Sun Intensity,ptin:_SunIntensity,varname:node_7055,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:2;n:type:ShaderForge.SFN_LightColor,id:2359,x:31088,y:32541,cmnt:Get color from directional light,varname:node_2359,prsc:2;n:type:ShaderForge.SFN_Tex2d,id:9821,x:31323,y:33962,ptovrint:False,ptlb:Sky,ptin:_Sky,varname:node_9821,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:4ed4aeae7237d674f9084d2fce4e5a28,ntxv:0,isnm:False|UVIN-4059-UVOUT;n:type:ShaderForge.SFN_Panner,id:4059,x:31137,y:33984,varname:node_4059,prsc:2,spu:0.03,spv:0|UVIN-6304-UVOUT,DIST-9664-TSL;n:type:ShaderForge.SFN_Time,id:9664,x:30890,y:34061,varname:node_9664,prsc:2;n:type:ShaderForge.SFN_TexCoord,id:6304,x:30890,y:33888,varname:node_6304,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_TexCoord,id:8462,x:29548,y:30866,varname:node_8462,prsc:2,uv:1,uaff:False;n:type:ShaderForge.SFN_Panner,id:9738,x:29880,y:30940,varname:node_9738,prsc:2,spu:-0.1,spv:0|UVIN-8462-UVOUT,DIST-595-TSL;n:type:ShaderForge.SFN_Time,id:595,x:29537,y:31032,varname:node_595,prsc:2;n:type:ShaderForge.SFN_Tex2d,id:3652,x:30084,y:30940,ptovrint:False,ptlb:Clouds,ptin:_Clouds,varname:_FogPattern_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:c1178dfbbc5ee1f4b82d214652566d2b,ntxv:1,isnm:False|UVIN-9738-UVOUT;n:type:ShaderForge.SFN_TexCoord,id:2690,x:29612,y:30930,varname:node_2690,prsc:2,uv:1,uaff:False;n:type:ShaderForge.SFN_Panner,id:1591,x:29944,y:31004,varname:node_1591,prsc:2,spu:-0.1,spv:0|UVIN-2690-UVOUT,DIST-7540-TSL;n:type:ShaderForge.SFN_Time,id:7540,x:29601,y:31096,varname:node_7540,prsc:2;n:type:ShaderForge.SFN_Tex2d,id:2643,x:30148,y:31004,ptovrint:False,ptlb:Clouds_copy,ptin:_Clouds_copy,varname:_Clouds_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:c1178dfbbc5ee1f4b82d214652566d2b,ntxv:1,isnm:False|UVIN-1591-UVOUT;n:type:ShaderForge.SFN_Tex2d,id:2521,x:31152,y:33343,ptovrint:False,ptlb:Clouds,ptin:_Clouds,varname:_Sky_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:c1178dfbbc5ee1f4b82d214652566d2b,ntxv:0,isnm:False|UVIN-6173-UVOUT;n:type:ShaderForge.SFN_Lerp,id:8792,x:31850,y:33559,varname:node_8792,prsc:2|A-9821-RGB,B-2521-RGB,T-522-OUT;n:type:ShaderForge.SFN_Panner,id:6173,x:30886,y:33342,varname:node_6173,prsc:2,spu:0.05,spv:0|UVIN-4322-UVOUT,DIST-7274-TSL;n:type:ShaderForge.SFN_Time,id:7274,x:30636,y:33399,varname:node_7274,prsc:2;n:type:ShaderForge.SFN_TexCoord,id:4322,x:30636,y:33237,varname:node_4322,prsc:2,uv:1,uaff:False;n:type:ShaderForge.SFN_Vector3,id:673,x:31325,y:33074,varname:node_673,prsc:2,v1:0.2352941,v2:0.3333333,v3:0.2352941;n:type:ShaderForge.SFN_Multiply,id:522,x:31396,y:33529,varname:node_522,prsc:2|A-2521-A,B-5298-OUT;n:type:ShaderForge.SFN_Slider,id:5298,x:30995,y:33607,ptovrint:False,ptlb:CloudStrength,ptin:_CloudStrength,varname:node_5298,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:1;proporder:8306-3839-2435-3144-7055-9821-2521-5298;pass:END;sub:END;*/

Shader "VoidWorld/BarrenSky" {
    Properties {
        _SkyColor ("Sky Color", Color) = (0.02553246,0.03709318,0.1827586,1)
        _HorizonColor ("Horizon Color", Color) = (0.06617647,0.5468207,1,1)
        _HorizonStrength ("Horizon Strength", Range(0, 1)) = 1
        _SunRadiusB ("Sun Radius B", Range(0, 0.1)) = 0.1
        _SunRadiusA ("Sun Radius A", Range(0, 0.1)) = 0
        _SunIntensity ("Sun Intensity", Float ) = 2
        _Sky("Sky", 2D) = "white" {}
        _SkyStrength("Sky Strength", Range(0, 1)) = 1
        _Clouds ("Clouds", 2D) = "white" {}
        _CloudStrength ("CloudStrength", Range(0, 1)) = 0
        _Tint("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Background"
            "RenderType"="Opaque"
            "PreviewType"="Skybox"
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
            #pragma multi_compile_fwdbase
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 3.0
            uniform float4 _TimeEditor;
            uniform float4 _SkyColor;
            uniform float4 _HorizonColor;
            uniform float _HorizonStrength;
            uniform sampler2D _Sky; uniform float4 _Sky_ST;
            uniform sampler2D _Clouds; uniform float4 _Clouds_ST;
            uniform float _CloudStrength;
            uniform float _SkyStrength;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 texcoord0 : TEXCOORD0;
                float3 texcoord1 : TEXCOORD1;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float3 uv0 : TEXCOORD0;
                float3 uv1 : TEXCOORD1;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.pos = UnityObjectToClipPos(v.vertex );
                return o;
            }
            float4 _Tint;
            float4 frag(VertexOutput i) : COLOR {
////// Lighting:
////// Emissive:
                float3 v = i.uv0;
                float p = v.y;
                float p1 = 1.3 - pow( min(1, 1 - p), pow(0.8, v.x * v.z) );
                float p2 = 1 - p1;

                float4 SkyColor = _SkyColor *p1 + _HorizonColor * p2;
                float4 SkyStrength = _SkyStrength;
                float4 node_9664 = _Time + _TimeEditor;
                float2 node_4059 = (i.uv0+node_9664.r*float2(0.03,0));
                float4 _Sky_var = tex2D(_Sky,TRANSFORM_TEX(node_4059, _Sky));
                float4 node_7274 = _Time + _TimeEditor;
                float2 node_6173 = (i.uv1+node_7274.r*float2(0.05,0));
                float4 _Clouds_var = tex2D(_Clouds,TRANSFORM_TEX(node_6173, _Clouds));
                float3 emissive = lerp(_Sky_var.rgb,_Clouds_var.rgb,(_Clouds_var.a*_CloudStrength*_Sky_var.a));
                float3 finalColor = emissive * SkyColor * _Tint.rgb;
                return fixed4( _Tint * lerp(SkyColor, finalColor, SkyStrength), 1 );
            }
            ENDCG
        }
    }
    CustomEditor "ShaderForgeMaterialInspector"
}
