// This value needs to be kept in sync with the one at Assets/Rendering/Lighting.cs
#define NUMBER_OF_POINT_LIGHTS 8
#define NUMBER_OF_MESH_XFORMS 128
#define INV_PI 0.31830988618
#define PI 3.14159265359
#define NOISE_EPSILON 0.1
#include "UnityImageBasedLighting.cginc"
#include "PenumbraShadows.cginc"
#include "noise.cginc"

float epsilon = 0.0000001;
float3 _FogColor = float3(0.9, 0.9, 1.0);
float _FogDistanceStart = 0.1;
float _FogDistanceEnd = 0.5;
float _FogStrength = 0.3;
float _Roughness = 0.0;
//Cheating
#define _AmbientBase 0.1
float _AmbientAdjust;
float _Metallic;
float _Mirror = 1;
float3 _EmissiveColor;
float _EmissiveAmount;
float _EmissivePulseAmount;
float _EmissivePulseFrequencyMultiplier;
float _LightPercentAdjust = 0.0;
sampler2D _EnvironmentSphere;
samplerCUBE _EnvCubeMap;
samplerCUBE _EnvCubeTex2;
float _EnviroStrength = 0.1;
float4 _EnvOverrideColor;
float _EnvOverrideAmount;
float _EnvSpecularAmount;
float _EnvDiffuseStrength;
float _EnvSpecularStrength;
float4 _FXPointLightColorStrength = float4(1, 0, 0, 1);
float4 _FXPointLightPosition = float4 (0, 0, 0, 1);
float4 _PointLightWorldPositions[NUMBER_OF_POINT_LIGHTS];
float4 _PointLightColors[NUMBER_OF_POINT_LIGHTS];
float4x4 _RemesherMeshTransforms[NUMBER_OF_MESH_XFORMS];

float _NoiseScale = 500;
float _NoiseIntensity = .15;
float _NoiseBrightCutoff = .5;
float _NoiseShadeCutoff = .5;

float3 curLightPosition;
float curLightRadius;
float curLightDist;

float3 projectPointOntoPlane(float3 pointToProject, float3 planePoint, float3 planeNormal) {
  return pointToProject - dot(pointToProject - planePoint, planeNormal) * planeNormal;
}

float3 closestPointParamOnLine(float3 L0P0, float3 L0P1, float3 L1P0, float3 L1P1)
{
    float3 u = L0P1 - L0P0;
    float3 v = L1P1 - L1P0;
    float3 w = L0P0 - L1P0;

    float a = dot(u, u);
    float b = dot(u, v);
    float c = dot(v, v);
    float d = dot(u, w);
    float e = dot(v, w);
    float D = a * c - b * b;
    if (D < epsilon)
        return 0;

    float3 retVal;

    retVal.x = (b * e - c * d) / D;
    retVal.y = (a * e - b * d) / D;

    float3 diff = w + retVal.x * u - retVal.y * v;

    retVal.z = length(diff);
    return retVal;
}

float _RefractiveIndex = 1.47; // Always default to Olive Oil.
float _FresnelPower = 5;

half evaluateFresnel(half VDotH, float base)
{
    return (1.0 - base) * pow(1.0 - VDotH, 5.0) + base;
}

float3 evaluateFresnelSchlick(float VDotH, float3 F0) {
    return F0 + (1 - F0) * pow(1 - VDotH, 5);
}

float fogAmount(float depth) {
 float exponent = -(max(0, depth - 0.05)) * _FogStrength;
 return exp(exponent);
}


float newGGX2(float NDotH, float roughness2) {
  float NDotH2 = NDotH * NDotH;
  float A = NDotH2 * (roughness2 - 1) + 1;
  float denominator = PI * A * A;
  float alphaPrime = saturate(roughness2 + curLightRadius / (2.0 * curLightDist));
  float normalization = pow(roughness2 / alphaPrime, 2.0);
  return normalization * ((NDotH2 > 0 ? 1 : 0) * roughness2) / denominator;
}

float newGGX(float NDotH, float roughness2) {
  float NDotH2 = NDotH * NDotH;
  float A = NDotH2 * (roughness2 - 1) + 1;
  float a2 = roughness2 * roughness2;
  float denominator = PI * pow(NDotH2 * (a2 - 1) + 1, 2.0);
  return a2 / denominator;
 
 
}

float GGX(float NDotH, float roughness2) {
    float den0 = NDotH * NDotH * (roughness2 * roughness2 - 1) + 1;
    return roughness2 * roughness2 / (PI * den0 * den0);
}

/*****
Still haven't gotten these to work.
float GeoAtten(float VDotH) {
  float VDotH2 = VDotH * VDotH;
  float tan2 = (1 - VDotH2) / VDotH;
  return (VDotH2 > 0 ? 2 : 0) / (1 + sqrt( 1 + _Roughness * _Roughness * tan2));
}

float chiGGX(float v)
{
    return v > 0 ? 1 : 0;
}

float GGX_PartialGeometryTerm(float3 v, float3 n, float3 h, float alpha)
{
    float VoH2 = saturate(dot(v,h));
    float chi = chiGGX( VoH2 / saturate(dot(v,n)) );
    VoH2 = VoH2 * VoH2;
    float tan2 = ( 1 - VoH2 ) / VoH2;
    return (chi * 2) / ( 1 + sqrt( 1 + alpha * alpha * tan2 ) );
}
*/

float CT_GeoAtten(float NDotV, float NDotH, float VDotH, float NDotL, float LDotH) {
  float a = 2 * NDotH * NDotV / VDotH;
  float b = 2 * NDotH * NDotL / LDotH;
  return min(1, min(a, b));
}

float3 closestPointOnRayToSphere(float3 lightVec, float3 ray, float lightRadius) {
  float3 toRay = dot(lightVec, ray) * ray - lightVec;
  float3 pointOnLine = lightVec + toRay * saturate(lightRadius/length(toRay));
  return pointOnLine;
}

// This is intended to be a physically based rendering shader similar to the one that will be available in the
// gltf PBR extension: https://github.com/KhronosGroup/glTF/pull/643
// This should allow us to get comparable rendering with gltf in Zandria as users see in app.
// Reference materials for PBR shaders:
// http://www.codinglabs.net/article_physically_based_rendering.aspx
// http://www.trentreed.net/blog/physically-based-shading-and-image-based-lighting/
void evaluatePBRLight(
  float3 materialColor,
  float3 lightColor, // light color (intensity already applied)
  float3 L, //worldspace vector from point to light
  float3 H, //worldspace half-vector
  float3 N, //worldspace normal
  float3 V, //worldspace vector from point to camera
  inout float3 diffuseOut,
  inout float3 specularOut) {


    float NDotL = saturate(dot(N, L));
    float NDotV = saturate(dot(N, V));
    float NDotH = saturate(dot(N, H));
    float VDotH = saturate(dot(V, H));
    float LDotH = saturate(dot(L, H));


    // Pieces to calculate
    float3 diffuse = NDotL * lightColor * INV_PI;

    // Specular components
    float3 D; //microfacet distribution function output, needs NDotH
    float3 F; //Fresnel reflection, needs VDotH
    float3 G; //geometric attenuation, need NDotL and NDotV

    D = newGGX(NDotH, _Roughness * _Roughness).xxx;
    G = CT_GeoAtten(NDotV, NDotH, VDotH, NDotL, LDotH);

    float fExp = (-5.55472 * VDotH - 6.98316) * VDotH;
    F = 0.04 + 0.96 * pow(2.0, fExp);

    float3 F0 = abs((1.0  - _RefractiveIndex) / (1.0 + _RefractiveIndex));
    F0 = F0 * F0;
    F0 = lerp(F0, materialColor, _Metallic);
    F = evaluateFresnelSchlick(VDotH, F0);

    diffuseOut = diffuseOut + (1 - saturate(F)) * (1 - _Metallic) * lightColor * diffuse;
    specularOut = specularOut + lightColor * saturate((D * G * F) / saturate(4 * NDotH * NDotV + 0.05));
}

float SchlickGeoInternalIBL(float3 N, float3 vec) {
    float k = _Roughness * _Roughness / 2;
    float NDotVec = dot(N, vec);
    return NDotVec / (NDotVec * (1 - k) + k);
}

float SchlickGeo(float3 N, float3 L, float3 V) {
    return SchlickGeoInternalIBL(N, L) * SchlickGeoInternalIBL(N, V);
}

// This is intended to be a physically based rendering shader similar to the one that will be available in the
// gltf PBR extension: https://github.com/KhronosGroup/glTF/pull/643
// This should allow us to get comparable rendering with gltf in Zandria as users see in app.
// Reference materials for PBR shaders:
// http://www.codinglabs.net/article_physically_based_rendering.aspx
// http://www.trentreed.net/blog/physically-based-shading-and-image-based-lighting/
void evaluateIBLPBRLight(
  float3 materialColor,
  samplerCUBE envMap,
  float3 N, //worldspace normal
  float3 V, //worldspace vector from point to camera
  inout float3 diffuseOut,
  inout float3 specularOut) {

    float3 L = normalize(-reflect(V, N));
    float3 H = normalize(N + L);
    half perceptualRoughness = _Roughness;
    perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
    half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);

    half3 specSample = _EnvSpecularStrength * texCUBElod(_EnvCubeMap, float4(L, mip)).rgb;
    half3 diffSample = _EnvDiffuseStrength * texCUBElod(_EnvCubeMap, float4(N, 5)).rgb;

    float NDotL = saturate(dot(N, L));
    float NDotV = saturate(dot(N, V));
    float NDotH = saturate(dot(N, H));
    float VDotH = saturate(dot(V, H));
    float LDotH = saturate(dot(L, H));

    float3 F0 = abs((1.0  - _RefractiveIndex) / (1.0 + _RefractiveIndex));
    F0 = F0 * F0;
    F0 = lerp(F0, materialColor, _Metallic);
    float3 F = (F0 + (1 - F0) * pow(1 - NDotV, 5) / (4 - 3 * (1 - _Roughness)));
    
    diffuseOut = diffuseOut + (1 - saturate(F)) * (1 - _Metallic) * diffSample;
    specularOut = specSample * (F0 + (1 - F0) * pow(1 - NDotV, 5) / (4 - 3 * (1 - _Roughness)));
}

void evaluateLight(
  float3 diffuseColor,
  float3 specularColor,
  float3 pixelPos,
  float3 pixelNormal,
  float3 cameraPos,
  float3 lightPos,
  float falloffStartDistance,
  float falloffEndDistance,
  inout float3 diffuseOut,
  inout float3 specularOut) {



    float3 diffuseAmount = 0.2;

    float3 V = pixelPos - cameraPos;
    float vDist = length(V);
    V = V / vDist;

    float3 L = normalize(lightPos - pixelPos);

    //Light falloff attenuation
    float lightDist = length(lightPos - pixelPos);
    float falloff = smoothstep(falloffEndDistance, falloffStartDistance, lightDist);

    float3 H = normalize(V + L);
    float NDotL = saturate(dot(pixelNormal, L));
    float NDotV = saturate(dot(pixelNormal, V));
    float NDotH = saturate(dot(pixelNormal, H));
    float VDotH = saturate(dot(V, H));
    float LDotH = saturate(dot(L, H));

    // Geo
    float DoubleNDotH = 2.0 * NDotH;
    float geoFactor0 = (DoubleNDotH * NDotV)  / VDotH;
    float geoFactor1 = (DoubleNDotH * NDotL) / LDotH;
    float geometricAttenuation = min(1.0, min(geoFactor0, geoFactor1));

    // Roughness
    float roughness2 = _Roughness * _Roughness;
    float NH2 = NDotH * NDotH;
    float r2nh2 = roughness2 * NH2;
    float r0 = INV_PI * (1.0 / (r2nh2 * NH2));
    float rExp = (NH2 - 1.0) / r2nh2;
    float roughnessAtten = r0 * exp(rExp);

    // Fresnel
    float3 fresnel = evaluateFresnel(VDotH, 0.8).xxx;

    // Specular
    float specular = fresnel * roughnessAtten * geometricAttenuation;
    specular = NDotL > 0.0 ? specular / (NDotV * NDotL * 3.1415) : 0.0;

    // Diffuse
    diffuseOut = diffuseColor * saturate(NDotL * (diffuseAmount + specular * (1.0 - diffuseAmount)));
    specularOut = float3(0.0, 0.0, 0.0);
}

void evaluatePointLights(float3 pixelPos, float3 pixelNormal, float3 materialColor, inout float3 diffuseOut, inout float3 specularOut) {
    for (int i = 0; i < NUMBER_OF_POINT_LIGHTS; i++) {
      float4 lightPos = _PointLightWorldPositions[i];
      float4 color = _PointLightColors[i];
      float distance = length(lightPos - pixelPos);
      float attenuation = 1 / (1 + 2 * distance / color.a + (1 / (color.a * color.a)) * distance * distance);
      float3 lightDiffuseOut = (float3)0;
      float3 lightSpecularOut = (float3)0;
      float3 L = normalize(lightPos - pixelPos);
      float3 V = normalize(_WorldSpaceCameraPos - pixelPos);
      float3 H = normalize(V + L);

      curLightRadius = .05;
      curLightDist = distance;

      evaluatePBRLight(
        materialColor,
        color.rgb,
        L,
        H,
        pixelNormal,
        V,
        lightDiffuseOut,
        lightSpecularOut);
      diffuseOut = diffuseOut + lightDiffuseOut;
      specularOut = specularOut + lightSpecularOut;

    }
}


void evaluateLightsNoEmissiveNoFog(float3 pixelPos, float3 pixelNormal, float4 materialColor, float4 shadowPosition, inout float3 lightOut, inout float3 specularOut) {
 float3 totalDirectionalDiffuseExitance = 0;
  float3 totalDirectionalSpecularExitance = 0;


  float shadow = calcShadow(pixelPos, pixelNormal, shadowPosition);

  float3 L = -_LightDirection.xyz * 10.0 - pixelPos;
  curLightRadius = .002;
  curLightDist = length(L);

  float3 N = normalize(pixelNormal);
  float3 closestPoint = closestPointOnRayToSphere(L, normalize(reflect(pixelPos - _WorldSpaceCameraPos, N)),  curLightRadius);
  L = normalize(closestPoint);
  float3 V = normalize(_WorldSpaceCameraPos - pixelPos);
  float3 H = normalize(V + L);

  evaluatePBRLight(
    materialColor,
    _LightColor * (1 + _LightPercentAdjust) /* lightColor */,
    L,
    H,
    N,
    V,
    totalDirectionalDiffuseExitance, // inout diffuseOut
    totalDirectionalSpecularExitance); //inout specularOut

  //lightOut = materialColor * totalDirectionalDiffuseExitance + totalDirectionalSpecularExitance;
  //return;
  float3 diffuseOut = shadow * totalDirectionalDiffuseExitance;
  specularOut = shadow * totalDirectionalSpecularExitance;

  float3 dirDiff = float3(.5, -.5, -.5);

  L = -float3(0, -1, 0) * dirDiff * 30.0 - pixelPos;
  curLightRadius = 1.0;
  curLightDist = length(L);
  closestPoint = closestPointOnRayToSphere(L, normalize(reflect(pixelPos - _WorldSpaceCameraPos, N)),  curLightRadius);
  L = normalize(closestPoint);
  H = normalize(V + L);
  evaluatePBRLight(
    materialColor,
    _FillLightColor *  (1 + _LightPercentAdjust),
    L,
    H,
    N,
    V,
    diffuseOut, // inout diffuseOut
    specularOut); //inout specularOut

  evaluatePointLights(pixelPos, pixelNormal, materialColor, diffuseOut, specularOut);

  L = normalize(_FXPointLightPosition - pixelPos);
  H = normalize(V + L);
  float fxAttenuation = smoothstep(0.4, 0.01, length(_FXPointLightPosition - pixelPos));
  curLightRadius = .05;
  curLightDist = length(_FXPointLightPosition - pixelPos);
  float3 FXDiffuseOut = 0;
  float3 FXSpecOut = 0;
  evaluatePBRLight(
    materialColor,
    _FXPointLightColorStrength.rgb * _FXPointLightColorStrength.a * (1 + _LightPercentAdjust),
    L,
    H,
    N,
    V,
    FXDiffuseOut, // inout diffuseOut
    FXSpecOut); //inout specularOut

  diffuseOut = diffuseOut + FXDiffuseOut * fxAttenuation;
  specularOut = specularOut + FXSpecOut;



  //pretend our environmental light is more muted than it really is
  //rgbm = lerp(rgbm, _EnvOverrideColor, _EnvOverrideAmount);

  // Cubemap reflections - add to specular only.
  //diffuseOut = (float3)0;
  //specularOut = (float3)0;
  float3 reflectDiffuseOut = (float3)0;
  float3 envSpecularOut = (float3)0;

  evaluateIBLPBRLight(
    materialColor,
    _EnvCubeMap,
    N,
    V,
    reflectDiffuseOut, // inout diffuseOut
    envSpecularOut); //inout specularOut


    float luminance = Luminance(reflectDiffuseOut);
    diffuseOut = diffuseOut + reflectDiffuseOut;

    specularOut = specularOut + envSpecularOut;
    lightOut = materialColor * diffuseOut + specularOut;
    
  return;

	

}

void evaluateLightsNoEmissiveNoFog(float3 pixelPos, float3 pixelNormal, float4 materialColor, float4 shadowPosition, inout float3 lightOut) {
  float3 specOut;
  evaluateLightsNoEmissiveNoFog(pixelPos, pixelNormal, materialColor, shadowPosition, lightOut, specOut);
}


float3 applyFog(float3 inColor, float distance) {
  float fogFactor = fogAmount(distance);
  return fogFactor * inColor + (1 - fogFactor) * _FogColor;
}


void evaluateLights(float3 pixelPos, float3 pixelNormal, float4 materialColor, float4 shadowPosition, inout float3 lightOut, inout float3 specularOut) {
    evaluateLightsNoEmissiveNoFog(pixelPos, pixelNormal, materialColor, shadowPosition, lightOut, specularOut);
    lightOut = lightOut + materialColor * (_EmissiveAmount * _EmissiveColor.rgb);
    lightOut = applyFog(lightOut, length(pixelPos- _WorldSpaceCameraPos));
}

void evaluateLightsNoFog(float3 pixelPos, float3 pixelNormal, float4 materialColor, float4 shadowPosition, inout float3 lightOut, inout float3 specularOut) {
    evaluateLightsNoEmissiveNoFog(pixelPos, pixelNormal, materialColor, shadowPosition, lightOut, specularOut);
    lightOut = lightOut + materialColor * (_EmissiveAmount * _EmissiveColor.rgb);
}


void evaluateLights(float3 pixelPos, float3 pixelNormal, float4 materialColor, float4 shadowPosition, inout float3 lightOut) {
float3 specOut = 0;
  evaluateLights(pixelPos, pixelNormal, materialColor, shadowPosition, lightOut, specOut);
}

void evaluateLightsNoFog(float3 pixelPos, float3 pixelNormal, float4 materialColor, float4 shadowPosition, inout float3 lightOut) {
float3 specOut = 0;
  evaluateLightsNoFog(pixelPos, pixelNormal, materialColor, shadowPosition, lightOut, specOut);
}


void evaluateLights(float3 pixelPos, float3 pixelNormal, float4 materialColor, inout float3 lightOut) {
    float4 shadowPosition = mul(_ShadowMatrix, pixelPos);
    evaluateLightsNoEmissiveNoFog(pixelPos, pixelNormal, materialColor, shadowPosition, lightOut);
    lightOut = lightOut + materialColor * (_EmissiveAmount * _EmissiveColor.rgb);
    lightOut = applyFog(lightOut, length(pixelPos- _WorldSpaceCameraPos));
}

float3 ennoisen(float3 baseColor, float3 noisePosition) {
  float rawNoise = snoise(noisePosition * _NoiseScale);
  float highCutoff = step(-_NoiseBrightCutoff, rawNoise);
  float lowCutoff = step(_NoiseShadeCutoff, rawNoise);
  return baseColor * (lowCutoff ? (1 - _NoiseIntensity) : highCutoff ? 1 : (1 + _NoiseIntensity));
}

float3 ennoisen(float3 baseColor, float3 noisePosition, float noiseScale, float noiseIntensity) {
  float rawNoise = snoise(noisePosition * noiseScale);
  float highCutoff = step(-_NoiseBrightCutoff, rawNoise);
  float lowCutoff = step(_NoiseShadeCutoff, rawNoise);
  return baseColor * (lowCutoff ? (1 - noiseIntensity) : highCutoff ? 1 : (1 + noiseIntensity));
}

float3 changeBasis(float3 origin, float3 x, float3 y, float3 z, float3 inVec) {
  float3 translated = inVec - origin;
  return float3(dot(x, translated), dot(y, translated), dot(z, translated));
}

void generatePapercraftColorNormal(float3 normal, float3 tangent, float3 binormal, float3 noisePos, inout float4 outColorMult, inout float3 outNormal) {
  float3x3 objectToTangent = float3x3(tangent.x, tangent.y, tangent.z,
            binormal.x, binormal.y, binormal.z,
            normal.x, normal.y, normal.z);
  float3x3 tangentToObject = transpose(objectToTangent);


  float3 intensificator = float3(_NoiseIntensity, _NoiseIntensity, 1);

  float3 noiseD = float3(snoise(noisePos * _NoiseScale + float3(NOISE_EPSILON, 0, 0)), snoise(noisePos * _NoiseScale + float3(0, NOISE_EPSILON, 0)), 0);
  float3 noiseBase = float3(snoise(noisePos * _NoiseScale).xx, 1);
  float3 tangentSpaceNormal = normalize(intensificator * (noiseBase - noiseD));
  float3 objectSpaceNormal = mul(tangentToObject, tangentSpaceNormal);
  outNormal = mul(unity_ObjectToWorld, objectSpaceNormal);

  outColorMult = float4((1 + noiseBase * _NoiseIntensity * 0.4).xxx, 1);
}

void generatePapercraftColorNormal(float3 normal, float3 tangent, float3 binormal, float3 noisePos, inout float4 outColorMult, inout float3 outNormal, float4x4 normalTransform) {
  float3x3 objectToTangent = float3x3(tangent.x, tangent.y, tangent.z,
            binormal.x, binormal.y, binormal.z,
            normal.x, normal.y, normal.z);
  float3x3 tangentToObject = transpose(objectToTangent);


  float3 intensificator = float3(_NoiseIntensity, _NoiseIntensity, 1);

  float3 noiseD = float3(snoise(noisePos * _NoiseScale + float3(NOISE_EPSILON, 0, 0)), snoise(noisePos * _NoiseScale + float3(0, NOISE_EPSILON, 0)), 0);
  float3 noiseBase = float3(snoise(noisePos * _NoiseScale).xx, 1);
  float3 tangentSpaceNormal = normalize(intensificator * (noiseBase - noiseD));
  float3 objectSpaceNormal = mul(tangentToObject, tangentSpaceNormal);
  outNormal = normalize(mul(unity_ObjectToWorld, mul(normalTransform, objectSpaceNormal)));

  outColorMult = float4((1 + noiseBase * _NoiseIntensity * 0.4).xxx, 1);
}

// Should only be used to transform vectors - no translational component!
// normal, tangent, and binormal should be specified in object space.
// This will generate a matrix that transforms a vector from object space to tangent space.
float3x3 genObjToTangentMat(float3 normal, float3 tangent, float3 binormal) {
  float3x3 objectToTangent = float3x3(tangent.x, tangent.y, tangent.z,
            binormal.x, binormal.y, binormal.z,
            normal.x, normal.y, normal.z);
  return objectToTangent;
}

// Should only be used to transform vectors - no translational component!
// normal, tangent, and binormal should be specified in object space.
// This will generate a matrix that transforms a vector from tangent space to object space.
float3x3 genFromTangentMat(float3 normal, float3 tangent, float3 binormal) {
  float3x3 objectToTangent = genObjToTangentMat(normal, tangent, binormal);
  return transpose(objectToTangent);
}

struct PNCVertexInput
{
  float4 position : POSITION;
  float3 normal : NORMAL;
  float4 color : COLOR;
  float2 selectData : TEXCOORD0;
  float2 meshBone : TEXCOORD2;
  float3 selectPoint : TANGENT;
};

struct CTVertexOutput
  {
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float3 tangent : TANGENT;
    float3 binormal : BINORMAL;
    float3 normal : NORMAL;
    float4 worldPosition : TEXCOORD0;
    float4 shadowPosition : TEXCOORD1;
    float3 objectPosition : TEXCOORD2;
    float2 selectData : TEXCOORD3;
    float3 selectPoint : TEXCOORD4;
    float4x4 meshTransform : TEXCOORD5;
  };

  struct PNVertexInput
  {
    float4 position : POSITION;
    float3 normal : NORMAL;
    float2 meshBone : TEXCOORD2;
  };

  struct TVertexOutput
  {
    float4 position : SV_POSITION;
    float3 tangent : TANGENT;
    float3 binormal : BINORMAL;
    float3 normal : TEXCOORD0;
    float4 worldPosition : TEXCOORD1;
    float4 shadowPosition : TEXCOORD2;
    float3 objectPosition : TEXCOORD3;
    float4x4 meshTransform : TEXCOORD5;
  };

CTVertexOutput vertWithColorAndTangents (struct PNCVertexInput vertex)
{

  CTVertexOutput output = (CTVertexOutput)0;
  float4 objectPosition = mul(_RemesherMeshTransforms[vertex.meshBone.x], vertex.position);
  output.position = UnityObjectToClipPos(objectPosition);

  output.worldPosition = mul(unity_ObjectToWorld, objectPosition);
  output.shadowPosition = mul(_ShadowMatrix ,output.worldPosition);
  output.color = float4(GammaToLinearSpace(vertex.color.rgb), vertex.color.a);
  output.objectPosition = vertex.position;
  output.normal = float4(normalize(vertex.normal), 0);

  // Looking for an arbitrary vector that isn't parallel to the normal.  Avoiding axis directions should improve our chances.
  float3 arbitraryVector = normalize(float3(0.42, -0.21, 0.15));
  float3 alternateArbitraryVector = normalize(float3(0.43, 1.5, 0.15));
  // If arbitrary vector is parallel to the normal, choose a different one.
  output.tangent = normalize(abs(dot(output.normal, arbitraryVector)) < 1 ? cross(output.normal, arbitraryVector) : cross(output.normal, alternateArbitraryVector));
  output.binormal = normalize(cross(output.normal, output.tangent));
  output.selectData = vertex.selectData;
  output.selectPoint = vertex.selectPoint;
  output.meshTransform = _RemesherMeshTransforms[vertex.meshBone.x];
  return output;
}

CTVertexOutput vertWithColorTangentsFlippedNormal (struct PNCVertexInput vertex)
{
  CTVertexOutput output = (CTVertexOutput)0;
  float4 objectPosition = mul(_RemesherMeshTransforms[vertex.meshBone.x], vertex.position);
  output.position = UnityObjectToClipPos(objectPosition);

  output.worldPosition = mul(unity_ObjectToWorld, objectPosition);
  output.shadowPosition = mul(_ShadowMatrix ,output.worldPosition);
  output.color = float4(GammaToLinearSpace(vertex.color.rgb), vertex.color.a);
  output.objectPosition = vertex.position;
  output.normal =  normalize(-vertex.normal);

  // Looking for an arbitrary vector that isn't parallel to the normal.  Avoiding axis directions should improve our chances.
  float3 arbitraryVector = normalize(float3(0.42, -0.21, 0.15));
  float3 alternateArbitraryVector = normalize(float3(0.43, 1.5, 0.15));
  // If arbitrary vector is parallel to the normal, choose a different one.
  output.tangent = normalize(abs(dot(output.normal, arbitraryVector)) < 1 ? cross(output.normal, arbitraryVector) : cross(output.normal, alternateArbitraryVector));
  output.binormal = normalize(cross(output.normal, output.tangent));
  return output;
}

TVertexOutput vertWithTangents (struct PNVertexInput vertex)
{

  TVertexOutput output;
  float4 objectPosition = mul(_RemesherMeshTransforms[vertex.meshBone.x], vertex.position);
  output.position = UnityObjectToClipPos(objectPosition);

  output.worldPosition = mul(unity_ObjectToWorld, objectPosition);
  output.shadowPosition = mul(_ShadowMatrix ,output.worldPosition);
  output.objectPosition = vertex.position;
  output.normal =  normalize(vertex.normal);

  // Looking for an arbitrary vector that isn't parallel to the normal.  Avoiding axis directions should improve our chances.
  float3 arbitraryVector = normalize(float3(0.42, -0.21, 0.15));
  float3 alternateArbitraryVector = normalize(float3(0.43, 1.5, 0.15));
  // If arbitrary vector is parallel to the normal, choose a different one.
  output.tangent = normalize(abs(dot(output.normal, arbitraryVector)) < 1 ? cross(output.normal, arbitraryVector) : cross(output.normal, alternateArbitraryVector));
  output.binormal = normalize(cross(output.normal, output.tangent));
  output.meshTransform = _RemesherMeshTransforms[vertex.meshBone.x];
  return output;
}