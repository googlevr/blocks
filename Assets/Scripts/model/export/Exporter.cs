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

using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.api_clients.assets_service_client;
using com.google.apps.peltzer.client.zandria;

namespace com.google.apps.peltzer.client.model.export {
  public class FormatSaveData {
    public FormatDataFile root;
    public List<FormatDataFile> resources;
    public Int64 triangleCount;
  }

  public class FormatDataFile {
    public String fileName;
    public String mimeType;
    public byte[] bytes;
    public String tag;
    public byte[] multipartBytes;
  }

  /// <summary>
  ///   A struct containing the serialized bytes of a model.
  /// </summary>
  public struct SaveData {
    public string filenameBase;
    public byte[] objFile;
    public int objPolyCount;
    public byte[] triangulatedObjFile;
    public int triangulatedObjPolyCount;
    public byte[] mtlFile;
    public FormatSaveData GLTFfiles;
    public byte[] fbxFile;
    public byte[] blocksFile;
    public byte[] thumbnailBytes;

    // Note: this is computed from the model at serialization time (as the union of all remix IDs in all meshes).
    public HashSet<string> remixIds;
  }

  /// <summary>
  ///   Handles exporting to the assets service.
  /// </summary>
  public class Exporter : MonoBehaviour {
    /// <summary>
    ///   Upload the serialized model as represented by SaveData to the assets service, opening a
    ///   window in the user's browser for them to complete publication, if 'publish' is true.
    /// </summary>
    public void UploadToVrAssetsService(SaveData saveData, bool publish, bool saveSelected) {
      AssetsServiceClient assetsServiceClient = gameObject.AddComponent<AssetsServiceClient>();
      AssetsServiceClientWork assetsServiceClientWork = gameObject.AddComponent<AssetsServiceClientWork>();
      assetsServiceClientWork.Setup(assetsServiceClient, PeltzerMain.Instance.AssetId,
        saveData.remixIds, saveData, publish, saveSelected);
      PeltzerMain.Instance.DoPolyMenuBackgroundWork(assetsServiceClientWork);
    }
  }
}
