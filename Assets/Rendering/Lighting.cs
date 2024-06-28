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

using UnityEngine;

[ExecuteInEditMode]
public class Lighting : MonoBehaviour
{
    public RenderTextureFormat renderTextureFormat = RenderTextureFormat.RGFloat;

    public Shader depthReplacementShader;
    public Camera shadowCamera;

    // This value needs to be kept in sync with the value in Assets\Rendering\shaderMath.cginc
    private const int NUMBER_OF_POINT_LIGHTS = 8;

    // Used to configure lighting within the Unity editor
    public Color environmentOverride = new Color(1f, 1f, 1f);
    public float environmentOverrideAmount = 0f;
    public float environmentSpecularAmount = 1f;
    public RenderTexture shadowTexture;
    private RenderTexture blurShadowTexture;

    public Cubemap environmentCube;
    public float environmentDiffuseStrength = 1f;
    public float environmentSpecularStrength = 1f;

    public float shadowDistance = 100;
    public Color lightColor = new Color(1f, 1f, 1f);
    public float lightStrength = .6f;
    public float lightSize = 0.5f;
    public Color fillLightColor = new Color(1f, 1f, 1f);
    public float fillLightStrength = .6f;
    public Color fogColor = new Color(.8f, .7f, .7f);
    public float fogStrength = 0.1f;
    public Material blurMat;
    public bool downsample = false;
    public int blurIterations = 0;
    const int shadowResolution = 1024;

    public Light[] pointLights = new Light[NUMBER_OF_POINT_LIGHTS];

    // Index of pointLights that holds the button light effect.
    private const int BUTTON_LIGHT_INDEX = 0;

    private Vector4[] lightColors = new Vector4[NUMBER_OF_POINT_LIGHTS];
    private Vector4[] lightPositions = new Vector4[NUMBER_OF_POINT_LIGHTS];

    // String identifiers for global shader uniforms used for lighting the majority of materials
    const string LIGHT_DIRECTION = "_LightDirection";
    const string LIGHT_COLOR = "_LightColor";
    const string LIGHT_POSITION = "_LightPosition";
    const string LIGHT_SIZE = "_LightSize";
    const string CAMERA_SIZE = "_CameraSize";
    const string FILL_LIGHT_COLOR = "_FillLightColor";
    const string FILL_LIGHT_POSITION = "_FillLightPosition";
    const string SHADOW_DISTANCE = "_ShadowInfo";
    const string SHADOW_MATRIX = "_ShadowMatrix";
    const string SHADOW_TEX = "_ShadowTexture";
    const string SHADOW_BLUR_TEX = "_ShadowBlurTexture";
    const string FOG_COLOR = "_FogColor";
    const string FOG_STRENGTH = "_FogStrength";
    const string POINT_LIGHT_POSITIONS = "_PointLightWorldPositions";
    const string POINT_LIGHT_COLORS = "_PointLightColors";
    const string ENV_OVERRIDE = "_EnvOverrideColor";
    const string ENV_OVERRIDE_AMOUNT = "_EnvOverrideAmount";
    const string ENV_SPECULAR_AMOUNT = "_EnvSpecularAmount";
    const string ENV_CUBEMAP = "_EnvCubeMap";
    const string ENV_DIFFUSE_STRENGTH = "_EnvDiffuseStrength";
    const string ENV_SPECULAR_STRENGTH = "_EnvSpecularStrength";

    // Integer ids for global shader uniforms - it's slightly faster to set values using these.
    int LIGHT_DIRECTION_ID;
    int LIGHT_COLOR_ID;
    int LIGHT_POSITION_ID;
    int LIGHT_SIZE_ID;
    int CAMERA_SIZE_ID;
    int FILL_LIGHT_COLOR_ID;
    int FILL_LIGHT_POSITION_ID;
    int SHADOW_DISTANCE_ID;
    int SHADOW_MATRIX_ID;
    int SHADOW_TEX_ID;
    int SHADOW_BLUR_TEX_ID;
    int FOG_COLOR_ID;
    int FOG_STRENGTH_ID;
    int POINT_LIGHT_WORLD_POSITIONS_ID;
    int POINT_LIGHT_COLORS_ID;
    int ENV_OVERRIDE_ID;
    int ENV_OVERRIDE_AMOUNT_ID;
    int ENV_SPECULAR_AMOUNT_ID;
    int ENV_CUBEMAP_ID;
    int ENV_DIFFUSE_STRENGTH_ID;
    int ENV_SPECULAR_STRENGTH_ID;

    void Start()
    {
        LIGHT_DIRECTION_ID = Shader.PropertyToID(LIGHT_DIRECTION);
        LIGHT_COLOR_ID = Shader.PropertyToID(LIGHT_COLOR);
        LIGHT_POSITION_ID = Shader.PropertyToID(LIGHT_POSITION);
        LIGHT_SIZE_ID = Shader.PropertyToID(LIGHT_SIZE);
        CAMERA_SIZE_ID = Shader.PropertyToID(CAMERA_SIZE);
        FILL_LIGHT_COLOR_ID = Shader.PropertyToID(FILL_LIGHT_COLOR);
        FILL_LIGHT_POSITION_ID = Shader.PropertyToID(FILL_LIGHT_POSITION);
        SHADOW_DISTANCE_ID = Shader.PropertyToID(SHADOW_DISTANCE);
        SHADOW_MATRIX_ID = Shader.PropertyToID(SHADOW_MATRIX);
        SHADOW_TEX_ID = Shader.PropertyToID(SHADOW_TEX);
        SHADOW_BLUR_TEX_ID = Shader.PropertyToID(SHADOW_BLUR_TEX);
        FOG_COLOR_ID = Shader.PropertyToID(FOG_COLOR);
        FOG_STRENGTH_ID = Shader.PropertyToID(FOG_STRENGTH);
        POINT_LIGHT_WORLD_POSITIONS_ID = Shader.PropertyToID(POINT_LIGHT_POSITIONS);
        POINT_LIGHT_COLORS_ID = Shader.PropertyToID(POINT_LIGHT_COLORS);
        ENV_OVERRIDE_ID = Shader.PropertyToID(ENV_OVERRIDE);
        ENV_OVERRIDE_AMOUNT_ID = Shader.PropertyToID(ENV_OVERRIDE_AMOUNT);
        ENV_SPECULAR_AMOUNT_ID = Shader.PropertyToID(ENV_SPECULAR_AMOUNT);
        ENV_CUBEMAP_ID = Shader.PropertyToID(ENV_CUBEMAP);
        ENV_DIFFUSE_STRENGTH_ID = Shader.PropertyToID(ENV_DIFFUSE_STRENGTH);
        ENV_SPECULAR_STRENGTH_ID = Shader.PropertyToID(ENV_SPECULAR_STRENGTH);
        InitializeShadows();
        UpdateLightPositions();
        Shader.SetGlobalTexture(ENV_CUBEMAP_ID, environmentCube);

    }

    public void UpdateLightPositions()
    {
        // Updates array of lighting values from configured Unity point lights. We need to pass the data into the shader in
        // this form.
        for (int i = 0; i < NUMBER_OF_POINT_LIGHTS; i++)
        {
            if (pointLights.Length > i && pointLights[i] != null)
            {
                Light curLight = pointLights[i];
                Vector3 pos = pointLights[i].transform.position;
                lightPositions[i] = new Vector4(pos.x, pos.y, pos.z, 1f);
                lightColors[i] = new Vector4(curLight.color.r, curLight.color.g, curLight.color.b, curLight.intensity);
            }
            else
            {
                lightPositions[i] = Vector4.zero;
                lightColors[i] = new Vector4(0f, 0f, 0f, 0f);
            }
        }
    }

    void InitializeShadows()
    {
        if (shadowCamera != null)
        {
            shadowCamera.SetReplacementShader(depthReplacementShader, "RenderType");
            if (shadowTexture == null)
            {
                shadowTexture = new RenderTexture(shadowResolution,
                shadowResolution,
                32,
                renderTextureFormat,
                RenderTextureReadWrite.Linear);
                shadowTexture.useMipMap = false;
                shadowTexture.autoGenerateMips = false;
                shadowTexture.filterMode = FilterMode.Point;
                shadowTexture.wrapMode = TextureWrapMode.Clamp;
            }
            shadowTexture.useMipMap = false;
            shadowTexture.autoGenerateMips = false;

            if (blurShadowTexture == null)
            {
                blurShadowTexture = new RenderTexture(shadowResolution,
                  shadowResolution,
                  24,
                  renderTextureFormat,
                  RenderTextureReadWrite.Linear);
                blurShadowTexture.filterMode = FilterMode.Bilinear;
            }
            blurShadowTexture.useMipMap = false;
            blurShadowTexture.autoGenerateMips = false;
            shadowCamera.targetTexture = shadowTexture;
            shadowCamera.nearClipPlane = 0f;
            shadowCamera.farClipPlane = shadowDistance;
            Shader.SetGlobalTexture(SHADOW_TEX_ID, shadowTexture);
            Shader.SetGlobalTexture(SHADOW_BLUR_TEX_ID, blurShadowTexture);
        }
    }

    void Update()
    {
        // Set global lighting information used by all Directional* material shaders.
        UpdateLightPositions();
        Shader.SetGlobalVector(LIGHT_DIRECTION_ID, transform.forward);
        Shader.SetGlobalVector(LIGHT_POSITION_ID, transform.position);
        Shader.SetGlobalFloat(LIGHT_SIZE_ID, lightSize);
        Shader.SetGlobalFloat(CAMERA_SIZE_ID, shadowCamera.orthographicSize);
        Shader.SetGlobalVector(LIGHT_COLOR_ID, new Vector3(lightStrength * lightColor.r,
          lightStrength * lightColor.g,
          lightStrength * lightColor.b));
        Shader.SetGlobalVector(FILL_LIGHT_POSITION_ID, transform.position * -1f);
        Shader.SetGlobalVector(FILL_LIGHT_COLOR_ID, new Vector3(fillLightStrength * fillLightColor.r,
          fillLightStrength * fillLightColor.g,
          fillLightStrength * fillLightColor.b));
        Shader.SetGlobalVector(FOG_COLOR_ID, fogColor);
        Shader.SetGlobalFloat(FOG_STRENGTH_ID, fogStrength);

        Shader.SetGlobalVector(SHADOW_DISTANCE_ID, new Vector4(1.0f / shadowDistance, shadowResolution, 0f, 0f));

        Shader.SetGlobalFloat(ENV_OVERRIDE_AMOUNT_ID, environmentOverrideAmount);
        Shader.SetGlobalVector(ENV_OVERRIDE_ID, environmentOverride);
        Shader.SetGlobalFloat(ENV_SPECULAR_AMOUNT_ID, environmentSpecularAmount);
        Shader.SetGlobalFloat(ENV_SPECULAR_STRENGTH_ID, environmentSpecularStrength);
        Shader.SetGlobalFloat(ENV_DIFFUSE_STRENGTH_ID, environmentDiffuseStrength);
        Matrix4x4 shadowVP = GetComponent<Camera>().projectionMatrix * GetComponent<Camera>().worldToCameraMatrix;
        Shader.SetGlobalMatrix(SHADOW_MATRIX_ID, shadowVP);
        Shader.SetGlobalVectorArray(POINT_LIGHT_WORLD_POSITIONS_ID, lightPositions);
        Shader.SetGlobalVectorArray(POINT_LIGHT_COLORS_ID, lightColors);

    }

    void OnDestroy()
    {
        if (shadowTexture != null)
        {
            shadowTexture.Release();
        }
    }

    void OnPostRender()
    {

        int res = shadowResolution;
        if (downsample)
        {
            res = shadowResolution / 2;
        }

        RenderTexture rtQuarter =
          RenderTexture.GetTemporary(res, res, 0, renderTextureFormat, RenderTextureReadWrite.Linear);
        RenderTexture rtQuarterB =
          RenderTexture.GetTemporary(res, res, 0, renderTextureFormat, RenderTextureReadWrite.Linear);

        Graphics.Blit(shadowTexture, rtQuarter, blurMat, 0);

        for (int i = 0; i < blurIterations; i++)
        {
            Graphics.Blit(rtQuarter, rtQuarterB, blurMat, 3);
            Graphics.Blit(rtQuarterB, rtQuarter, blurMat, 4);
        }

        Graphics.Blit(rtQuarter, blurShadowTexture);

        rtQuarter.DiscardContents();
        RenderTexture.ReleaseTemporary(rtQuarter);

        rtQuarterB.DiscardContents();
        RenderTexture.ReleaseTemporary(rtQuarterB);
    }
}
