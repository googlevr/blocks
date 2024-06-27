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
using com.google.apps.peltzer.client.serialization;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.export
{
    class ExportUtils
    {
        public static readonly string OBJ_FILENAME = "model.obj";
        public static readonly string TRIANGULATED_OBJ_FILENAME = "model-triangulated.obj";
        public static readonly string MTL_FILENAME = "materials.mtl";
        public static readonly string THUMBNAIL_FILENAME = "thumbnail.png";
        public static readonly string BLOCKS_FILENAME = "model.blocks";
        public static readonly string GLTF_FILENAME = "model.gltf";
        public static readonly string GLTF_BIN_FILENAME = "model.bin";
        public static readonly string FBX_FILENAME = "model.fbx";

        /// <summary>
        ///   Serialize the model to bytes in a range of formats.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="meshes">The meshes composing the content we would like to serialize and save.</param>    
        /// <param name="saveGltf">If true, will serialize a .gltf file</param>
        /// <param name="saveFbx">If true, will serialize a .fbx file</param>
        /// <param name="saveTriangulatedObj">If true, will serialize a triangulated .obj file</param>
        /// <param name="includeDisplayRotation">
        ///   Whether or not to include the recommended model display rotation in save.
        /// </param>
        /// <param name="serializer">A serializer to perform the work for .blocks files.</param>
        public static SaveData SerializeModel(Model model, ICollection<MMesh> meshes,
          bool saveGltf, bool saveFbx, bool saveTriangulatedObj, bool includeDisplayRotation, PolySerializer serializer, bool saveSelected)
        {

            // Serialize data.
            SaveData saveData = new SaveData();
            HashSet<int> materials = new HashSet<int>();
            ObjFileExporter.ObjFileFromMeshes(meshes, MTL_FILENAME, model.meshRepresentationCache, ref materials,
              /*triangulated*/ false, out saveData.objFile, out saveData.objPolyCount);
            if (saveTriangulatedObj)
            {
                ObjFileExporter.ObjFileFromMeshes(meshes, MTL_FILENAME, model.meshRepresentationCache, ref materials,
                /*triangulated*/ true, out saveData.triangulatedObjFile, out saveData.triangulatedObjPolyCount);
            }

            saveData.mtlFile = ObjFileExporter.MtlFileFromSet(materials);
            if (saveGltf)
            {
                ReMesher remesher;
                if (saveSelected)
                {
                    remesher = new ReMesher();
                    foreach (MMesh mesh in meshes)
                    {
                        remesher.AddMesh(mesh);
                    }
                    remesher.Flush();
                    remesher.UpdateTransforms(model);
                }
                else
                {
                    remesher = model.GetReMesher();
                }
                saveData.GLTFfiles = PolyGLTFExporter.GLTFFileFromRemesher(remesher,
                  Path.Combine(PeltzerMain.Instance.modelsPath, GLTF_FILENAME),
                  Path.Combine(PeltzerMain.Instance.modelsPath, GLTF_BIN_FILENAME),
                  model.meshRepresentationCache);
            }
            saveData.fbxFile = FbxExporter.FbxFileFromMeshes(meshes, Path.Combine(PeltzerMain.Instance.modelsPath,
                FBX_FILENAME));
            saveData.blocksFile = PeltzerFileHandler.PeltzerFileFromMeshes(meshes, includeDisplayRotation, serializer);
            saveData.remixIds = model.GetAllRemixIds(meshes);

            return saveData;
        }

        /// <summary>
        ///   Writes SaveData to the user's local disk.
        /// </summary>
        /// <param name="saveData">A struct containing all the binary data for the save.</param>
        /// <param name="directory">Where to save the data.</param>
        public static bool SaveLocally(SaveData saveData, string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bool allSucceeded = true;

            allSucceeded &= SaveBytesLocally(Path.Combine(directory, OBJ_FILENAME), saveData.objFile);
            if (saveData.triangulatedObjFile != null)
            {
                allSucceeded &= SaveBytesLocally(Path.Combine(directory, TRIANGULATED_OBJ_FILENAME),
                  saveData.triangulatedObjFile);
            }
            allSucceeded &= SaveBytesLocally(Path.Combine(directory, MTL_FILENAME), saveData.mtlFile);
            allSucceeded &= SaveBytesLocally(Path.Combine(directory, BLOCKS_FILENAME), saveData.blocksFile);
            if (saveData.GLTFfiles != null)
            {
                allSucceeded &= SaveBytesLocally(Path.Combine(directory, GLTF_FILENAME), saveData.GLTFfiles.root.bytes);
                foreach (FormatDataFile file in saveData.GLTFfiles.resources)
                {
                    allSucceeded &= SaveBytesLocally(Path.Combine(directory, file.fileName), file.bytes);
                }
            }
            if (saveData.thumbnailBytes != null)
            {
                allSucceeded &= SaveBytesLocally(Path.Combine(directory, THUMBNAIL_FILENAME), saveData.thumbnailBytes);
            }
            if (saveData.fbxFile != null)
            {
                allSucceeded &= SaveBytesLocally(Path.Combine(directory, FBX_FILENAME), saveData.fbxFile);
            }

            return allSucceeded;
        }

        /// <summary>
        ///   Utility to save given bytes to a given path.
        /// </summary>
        /// <param name="path">Where to save the data</param>
        /// <param name="bytes">The data</param>
        private static bool SaveBytesLocally(string path, byte[] bytes)
        {
            try
            {
                // Open file for writing.
                System.IO.FileStream fileStream =
                   new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                // Writes a block of bytes to this stream using data from a byte array.
                fileStream.Write(bytes, 0, bytes.Length);

                // Close file stream
                fileStream.Close();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarningFormat("{0}\n{1}", exception.Message, exception.StackTrace);
                return false;
            }
        }
    }
}
