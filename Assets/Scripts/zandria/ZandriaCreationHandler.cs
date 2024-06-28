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
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.api_clients.objectstore_client;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.tools;

namespace com.google.apps.peltzer.client.zandria
{
    public class ZandriaCreationHandler : MonoBehaviour
    {
        // This is in Unity units where 1.0 = 1m.
        private const float MENU_TILE_SIZE = 0.05f;

        public List<MMesh> originalMeshes { get; private set; }
        public List<MMesh> previewMeshes { get; private set; }
        public List<MMesh> detailSizedMeshes { get; set; }
        public PeltzerFile peltzerFile { get; private set; }
        public string creatorName { get; private set; }
        public string creationDate { get; private set; }
        public string creationTitle { get; private set; }
        public string creationAssetId { get; private set; }
        public string creationLocalId { get; private set; }
        public bool isActiveOnMenu { get; set; }
        public bool hasPublishedRotation { get; set; }
        public float recommendedRotation { get; private set; }

        public void Setup(ObjectStoreEntry objectStoreEntry)
        {
            previewMeshes = new List<MMesh>();
            originalMeshes = new List<MMesh>();
            detailSizedMeshes = new List<MMesh>();

            creatorName = objectStoreEntry.author;
            creationDate = objectStoreEntry.createdDate.ToString();
            creationTitle = objectStoreEntry.title;
            creationAssetId = objectStoreEntry.id;
            creationLocalId = objectStoreEntry.localId;
            // If the model was published and the camera forward is available, rotate the model about the
            // y-axis so it faces the camera forward when positioned on the Poly menu.
            if (objectStoreEntry.cameraForward != null && objectStoreEntry.cameraForward != Vector3.zero)
            {
                Vector3 cameraForward = objectStoreEntry.cameraForward;
                Quaternion publishedRotationQuaternion = Quaternion.LookRotation(cameraForward);
                recommendedRotation = publishedRotationQuaternion.eulerAngles.y;
                hasPublishedRotation = true;
            }
            else
            {
                hasPublishedRotation = false;
                recommendedRotation = 0f;
            }
        }

        /// <summary>
        ///   Takes the raw data for a PeltzerFile and converts it to MMeshes scaled to fit on the menu.
        /// </summary>
        /// <param name="rawFileData">The raw file data.</param>
        /// <param name="callback">Callback function on successful retrieval.</param>
        /// <returns>Whether the file was valid.</returns>
        public bool GetMMeshesFromPeltzerFile(byte[] rawFileData, System.Action<List<MMesh>, float> callback)
        {
            PeltzerFile peltzerFile;
            bool validFile = PeltzerFileHandler.PeltzerFileFromBytes(rawFileData, out peltzerFile);

            if (validFile)
            {
                // Keep a reference to the peltzerFile so that it can be loaded into the model.
                this.peltzerFile = peltzerFile;

                // Keep a reference to the original meshes so that they can be loaded into a scene at full scale.
                originalMeshes = peltzerFile.meshes;
                // Keep a reference to the meshes that have been scaled to be previews on the PolyMenu so they can
                // be loaded into the scene at this size without re-scaling.
                previewMeshes = Scaler.ScaleMeshes(originalMeshes, MENU_TILE_SIZE);

                // If there was not a published rotation, recommend the rotation the model was saved with (if available).
                if (!hasPublishedRotation)
                {
                    recommendedRotation = peltzerFile.metadata.recommendedRotation;
                }

                // Returns the scaled MMeshes with a recommended display rotation.
                callback(previewMeshes, recommendedRotation);

                return true;
            }
            else
            {
                Debug.LogError("Invalid file with asset id " + creationAssetId + " and local id " + creationLocalId);
            }

            return false;
        }
    }
}