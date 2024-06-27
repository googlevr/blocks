float4x4 _ShadowMatrix;
sampler2D _ShadowTexture;
float3 _LightColor;
float4 _LightDirection;
float4 _LightPosition;
float3 _FillLightColor;
float4 _FillLightPosition;
float _InvShadowLength;

#define E_20 485165195.40979
#define E_10 22026.46579
#define E_40 235385266837019985.4078

float ESMShadows(float esm, float depth){
    float result =  min(1,(esm) * E_40*exp(-40.*min(1.0, depth) ));
    return smoothstep(0.5,1,result);
}