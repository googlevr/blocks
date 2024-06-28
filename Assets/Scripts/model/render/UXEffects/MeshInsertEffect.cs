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
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.render
{
    class MeshInsertEffect : UXEffectManager.UXEffect
    {
        // The base length of the duration, to be scaled by the size of the mesh being sized.
        // Not marked as const, as it is editable from the debug console.
        public static float DURATION_BASE = 0.6f;

        // How long the animation will play for.
        private float duration = DURATION_BASE;

        private static MaterialCycler insertCycler;

        public static void Setup(MaterialLibrary library)
        {
            insertCycler = new MaterialCycler(library.meshInsertEffectMaterial, 10);
        }


        private int snapTarget = -1;
        private MMesh insertionMesh;
        Vector3 basePreviewPosition;
        private List<Mesh> previewMeshes;

        private bool inSnapThreshhold = false;
        private Model model;
        private float startTime = 0f;
        private float pctDone = 0f;

        /// <summary>
        /// Constructs the effect, Initialize must still be called before the effect starts to take place.
        /// </summary>
        /// <param name="snapTarget">The MMesh id of the target mesh to play the shader on.</param>
        public MeshInsertEffect(MMesh insertionMesh, Model model)
        {
            this.insertionMesh = insertionMesh;
            this.model = model;

        }

        public override void Initialize(MeshRepresentationCache cache, MaterialLibrary materialLibrary,
          WorldSpace worldSpace)
        {

            base.Initialize(cache, insertCycler.GetInstanceOfMaterial(), worldSpace);
            if (insertionMesh != null)
            {
                previewMeshes =
                  MeshHelper.ToUnityMeshes(cache, PeltzerMain.Instance.model.GetMesh(insertionMesh.id));
            }
            else
            {
                UXEffectManager.GetEffectManager().EndEffect(this);
                return;
            }
            startTime = Time.time;

            Vector3 minBoundsWorld = worldSpace.ModelToWorld(insertionMesh.bounds.min);
            Vector3 maxBoundsWorld = worldSpace.ModelToWorld(insertionMesh.bounds.max);
            effectMaterial.SetVector("_MeshShaderBounds", new Vector4(minBoundsWorld.y, maxBoundsWorld.y, 0f, 0f));
            // Adjust for constant velocity so that effect works for big and small meshes.
            duration = DURATION_BASE * Mathf.Sqrt(maxBoundsWorld.y - minBoundsWorld.y);
        }

        public override void Render()
        {
            foreach (Mesh subMesh in previewMeshes)
            {
                Graphics.DrawMesh(subMesh,
                  worldSpace.modelToWorld,
                  effectMaterial,
                  0); // Layer
            }
        }

        public override void Finish()
        {
            Shader.SetGlobalVector("_FXPointLightColorStrength", new Vector4(0f, 0f, 0f, 0f));
            Shader.SetGlobalVector("_FXPointLightPosition", new Vector4(0f, 0f, 0f, 1f));
            UXEffectManager.GetEffectManager().EndEffect(this);
            model.AddToRemesher(insertionMesh.id);
        }

        /// <summary>
        /// Updates the face snap shader with info about the current snap.
        /// </summary>
        public override void Update()
        {
            pctDone = Mathf.Min(1f, (Time.time - startTime) / duration);
            // Insertion doesn't get an effect light, so turn it off.
            Shader.SetGlobalVector("_FXPointLightColorStrength", new Vector4(0f, 0f, 0f, 0f));
            Shader.SetGlobalVector("_FXPointLightPosition", new Vector4(0f, 0f, 0f, 1f));
            effectMaterial.SetFloat("_AnimPct", pctDone);
            if (pctDone >= 1f)
            {
                Finish();
            }
        }
    }
}
