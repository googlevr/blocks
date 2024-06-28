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
using UnityEngine;

namespace com.google.apps.peltzer.client.model.render
{
    class FaceSnapEffect : UXEffectManager.UXEffect
    {
        private const float DEFAULT_DURATION = 1.0f;

        private int snapTarget = -1;
        Vector3 basePreviewPosition;
        private List<Mesh> previewMeshes;

        private bool inSnapThreshhold = false;

        /// <summary>
        /// Constructs the effect, Initialize must still be called before the effect starts to take place.
        /// </summary>
        /// <param name="snapTarget">The MMesh id of the target mesh to play the shader on.</param>
        public FaceSnapEffect(int snapTarget)
        {
            this.snapTarget = snapTarget;
        }

        public override void Initialize(MeshRepresentationCache cache, MaterialLibrary materialLibrary,
          WorldSpace worldSpace)
        {
            base.Initialize(cache, materialLibrary.snapEffectMaterial, worldSpace);
            if (snapTarget != -1)
            {
                previewMeshes =
                  MeshHelper.ToUnityMeshes(cache, PeltzerMain.Instance.model.GetMesh(snapTarget));
            }
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
        }

        /// <summary>
        /// Updates the face snap shader with info about the current snap.
        /// </summary>
        public void UpdateSnapEffect(SnapInfo snapInfo)
        {
            Vector3 snapFaceWorld = worldSpace.ModelToWorld(snapInfo.snappingFacePosition);
            effectMaterial.SetVector("_ImpactPointWorld", worldSpace.ModelToWorld(snapInfo.snapPoint));
            effectMaterial.SetVector("_ImpactNormalWorld", worldSpace.ModelVectorToWorld(snapInfo.snapNormal));
            effectMaterial.SetVector("_ImpactObjectPosWorld", snapFaceWorld);
            Shader.SetGlobalVector("_FXPointLightColorStrength", new Vector4(0f, 0.8f, .4f, 1f));
            Shader.SetGlobalVector("_FXPointLightPosition",
              new Vector4(snapFaceWorld.x, snapFaceWorld.y, snapFaceWorld.z, 1f));
            if (!inSnapThreshhold && snapInfo.inSurfaceThreshhold)
            {
                inSnapThreshhold = true;
                effectMaterial.SetFloat("_EffectStartTime", Time.time);
            }
            else if (!snapInfo.inSurfaceThreshhold)
            {
                inSnapThreshhold = false;
            }
        }
    }
}
