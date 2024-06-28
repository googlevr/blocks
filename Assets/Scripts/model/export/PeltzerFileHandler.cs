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
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.serialization;

namespace com.google.apps.peltzer.client.model.export
{
    /// <summary>
    ///   Export & import logic for Peltzer files: go/peltzer-file-format
    /// </summary>
    public class PeltzerFileHandler
    {
        /// <summary>
        ///   Generates a Peltzer file from the given meshes.
        /// </summary>
        /// <param name="meshes">The meshes to serialize.</param>
        /// <param name="includeDisplayRotation">Whether or not to include the recommended model display rotation
        /// in save.</param>
        /// <param name="serializer">Optionally, the Serializer to use (used for the V3_POLY file format only).</param>
        /// <returns>The bytes of the Peltzer file</returns>
        public static byte[] PeltzerFileFromMeshes(ICollection<MMesh> meshes, bool includeDisplayRotation = false, PolySerializer serializer = null)
        {
            // TODO(bug): Get the username/date.
            Metadata metadata = new Metadata("temp_username", "temp_creation_date", Config.Instance.version);
            // TODO(bug): Get the real zoom factor.
            float zoomFactor = 1;

            // Find all materials used in the given meshes.
            HashSet<int> materialsUsed = new HashSet<int>();
            foreach (MMesh mesh in meshes)
            {
                foreach (Face face in mesh.GetFaces())
                {
                    materialsUsed.Add(face.properties.materialId);
                }
            }

            List<PeltzerMaterial> materials = new List<PeltzerMaterial>();
            foreach (int materialId in materialsUsed)
            {
                // TODO(bug): Deal with saving custom materials.
                materials.Add(new PeltzerMaterial(materialId));
            }

            PeltzerFile peltzerFile = new PeltzerFile(metadata, zoomFactor, materials, meshes.ToList());

            if (serializer == null)
            {
                serializer = new PolySerializer();
            }
            int estimate = peltzerFile.GetSerializedSizeEstimate();
            serializer.SetupForWriting(/* minInitialCapacity */ estimate);
            peltzerFile.Serialize(serializer, includeDisplayRotation);
            serializer.FinishWriting();
            byte[] result = serializer.ToByteArray();

            if (result.Length > estimate)
            {
                // This indicates a bug in the estimation logic. It's not a serious bug because the only consequence
                // is the reallocation of the buffer on save. But let's print an error just so we know something
                // is wrong:
                Debug.LogError("Actual serialized length was above estimate. Estimate " + estimate +
                  ", actual " + result.Length + ". No harm done, but loading code may be generating more " +
                  "garbage than necessary.");
            }

            return result;
        }

        /// <summary>
        /// Filters out all commands associated with ReferenceImages.
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        private static List<Command> FilterOutReferenceImageCommands(List<Command> commands)
        {
            List<Command> filtered = new List<Command>();
            foreach (Command command in commands)
            {
                if (command.GetType() == typeof(AddReferenceImageCommand) || command.GetType() == typeof(DeleteReferenceImageCommand))
                {
                    continue;
                }
                if (command.GetType() == typeof(CompositeCommand))
                {
                    List<Command> filteredCompositeCommand = FilterOutReferenceImageCommands((command as CompositeCommand).GetCommands());
                    if (filteredCompositeCommand.Count > 0)
                    {
                        filtered.Add(new CompositeCommand(filteredCompositeCommand));
                    }
                }
                else
                {
                    filtered.Add(command);
                }
            }
            return filtered;
        }

        /// <summary>
        ///   Deserializes a Peltzer file.
        /// </summary>
        /// <param name="peltzerFileBytes">The bytes of the Peltzer file.</param>
        /// <param name="peltzerFile">The decoded model.</param>
        /// <returns>True if the file wasn't corrupt.</returns>
        public static bool PeltzerFileFromBytes(byte[] peltzerFileBytes, out PeltzerFile peltzerFile)
        {
            if (peltzerFileBytes.Length == 0)
            {
                Debug.LogErrorFormat("No bytes loaded for peltzerFile.");
                peltzerFile = null;
                return false;
            }

            if (!LoadPolyFileFormat(peltzerFileBytes, out peltzerFile))
            {
                Debug.LogErrorFormat("Failed to load PeltzerFile. Invalid format.");
                peltzerFile = null;
                return false;
            }
            return true;
        }

        private static bool LoadPolyFileFormat(byte[] peltzerFileBytes, out PeltzerFile peltzerFile)
        {
            try
            {
                // Note: since we potentially support different file formats, at this point for all we know the
                // byte buffer might not even be in the right format, so we first check to see if it has a valid
                // header before proceeding. We could skip this check and proceed anyway and we would fail later,
                // but it would be more noisy and look like a fatal error when in fact it's just a case of "oops,
                // we chose the wrong file format, let's try another one".
                if (!PolySerializer.HasValidHeader(peltzerFileBytes, 0, peltzerFileBytes.Length))
                {
                    // Not in the Poly file format.
                    peltzerFile = null;
                    return false;
                }

                // Note: we don't mind creating a new serializer here instead of trying to re-use an existing one because
                // there is negligible overhead in creating a serializer for READING (since it doesn't allocate a buffer).
                // So it's pretty cheap to create a new one every time we load a file.
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForReading(peltzerFileBytes, 0, peltzerFileBytes.Length);
                peltzerFile = new PeltzerFile(serializer);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error while reading Poly file format: " + ex);
                peltzerFile = null;
                return false;
            }
        }
    }
}
