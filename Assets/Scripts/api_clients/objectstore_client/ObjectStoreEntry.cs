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
using System.Collections;
using System;

/// <summary>
/// Represents an object in the object store. Serializable to/from json.
///
/// We use this to represent an entry that comes from the original "Object Store" (predecessor of Zandria) and
/// also to represent a Zandria entry. So, in that sense, this is agnostic to the actual service that it
/// was obtained from. This is why you might see in the code that ObjectStoreEntry (and related classes) are
/// used for Zandria loading code.
/// </summary>
namespace com.google.apps.peltzer.client.api_clients.objectstore_client {
  [Serializable]
  public class ObjectStoreEntry {
    public string id; // This is the 'asset id', we can't rename it due to a dependency in mogwai-objectstore, 
    public string localId;
    public string[] tags;
    public ObjectStoreObjectAssetsWrapper assets;
    public bool isPrivateAsset;
    public string thumbnail;
    public string author;
    public string title;
    public string description;
    public string webViewConfig;
    public DateTime createdDate;
    public string localThumbnailFile;
    public string localPeltzerFile;
    public Vector3 cameraForward;
  }
}