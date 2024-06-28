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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.render
{
    /// <summary>
    /// Singleton class for managing UX effects which may have lifecycles beyond the tool that generates them.
    /// </summary>
    class UXEffectManager
    {
        private static UXEffectManager instance;

        public enum UXEffectType { FACE_SNAP };

        public abstract class UXEffect
        {
            internal UXEffectType type;
            internal float startTime;
            public Material effectMaterial;
            protected WorldSpace worldSpace;

            /// <summary>
            /// Set up the effect - generate needed preview meshes, etc. This is called by UXEffectManager, so all params
            /// are general.  Params specific to an effect type should be supplied in the effect specific constructor by the
            /// tool which is triggering the effect.
            /// </summary>
            public void Initialize(MeshRepresentationCache cache, Material effectMaterial, WorldSpace worldSpace)
            {
                this.effectMaterial = effectMaterial;
                this.worldSpace = worldSpace;
            }

            public virtual void Initialize(MeshRepresentationCache cache, MaterialLibrary materialLibrary,
              WorldSpace worldSpace)
            { }

            /// <summary>
            /// Update the effect
            /// </summary>
            public virtual void Update()
            {
            }

            /// <summary>
            /// Render the effect
            /// </summary>
            public virtual void Render()
            {
            }

            /// <summary>
            /// Clean up the effect - kill preview meshes, for example.
            /// </summary>
            public virtual void Finish()
            {
            }
        }

        private MeshRepresentationCache componentCache;
        //TODO maintain dictionary of material per effect type.  Figure out a more organized way of managing these mats.
        private MaterialLibrary materialLibrary;

        private HashSet<UXEffect> effects;
        private HashSet<UXEffect> effectsToRemove;

        public WorldSpace worldSpace;


        private UXEffectManager(MeshRepresentationCache meshRepresentationCache, MaterialLibrary materialLibrary,
          WorldSpace worldSpace)
        {
            this.effects = new HashSet<UXEffect>();
            this.effectsToRemove = new HashSet<UXEffect>();
            this.componentCache = meshRepresentationCache;
            this.materialLibrary = materialLibrary;
            this.worldSpace = worldSpace;
            MeshInsertEffect.Setup(materialLibrary);
        }

        // Begin the given UXEffect.  Caller is still responsible for calling the effect specific update method
        // if it requires updated information.
        public void StartEffect(UXEffect effect)
        {
            effect.Initialize(componentCache, materialLibrary, worldSpace);
            effects.Add(effect);
            if (effectsToRemove.Contains(effect))
            {
                effectsToRemove.Remove(effect);
            }
        }

        public void EndEffect(UXEffect effect)
        {
            effectsToRemove.Add(effect);
        }

        public void Update()
        {
            foreach (UXEffect effect in effects)
            {
                effect.Update();
            }
            foreach (UXEffect effect in effectsToRemove)
            {
                if (effects.Contains(effect))
                {
                    effects.Remove(effect);
                }
            }
            effectsToRemove.Clear();
        }

        public void Render()
        {
            foreach (UXEffect effect in effects)
            {
                effect.Render();
            }
        }

        // Set up the singleton. This is called from PeltzerMain, so should always be set up.
        public static void Setup(MeshRepresentationCache meshRepresentationCache,
          MaterialLibrary materialLibrary,
          WorldSpace worldSpace)
        {
            instance = new UXEffectManager(meshRepresentationCache, materialLibrary, worldSpace);
            Shader.SetGlobalVector("_FXPointLightColorStrength", new Vector4(0f, 0f, 0f, 0f));
            Shader.SetGlobalVector("_FXPointLightPosition", new Vector4(0f, 0f, 0f, 1f));
        }

        // Gets the singleton UXEffectManager
        public static UXEffectManager GetEffectManager()
        {
            return instance;
        }

    }
}
