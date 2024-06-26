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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.app;

public delegate void EnvironmentThemeActionHandler(object sender, EnvironmentThemeManager.EnvironmentTheme theme);

public class EnvironmentThemeManager : MonoBehaviour {

  public event EnvironmentThemeActionHandler EnvironmentThemeActionHandler;

  public enum EnvironmentTheme {
    DAY = 0,
    SUNSET = 1,
    NIGHT = 2,
    PURPLE = 3,
    SNOW = 4,
    DESERT = 5,
    GREEN = 6,
    BLACK = 7,
    WHITE = 8
  };

  /// <summary>
  ///   Sky colors for themes.
  /// </summary>
  private readonly Color DAY = new Color(231f / 255f, 251f / 255f, 255f / 255f);
  private readonly Color NIGHT = new Color(77f / 255f, 69f / 255f, 103f / 255f);
  private readonly Color PURPLE = new Color(206f / 255f, 178f / 255f, 255f / 255f);
  private readonly Color BLACK = new Color(0F / 255f, 0F / 255f, 0F / 255f);
  private readonly Color WHITE = new Color(229f / 255f, 229f / 255f, 229f / 255f);

  /// <summary>
  ///   Horizon colors for themes.
  /// </summary>
  private readonly Color DAY_HORIZON = new Color(17f / 255f, 139f / 255f, 255f / 255f);
  private readonly Color NIGHT_HORIZON = new Color(77f / 255f, 69f / 255f, 103f / 255f);
  private readonly Color PURPLE_HORIZON = new Color(255f / 255f, 255f / 255f, 255f / 255f);
  private readonly Color BLACK_HORIZON = new Color(0F / 255f, 0F / 255f, 0F / 255f);
  private readonly Color WHITE_HORIZON = new Color(0f / 255f, 0f / 255f, 0f / 255f);

  /// <summary>
  ///   Ground color for themes.
  /// </summary>
  private readonly Color DAY_GROUND = new Color(126f / 255f, 109f / 255f, 91f / 255f);
  private readonly Color NIGHT_GROUND = new Color(77f / 255f, 69f / 255f, 103f / 255f);
  private readonly Color PURPLE_GROUND = new Color(31f / 255f, 25f / 255f, 56f / 255f);
  private readonly Color BLACK_GROUND = new Color(0F / 255f, 0F / 255f, 0F / 255f);
  private readonly Color WHITE_GROUND = new Color(229f / 255f, 229f / 255f, 229f / 255f);

  /// <summary>
  ///   Fog color for themes.
  /// </summary>
  private readonly Color DAY_FOG = new Color(126f / 255f, 109f / 255f, 91f / 255f);
  private readonly Color NIGHT_FOG = new Color(77f / 255f, 69f / 255f, 103f / 255f);
  private readonly Color PURPLE_FOG = new Color(77f / 255f, 69f / 255f, 103f / 255f);
  private readonly Color BLACK_FOG = new Color(10F / 255f, 10F / 255f, 10F / 255f);
  private readonly Color WHITE_FOG = new Color(229f / 255f, 229f / 255f, 229f / 255f);

  /// <summary>
  ///   Fog density for themes.
  /// </summary>
  private readonly float DAY_FOG_DENSITY = 0.00f;
  private readonly float NIGHT_FOG_DENSITY = 0.001f;
  private readonly float PURPLE_FOG_DENSITY = 0.0005f;
  private readonly float BLACK_FOG_DENSITY = 0.0f;
  private readonly float WHITE_FOG_DENSITY = 0.0f;

  /// <summary>
  ///   Ambient color for themes.
  /// </summary>
  private readonly Color DAY_AMBIENT = new Color(0.2352941f, 0.1686274f, 0.2509804f);
  private readonly Color NIGHT_AMBIENT = new Color(0.2352941f, 0.1686274f, 0.2509804f);
  private readonly Color PURPLE_AMBIENT = new Color(0.2352941f, 0.1686274f, 0.2509804f);
  private readonly Color BLACK_AMBIENT = new Color(0F / 255f, 0F / 255f, 0F / 255f);
  private readonly Color WHITE_AMBIENT = new Color(229f / 255f, 229f / 255f, 229f / 255f);

  /// <summary>
  ///   Sun Radius B for themes.
  /// </summary>
  private readonly float DAY_SUN_RADIUS_B = 0.0463f;
  private readonly float NIGHT_SUN_RADIUS_B = 0f;
  private readonly float PURPLE_SUN_RADIUS_B = 0f;
  private readonly float BLACK_SUN_RADIUS_B = 0f;
  private readonly float WHITE_SUN_RADIUS_B = 0f;

  /// <summary>
  ///   Cloud strength for themes.
  /// </summary>
  private readonly float DAY_CLOUD_STRENGTH = 0.273f;
  private readonly float NIGHT_CLOUD_STRENGTH = 0.138f;
  private readonly float PURPLE_CLOUD_STRENGTH = 0.138f;
  private readonly float BLACK_CLOUD_STRENGTH = 0.138f;
  private readonly float WHITE_CLOUD_STRENGTH = 0.138f;

  public bool loop = true;
  public EnvironmentTheme currentTheme = EnvironmentTheme.PURPLE;
  public float transitionLength = 7.50f;
  private float transitionStartTime;

  private GameObject sky;
  private Material skyMaterial;
  private Material groundMaterial;
  private Texture skyTexture;
  private Texture daySkyTexture;

  private Color currentSkyColor;
  private Color currentGroundColor;
  private Color currentFogColor;
  private Color currentAmbientColor;
  private Color currentHorizonColor;

  private float currentSkyStrength;
  private float currentSunRadiusB;
  private float currentCloudStrength;
  private float currentFogDensity;
  private float tempFogDensity = -1f;

  private Texture currentSkyTexture;

  private RaycastHit menuHit;
  private bool isHoldingSky = false;

  private GameObject purpleGroundAndPlane;
  public GameObject nightEnvironment;
  public GameObject dayEnvironment;

  
  public void Setup() {
    transitionStartTime = Time.time;
    ResolveReferences();
  }

  private void ResolveReferences() {
    // Purple environment.
    sky = transform.Find("Sky").gameObject;
    skyMaterial = sky.GetComponent<Renderer>().material;
    skyTexture = skyMaterial.GetTexture("_Sky");
    purpleGroundAndPlane = ObjectFinder.ObjectById("ID_PurpleGroundAndPlane");
    groundMaterial = purpleGroundAndPlane.transform.Find("Ground").GetComponent<Renderer>().material;

    // Day environment.
    GameObject daySky = transform.Find("Environment/S_SkySphere").gameObject;
    daySkyTexture = daySky.GetComponent<Renderer>().material.GetTexture("_Sky");

    currentSkyColor = skyMaterial.GetColor("_SkyColor");
    currentSkyStrength = skyMaterial.GetFloat("_SkyStrength");
    currentHorizonColor = skyMaterial.GetColor("_HorizonColor");
    currentSunRadiusB = skyMaterial.GetFloat("_SunRadiusB");
    currentCloudStrength = skyMaterial.GetFloat("_CloudStrength");
    currentFogDensity = RenderSettings.fogDensity;
    currentSkyTexture = skyMaterial.GetTexture("_Sky");
    currentFogColor = RenderSettings.fogColor;
    currentAmbientColor = RenderSettings.ambientLight;
    currentGroundColor = groundMaterial.GetColor("_Color");
  }

  void Update () {
    // Animate
    float pctDone = (Time.time - transitionStartTime) / transitionLength;
    if (pctDone <= 1.0f) {
      // Set color transtion
      UpdateSkyColor(pctDone);
    }
  }

  /// <summary>
  ///   Entry point for setting environment. Things that need only happen once and not on update loop.
  ///   Mainly we cache some previous values to facilitate the transitions.
  /// </summary>
  /// <param name="theme"></param>
  public void SetEnvironment(EnvironmentTheme theme) {
    tempFogDensity = -1f;
    if (EnvironmentThemeActionHandler != null) EnvironmentThemeActionHandler(null, theme);
    if (skyMaterial != null) {
      currentSkyColor = skyMaterial.GetColor("_SkyColor");
      currentSkyStrength = skyMaterial.GetFloat("_SkyStrength");
      currentHorizonColor = skyMaterial.GetColor("_HorizonColor");
      currentSunRadiusB = skyMaterial.GetFloat("_SunRadiusB");
      currentCloudStrength = skyMaterial.GetFloat("_CloudStrength");
      currentSkyTexture = skyMaterial.GetTexture("_Sky");
    }
    currentFogColor = RenderSettings.fogColor;
    currentFogDensity = RenderSettings.fogDensity;
    currentAmbientColor = RenderSettings.ambientLight;
    if(groundMaterial != null) currentGroundColor = groundMaterial.GetColor("_Color");
    currentTheme = theme;
    transitionStartTime = Time.time;
    switch (theme) {
      case EnvironmentTheme.DAY:
        dayEnvironment.SetActive(true);
        nightEnvironment.SetActive(false);
        purpleGroundAndPlane.SetActive(true);
        break;
      case EnvironmentTheme.PURPLE:
        dayEnvironment.SetActive(false);
        nightEnvironment.SetActive(true);
        purpleGroundAndPlane.SetActive(true);
        break;
      case EnvironmentTheme.BLACK:
        dayEnvironment.SetActive(false);
        nightEnvironment.SetActive(true);
        purpleGroundAndPlane.SetActive(false);
        break;
      case EnvironmentTheme.WHITE:
        dayEnvironment.SetActive(false);
        nightEnvironment.SetActive(true);
        purpleGroundAndPlane.SetActive(false);
        break;
    }
  }

  /// <summary>
  ///   Lerps the current theme parameters, updates anything that needs to be per frame.
  /// </summary>
  /// <param name="pctDone">Percent done of transition time.</param>
  private void UpdateSkyColor(float pctDone) {
    switch (currentTheme) {
      case EnvironmentTheme.DAY:
        // Sky
        skyMaterial.SetColor("_HorizonColor", Color.Lerp(currentHorizonColor, DAY_HORIZON, pctDone));
        skyMaterial.SetFloat("_SunRadiusB", Mathf.Lerp(currentSunRadiusB, DAY_SUN_RADIUS_B, pctDone));
        skyMaterial.SetFloat("_CloudStrength", Mathf.Lerp(currentCloudStrength, DAY_CLOUD_STRENGTH, pctDone));
        skyMaterial.SetTexture("_Sky", daySkyTexture);

        skyMaterial.SetColor("_SkyColor", Color.Lerp(currentSkyColor, DAY, pctDone));
        skyMaterial.SetFloat("_SkyStrength", Mathf.Lerp(currentSkyStrength, 1.00f, pctDone));
        groundMaterial.SetColor("_Color", Color.Lerp(currentGroundColor, DAY_GROUND, pctDone));
        RenderSettings.fogColor = Color.Lerp(currentFogColor, DAY_FOG, pctDone);
        if (pctDone < 0.5f) {
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, 1.0f, pctDone * 2.0f);
        } else {
          if (tempFogDensity < 0) currentFogDensity = RenderSettings.fogDensity;
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, DAY_FOG_DENSITY, (pctDone - 0.5f) / 0.5f);
        }
        RenderSettings.ambientLight = Color.Lerp(currentAmbientColor, DAY_AMBIENT, pctDone);
        break;
      case EnvironmentTheme.NIGHT:
        skyMaterial.SetColor("_HorizonColor", Color.Lerp(currentHorizonColor, NIGHT_HORIZON, pctDone));
        skyMaterial.SetFloat("_SunRadiusB", Mathf.Lerp(currentSunRadiusB, NIGHT_SUN_RADIUS_B, pctDone));
        skyMaterial.SetFloat("_CloudStrength", Mathf.Lerp(currentCloudStrength, NIGHT_CLOUD_STRENGTH, pctDone));
        skyMaterial.SetTexture("_Sky", skyTexture);

        skyMaterial.SetColor("_SkyColor", Color.Lerp(currentSkyColor, NIGHT, pctDone));
        skyMaterial.SetFloat("_SkyStrength", Mathf.Lerp(currentSkyStrength, 1.00f, pctDone));
        groundMaterial.SetColor("_Color", Color.Lerp(currentGroundColor, NIGHT_GROUND, pctDone));
        RenderSettings.fogColor = Color.Lerp(currentFogColor, NIGHT_FOG, pctDone);
        if(pctDone < 0.5f) {
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, 1.0f, pctDone * 2.0f);
        } else {
          if (tempFogDensity < 0) currentFogDensity = RenderSettings.fogDensity;
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, NIGHT_FOG_DENSITY, (pctDone - 0.5f) / 0.5f);
        }
        RenderSettings.fog = true;
        RenderSettings.ambientLight = Color.Lerp(currentAmbientColor, NIGHT_AMBIENT, pctDone);
        break;
      case EnvironmentTheme.PURPLE:
        skyMaterial.SetColor("_HorizonColor", Color.Lerp(currentHorizonColor, PURPLE_HORIZON, pctDone));
        skyMaterial.SetFloat("_SunRadiusB", Mathf.Lerp(currentSunRadiusB, PURPLE_SUN_RADIUS_B, pctDone));
        skyMaterial.SetFloat("_CloudStrength", Mathf.Lerp(currentCloudStrength, PURPLE_CLOUD_STRENGTH, pctDone));
        skyMaterial.SetTexture("_Sky", skyTexture);

        skyMaterial.SetColor("_SkyColor", Color.Lerp(currentSkyColor, PURPLE, pctDone));
        skyMaterial.SetFloat("_SkyStrength", Mathf.Lerp(currentSkyStrength, 1.00f, pctDone));
        groundMaterial.SetColor("_Color", Color.Lerp(currentGroundColor, PURPLE_GROUND, pctDone));
        RenderSettings.fogColor = Color.Lerp(currentFogColor, PURPLE_FOG, pctDone);
        if (pctDone < 0.5f) {
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, 1.0f, pctDone * 2.0f);
        } else {
          if (tempFogDensity < 0) currentFogDensity = RenderSettings.fogDensity;
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, PURPLE_FOG_DENSITY, (pctDone - 0.5f) / 0.5f);
        }
        RenderSettings.fog = true;
        RenderSettings.ambientLight = Color.Lerp(currentAmbientColor, PURPLE_AMBIENT, pctDone);
        break;
      case EnvironmentTheme.BLACK:
        skyMaterial.SetColor("_HorizonColor", Color.Lerp(currentHorizonColor, BLACK_HORIZON, pctDone));
        skyMaterial.SetFloat("_SunRadiusB", Mathf.Lerp(currentSunRadiusB, BLACK_SUN_RADIUS_B, pctDone));
        skyMaterial.SetFloat("_CloudStrength", Mathf.Lerp(currentCloudStrength, BLACK_CLOUD_STRENGTH, pctDone));
        skyMaterial.SetTexture("_Sky", skyTexture);

        skyMaterial.SetColor("_SkyColor", Color.Lerp(currentSkyColor, BLACK, pctDone));
        skyMaterial.SetFloat("_SkyStrength", Mathf.Lerp(currentSkyStrength, 0.00f, pctDone));
        groundMaterial.SetColor("_Color", Color.Lerp(currentGroundColor, BLACK_GROUND, pctDone));
        RenderSettings.fogColor = Color.Lerp(currentFogColor, BLACK_FOG, pctDone);
        if (pctDone < 0.5f) {
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, 1.0f, pctDone * 2.0f);
        } else {
          if (tempFogDensity < 0) currentFogDensity = RenderSettings.fogDensity;
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, BLACK_FOG_DENSITY, (pctDone - 0.5f) / 0.5f);
        }
        RenderSettings.fog = true;
        RenderSettings.ambientLight = Color.Lerp(currentAmbientColor, BLACK_AMBIENT, pctDone);
        break;
      case EnvironmentTheme.WHITE:
        skyMaterial.SetColor("_HorizonColor", Color.Lerp(currentHorizonColor, WHITE_HORIZON, pctDone));
        skyMaterial.SetFloat("_SunRadiusB", Mathf.Lerp(currentSunRadiusB, WHITE_SUN_RADIUS_B, pctDone));
        skyMaterial.SetFloat("_CloudStrength", Mathf.Lerp(currentCloudStrength, WHITE_CLOUD_STRENGTH, pctDone));
        skyMaterial.SetTexture("_Sky", skyTexture);

        skyMaterial.SetColor("_SkyColor", Color.Lerp(currentSkyColor, WHITE, pctDone));
        skyMaterial.SetFloat("_SkyStrength", Mathf.Lerp(currentSkyStrength, 0.00f, pctDone));
        groundMaterial.SetColor("_Color", Color.Lerp(currentGroundColor, WHITE_GROUND, pctDone));
        RenderSettings.fogColor = Color.Lerp(currentFogColor, WHITE_FOG, pctDone);
        if (pctDone < 0.5f) {
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, 1.0f, pctDone * 2.0f);
        } else {
          if (tempFogDensity < 0) currentFogDensity = RenderSettings.fogDensity;
          RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, WHITE_FOG_DENSITY, (pctDone - 0.5f) / 0.5f);
        }
        RenderSettings.fog = true;
        RenderSettings.ambientLight = Color.Lerp(currentAmbientColor, WHITE_AMBIENT, pctDone);
        break;
      default:
        break;
    }
  }
}
