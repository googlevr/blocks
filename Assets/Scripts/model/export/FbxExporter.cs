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
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.export
{
    /// <summary>
    /// Class to handle exporting models to .fbx file format. This uses the BlocksFileExporter external
    /// DLL located in Plugins to call the FBX SDK c++ code.
    /// </summary>
    public static class FbxExporter
    {
        /// <summary>
        /// Print debug messages that originate from within the unmanaged DLL code.
        /// </summary>
        /// <param name="fp">Pointer to the debug function.</param>
        [DllImport("BlocksNativeLib")]
        public static extern void SetDebugFunction(IntPtr fp);

        /// <summary>
        /// Initializes the Fbx manager and scene.
        /// </summary>
        /// <param name="filePath">Path of the saved .fbx model.</param>
        [DllImport("BlocksNativeLib", EntryPoint = "StartExport")]
        public static extern void StartExport(string filePath);

        /// <summary>
        /// Responsible for calling FbxExporter.Export and saving the file, and performing necessary
        /// cleanup.
        /// </summary>
        [DllImport("BlocksNativeLib", EntryPoint = "FinishExport")]
        public static extern void FinishExport();

        /// <summary>
        /// Starts a new mesh node that holds a mesh and materials. If groupKey is nonzero, the mesh
        /// will be added to the relevant group node; otherwise it will be added to the scene's
        /// root node. It is not necessary to end a mesh node before starting a new one.
        /// </summary>
        /// <param name="meshId">ID of the mesh being exported.</param>
        /// <param name="groupKey">Group ID of the mesh being exported.</param>
        [DllImport("BlocksNativeLib", EntryPoint = "StartMesh")]
        public static extern void StartMesh(int meshId, int groupKey);

        /// <summary>
        /// Adds vertices to the current mesh.
        /// </summary>
        /// <param name="vertices">All vertices of the mesh.</param>
        /// <param name="numVerts">The number of vertices of the mesh.</param>
        [DllImport("BlocksNativeLib", EntryPoint = "AddMeshVertices")]
        public static extern void AddMeshVertices(Vector3[] vertices, int numVerts);

        /// <summary>
        /// Adds a new polygon to the current mesh.
        /// </summary>
        /// <param name="matId">Index of the material the face uses.</param>
        /// <param name="vertexIndices">Indices of the vertexes that make up this face (that map to the list
        /// of mesh vertices).</param>
        /// <param name="numVertices">Number of vertices belonging to this face.</param>
        /// <param name="normal">The normal of this face.</param>
        [DllImport("BlocksNativeLib", EntryPoint = "AddFace")]
        public static extern void AddFace(int matId, int[] vertexIndices, int numVertices, Vector3 normal);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MyDelegate(string str);

        private static Mutex fbxExportMutex;

        public static void Setup()
        {
            try
            {
                MyDelegate del = new MyDelegate(CallBackFunction);
                // Convert callback_delegate into a function pointer that can be
                // used in unmanaged code.
                IntPtr intptr_delegate =
                  Marshal.GetFunctionPointerForDelegate(del);
                SetDebugFunction(intptr_delegate);
                fbxExportMutex = new Mutex();
            }
            catch (Exception ex)
            {
                // Missing FBX DLL.
                Debug.LogError("Unable to load FBXExporter DLL: " + ex.ToString());
            }
        }

        /// <summary>
        /// Debug function to print information from the c++ side.
        /// </summary>
        static void CallBackFunction(string str)
        {
            Debug.Log("Callback " + str);
        }

        /// <summary>
        /// Exports the given meshes to .fbx and returns the bytes of the .fbx file.
        /// </summary>
        public static byte[] FbxFileFromMeshes(ICollection<MMesh> meshes, string fbxFileName)
        {
            fbxExportMutex.WaitOne();
            try
            {
                StartExport(fbxFileName);

                // Export all meshes.
                foreach (MMesh mesh in meshes)
                {
                    ExportComponent(mesh, mesh.groupId);
                }

                FinishExport();
                byte[] bytes = File.ReadAllBytes(fbxFileName);
                return bytes;
            }
            catch (Exception ex)
            {
                // When saving on the background thread, we can get out of sync exceptions - it's okay for save to fail in those
                // scenarios so we just log the exception and release the mutex.
                Debug.LogWarning("Error trying to export fbx: " + ex.Message + " at " + ex.StackTrace + " if this is from the"
                  + " background thread this is probably nothing to worry about.");
                return null;
            }
            finally
            {
                fbxExportMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Add a mesh node with vertices, normals, polygons, and materials.
        /// </summary>
        /// <param name="mesh">The mesh being exported to fbx.</param>
        /// <param name="groupId">The group id of the mesh, or GROUP_NONE value if the mesh is not in a group.</param>
        private static void ExportComponent(MMesh mesh, int groupId = MMesh.GROUP_NONE)
        {
            // We do not wish to duplicate vertices that are shared across faces. As such, we maintain a dictionary from
            // Vertex.id to the index in the vertices list this method updates. We only need to maintain this dictionary
            // per mesh, as Vertex.id is only shared within a single MMesh. Similar to what the .obj exporter does to 
            // maintain non-duplicate vertices, but zero indexed.
            List<Vector3> meshVertices = new List<Vector3>();
            List<Vector3> meshNormals = new List<Vector3>();
            Dictionary<int, int> vertexIdToIndex = new Dictionary<int, int>(mesh.vertexCount);

            // Start a new FBX MeshNode with its own vertices, polygons, and materials.
            StartMesh(mesh.id, groupId);

            foreach (Face face in mesh.GetFaces())
            {
                List<int> vertexIdsForFace = new List<int>(face.vertexIds.Count);
                for (int i = 0; i < face.vertexIds.Count; i++)
                {
                    int vertexId = face.vertexIds[i];
                    int vertexIndex;
                    Vector3 modelCoords = mesh.VertexPositionInModelCoords(face.vertexIds[i]);
                    if (!vertexIdToIndex.TryGetValue(vertexId, out vertexIndex))
                    {
                        meshVertices.Add(modelCoords);
                        vertexIndex = meshVertices.Count - 1;
                        vertexIdToIndex.Add(vertexId, vertexIndex);
                    }
                    vertexIdsForFace.Add(vertexIndex);
                }
                // Because Unity is a left handed coordinate system and .fbx is right handed we must reverse
                // the winding order of the vertices -- also to this effect, the .dll code negates the x
                // coordinate of each vertex when adding it to the mesh.
                vertexIdsForFace.Reverse();
                meshNormals.Add((mesh.rotation * face.normal).normalized);
                AddFace(face.properties.materialId, vertexIdsForFace.ToArray(), face.vertexIds.Count,
                  (mesh.rotation * face.normal).normalized);
            }

            AddMeshVertices(meshVertices.ToArray(), meshVertices.Count);
        }
    }
}
