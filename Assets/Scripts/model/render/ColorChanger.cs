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

namespace com.google.apps.peltzer.client.model.render
{
    /// <summary>
    ///   Placed on a component with a renderer, this will update the renderer to use a given material from the
    ///   registry when ChangeMaterial is called.
    /// </summary>
    public class ColorChanger : MonoBehaviour
    {
        public Renderer rend;

        void Start()
        {
            rend = GetComponent<Renderer>();
        }

        public void ChangeMaterial(int materialId)
        {
            if (rend != null)
            {
                if (rend.material.HasProperty("_OverrideAmount"))
                { // TODO: Could add a default material to stuff instead.
                    float overrideAmount = rend.material.GetFloat("_OverrideAmount");
                    rend.material = new Material(MaterialRegistry.GetMaterialWithAlbedoById(materialId));
                    rend.material.SetFloat("_OverrideAmount", overrideAmount);
                }
                else
                {
                    rend.material = new Material(MaterialRegistry.GetMaterialWithAlbedoById(materialId));
                }
            }
        }
    }
}
