float4x4 _ShadowMatrix;
sampler2D _ShadowTexture;
sampler2D _ShadowPointTexture;
sampler2D _ShadowBlurTexture;
float3 _LightColor;
float4 _LightDirection;
float4 _LightPosition;
float _LightSize;
float _CameraSize;
float3 _FillLightColor;
float4 _FillLightPosition;
float4 _ShadowInfo;

#define PIVAL 3.14159265359
#define E_20 485165195.40979
#define E_10 22026.46579
#define E_40 235385266837019985.4078
#define INV_E_20 0.00000000206115362243
#define INV_E_10 0.00004539992976248485
#define INV_E_40 0.000000000000000004248354255291588995329



static const float shadowTuning = 25;

float gaussWeight(float val, float stddev2) {
  float A = exp((-val * val) / (2 * stddev2));
  float B = sqrt(2 * PIVAL * stddev2);
  return (1 / B) * A;
}

float gaussWeight(float2 val, float stddev2) {
  float A = 1 / (2 * PIVAL * stddev2);
  float B = exp(-dot(val, val) / (2 * stddev2));
  return A * B;
}

float ESMShadows(float esm, float depth){
    float result =  min(1,(esm) * E_40*exp(-40.*min(1.0, depth) ));
    return smoothstep(0.5,1,result);
}

static const half curve[5] = {0.000229,	0.005977,	0.060598,	0.241732,	0.382928};

// This looks nice.  NUM_ESM_SAMPLES could potentially be turned down to 3 but the difference is noticeable.  BLOCKERKERNELSIZE will have
// artifacts if lowered.
#define BLOCKERKERNELSIZE 5
#define NUM_ESM_SAMPLES 5
// ExponentialPenumbralShadows implementation, using NVidia PCSS as a starting point, but implementing the base shadows map as ESM
// shadows instead of Percentage Closer Filtering shadows. See http://developer.download.nvidia.com/whitepapers/2008/PCSS_Integration.pdf
// and http://gamedevs.org/uploads/advanced-soft-shadow-mapping-techniques.pdf for reference.
// One particularly interesting thing to look into for the future is summed area tables for faster precomputed blurs with variable kernels.
float3 ExponentialPenumbralShadows(float3 pixelWorldPos, float3 pixelWorldNormal, float4 shadowPosition) {
    float2 rawCoords = shadowPosition.xy / shadowPosition.w;
    float2 shadowCoords = 0.5*(1.0 + rawCoords);

    float lightDist = length(pixelWorldPos - _LightPosition.xyz);
    float bias = 0.01;
    float nDotL = saturate(dot(pixelWorldNormal, normalize(-_LightDirection)));

    float numBlockers = 0;
    float blockerDepth = 0;
    float4 rawSamples[BLOCKERKERNELSIZE * BLOCKERKERNELSIZE];
    float4 average = (float4)0;
    float4 maxSample = (float4)0;
    float4 minSample = (float4)1;
    float rawWeights[1];
    float totalWeight = 0;

    float worldTexelSize = _CameraSize / _ShadowInfo.y;
    float invWorldTexelSize = _ShadowInfo.y / _CameraSize;
    float texelSize = 1 / _ShadowInfo.y;
    
    // Calculate slopeBias for shadows
    float t2 = worldTexelSize / 2.0;
    float hyp = worldTexelSize * 0.5 / nDotL;
    float slopeBias = sqrt(hyp * hyp - t2 * t2);
    
    float sampleSize = 0.5 * (_LightSize / _CameraSize) / (BLOCKERKERNELSIZE);
    float blockedESMAmount;
    float normalESMResult;
    float minSampleComp = 1000000;
    float center = (BLOCKERKERNELSIZE - 1) / 2;
      //return ESMShadows(tex2Dlod(_ShadowBlurTexture, float4(shadowCoords, 0, 0)).g, length(pixelWorldPos - _LightPosition.xyz) * _ShadowInfo.x);
    // Do a search based on the relative size of the light to the shadowCamera for blockers, and average the depth of the blockers the pixel has.
    // We will render shadows from objects further away with fuzzier outlines.
    for(int i = 0; i < BLOCKERKERNELSIZE; i++) {
      for(int j = 0; j < BLOCKERKERNELSIZE; j++) {
        int index = i * BLOCKERKERNELSIZE + j;
        float2 base = float2(i - center, j - center);
        //float2 base = float2(0, 0);
        float2 coords = base * sampleSize;
        float depth = lightDist * _ShadowInfo.x;

        float curBlockerDepth = tex2Dlod(_ShadowTexture, float4(coords + shadowCoords, 0, 0)).r / _ShadowInfo.x;

        float blocked = curBlockerDepth < lightDist - slopeBias - bias ? 1: 0;//step(min(1,(esm.g) * E_40*exp(-40.*min(1.0, depth))), 0.45);

        numBlockers += blocked;
        average += blocked * curBlockerDepth;
       
      }
    }

    if (numBlockers == 0) {
      // Early return
      return float3(1, 1, 1);
    }
    
    float avgBlockerDepth = average / numBlockers;
    avgBlockerDepth = avgBlockerDepth;

    float total = 0;
    float sumWeight = 0;

    // Determine the sampleSize radius total use for the shadows - wider will result in fuzzier shadows and a wider penumbra.
    float sampleWidthWorld = (lightDist - avgBlockerDepth) * _LightSize / (avgBlockerDepth + shadowTuning);
    float sampleWidthUV = sampleWidthWorld / _CameraSize;
    sampleSize = max(texelSize * 0.2, min(texelSize * 7, sampleWidthUV / NUM_ESM_SAMPLES));
    
    
    float pcfCenter = (NUM_ESM_SAMPLES - 1) / 2;
    
    // Do a box filter on ESMShadows
    for(i = 0; i < NUM_ESM_SAMPLES; i++) {
      for(int j = 0; j < NUM_ESM_SAMPLES; j++) {
            float2 base = float2(i - pcfCenter, j - pcfCenter);
            float2 coords = base * sampleSize;
            float weight = 1;

            //float curBlockerDepth = tex2Dlod(_ShadowTexture, float4(coords + shadowCoords, 0, 0)).b / _ShadowInfo.x;
            //float pcf = curBlockerDepth < lightDist - slopeBias - bias ? 0: 1;
            //total += weight * pcf;
            
            total += weight * ESMShadows(tex2Dlod(_ShadowBlurTexture, float4(coords + shadowCoords, 0, 0)).g, lightDist * _ShadowInfo.x);
            sumWeight += weight;
      }
    }
    return total / sumWeight;
}


float3 calcShadow(float3 pixelWorldPos, float3 pixelWorldNormal, float4 shadowPosition) {
  float3 rawCoords = shadowPosition.xyz / shadowPosition.w;

  float3 edge = 10*(abs(rawCoords)-0.9);
  float edgeFadeX = saturate(1 - smoothstep(-1, 1, edge.x));
  float edgeFadeY = saturate(1 - smoothstep(-1, 1, edge.y));
  float edgeFadeZ = saturate(1 - smoothstep(-5, -2, edge.z));
  float totalFade = edgeFadeX * edgeFadeY * edgeFadeZ;
  if (totalFade < 0.01) {
    return 1;
  }

  return lerp(1, ExponentialPenumbralShadows(pixelWorldPos, pixelWorldNormal, shadowPosition), totalFade);
}
