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

namespace com.google.apps.peltzer.client.model.render {
  /// <summary>
  ///   A place to get materials by id.
  /// </summary>
  public class MaterialRegistry {
    public enum MaterialType {
      PAPER,
      GEM,
      GLASS
    }

    // Taken from UI docs, starting at: go/oblinks
    public static int[] rawColors = {
      0xBA68C8,
      0x9C27B0,
      0x673AB7,
      0x80DEEA,
      0x00BCD4,
      0x039BE5,
      0xF8BBD0,
      0xF06292,
      0xF44336,
      0x8BC34A,
      0x4CAF50,
      0x009688,
      0xFFEB3B,
      0xFF9800,
      0xFF5722,
      0xCFD8DC,
      0x78909C,
      0x455A64,
      0xFFCC88,
      0xDD9944,
      0x795548,
      0xFFFFFF,
      0x9E9E9E,
      0x1A1A1A,
    };

    // Unity Materials with albedo set.
    private static Material[] materialsWithAlbedo = null;

    // Our custom MaterialAndColor, with albedo unset, as we use vertex colours.
    private static MaterialAndColor[] materials = null;
    private static MaterialAndColor[] previewMaterials = null;
    private static MaterialAndColor[] highlightMaterials = null;
    private static Color32[] color32s = null;

    private static MaterialAndColor highlightSilhouetteMaterial;

    public static int GLASS_ID = rawColors.Length;
    public static int GEM_ID = rawColors.Length + 1;
    public static int PINK_WIREFRAME_ID = rawColors.Length + 2;
    public static int GREEN_WIREFRAME_ID = rawColors.Length + 3;
    public static int HIGHLIGHT_SILHOUETTE_ID = rawColors.Length + 4;
    
    public static readonly int RED_ID = 8;
    public static readonly int DEEP_ORANGE_ID = 14;
    public static readonly int YELLOW_ID = 12;
    public static readonly int WHITE_ID = 21;
    public static readonly int BLACK_ID = 23;

    private static readonly string BASE_SHADER = "Mogwai/DirectionalPaperUniform";
    private static readonly string PREVIEW_SHADER = "Mogwai/DirectionalTransparent";

    /// <summary>
    ///   Must be called before used.  Creates the materials for the given color codes.
    /// </summary>
    public static void init(MaterialLibrary materialLibrary) {
      Material baseMaterial = materialLibrary.baseMaterial;
      Material glassMaterial = materialLibrary.glassMaterial;
      Material gemMaterial = materialLibrary.gemMaterial;
      Material subtractMaterial = materialLibrary.subtractMaterial;
      Material copyMaterial = materialLibrary.copyMaterial;
      materials = new MaterialAndColor[rawColors.Length + 4];
      materialsWithAlbedo = new Material[rawColors.Length + 4];
      previewMaterials = new MaterialAndColor[rawColors.Length + 4];
      color32s = new Color32[rawColors.Length + 4];
      Material templateMaterial =
        baseMaterial == null ? new Material(Shader.Find(BASE_SHADER)) : new Material(baseMaterial);
      for (int i = 0; i < rawColors.Length; i++) {
        materials[i] = new MaterialAndColor(templateMaterial, i);
        materials[i].color = new Color(r(rawColors[i]), g(rawColors[i]), b(rawColors[i]));
        color32s[i] = materials[i].color;
        materialsWithAlbedo[i] = new Material(Shader.Find(BASE_SHADER));
        materialsWithAlbedo[i].color = new Color(r(rawColors[i]), g(rawColors[i]), b(rawColors[i]));
        previewMaterials[i] = new MaterialAndColor(new Material(Shader.Find(PREVIEW_SHADER)), i);
        previewMaterials[i].color = new Color(r(rawColors[i]), g(rawColors[i]), b(rawColors[i]), /* alpha */ 1.0f);
        previewMaterials[i].material.SetFloat("_MultiplicitiveAlpha", 0.3f);
      }
      // "Special" materials.
      materials[GLASS_ID] = new MaterialAndColor(glassMaterial, materialLibrary.glassSpecMaterial, glassMaterial.color, GLASS_ID);
      materialsWithAlbedo[GLASS_ID] = glassMaterial;
      materials[GEM_ID] = new MaterialAndColor(gemMaterial, GEM_ID);
      materialsWithAlbedo[GEM_ID] = gemMaterial;
      materials[PINK_WIREFRAME_ID] = new MaterialAndColor(subtractMaterial, PINK_WIREFRAME_ID);
      materialsWithAlbedo[PINK_WIREFRAME_ID] = subtractMaterial;
      materials[GREEN_WIREFRAME_ID] = new MaterialAndColor(copyMaterial, GREEN_WIREFRAME_ID);
      materialsWithAlbedo[GREEN_WIREFRAME_ID] = copyMaterial;

      previewMaterials[GLASS_ID] = new MaterialAndColor(new Material(glassMaterial), GLASS_ID);
      previewMaterials[GEM_ID] = new MaterialAndColor(new Material(gemMaterial), GEM_ID);

      Color old = previewMaterials[GEM_ID].color;
      previewMaterials[GEM_ID].color = new Color(old.r, old.g, old.b, 0.1f);
      highlightMaterials = new MaterialAndColor[materials.Length];
      for (int i = 0; i < materials.Length; i++) {
        MaterialAndColor highlightedVersion = new MaterialAndColor(materials[i].material, i);
        Color32 highlightedVersionColor = highlightedVersion.color;
        Color originalColor = new Color(highlightedVersionColor.r, highlightedVersionColor.g, highlightedVersionColor.b, highlightedVersionColor.a);
        highlightedVersion.color = originalColor * (4.5f - originalColor.maxColorComponent * 3);
        highlightMaterials[i] = highlightedVersion;
      }
      highlightSilhouetteMaterial = new MaterialAndColor(materialLibrary.highlightSilhouetteMaterial,
        new Color32(255, 255, 255, 255), HIGHLIGHT_SILHOUETTE_ID);
    }

    private static float r(int raw) {
      return ((raw >> 16) & 255) / 255.0f;
    }

    private static float g(int raw) {
      return ((raw >> 8) & 255) / 255.0f;
    }

    private static float b(int raw) {
      return (raw & 255) / 255.0f;
    }

    /// <summary>
    ///   Get a MaterialAndColor given a materialId.
    /// </summary>
    /// <param name="materialId">The material id.</param>
    /// <returns>A Material.</returns>
    public static MaterialAndColor GetMaterialAndColorById(int materialId) {
      // For tests, if we haven't been initialized, do it now.
      if (materials == null) {
        MaterialLibrary matLib = new MaterialLibrary();
        matLib.glassMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.glassSpecMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.gemMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.copyMaterial =new Material(Shader.Find(BASE_SHADER));
        matLib.subtractMaterial = new Material(Shader.Find(BASE_SHADER));
        Debug.Log("initializing mats in wrong place - this is an error if a test isn't running.");
        init(matLib);
      }
      return materials[materialId % materials.Length];
    }

    /// <summary>
    ///   Get a Material's color given a materialId.
    /// </summary>
    /// <param name="materialId">The material id.</param>
    /// <returns>A Color.</returns>
    public static Color GetMaterialColorById(int materialId) {
      if (materialId < rawColors.Length) {
        return new Color(r(rawColors[materialId]), g(rawColors[materialId]), b(rawColors[materialId]));
      }
      else {
        return new Color(1f, 1f, 1f, 1f);
      }
    }
    
    /// <summary>
    ///   Get a Material's color given a materialId.
    /// </summary>
    /// <param name="materialId">The material id.</param>
    /// <returns>A Color.</returns>
    public static Color32 GetMaterialColor32ById(int materialId) {
      if (materialId < rawColors.Length) {
        return color32s[materialId];
      } else {
        return new Color(1f, 1f, 1f, 1f);
      }
    }

    /// <summary>
    ///   Get a Material, with albedo already set, given a materialId.
    /// </summary>
    /// <param name="materialId">The material id.</param>
    /// <returns>A Material with .color set.</returns>
    public static Material GetMaterialWithAlbedoById(int materialId) {
      return materialsWithAlbedo[materialId];
    }

    /// <summary>
    ///   Get a preview version (low alpha) of a Material given a materialId.
    /// </summary>
    /// <param name="materialId">The material id.</param>
    /// <returns>A Material.</returns>
    public static MaterialAndColor GetPreviewOfMaterialById(int materialId) {
      // For tests, if we haven't been initialized, do it now.
      if (materials == null) {
        MaterialLibrary matLib = new MaterialLibrary();
        matLib.glassMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.glassSpecMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.gemMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.copyMaterial =new Material(Shader.Find(BASE_SHADER));
        matLib.subtractMaterial = new Material(Shader.Find(BASE_SHADER));
        Debug.Log("initializing mats in wrong place - this is an error if a test isn't running.");
        init(matLib);
      }
      return previewMaterials[materialId % materials.Length];
    }

    /// <summary>
    ///   Get the material we should show when a user is attempting to make an invalid reshape operation.
    /// </summary>
    /// <returns>A Material.</returns>
    public static MaterialAndColor GetReshaperErrorMaterial() {
      return previewMaterials[8]; // Red.
    }

    /// <summary>
    /// Gets a highlighted version of the material (brightened).
    /// </summary>
    /// <param name="materialId">The material id.</param>
    /// <returns>A material.</returns>
    public static MaterialAndColor GetHighlightMaterialById(int materialId) {
      // For tests, if we haven't been initialized, do it now.
      if (highlightMaterials == null) {
        MaterialLibrary matLib = new MaterialLibrary();
        matLib.glassMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.glassSpecMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.gemMaterial = new Material(Shader.Find(PREVIEW_SHADER));
        matLib.copyMaterial =new Material(Shader.Find(BASE_SHADER));
        matLib.subtractMaterial = new Material(Shader.Find(BASE_SHADER));
        Debug.Log("initializing mats in wrong place - this is an error if a test isn't running.");
        init(matLib);
      }
      return highlightMaterials[materialId % highlightMaterials.Length];
    }

    public static Material[] GetExportableMaterialList() {
      return materialsWithAlbedo;
    }

    public static MaterialAndColor getHighlightSilhouetteMaterial() {
      return highlightSilhouetteMaterial;
    }

    /// <summary>
    ///   Get the number of materials we support.
    /// </summary>
    /// <returns></returns>
    public static int GetNumMaterials() {
      return materials.Length;
    }

    /// <summary>
    ///   Returns true if the material is transparent.  This may be needed to render things correctly.
    /// </summary>
    public static bool IsMaterialTransparent(int materialId) {
      return materialId == GLASS_ID || materialId == GEM_ID;
    }

    public static MaterialType GetMaterialType(int id) {
      // Can't use a switch statement because GEM_ID and GLASS_ID aren't real constants.
      if (id == GEM_ID) {
        return MaterialType.GEM;
      }
      if (id == GLASS_ID) {
        return MaterialType.GLASS;
      }
      return MaterialType.PAPER;
    }
  }
}
