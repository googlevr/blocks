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

using System.Collections.Generic;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tools.utils {
  /// <summary>
  /// This class exists primarily to hold the static method for RenderMeshes when SELECT is set. It may be possible to
  /// consolidate this with the other Mesh*Style classes in the future.
  /// </summary>
  public class MeshSelectStyle {
    private static readonly float HIGHLIGHT_EMISSIVE_AMOUNT = 0.7f;
    private static readonly float HIGHLIGHT_ALPHA = 0.75f;
    public static Material material;
    public static Material silhouetteMaterial;
    private static Dictionary<MaterialRegistry.MaterialType, MaterialCycler> matDict;
    private static MaterialCycler silhouetteInstancer;
    private static Material gemInstance;

    public static void Setup() {
      matDict = new Dictionary<MaterialRegistry.MaterialType, MaterialCycler>();
      MaterialCycler gemCycler = new MaterialCycler(
        MaterialRegistry.GetMaterialAndColorById(MaterialRegistry.GEM_ID).material, 5);
      matDict[MaterialRegistry.MaterialType.GEM] = gemCycler;
      MaterialCycler glassCycler = new MaterialCycler(
        MaterialRegistry.GetMaterialAndColorById(MaterialRegistry.GLASS_ID).material, 5);
      matDict[MaterialRegistry.MaterialType.GLASS] = glassCycler;
      MaterialCycler paperCycler = new MaterialCycler(
        MaterialRegistry.GetMaterialAndColorById(1).material, 5);
      matDict[MaterialRegistry.MaterialType.PAPER] = paperCycler;
      silhouetteInstancer = new MaterialCycler(silhouetteMaterial, 5);
    }

    // Renders mesh highlights.
    // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
    // mesh geometry frame to frame) - 37281287
    public static void RenderMeshes(Model model,
        HighlightUtils.TrackedHighlightSet<int> meshHighlights,
        WorldSpace worldSpace) {
      // Get world position of selector position.
      Vector4 selectorWorldPosition = PeltzerMain.Instance.worldSpace
        .ModelToWorld(PeltzerMain.Instance.GetSelector().selectorPosition);
      foreach (int key in meshHighlights.getKeysForStyle((int) MeshStyles.MESH_SELECT)) {
        if (!model.HasMesh(key)) continue;

        Dictionary<int, MeshGenContext> contexts =
          model.meshRepresentationCache.FetchComponentsForMesh(
            key, /* abortOnTooManyCacheMisses */ true);
        if (contexts == null) {
          continue;
        }

        float animPct = meshHighlights.GetAnimPct(key);
        float emissiveAmount = HIGHLIGHT_EMISSIVE_AMOUNT * animPct;

        foreach (KeyValuePair<int, MeshGenContext> pair in contexts) {
          int matId = pair.Key;
          MeshGenContext meshGenContext = pair.Value;

          bool needToPopulateMesh;
          Mesh curMesh = MeshCycler.GetTempMeshForMeshMatId(key, matId, out needToPopulateMesh);
          Material curMaterial = animPct >= .99f ? matDict[MaterialRegistry.GetMaterialType(matId)].GetFixedMaterial() :
            matDict[MaterialRegistry.GetMaterialType(matId)].GetInstanceOfMaterial();
          curMaterial.SetFloat("_EmissiveAmount", emissiveAmount);
          float transparentMult = curMaterial.GetFloat("_MultiplicitiveAlpha") * (1 - animPct) + HIGHLIGHT_ALPHA * animPct;
          curMaterial.SetFloat("_MultiplicitiveAlpha", transparentMult);
          Material curSilMat = animPct >= .99f ? silhouetteInstancer.GetFixedMaterial()
            : silhouetteInstancer.GetInstanceOfMaterial();
          curSilMat.SetFloat("_EmissiveAmount", emissiveAmount);
          curSilMat.SetFloat("_MultiplicitiveAlpha", transparentMult);
          // Set w component to indicate active vs inactive.
          selectorWorldPosition.w = 0f;
          if (PeltzerMain.Instance.GetSelector().isMultiSelecting) {
            selectorWorldPosition.w = 1.0f;
          }
          curMaterial.SetVector("_SelectorPosition", selectorWorldPosition);
          curMaterial.renderQueue = 3000;
          if (needToPopulateMesh) {
            curMesh.SetVertices(meshGenContext.verts);
            curMesh.SetColors(meshGenContext.colors);
            curMesh.SetTriangles(meshGenContext.triangles, /* subMesh */ 0);
            curMesh.RecalculateNormals();
          }

          Graphics.DrawMesh(curMesh, worldSpace.modelToWorld, curMaterial, 0);
          Graphics.DrawMesh(curMesh, worldSpace.modelToWorld, curSilMat, 0);
        }
      }
    }
  }
}