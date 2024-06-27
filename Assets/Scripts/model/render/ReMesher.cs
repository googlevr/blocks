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
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.render
{

    /// <summary>
    ///   Generates, maintains and renders a collection of Unity Meshes based on a collection of MMeshes.
    ///
    ///   Multiple MMeshes can be coalesced into a single Unity Mesh.  MMeshes that have multiple
    ///   different materials will be divided between multiple Unity Meshes.  (Meaning that you end up with
    ///   a many-to-many relationship between MMeshes and Unity Meshes.)
    ///
    ///   Every time a MMesh is added, we first retrieve its triangles from the cache (potentially calculating them on a
    ///   cache miss.  We then add those triangles to a Unity Mesh of the same material.  When that mesh is "full" (in 
    ///   this case, has MAX_VERTS_PER_MESH vertices), we create a new Unity Mesh for that material.  Each time we add 
    ///   triangles to a Unity Mesh, we need to regenerate that mesh.
    /// </summary>
    /// TODO(bug): Support meshes with more than 65k verts.
    public class ReMesher
    {
        // Maximum number of vertices we'd put into a coalesced Mesh.
        public const int MAX_VERTS_PER_MESH = 20000;

        // Maximum number of vertices we allow for any mesh.
        private const int MAX_VERTS_PER_MMESH = 20000;

        // Maximum number of MMeshes in a single MeshInfo.
        private const int MAX_MMESH_PER_MESHINFO = 128;

        // A number of static Vector2 arrays, each of which is filled with the same constant Vector2 - ie, the first array
        // is all Vector2(0,0), the second is Vector2(1, 0) and so on.  This allows us to use cheap Array.Copy() calls to
        // fill an array with the same value.
        private static List<Vector2[]> BufferCaches;

        /// <summary>
        ///   Info about a Unity Mesh that will be drawn at render time.  MeshInfo batches a number of meshes together, and
        ///   renders them in a single draw call, passing an array of transform matrix uniforms to position them correctly.
        /// </summary>
        public class MeshInfo
        {
            // The material we draw this mesh with.
            public MaterialAndColor materialAndColor;

            // All MMeshes that contribute to this mesh.
            private HashSet<int> mmeshes = new HashSet<int>();

            // It's cheaper to render extra data in the form of degenerate triangles than it is to resize our vertex array,
            // so all of these are preallocated.
            public Vector3[] verts = new Vector3[MAX_VERTS_PER_MESH];
            public Color32[] colors = new Color32[MAX_VERTS_PER_MESH];
            public Vector3[] normals = new Vector3[MAX_VERTS_PER_MESH];
            // List here because the number of triangles isn't predictable based on the number of vertices, so we may need
            // to eventually resize.
            public List<int> triangles = new List<int>(3 * MAX_VERTS_PER_MESH);

            // Buffer that holds the transform index each vertex should use.
            public Vector2[] transformIndexBuffer = new Vector2[MAX_VERTS_PER_MESH];

            // The number of vertices tracked by this MeshInfo - defaults to 2, because we start with 2 vertices due to a
            // hack to avoid the mesh being view frustum culled.
            public int numVerts = 2;

            // The number of vertices that are waiting to be added to number of vertices tracked by this MeshInfo when
            // Regenerate is called.
            public int numPendingVerts;

            // The Unity Mesh, itself.
            public Mesh mesh = new Mesh();

            // Whether anything has been deleted from this meshInfo, causing it to need regeneration.
            public bool needsRegeneration = false;

            // Each mesh needs a transformation matrix when we render.  In order to batch meshes with different transforms,
            // we pass the transforms as an array of uniforms, and supply each vertex with the index of the transform it
            // should use.
            public Dictionary<int, int> mmeshToTransformIndex = new Dictionary<int, int>();

            // One available per MAX_MMESH_PER_MESHINFO
            private Queue<int> availableTransformIndices = new Queue<int>();

            // Transform uniforms.
            public Matrix4x4[] xformMats = new Matrix4x4[MAX_MMESH_PER_MESHINFO];

            // Map from MMesh id to the set of MeshGenContexts contained in this MeshInfo that are associated with it.
            private Dictionary<int, HashSet<MeshGenContext>> meshGenContexts
              = new Dictionary<int, HashSet<MeshGenContext>>();

            /// <summary>
            /// Gets the mesh ids of all meshes in this MeshInfo
            /// </summary>
            public HashSet<int> GetMeshIds()
            {
                return mmeshes;
            }

            /// <summary>
            /// Gets the number of meshes in this MeshInfo
            /// </summary>
            public int GetNumMeshes()
            {
                return mmeshes.Count;
            }

            /// <summary>
            /// Returns whether this MeshInfo contains the specified mesh
            /// </summary>
            public bool HasMesh(int meshId)
            {
                return mmeshes.Contains(meshId);
            }

            /// <summary>
            ///   Builds a Unity Mesh from the given MeshInfo that has correct (in world-space) vertex positions, and does 
            ///   not have any hacks or optimizations that ReMesher relies upon, for export.
            /// </summary>
            public static Mesh BuildExportableMeshFromMeshInfo(MeshInfo meshInfo)
            {
                Mesh exportableMesh = new Mesh();

                // We need to work around the 2 extra verts we hack into the MeshInfo mesh to work around frustrum culling.
                // These verts exist in the first two indices of the verts array.
                int numVertsInMesh = meshInfo.numVerts - 2;
                int indexOfFirstVert = 2;

                // Generate a new list of vertices in their correct world-space positions.
                Vector3[] newLocs = new Vector3[numVertsInMesh];
                for (int i = 0; i < numVertsInMesh; i++)
                {
                    int transformIndex = (int)meshInfo.transformIndexBuffer[i + indexOfFirstVert].x;
                    newLocs[i] = meshInfo.xformMats[transformIndex].MultiplyPoint(meshInfo.verts[i + indexOfFirstVert]);
                }
                exportableMesh.vertices = newLocs;

                // Copy Colors.
                Color32[] copiedColors = new Color32[numVertsInMesh];
                Array.Copy(meshInfo.colors, indexOfFirstVert, copiedColors, 0, numVertsInMesh);
                exportableMesh.colors32 = copiedColors;

                // Copy Triangles.
                int[] copiedTriangles = new int[meshInfo.triangles.Count];
                for (int i = 0; i < meshInfo.triangles.Count; i++)
                {
                    copiedTriangles[i] = meshInfo.triangles[i] - indexOfFirstVert;
                }
                exportableMesh.triangles = copiedTriangles;

                // Copy Normals
                // TODO(bug): Get rid of this recalculation and just rely on the normals when they are fixed.
                exportableMesh.RecalculateNormals();
                // Vector3[] copiedNormals = new Vector3[numVertsInMesh];
                // Array.Copy(meshInfo.normals, indexOfFirstVert, copiedNormals, 0, numVertsInMesh);
                // exportableMesh.normals = copiedNormals;

                return exportableMesh;
            }

            /// <summary>
            /// Adds the supplied MeshGenContext to this mesh under the supplied id.  Multiple contexts with the same mesh id
            /// can be supplied (Different mats that use the same shader to render)
            /// </summary>
            public void AddMesh(int meshId, MeshGenContext context)
            {
                if (mmeshes.Add(meshId))
                {
                    // Since we're only allocating one index per mmesh and have indices up to MAX_MMESH_PER_MESHINFO we're
                    // guaranteed to have enough.
                    int availableTransformIndex = availableTransformIndices.Dequeue();
                    mmeshToTransformIndex.Add(meshId, availableTransformIndex);
                }
                HashSet<MeshGenContext> contextSet;
                // Append the triangles, add to the set of MeshInfos to regenerate and other bookeeping.
                if (!meshGenContexts.TryGetValue(meshId, out contextSet))
                {
                    contextSet = new HashSet<MeshGenContext>();
                    meshGenContexts[meshId] = contextSet;
                }
                contextSet.Add(context);
                if (!needsRegeneration)
                {
                    AddContext(meshId, context);
                }
                else
                {
                    numPendingVerts += context.verts.Count;
                }
            }

            /// <summary>
            /// Removes a mesh from this MeshInfo.
            /// </summary>
            public void RemoveMesh(int meshId)
            {
                mmeshes.Remove(meshId);
                availableTransformIndices.Enqueue(mmeshToTransformIndex[meshId]);
                mmeshToTransformIndex.Remove(meshId);
                meshGenContexts.Remove(meshId);
                needsRegeneration = true;
            }

            /// <summary>
            ///   Add vertices and triangles to a MeshInfo.  This method assumes we've ensured there is "room" in the
            ///   MeshInfo for the new components.
            /// </summary>
            /// <param name="meshId">The id of the mesh whose context we are adding.</param>
            /// <param name="source">The MehGenContext whose data we're adding to this MeshInfo.</param>
            public void AddContext(int meshId, MeshGenContext source)
            {
                int transformIndex = mmeshToTransformIndex[meshId];
                Array.Copy(source.verts.ToArray(), 0, verts, numVerts, source.verts.Count);
                Array.Copy(source.normals.ToArray(), 0, normals, numVerts, source.verts.Count);
                Array.Copy(source.colors.ToArray(), 0, colors, numVerts, source.verts.Count);
                Array.Copy(BufferCaches[transformIndex], 0, transformIndexBuffer, numVerts, source.verts.Count);
                int triCount = source.triangles.Count;
                for (int i = 0; i < triCount; i++)
                {
                    triangles.Add(source.triangles[i] + numVerts);
                }
                numVerts += source.verts.Count;
            }


            /// <summary>
            /// Updates the array of transform mats for the meshes this info renders.
            /// </summary>
            public void UpdateTransforms(Model model)
            {
                foreach (int meshId in mmeshes)
                {
                    xformMats[mmeshToTransformIndex[meshId]] = model.GetMesh(meshId).GetJitteredTransform();
                }
            }

            /// <summary>
            /// Sets the transform mat array as a shader uniform for the supplied material.
            /// </summary>
            public void SetTransforms(Material mat)
            {
                mat.SetMatrixArray("_RemesherMeshTransforms", xformMats);
            }

            /// <summary>
            /// Regenerates the buffers used for constructing meshes.  We need to do this when meshes are removed from the
            /// info, as doing that invalidates our triangle indices (because they are calculated based on offset).
            /// </summary>
            public void Regenerate()
            {
                // Every vert we don't care about will form a degenerate triangle.  Keeps our first two verts.
                triangles.Clear();
                Array.Clear(verts, 2, verts.Length - 2);
                numVerts = 2;

                foreach (int meshId in mmeshes)
                {
                    foreach (MeshGenContext context in meshGenContexts[meshId])
                    {
                        AddContext(meshId, context);
                    }
                }
                needsRegeneration = false;
                numPendingVerts = 0;
            }

            public MeshInfo()
            {
                // This is a really undesirable hack.  Since we're passing an additional transform matrix into the shader, it breaks
                // Unity's view frustum culling, and the meshinfo will disappear at various angles.  By adding these two extreme
                // vertices, it causes the mesh to never be culled because it has an enormous bounding box.
                // While setting the bounds on the Unity mesh directly should in theory work, in practice it seems not to.
                verts[0] = new Vector3(999999f, 999999f, 999999f);
                verts[1] = new Vector3(-999999f, -999999f, -999999f);
                numVerts = 2;

                for (int i = 0; i < MAX_MMESH_PER_MESHINFO; i++)
                {
                    xformMats[i] = Matrix4x4.identity;
                    availableTransformIndices.Enqueue(i);
                }
            }
        }

        // For each MMesh, the set of MeshInfos that MMesh contributes triangles to.
        private Dictionary<int, HashSet<MeshInfo>> meshInfosByMesh = new Dictionary<int, HashSet<MeshInfo>>();

        // All MeshInfos that we need to render.
        private HashSet<MeshInfo> allMeshInfos = new HashSet<MeshInfo>();

        // IDs of meshes that we have yet to add-to/remove- from the ReMesher, and haven't gotten around to doing yet.
        // We only add when ReMesher.Flush() is called so that a bunch of those operations can be batched.
        private HashSet<int> meshesPendingAdd = new HashSet<int>();
        private HashSet<int> meshesPendingRemove = new HashSet<int>();
        private HashSet<MeshInfo> meshInfosPendingRegeneration = new HashSet<MeshInfo>();

        // Meshinfos should not be modified outside of remesher.
        // This exists to enable exporting coalesced meshes.
        public HashSet<MeshInfo> GetAllMeshInfos()
        {
            Flush();
            return allMeshInfos;
        }

        /// <summary>
        /// Initializes a set of buffers that are used to efficiently add multiples of the same value to a list.
        /// Each of these lists contains an array of identical Vector2 values, which lets us efficiently use Array.Copy
        /// to set a large range to the same value.
        /// </summary>
        public static void InitBufferCaches()
        {
            BufferCaches = new List<Vector2[]>(MAX_MMESH_PER_MESHINFO);
            for (int i = 0; i < MAX_MMESH_PER_MESHINFO; i++)
            {
                BufferCaches.Add(new Vector2[MAX_VERTS_PER_MESH]);
                Vector2 val = new Vector2(i, 0f);
                for (int j = 0; j < MAX_VERTS_PER_MESH; j++)
                {
                    BufferCaches[i][j] = val;
                }
            }
        }

        // All MeshInfos that have room for more triangles to be added, by material.
        private Dictionary<Material, List<MeshInfo>> meshInfosByMaterial = new Dictionary<Material, List<MeshInfo>>();

        public void Clear()
        {
            meshInfosByMaterial.Clear();
            allMeshInfos.Clear();
            meshesPendingAdd.Clear();
            meshesPendingRemove.Clear();
            meshInfosPendingRegeneration.Clear();
        }

        /// <summary>
        ///   Add a mesh to be rendered.  A mesh with the same id must exist in the model.
        /// </summary>
        /// <param name="mmesh">The mesh.</param>
        public void AddMesh(MMesh mmesh)
        {
            // Generate or fetch the triangles, etc for the mesh.
            // TODO(bug): This only works because Model.cs happens to update the model before calling the ReMesher.
            //                   We shoud make that more robust.

            // Generate the Unity meshes for this mesh.
            // TODO(bug): We should cache these Meshes for MMeshes too, if possible.
            meshesPendingAdd.Add(mmesh.id);
        }

        /// <summary>
        /// Flushes any pending deferred operations on the ReMesher.
        /// </summary>
        public void Flush()
        {
            ActuallyRemoveMeshes();
            GenerateMeshesForMMeshes();

            // For all the MeshInfos that have had triangles modified, regenerate their Unity Meshes.
            foreach (MeshInfo meshInfo in meshInfosPendingRegeneration)
            {
                RegenerateMesh(meshInfo);
            }

            meshInfosPendingRegeneration.Clear();
        }

        /// <summary>
        ///   Marks a mesh to be removed from being rendered. 
        ///   Actual removal will happen in batch the next time Flush is called.
        /// </summary>
        /// <param name="meshId">The mesh id.</param>
        /// <exception cref="System.Exception">
        ///   If the given mesh is not actually being rendered.
        /// </exception>
        public void RemoveMesh(int meshId)
        {
            meshesPendingAdd.Remove(meshId);
            meshesPendingRemove.Add(meshId);
        }

        /// <summary>
        ///   Removes the given meshes from ReMesher immediately.
        ///   Note that this will update meshesPendingAdd with the IDs of meshes who contributed to the same
        ///   MeshInfo as a mesh being removed.
        /// </summary>
        private void ActuallyRemoveMeshes()
        {
            // The meshinfos affected by removing this mesh.
            foreach (int meshId in meshesPendingRemove)
            {
                HashSet<MeshInfo> affectedMeshInfos;
                if (!meshInfosByMesh.TryGetValue(meshId, out affectedMeshInfos))
                {
                    continue; // Nothing to do here.
                }

                // Recursively find the transitive closure of all MeshInfos we need to re-add after
                // removing this mesh.
                foreach (MeshInfo info in affectedMeshInfos)
                {
                    info.RemoveMesh(meshId);
                    meshInfosPendingRegeneration.Add(info);
                }

                // Remove this mesh's entry from the list of mesh infos per mesh.
                meshInfosByMesh.Remove(meshId);
            }

            meshesPendingRemove.Clear();
        }

        /// <summary>
        ///   For a list of MMeshes, add their triangles to "unfull" MeshInfos.  When any of those
        ///   MeshInfos becomes full, create a new MeshInfo.  Once we've added all the triangles to MeshInfos,
        ///   we need to regenerate all of the Unity Meshes for those MeshInfos.
        /// </summary>
        /// <param name="mmeshIds"></param>
        private void GenerateMeshesForMMeshes()
        {
            Model model = PeltzerMain.Instance.model;
            HashSet<int> meshesStillPendingAdd = new HashSet<int>();

            foreach (int meshId in meshesPendingAdd)
            {
                // Since this method is called lazily on Flush(), we may have an out of date mesh ID that no longer
                // exists in the model. In that case, skip it.
                if (!model.HasMesh(meshId)) continue;

                Dictionary<int, MeshGenContext> components =
                  model.meshRepresentationCache.FetchMeshSpaceComponentsForMesh(meshId, /* abortOnTooManyCacheMisses */ false);
                if (components == null)
                {
                    meshesStillPendingAdd.Add(meshId);
                    continue;
                }

                HashSet<MeshInfo> meshInfos = new HashSet<MeshInfo>();
                foreach (KeyValuePair<int, MeshGenContext> pair in components)
                {
                    // Doing the Assert within an if statement to prevent the string concatenation from occurring unless the
                    // condition has failed.  The concatenation was expensive enough to show up in profiling for large models.
                    if (pair.Value.verts.Count >= MAX_VERTS_PER_MMESH)
                    {
                        AssertOrThrow.True(pair.Value.verts.Count < MAX_VERTS_PER_MMESH,
                          "MMesh has too many vertices ( " + pair.Value.verts.Count + " vs a max of " + MAX_VERTS_PER_MMESH);
                    }
                    // Find or create an unfull MeshInfo for the given material
                    MeshInfo infoForMaterial = GetInfoForMaterialAndVertCount(pair.Key, pair.Value.verts.Count);

                    infoForMaterial.AddMesh(meshId, pair.Value);

                    meshInfosPendingRegeneration.Add(infoForMaterial);
                    meshInfos.Add(infoForMaterial);
                }
                meshInfosByMesh[meshId] = meshInfos;
            }

            meshesPendingAdd = meshesStillPendingAdd;
        }

        /// <summary>
        /// Gets a MeshInfo with sufficient space for the given material, or creates a new one if none currently exists.
        /// </summary>
        private MeshInfo GetInfoForMaterialAndVertCount(int materialId, int spaceNeeded)
        {
            List<MeshInfo> infosForMaterial;
            MaterialAndColor materialAndColor = MaterialRegistry.GetMaterialAndColorById(materialId);
            meshInfosByMaterial.TryGetValue(materialAndColor.material, out infosForMaterial);
            if (infosForMaterial == null)
            {
                infosForMaterial = new List<MeshInfo>();
                meshInfosByMaterial.Add(materialAndColor.material, infosForMaterial);
            }
            // Just return the first info with room.
            for (int i = 0; i < infosForMaterial.Count; i++)
            {
                MeshInfo curInfo = infosForMaterial[i];
                if (curInfo.numVerts + curInfo.numPendingVerts + spaceNeeded < MAX_VERTS_PER_MESH
                  && curInfo.GetNumMeshes() + 1 < MAX_MMESH_PER_MESHINFO)
                {
                    return curInfo;
                }
            }
            // And create one if no viable option was found.
            MeshInfo newInfoForMaterial = new MeshInfo();
            // Cloned to make sure it has its own matrix transform uniform, otherwise other things rendering using the
            // same material will have the wrong transforms.
            newInfoForMaterial.materialAndColor = materialAndColor.Clone();
            allMeshInfos.Add(newInfoForMaterial);
            meshInfosByMaterial[materialAndColor.material].Add(newInfoForMaterial);
            return newInfoForMaterial;
        }


        /// <summary>
        ///   Generate the Unity Mesh for a given MeshInfo.
        /// </summary>
        private void RegenerateMesh(MeshInfo meshInfo)
        {
            if (meshInfo.needsRegeneration)
            {
                meshInfo.Regenerate();
            }
            meshInfo.mesh.Clear();
            meshInfo.mesh.vertices = meshInfo.verts;
            meshInfo.mesh.SetTriangles(meshInfo.triangles, /* Submesh */ 0);
            meshInfo.mesh.colors32 = meshInfo.colors;
            meshInfo.mesh.normals = meshInfo.normals;
            meshInfo.mesh.uv2 = meshInfo.transformIndexBuffer;
        }

        /// <summary>
        ///   Render the meshes.
        /// </summary>
        public void Render(Model model)
        {
            // Flush to apply any outstanding changes, if necessary.
            Flush();

            WorldSpace worldSpace = PeltzerMain.Instance.worldSpace;

            foreach (MeshInfo meshInfo in allMeshInfos)
            {
                meshInfo.UpdateTransforms(model);
                meshInfo.SetTransforms(meshInfo.materialAndColor.material);
                Graphics.DrawMesh(meshInfo.mesh, worldSpace.modelToWorld, meshInfo.materialAndColor.material, /* Layer */ 0);
                if (meshInfo.materialAndColor.material2 != null)
                {
                    meshInfo.SetTransforms(meshInfo.materialAndColor.material2);
                    Graphics.DrawMesh(meshInfo.mesh, worldSpace.modelToWorld, meshInfo.materialAndColor.material2, /* Layer */ 0);
                }
            }
        }

        /// <summary>
        ///   Update transformations for all meshInfos in the given model.
        /// </summary>
        public void UpdateTransforms(Model model)
        {
            foreach (MeshInfo meshInfo in allMeshInfos)
            {
                meshInfo.UpdateTransforms(model);
            }
        }

        // Visible for testing
        public bool HasMesh(int meshId)
        {
            return meshInfosByMesh.ContainsKey(meshId);
        }

        // Visible for testing.  Walk all MeshInfos and count how many depend on a given mesh.
        public int MeshInMeshInfosCount(int meshId)
        {
            int count = 0;
            foreach (MeshInfo meshInfo in allMeshInfos)
            {
                if (meshInfo.HasMesh(meshId))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
