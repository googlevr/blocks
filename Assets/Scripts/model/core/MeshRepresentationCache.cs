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

using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    /// Cache of useful representations of Meshes.
    /// </summary>
    public class MeshRepresentationCache : MonoBehaviour
    {
        // Stagger out the cost of triangulation over many frames. The smaller this number is, the longer it'll take
        // operations to 'fade in'.
        private const int MAX_CACHE_MISSES_PER_FRAME = 20;
        private int cacheMissesThisFrame = 0;

        // Dictionary of mesh ID to the corresponding standard preview.
        private Dictionary<int, GameObject> normalTemplateForMeshId = new Dictionary<int, GameObject>();
        // Dictionary of mesh ID to the corresponding preview, by material.
        private Dictionary<MaterialAndColor, Dictionary<int, GameObject>> highlightedTemplatesForMeshIdsByMaterial =
          new Dictionary<MaterialAndColor, Dictionary<int, GameObject>>();
        // Dictionary of mesh ID to:
        //   Dictionaries of material IDs to the components (tris, verts) of that mesh with the given material.
        private Dictionary<int, Dictionary<int, MeshGenContext>> componentsByMaterialForMeshId =
          new Dictionary<int, Dictionary<int, MeshGenContext>>();

        // Dictionary of mesh ID to:
        //   Dictionaries of material IDs to the components (tris, verts) of that mesh with the given material.
        private Dictionary<int, Dictionary<int, MeshGenContext>> meshSpaceComponentsByMaterialForMeshId =
          new Dictionary<int, Dictionary<int, MeshGenContext>>();

        private Model model;
        private WorldSpace worldSpace;

        /// <summary>
        /// Sets up the cache.
        /// </summary>
        /// <param name="model">The model we are working on.</param>
        /// <param name="worldSpace">The world space.</param>
        public void Setup(Model model, WorldSpace worldSpace)
        {
            this.model = model;
            this.worldSpace = worldSpace;

            // When meshes get modified or deleted in the model, we have to invalidate the corresponding previews.
            model.OnMeshChanged += (MMesh mesh, bool materialsChanged, bool geometryChanged, bool facesOrVertsChanged) =>
            {
                // Only invalidate if the mesh mutated (but not if it was just transformed).
                if (materialsChanged || geometryChanged)
                {
                    InvalidatePreviews(mesh.id);
                }
                componentsByMaterialForMeshId.Remove(mesh.id);
                if (facesOrVertsChanged)
                {
                    meshSpaceComponentsByMaterialForMeshId.Remove(mesh.id);
                }
            };
            model.OnMeshDeleted += (MMesh mesh) =>
            {
                InvalidatePreviews(mesh.id);
                componentsByMaterialForMeshId.Remove(mesh.id);
                meshSpaceComponentsByMaterialForMeshId.Remove(mesh.id);
            };
        }

        public void Update()
        {
            // This is something of a trick: it is known that code depending on our cache miss logic 
            // only runs in LateUpdate, thus we reset this counter on Update.
            cacheMissesThisFrame = 0;
        }

        /// <summary>
        ///   Generates a preview for the given mesh. If we have a cached GameObject, we just clone it; if not,
        ///   we construct it from the MMesh (and cache it for next time).
        /// </summary>
        /// <param name="mesh">The mesh for which to create a preview.</param>
        /// <param name="highlightMaterial">
        ///   A material with which to colour the entire mesh, or null if the preview should use the same materials
        ///   as exist on the given mesh.
        /// </param>
        /// <returns>The preview GameObject.</returns>
        public GameObject GeneratePreview(MMesh mesh, MaterialAndColor highlightMaterial = null)
        {
            // Find the right cache.
            Dictionary<int, GameObject> dict;
            if (highlightMaterial == null)
            {
                dict = normalTemplateForMeshId;
            }
            else if (!highlightedTemplatesForMeshIdsByMaterial.TryGetValue(highlightMaterial, out dict))
            {
                dict = new Dictionary<int, GameObject>();
                highlightedTemplatesForMeshIdsByMaterial[highlightMaterial] = dict;
            }

            // See if we have a cache hit.
            GameObject template;
            bool isCacheHit = dict.ContainsKey(mesh.id);
            if (isCacheHit)
            {
                // Yes!
                template = dict[mesh.id];
            }
            else
            {
                // Cache miss. Disappointing. Create it now, using the highlight material if requested.
                template = new GameObject();
                Dictionary<int, MeshGenContext> components;
                bool componentsAreCached = componentsByMaterialForMeshId.TryGetValue(mesh.id, out components);
                if (componentsAreCached)
                {
                    // If we have cached components, just use those to make the GameObject.
                    MeshWithMaterialRenderer renderer = template.AddComponent<MeshWithMaterialRenderer>();
                    renderer.Init(worldSpace);
                    renderer.meshes = MeshHelper.ToMeshes(components, highlightMaterial, mesh.rotation, mesh.offset);
                    renderer.SetPositionModelSpace(mesh.offset);
                    renderer.SetOrientationModelSpace(mesh.rotation, /* smooth */ false);
                }
                else
                {
                    // Else, fetch the components of the mesh, make a GameObject, and cache the components if this mesh exists 
                    // in the model.
                    MMesh.AttachMeshToGameObject(worldSpace, template, mesh, out components,
                      /* updateOnly */ false, highlightMaterial);
                    if (model.HasMesh(mesh.id))
                    {
                        componentsByMaterialForMeshId.Add(mesh.id, components);
                    }
                }
            }

            // Templates are inactive (we don't want them to appear on the scene!).
            template.SetActive(false);

            // Only store the preview in the cache if the mesh is in the model (otherwise it could be a temporary
            // mesh that will later be discarded and we won't be warned about it).
            if (!isCacheHit && model.HasMesh(mesh.id))
            {
                // Cache it.
                dict[mesh.id] = template;
                isCacheHit = true;
            }

            // If we are not caching the template, we can return the template directly; if we are caching,
            // we have to clone it.
            GameObject result;
            if (isCacheHit)
            {
                // The template belongs to our cache, so we want to return a copy, not our precious cached template.
                result = GameObject.Instantiate(template);
                result.GetComponent<MeshWithMaterialRenderer>().SetupAsCopyOf(template.GetComponent<MeshWithMaterialRenderer>());
            }
            else
            {
                // We're not caching this one, so just return the template.
                result = template;
            }

            // Position/rotate the gameObject so that the preview has the correct position.
            result.GetComponent<MeshWithMaterialRenderer>().SetPositionModelSpace(mesh.offset);
            result.GetComponent<MeshWithMaterialRenderer>().SetOrientationModelSpace(Math3d.Normalize(mesh.rotation),
              /* smooth */ false);

            result.SetActive(true);
            return result;
        }

        /// <summary>
        ///   Get the components, keyed by material ID, of a given mesh, fetching from the cache if possible,
        ///   else triangulating on-the-fly and then adding to the cache and returning.
        ///   If the mesh does not exist in the model, nothing will be added to the cache.
        /// </summary>
        /// <param name="mesh">The mesh for which to fetch the previews.</param>
        /// <returns>
        ///   A dictionary keyed by material ID, with values containing the vertices and triangles of the triangulated
        ///   mesh.
        /// </returns>
        public Dictionary<int, MeshGenContext> FetchComponentsForMesh(MMesh mesh)
        {
            Dictionary<int, MeshGenContext> output;

            // Look for a hit in the cache, and return it if found.
            bool isCacheHit = componentsByMaterialForMeshId.TryGetValue(mesh.id, out output);
            if (isCacheHit)
            {
                return output;
            }

            // Else create the required information.
            output = MeshHelper.MeshComponentsFromMMesh(mesh, /* useModelSpace */ true);
            if (model.HasMesh(mesh.id))
            {
                // If a mesh with this ID exists in the model, we add an entry to the cache.
                componentsByMaterialForMeshId.Add(mesh.id, output);
            }
            return output;
        }

        /// <summary>
        ///   Get the components, keyed by material ID, of a given mesh that is expected to exist in the model,
        ///   fetching from the cache if possible, else triangulating on-the-fly and then adding to the cache 
        ///   and returning.
        /// </summary>
        /// <param name="meshId">The mesh id for which to fetch the previews.</param>
        /// <param name="abortOnCacheMiss">
        ///   If true, and the mesh is not found in the cache, and there have been too many 
        ///   cache misses already this frame, this method will return null.</param>
        /// <returns>
        ///   A dictionary keyed by material ID, with values containing the vertices and triangles of the triangulated
        ///   mesh.
        /// </returns>
        /// <exception cref="System.Exception">
        ///   If the given mesh id does not exist in the model.
        /// </exception>
        public Dictionary<int, MeshGenContext> FetchComponentsForMesh(int meshId, bool abortOnTooManyCacheMisses)
        {
            Dictionary<int, MeshGenContext> output;
            // Look for a hit in the cache, and return it if found.
            bool foundInCache = componentsByMaterialForMeshId.TryGetValue(meshId, out output);
            if (foundInCache)
            {
                return output;
            }
            else
            {
                cacheMissesThisFrame++;
                if (abortOnTooManyCacheMisses && cacheMissesThisFrame >= MAX_CACHE_MISSES_PER_FRAME)
                {
                    return null;
                }
            }

            AssertOrThrow.True(model.HasMesh(meshId),
              "Attempted to get components for a mesh that does not exist in the model");

            // Else create the required information.
            output = MeshHelper.MeshComponentsFromMMesh(model.GetMesh(meshId), /* useModelSpace */ true);
            if (model.HasMesh(meshId))
            {
                // If a mesh with this ID exists in the model, we add an entry to the cache.
                componentsByMaterialForMeshId.Add(meshId, output);
            }
            return output;
        }

        /// <summary>
        ///   Get the components, keyed by material ID, of a given mesh that is expected to exist in the model,
        ///   fetching from the cache if possible, else triangulating on-the-fly and then adding to the cache 
        ///   and returning.
        /// </summary>
        /// <param name="meshId">The mesh id for which to fetch the previews.</param>
        /// <param name="abortOnCacheMiss">
        ///   If true, and the mesh is not found in the cache, and there have been too many 
        ///   cache misses already this frame, this method will return null.</param>
        /// <returns>
        ///   A dictionary keyed by material ID, with values containing the vertices and triangles of the triangulated
        ///   mesh.
        /// </returns>
        /// <exception cref="System.Exception">
        ///   If the given mesh id does not exist in the model.
        /// </exception>
        public Dictionary<int, MeshGenContext> FetchMeshSpaceComponentsForMesh(int meshId, bool abortOnTooManyCacheMisses)
        {
            Dictionary<int, MeshGenContext> output;
            // Look for a hit in the cache, and return it if found.
            bool foundInCache = meshSpaceComponentsByMaterialForMeshId.TryGetValue(meshId, out output);
            if (foundInCache)
            {
                return output;
            }
            else
            {
                cacheMissesThisFrame++;
                if (abortOnTooManyCacheMisses && cacheMissesThisFrame >= MAX_CACHE_MISSES_PER_FRAME)
                {
                    return null;
                }
            }

            AssertOrThrow.True(model.HasMesh(meshId),
              "Attempted to get components for a mesh that does not exist in the model");

            // Else create the required information.
            output = MeshHelper.MeshComponentsFromMMesh(model.GetMesh(meshId), /* useModelSpace */ false);
            if (model.HasMesh(meshId))
            {
                // If a mesh with this ID exists in the model, we add an entry to the cache.
                meshSpaceComponentsByMaterialForMeshId.Add(meshId, output);
            }
            return output;
        }

        /// <summary>
        ///   Invalidates the preview templates for the given mesh ID.
        /// </summary>
        /// <param name="meshId">The ID of the mesh to invalidate.</param>
        public void InvalidatePreviews(int meshId)
        {
            if (normalTemplateForMeshId.ContainsKey(meshId))
            {
                GameObject.DestroyImmediate(normalTemplateForMeshId[meshId]);
                normalTemplateForMeshId.Remove(meshId);
            }
            foreach (Dictionary<int, GameObject> dict in highlightedTemplatesForMeshIdsByMaterial.Values)
            {
                if (dict.ContainsKey(meshId))
                {
                    GameObject.DestroyImmediate(dict[meshId]);
                    dict.Remove(meshId);
                }
            }
        }

        /// <summary>
        ///   Empties the entire cache, destroying any Preview GameObjects.
        /// </summary>
        public void Clear()
        {
            foreach (int meshId in normalTemplateForMeshId.Keys)
            {
                GameObject.DestroyImmediate(normalTemplateForMeshId[meshId]);
            }
            foreach (Dictionary<int, GameObject> dict in highlightedTemplatesForMeshIdsByMaterial.Values)
            {
                foreach (GameObject preview in dict.Values)
                {
                    GameObject.DestroyImmediate(preview);
                }
            }
            normalTemplateForMeshId.Clear();
            highlightedTemplatesForMeshIdsByMaterial.Clear();
            componentsByMaterialForMeshId.Clear();
            meshSpaceComponentsByMaterialForMeshId.Clear();
        }
    }
}
