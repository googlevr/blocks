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
    /// This class defines the static method for RenderMeshes when a mesh needs to be 
    /// highlighted during a tutorial. It may be possible to consolidate this with the
    /// other Mesh*Style classes in the future.
    /// </summary>
    public class TutorialHighlightStyle
    {

        /// <summary>
        /// The period of the "glow" animation (the time taken by each repetition of it).
        /// Shorter values means a faster animation. Given in seconds.
        /// </summary>
        private const float GLOW_PERIOD = 1.0f;

        /// <summary>
        /// The maximum emission factor when glowing. More is brighter.
        /// </summary>
        private const float GLOW_MAX_EMISSION = 0.45f;

        private static readonly float HIGHLIGHT_ALPHA = 0.75f;

        /// <summary>
        /// String names of the shader properties to be changed.
        /// </summary>
        private const string EMISSIVE_AMT_NAME = "_EmissiveAmount";
        private const string EMISSIVE_COLOR_NAME = "_EmissiveColor";

        /// <summary>
        /// Base color of the glowing animations. (Greenish)
        /// </summary>
        private static readonly Color GLOW_COLOR = new Color(0.2f, 1.0f, 0.2f);

        public static Material material;
        private static Dictionary<MaterialRegistry.MaterialType, MaterialCycler> matDict;

        public static void Setup()
        {
            matDict = new Dictionary<MaterialRegistry.MaterialType, MaterialCycler>();
            MaterialCycler paperCycler = new MaterialCycler(
              MaterialRegistry.GetMaterialAndColorById(1).material, 5);
            matDict[MaterialRegistry.MaterialType.PAPER] = paperCycler;
        }

        // Renders mesh highlights.
        public static void RenderMeshes(Model model,
            HighlightUtils.TrackedHighlightSet<int> meshHighlights, WorldSpace worldSpace)
        {
            foreach (int key in meshHighlights.getKeysForStyle((int)MeshStyles.TUTORIAL_HIGHLIGHT))
            {
                if (!model.HasMesh(key)) continue;

                Dictionary<int, MeshGenContext> contexts =
                  model.meshRepresentationCache.FetchComponentsForMesh(
                    key, /* abortOnTooManyCacheMisses */ true);
                if (contexts == null)
                {
                    continue;
                }

                float factor = Mathf.PingPong(Time.time / GLOW_PERIOD, GLOW_MAX_EMISSION);

                foreach (KeyValuePair<int, MeshGenContext> pair in contexts)
                {
                    int matId = pair.Key;
                    MeshGenContext meshGenContext = pair.Value;

                    bool needToPopulateMesh;
                    Mesh curMesh = MeshCycler.GetTempMeshForMeshMatId(key, matId, out needToPopulateMesh);
                    Material curMaterial = matDict[MaterialRegistry.GetMaterialType(matId)].GetInstanceOfMaterial();
                    float emissiveAmount = factor;
                    curMaterial.SetFloat("_EmissiveAmount", factor);
                    float transparentMult =
                      curMaterial.GetFloat("_MultiplicitiveAlpha") * (1 - factor) + HIGHLIGHT_ALPHA * factor;
                    curMaterial.SetFloat("_MultiplicitiveAlpha", transparentMult);

                    curMaterial.renderQueue = 3000;
                    if (needToPopulateMesh)
                    {
                        curMesh.SetVertices(meshGenContext.verts);
                        curMesh.SetColors(meshGenContext.colors);
                        curMesh.SetTriangles(meshGenContext.triangles, /* subMesh */ 0);
                        curMesh.RecalculateNormals();
                    }

                    Graphics.DrawMesh(curMesh, worldSpace.modelToWorld, curMaterial, 0);
                }
            }
        }
    }
}