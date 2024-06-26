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

Shader "Hidden/FastBlur" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Bloom ("Bloom (RGB)", 2D) = "black" {}
	}
	
	CGINCLUDE

		#include "UnityCG.cginc"

		sampler2D _MainTex;
		sampler2D _Bloom;
				
		uniform half4 _MainTex_TexelSize;
		half4 _MainTex_ST;

		half4 _Bloom_ST;

		uniform half4 _Parameter;

		struct v2f_tap
		{
			float4 pos : SV_POSITION;
			half2 uv20 : TEXCOORD0;
			half2 uv21 : TEXCOORD1;
			half2 uv22 : TEXCOORD2;
			half2 uv23 : TEXCOORD3;
		};

		v2f_tap vert4Tap ( appdata_img v )
		{
			v2f_tap o;

			o.pos = UnityObjectToClipPos (v.vertex);
        	o.uv20 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy, _MainTex_ST);
			o.uv21 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(-0.5h,-0.5h), _MainTex_ST);
			o.uv22 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(0.5h,-0.5h), _MainTex_ST);
			o.uv23 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(-0.5h,0.5h), _MainTex_ST);

			return o; 
		}					
		
		fixed4 fragDownsample ( v2f_tap i ) : SV_Target
		{				
			fixed4 color = tex2D (_MainTex, i.uv20);
			color += tex2D (_MainTex, i.uv21);
			color += tex2D (_MainTex, i.uv22);
			color += tex2D (_MainTex, i.uv23);
			return color / 4;
		}

		struct v2f_5tap
    {
      float4 pos : SV_POSITION;
      half2 uv20 : TEXCOORD0;
      half2 uv21 : TEXCOORD1;
      half2 uv22 : TEXCOORD2;
      half2 uv23 : TEXCOORD3;
      half2 uv00 : TEXCOORD4;
    };

    v2f_5tap vert5TapDS ( appdata_img v )
    {
      v2f_5tap o;

      o.pos = UnityObjectToClipPos (v.vertex);
      o.uv00 = UnityStereoScreenSpaceUVAdjust(v.texcoord, _MainTex_ST);
      o.uv20 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(1.0h, 1.0h), _MainTex_ST);
      o.uv21 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(-1.0h,-1.0h), _MainTex_ST);
      o.uv22 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(1.0h,-1.0h), _MainTex_ST);
      o.uv23 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(-1.0h,1.0h), _MainTex_ST);

      return o;
    }
    
    
    
     fixed4 fragDistDownsample ( v2f_5tap i ) : SV_Target
        {
          fixed4 baseColor = float4(tex2D (_MainTex, i.uv00).r, i.uv00, 1);
          fixed4 color0 = float4(tex2D (_MainTex, i.uv20).r, i.uv20, 1);
          fixed4 color1 = float4(tex2D (_MainTex, i.uv21).r, i.uv21, 1);
          fixed4 color2 = float4(tex2D (_MainTex, i.uv22).r, i.uv22, 1);
          fixed4 color3 = float4(tex2D (_MainTex, i.uv23).r, i.uv23, 1);
          color0 = color0.r < color1.r ? color0 : color1;
          color2 = color2.r < color3.r ? color2 : color3;
          color0 = color0.r < color2.r ? color0 : color2;
          return color0;//tex2D (_MainTex, i.uv00);
        }
        
      fixed4 fragDistBlur ( v2f_5tap i ) : SV_Target
      {
        fixed4 baseColor = tex2D (_MainTex, i.uv00);
        fixed4 color0 = tex2D (_MainTex, i.uv20);
        fixed4 color1 = tex2D (_MainTex, i.uv21);
        fixed4 color2 = tex2D (_MainTex, i.uv22);
        fixed4 color3 = tex2D (_MainTex, i.uv23);
        color0 = color0.r < color1.r ? color0 : color1;
        color2 = color2.r < color3.r ? color2 : color3;
        color0 = color0.r < color2.r ? color0 : color2;
        return baseColor.r - 0.01 < color0.r ? baseColor : color0;
      }

    fixed4 fragDistField ( v2f_5tap i ) : SV_Target
    {
      fixed4 baseColor = tex2D (_MainTex, i.uv00);
      return float4(baseColor.r, length(i.uv00 - baseColor.gb), 0, 0);
    }

   
     struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

    struct v2f {
      half2 uv_0 : TEXCOORD0;
      half2 uv_1 : TEXCOORD1;
      half2 uv_2 : TEXCOORD2;
      half2 uv_3 : TEXCOORD3;
      half2 uv_4 : TEXCOORD4;
      half2 uv_5 : TEXCOORD5;
      half2 uv_6 : TEXCOORD6;
      half2 uv_7 : TEXCOORD7;
      half2 uv_8 : TEXCOORD8;
      float4 position : SV_POSITION;
    };

    v2f Vertex_Horizontal (appdata v) {
      v2f o;
      o.position = UnityObjectToClipPos(v.vertex);

      o.uv_0 = v.uv - half2(4*_MainTex_TexelSize.x,0);
      o.uv_1 = v.uv - half2(3*_MainTex_TexelSize.x,0);
      o.uv_2 = v.uv - half2(2*_MainTex_TexelSize.x,0);
      o.uv_3 = v.uv - half2(1*_MainTex_TexelSize.x,0);
      o.uv_4 = v.uv;
      o.uv_5 = v.uv + half2(1*_MainTex_TexelSize.x,0);
      o.uv_6 = v.uv + half2(2*_MainTex_TexelSize.x,0);
      o.uv_7 = v.uv + half2(3*_MainTex_TexelSize.x,0);
      o.uv_8 = v.uv + half2(4*_MainTex_TexelSize.x,0);

      return o;
    }
    v2f Vertex_Vertical (appdata v) {
      v2f o;
      o.position = UnityObjectToClipPos(v.vertex);

      o.uv_0 = v.uv - half2(0,4*_MainTex_TexelSize.y);
      o.uv_1 = v.uv - half2(0,3*_MainTex_TexelSize.y);
      o.uv_2 = v.uv - half2(0,2*_MainTex_TexelSize.y);
      o.uv_3 = v.uv - half2(0,1*_MainTex_TexelSize.y);
      o.uv_4 = v.uv;
      o.uv_5 = v.uv + half2(0,1*_MainTex_TexelSize.y);
      o.uv_6 = v.uv + half2(0,2*_MainTex_TexelSize.y);
      o.uv_7 = v.uv + half2(0,3*_MainTex_TexelSize.y);
      o.uv_8 = v.uv + half2(0,4*_MainTex_TexelSize.y);

      return o;
    }

    // sigma 1.75
    //static const float _Kernel[9] = {0.0185,0.054,0.120,0.194,0.227,0.194,0.120,0.054,0.0185};
    static const float _Kernel[9] = {10/1022.0,45/1022.0,120/1022.0,210/1022.0,252/1022.0,210/1022.0,120/1022.0,45/1022.0,10/1022.0};

    float frag (v2f i) : SV_Target {

      float val = _Kernel[0] * (tex2D(_MainTex, i.uv_0).r);
      val += _Kernel[1] * (tex2D(_MainTex, i.uv_1).r);
      val += _Kernel[2] * (tex2D(_MainTex, i.uv_2).r);
      val += _Kernel[3] * (tex2D(_MainTex, i.uv_3).r);
      val += _Kernel[4] * (tex2D(_MainTex, i.uv_4).r);
      val += _Kernel[5] * (tex2D(_MainTex, i.uv_5).r);
      val += _Kernel[6] * (tex2D(_MainTex, i.uv_6).r);
      val += _Kernel[7] * (tex2D(_MainTex, i.uv_7).r);
      val += _Kernel[8] * (tex2D(_MainTex, i.uv_8).r);

      return (val);
    }

    float4 fragBilateral (v2f i) : SV_Target {

      float4 values[9];
      values[0] = (tex2D(_MainTex, i.uv_0));
      values[1] = (tex2D(_MainTex, i.uv_1));
      values[2] = (tex2D(_MainTex, i.uv_2));
      values[3] = (tex2D(_MainTex, i.uv_3));
      values[4] = (tex2D(_MainTex, i.uv_4));
      values[5] = (tex2D(_MainTex, i.uv_5));
      values[6] = (tex2D(_MainTex, i.uv_6));
      values[7] = (tex2D(_MainTex, i.uv_7));
      values[8] = (tex2D(_MainTex, i.uv_8));

      float4 total=0;
      float4 sum=0;

      for(int i=0; i<9; i++){

        // exp(-C)*exp(C*z )
        //exp(C*z -C)
        //exp(C*z0 -C) / exp(C*z1 -C)
        //exp(C*z0 - C*z1 )

        // min ( exp(C*z0 - C*z1 ), exp(C*z1 - C*z0 ) )
        // exp(C*z0 - C*z1 )^2
        float4 dist = min(max(.75,values[i] / values[4]) ,values[4] / values[i] );
        dist *= dist;
        //float x = values[4] - values[i];
        //dist = exp(-x*x*exp(20)/2);

        total += _Kernel[i]*dist;
        sum += _Kernel[i]*values[i] * dist;
      }
      sum /= total;
      return sum / total;
    }
   

					
	ENDCG
	
	SubShader {
	  ZTest Off Cull Off ZWrite Off Blend Off

	// 0
	Pass { 
	
		CGPROGRAM
		
		#pragma vertex vert4Tap
		#pragma fragment fragDownsample
		
		ENDCG
		 
		}
		
		// 1
    	Pass { 
    	
    		CGPROGRAM
    		
    		#pragma vertex vert5TapDS
    		#pragma fragment fragDistBlur
    		
    		ENDCG
    		 
    		}
    		
    		// 2
        Pass { 
        
          CGPROGRAM
          
          #pragma vertex vert5TapDS
          #pragma fragment fragDistField
          
          ENDCG
           
          }
            // 3     
              Pass {
      CGPROGRAM
      #pragma vertex Vertex_Horizontal
      #pragma fragment fragBilateral


      ENDCG
    }
        // 4
    Pass {
      CGPROGRAM
      #pragma vertex Vertex_Vertical
      #pragma fragment fragBilateral


      ENDCG
    }

      }

	

	FallBack Off
}
