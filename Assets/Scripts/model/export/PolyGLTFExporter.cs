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

using System;
using System.Collections.Generic;
using System.IO;
using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.export {
  /// <summary>
  /// Container class for functions that implement gltf export. Contains no state.
  /// </summary>
  public class PolyGLTFExporter {

    private static readonly string POLY_RESOURCE_PATH = "https://vr.google.com/shaders/w/";

    private static readonly string opaqueVSPath = POLY_RESOURCE_PATH + "vs.glsl";
    private static readonly string opaqueFSPath = POLY_RESOURCE_PATH + "fs.glsl";

    private static readonly string glassVSPath = POLY_RESOURCE_PATH + "glassVS.glsl";
    private static readonly string glassFSPath = POLY_RESOURCE_PATH + "glassFS.glsl";

    private static readonly string gemVSPath = POLY_RESOURCE_PATH + "gemVS.glsl";
    private static readonly string gemFSPath = POLY_RESOURCE_PATH + "gemFS.glsl";
    private static readonly string gemTexPath = POLY_RESOURCE_PATH + "GemRefractions.png";

    private static readonly string reflectionProbePath = "ReflectionProbe.png";
    private static readonly string reflectionProbeBlurPath = "ReflectionProbeBlur.png";


    /// <summary>
    /// Creates gltf save data for the meshes in the given remesher, and populates the raw bytes for the main 
    /// gltf file, the bin file, and all resources required to render the gltf (primarily shaders).
    /// 
    /// We use data from a remesher as that is already optimized for rendering, which is the intent of our glTF export.
    /// </summary>
    public static FormatSaveData GLTFFileFromRemesher(ReMesher remesher,
      string gltfFileName,
      string gltfBinFileName,
      MeshRepresentationCache meshRepresentationCache) {
      GlTFScriptableExporter gltfExporter = new GlTFScriptableExporter();
      int meshNum = 0;
      HashSet<GameObject> objectsToClean = new HashSet<GameObject>();
      HashSet<int> usedMaterialIds = new HashSet<int>();
      objectsToClean = new HashSet<GameObject>();
      GameObject rootNode = new GameObject();
      int triangleCount = 0;


      // The preset object is used to tell the gltfExporter which materials exist and which shaders they use.
      GlTF_Technique.States baseState = new GlTF_Technique.States();
      baseState.enable = new List<GlTF_Technique.Enable> {GlTF_Technique.Enable.DEPTH_TEST, GlTF_Technique.Enable.CULL_FACE};
      baseState.functions["cullFace"] = new GlTF_Technique.Value(1029);

      GlTF_Technique.States softAdditive = new GlTF_Technique.States();
      softAdditive.enable = new List<GlTF_Technique.Enable> {GlTF_Technique.Enable.DEPTH_TEST,
        GlTF_Technique.Enable.BLEND};
      // Blend array format: [srcRGB, dstRGB, srcAlpha, dstAlpha]
      // https://github.com/KhronosGroup/glTF/blob/master/specification/1.0/schema/technique.states.functions.schema.json#L40
      softAdditive.functions["blendFuncSeparate"] =
        new GlTF_Technique.Value(new Vector4(775.0f, 774.0f, 773.0f, 772.0f));  // Alpha, OneMinusAlpha blending.
      softAdditive.functions["depthMask"] = new GlTF_Technique.Value(true);  // No depth write.
      Preset matPresets = new Preset();
      matPresets.techniqueStates["mat" + MaterialRegistry.GLASS_ID] = softAdditive;
      matPresets.techniqueStates["mat" + MaterialRegistry.GEM_ID] = baseState;

      matPresets.techniqueExtras["mat" + MaterialRegistry.GLASS_ID] =
        "{\"gvrss\" : \"https://vr.google.com/shaders/w/gvrss/glass.json\"}";

      matPresets.techniqueExtras["mat" + MaterialRegistry.GEM_ID] =
        "{\"gvrss\" : \"https://vr.google.com/shaders/w/gvrss/gem.json\"}";

      // Set shaders for all materials.
      Material[] exportableMaterialList = MaterialRegistry.GetExportableMaterialList();
      for(int i = 0; i < MaterialRegistry.rawColors.Length; i++) {
        matPresets.SetShaders("mat" + i, opaqueVSPath, opaqueFSPath);
        matPresets.techniqueStates["mat" + i] = baseState;
        matPresets.techniqueExtras["mat" + i] =
          "{\"gvrss\" : \"https://vr.google.com/shaders/w/gvrss/paper.json\"}";
      }
      matPresets.SetShaders("mat" + MaterialRegistry.GLASS_ID, glassVSPath, glassFSPath);
      matPresets.SetShaders("mat" + MaterialRegistry.GEM_ID, gemVSPath, gemFSPath);

      GlTFScriptableExporter.TransformFilter filter = (Transform tr) => Matrix4x4.identity;

      gltfExporter.BeginExport(gltfFileName, matPresets, filter);
      gltfExporter.SetMetadata(Config.Instance.appName + " " + Config.Instance.version, "1.1", "Unknown");
      // Export each piece of mesh geometry
      foreach (ReMesher.MeshInfo polyMeshInfo in remesher.GetAllMeshInfos()) {
        meshNum = ExportMeshInfo(gltfExporter,
          polyMeshInfo,
          rootNode,
          ref usedMaterialIds,
          ref objectsToClean,
          ref triangleCount,
          meshNum);
      }

      GameObject lightingObject = ObjectFinder.ObjectById("ID_Lighting");
      Vector3 lightingPosition = Vector3.zero;
      Lighting lighting = null;
      if (lightingObject != null) {
        lightingPosition = lightingObject.transform.position;
        lighting = lightingObject.GetComponent<Lighting>();
      }
      // Then for each material that is actually used, set material properties.
      foreach(int i in usedMaterialIds) {
        gltfExporter.BaseName = "mat" + i;
        gltfExporter.AddMaterialWithDependencies();
        MaterialAndColor mat = MaterialRegistry.GetMaterialAndColorById(i);
        Color col = mat.color;
        // Make sure the material sets the shader uniform for color correctly since gltf isn't exporting vertex colors.
        gltfExporter.ExportShaderUniform("color", col);
        float roughness = mat.material.GetFloat("_Roughness");
        gltfExporter.ExportShaderUniform("roughness", roughness);
        float metallic = mat.material.GetFloat("_Metallic");
        gltfExporter.ExportShaderUniform("metallic", metallic);
        if (i == MaterialRegistry.GEM_ID) {
          //u_ will be automatically prepended to gem - so shader uniform should be 'u_gem'
          gltfExporter.ExportTexture("gem", gemTexPath);
        }
        //u_ will be automatically prepended - so shader uniform should be 'u_reflectionCube'
        //gltfExporter.ExportTexture("reflectionCube", "ReflectionProbe.png");
        //u_ will be automatically prepended - so shader uniform should be 'u_reflectionCubeBlur'
        //gltfExporter.ExportTexture("reflectionCubeBlur", "ReflectionProbeBlur.png");
        if (lighting != null) {
          gltfExporter.ExportShaderUniform("light0Pos", lightingPosition);
          Vector3 light0Color = new Vector3(lighting.lightColor.r, lighting.lightColor.g, lighting.lightColor.b) *
                                lighting.lightStrength;
          gltfExporter.ExportShaderUniform("light0Color", light0Color * 0.8f);
          gltfExporter.ExportShaderUniform("light1Pos", -lightingPosition);
          Vector3 light1Color =
            new Vector3(lighting.fillLightColor.r, lighting.fillLightColor.g, lighting.fillLightColor.b) *
            lighting.fillLightStrength;
          gltfExporter.ExportShaderUniform("light1Color", light1Color * 0.8f);
        }
      }
      gltfExporter.EndExport();

      // Read the bytes of our exported files into FormatSaveData
      FormatSaveData gltfSaveData = new FormatSaveData();
      gltfSaveData.root = new FormatDataFile();
      gltfSaveData.root.fileName = ExportUtils.GLTF_FILENAME;
      gltfSaveData.root.bytes = File.ReadAllBytes(gltfFileName);
      gltfSaveData.root.mimeType = "model/gltf+json";
      gltfSaveData.root.tag = "gltf";

      // Export the triangle count so it can be uploaded to the asset service
      gltfSaveData.triangleCount = triangleCount;

      // Export all required resource files.
      // This capacity is just counted by hand for now.
      //  1. gltf bin file
      //  2. default pixel shader
      //  3. default fragment shader
      // This will be of variable length in the future.

      // Export the bytes of the bin file for gltf.
      gltfSaveData.resources = new List<FormatDataFile>(3);
      FormatDataFile binFile = new FormatDataFile();
      binFile.fileName = ExportUtils.GLTF_BIN_FILENAME;
      binFile.bytes = File.ReadAllBytes(gltfBinFileName);
      binFile.mimeType = "application/octet-stream";
      binFile.tag = "bin";
      gltfSaveData.resources.Add(binFile);
      // Clean up game objects
      foreach(GameObject obj in objectsToClean)
      {
        GameObject.Destroy(obj);
      }

      return gltfSaveData;
    }

    private static FormatDataFile addTextResource(String name, String path) {
      TextAsset shaderAsset = Resources.Load<TextAsset>(path);
      FormatDataFile shaderFile = new FormatDataFile();
      shaderFile.fileName = name;
      shaderFile.bytes = shaderAsset.bytes;
      shaderFile.mimeType = "text/plain";
      shaderFile.tag = name;
      return shaderFile;
    }

    private static FormatDataFile addTextureResource(String name, String path) {
      Texture2D shaderAsset = Resources.Load<Texture2D>(path);
      FormatDataFile shaderFile = new FormatDataFile();
      shaderFile.fileName = name;
      shaderFile.bytes = shaderAsset.EncodeToPNG();
      shaderFile.mimeType = "image/png";
      shaderFile.tag = name;
      return shaderFile;
    }

    private static int ExportMeshInfo(GlTFScriptableExporter gltfExporter,
      ReMesher.MeshInfo meshInfo,
      GameObject rootNode,
      ref HashSet<int> usedMaterialIds,
      ref HashSet<GameObject> objectsToClean,
      ref int triangleCount,
      int meshNum) {

      Mesh exportableMesh = ReMesher.MeshInfo.BuildExportableMeshFromMeshInfo(meshInfo);
      int matId = meshInfo.materialAndColor.matId;

      // The TiltBrush gltfExporter doesn't seem to work properly for me without every mesh in its own transform
      // (even if they're all identity transforms) - so for now we workaround this with a dummy transform.
      GameObject tempObject = new GameObject();
      tempObject.transform.SetParent(rootNode.transform);
      String matPrefix = "PolyPaper";
      if (matId == MaterialRegistry.GLASS_ID) {
        matPrefix = "PolyGlass";
      } else if (matId == MaterialRegistry.GEM_ID) {
        matPrefix = "PolyGem";
      }
      tempObject.name = "MeshObject" + meshInfo.GetHashCode() + "-" + matPrefix + matId;
      objectsToClean.Add(tempObject);
      usedMaterialIds.Add(matId);

      // context.triangles is a list of the vertices in the triangles. Each triple
      // of elements is one triangle, so the total number of triangles in this mesh
      // is one third the size of the triangles list.
      triangleCount += meshInfo.triangles.Count / 3;

      // The actual geometry is exported in the .bin file for efficiency
      // This sets up the structure of the vertex data in the .bin file
      GlTF_VertexLayout layout = new GlTF_VertexLayout();
      layout.hasColors = true;
      layout.hasNormals = true;
      layout.uv0 = GlTF_VertexLayout.UvElementCount.None;
      layout.uv1 = GlTF_VertexLayout.UvElementCount.None;
      layout.uv2 = GlTF_VertexLayout.UvElementCount.None;
      layout.uv3 = GlTF_VertexLayout.UvElementCount.None;
      layout.hasTangents = false;
      layout.hasVertexIds = false;
      // Give it a unique name
      exportableMesh.name = "m" + meshNum + "-" + matPrefix + matId;
      // Exporter needs us to set the active material name before we export the mesh
      gltfExporter.BaseName = "mat" + matId;
      gltfExporter.ExportSimpleMesh(exportableMesh, tempObject.transform, layout);
      meshNum++;

      return meshNum;
    }
  }
}
