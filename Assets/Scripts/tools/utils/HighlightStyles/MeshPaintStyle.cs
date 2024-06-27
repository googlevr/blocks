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

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    /// This class exists primarily to hold the static method for RenderMeshes when PAINT is set. It may be possible to
    /// consolidate this with the other Mesh*Style classes in the future.
    /// </summary>
    public class MeshPaintStyle
    {
        private static readonly float HIGHLIGHT_EMISSIVE_AMOUNT = 0.7f;
        private static readonly float HIGHLIGHT_ALPHA = 0.75f;
        public static Material material;
        private static Dictionary<MaterialRegistry.MaterialType, MaterialCycler> matDict;

        public static void Setup()
        {
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
        }

        // Renders mesh highlights.
        public static void RenderMeshes(Model model,
            HighlightUtils.TrackedHighlightSet<int> meshHighlights, WorldSpace worldSpace)
        {
            foreach (int key in meshHighlights.getKeysForStyle((int)MeshStyles.MESH_PAINT))
            {
                if (!model.HasMesh(key)) continue;

                Dictionary<int, MeshGenContext> contexts =
                  model.meshRepresentationCache.FetchComponentsForMesh(
                    key, /* abortOnTooManyCacheMisses */ true);
                if (contexts == null)
                {
                    continue;
                }

                float animPct = meshHighlights.GetAnimPct(key);
                MaterialAndColor currentMaterialAndColor = MaterialRegistry.GetMaterialAndColorById(
                  PeltzerMain.Instance.peltzerController.currentMaterial);
                Material currentMaterial = animPct >= .99f ? matDict[MaterialRegistry.GetMaterialType(currentMaterialAndColor.matId)].GetFixedMaterial() :
                  matDict[MaterialRegistry.GetMaterialType(currentMaterialAndColor.matId)].GetInstanceOfMaterial();
                Color currentColor = currentMaterialAndColor.color;

                float emissiveAmount = HIGHLIGHT_EMISSIVE_AMOUNT * animPct;
                float transparentMult = currentMaterial.GetFloat("_MultiplicitiveAlpha") * (1 - animPct) + HIGHLIGHT_ALPHA * animPct;
                currentMaterial.SetFloat("_EmissiveAmount", emissiveAmount);
                currentMaterial.SetFloat("_MultiplicitiveAlpha", transparentMult);
                currentMaterial.renderQueue = 3000;

                foreach (KeyValuePair<int, MeshGenContext> pair in contexts)
                {
                    int matId = pair.Key;
                    MeshGenContext meshGenContext = pair.Value;

                    bool needToPopulateMesh;
                    Mesh curMesh = MeshCycler.GetTempMeshForMeshMatId(key, matId, out needToPopulateMesh);

                    if (needToPopulateMesh)
                    {
                        curMesh.SetVertices(meshGenContext.verts);
                        int count = meshGenContext.verts.Count;
                        List<Color32> colors = new List<Color32>(count);
                        for (int i = 0; i < count; i++)
                        {
                            colors.Add(currentColor);
                        }
                        curMesh.SetColors(colors);
                        curMesh.SetTriangles(meshGenContext.triangles, /* subMesh */ 0);
                        curMesh.RecalculateNormals();
                    }
                    Graphics.DrawMesh(curMesh, worldSpace.modelToWorld, currentMaterial, 0);
                }
            }
        }
    }
}