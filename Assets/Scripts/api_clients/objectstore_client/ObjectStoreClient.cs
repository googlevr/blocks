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

using ICSharpCode.SharpZipLibUnityPort.Zip;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.import;
using com.google.apps.peltzer.client.model.export;
using System.Text;
using System;
using com.google.apps.peltzer.client.entitlement;

namespace com.google.apps.peltzer.client.api_clients.objectstore_client {
  internal class CreateMeshWork : BackgroundWork {
    private Dictionary<string, Material> materials;
    private string objString;
    private bool successfullyReadMesh;
    private System.Action<Dictionary<Material, Mesh>> callback;

    private Dictionary<Material, List<MeshVerticesAndTriangles>> meshes;

    internal CreateMeshWork(Dictionary<string, Material> materials, string obj,
      System.Action<Dictionary<Material, Mesh>> callback) {
      this.materials = materials;
      this.objString = obj;
      this.callback = callback;
    }

    public void BackgroundWork() {
      successfullyReadMesh = ObjImporter.ImportMeshes(objString, materials, out meshes);
    }

    public void PostWork() {
      if (successfullyReadMesh) {
        Dictionary<Material, Mesh> finalizedMeshes = new Dictionary<Material, Mesh>();
        foreach (KeyValuePair<Material, List<MeshVerticesAndTriangles>> materialAndMesh in meshes) {
          foreach (MeshVerticesAndTriangles mesh in materialAndMesh.Value) {
            finalizedMeshes.Add(new Material(materialAndMesh.Key), mesh.ToMesh());
          }
        }
        callback(finalizedMeshes);
      }
    }
  }

  public class ObjectStoreClient {
    public static readonly string OBJECT_STORE_BASE_URL = "[Removed]";

    public ObjectStoreClient() { }

    // Create a url string for making web requests.
    public StringBuilder GetObjectStoreURL(StringBuilder tag) {
      StringBuilder url = new StringBuilder(OBJECT_STORE_BASE_URL).Append("/s");

      if (tag != null) {
        url.Append("?q=" + tag);
      }

      return url;
    }

    // Makes a query to the ObjectStore for objects with a given tag.
    public IEnumerator GetObjectStoreListingsForTag(string tag, System.Action<ObjectStoreSearchResult> callback) {
      string url = OBJECT_STORE_BASE_URL + "/s";
      if (tag != null) {
        url += "?q=" + tag;
      }
      return GetObjectStoreListings(GetNewGetRequest(new StringBuilder(url), "text/json"), callback);
    }

    // Makes a query to the ObjectStore for objects made by a user.
    public IEnumerator GetObjectStoreListingsForUser(string userId, System.Action<ObjectStoreSearchResult> callback) {
      string url = OBJECT_STORE_BASE_URL + "/s";
      if (userId != null) {
        url += "?q=userId=" + userId;
      }
      return GetObjectStoreListings(GetNewGetRequest(new StringBuilder(url), "text/json"), callback);
    }

    // Makes a query to the ObjectStore for a given UnityWeb search request.
    public IEnumerator GetObjectStoreListings(UnityWebRequest searchRequest,
      System.Action<ObjectStoreSearchResult> callback) {
      using (searchRequest) {
        yield return searchRequest.Send();
        if (!searchRequest.isNetworkError) {
          callback(JsonUtility.FromJson<ObjectStoreSearchResult>(searchRequest.downloadHandler.text));
        }
      }
    }

    // Given the entry metadata for an object queries the actual object from the ObjectStore.
    public IEnumerator GetObject(ObjectStoreEntry entry, System.Action<Dictionary<Material, Mesh>> callback) {
      // First, check and see if there's a zip file, because it will load a lot faster.
      if (entry.assets.object_package != null
          && !string.IsNullOrEmpty(entry.assets.object_package.rootUrl)
          && !string.IsNullOrEmpty(entry.assets.object_package.baseFile)) {
        StringBuilder zipUrl = new StringBuilder(
            OBJECT_STORE_BASE_URL).Append(entry.assets.object_package.rootUrl)
            .Append(entry.assets.object_package.baseFile);
        using (UnityWebRequest fetchRequest = GetNewGetRequest(zipUrl, "text/plain")) {
          yield return fetchRequest.Send();
          if (!fetchRequest.isNetworkError) {
            PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CreateMeshFromStreamWork(fetchRequest.downloadHandler, callback));
          }
        }
      } else {
        StringBuilder url =
          new StringBuilder(OBJECT_STORE_BASE_URL).Append(entry.assets.obj.rootUrl).Append(entry.assets.obj.baseFile);
        using (UnityWebRequest fetchRequest = GetNewGetRequest(url, "text/plain")) {
          yield return fetchRequest.Send();
          if (!fetchRequest.isNetworkError) {
          } else {
            if (entry.assets.obj.supportingFiles != null && entry.assets.obj.supportingFiles.Length > 0) {
              using (UnityWebRequest materialFetch =
                    GetNewGetRequest(new StringBuilder(OBJECT_STORE_BASE_URL).Append(entry.assets.obj.rootUrl).Append(
                    entry.assets.obj.supportingFiles[0]), "text/plain")) {
                yield return materialFetch.Send();
                if (!materialFetch.isNetworkError) {
                  PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CreateMeshWork(ObjImporter.ImportMaterials(
                      materialFetch.downloadHandler.text), fetchRequest.downloadHandler.text, callback));
                }
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Downloads the raw file data for an object. This method was originally designed for the Object Store
    /// (predecessor of Zandria) but is actually agnostic to the underlying service, as it just pulls data
    /// from a URL, so we use it from ZandriaCreationsManager.
    /// </summary>
    /// <param name="entry">The entry for which to load the raw data.</param>
    /// <param name="callback">The callback to call when loading is complete.</param>
    public static void GetRawFileData(ObjectStoreEntry entry, System.Action<byte[]> callback) {
      if (entry.localPeltzerFile != null) {
        callback(File.ReadAllBytes(entry.localPeltzerFile));
      } else if (entry.assets.peltzer_package != null
                && !string.IsNullOrEmpty(entry.assets.peltzer_package.rootUrl)
                && !string.IsNullOrEmpty(entry.assets.peltzer_package.baseFile)) {
        StringBuilder zipUrl = new StringBuilder(entry.assets.peltzer_package.rootUrl)
          .Append(entry.assets.peltzer_package.baseFile);

        PeltzerMain.Instance.webRequestManager.EnqueueRequest(
          () => { return GetNewGetRequest(zipUrl, "text/plain"); },
          (bool success, int responseCode, byte[] responseBytes) => {
            if (!success) {
              callback(null);
            } else {
              PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CopyStreamWork(responseBytes, callback));
            }
          });
      } else {
        StringBuilder url = new StringBuilder(entry.assets.peltzer.rootUrl)
          .Append(entry.assets.peltzer.baseFile);

        PeltzerMain.Instance.webRequestManager.EnqueueRequest(
          () => { return GetNewGetRequest(url, "text/plain"); },
          (bool success, int responseCode, byte[] responseBytes) => {
            if (!success) {
              callback(null);
            } else {
              callback(responseBytes);
            }
          });
      }
    }

    // Queries the ObjectStore for an object given its entry metadata and parses it into a PeltzerFile.
    public IEnumerator GetPeltzerFile(ObjectStoreEntry entry, System.Action<PeltzerFile> callback) {
      if (entry.assets.peltzer_package != null
          && !string.IsNullOrEmpty(entry.assets.peltzer_package.rootUrl)
          && !string.IsNullOrEmpty(entry.assets.peltzer_package.baseFile)) {
        StringBuilder zipUrl = new StringBuilder(OBJECT_STORE_BASE_URL).Append(entry.assets.peltzer_package.rootUrl)
          .Append(entry.assets.peltzer_package.baseFile);
        using (UnityWebRequest fetchRequest = GetNewGetRequest(zipUrl, "text/plain")) {
          yield return fetchRequest.Send();
          if (!fetchRequest.isNetworkError) {
            PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CopyStreamWork(fetchRequest.downloadHandler.data, /* byteCallback */ null, callback));
          }
        }
      } else {
        StringBuilder url = new StringBuilder(OBJECT_STORE_BASE_URL).Append(entry.assets.peltzer.rootUrl)
          .Append(entry.assets.peltzer.baseFile);
        using (UnityWebRequest fetchRequest = GetNewGetRequest(url, "text/plain")) {
          yield return fetchRequest.Send();
          if (!fetchRequest.isNetworkError) {
            PeltzerFile peltzerFile;
            bool validFile =
              PeltzerFileHandler.PeltzerFileFromBytes(fetchRequest.downloadHandler.data, out peltzerFile);

            if (validFile) {
              callback(peltzerFile);
            }
          }
        }
      }
    }

    // Sets properties for a UnityWebRequest.
    public IEnumerator SetListingProperties(string id, string title, string author, string description) {
      string url = OBJECT_STORE_BASE_URL + "/m/" + id + "?";
      if (!string.IsNullOrEmpty(title)) {
        url += "title=" + title + "&";
      }
      if (!string.IsNullOrEmpty(author)) {
        url += "author=" + author + "&";
      }
      if (!string.IsNullOrEmpty(description)) {
        url += "description=" + description;
      }
      UnityWebRequest request = new UnityWebRequest(url);
      request.method = UnityWebRequest.kHttpVerbPOST;
      request.SetRequestHeader("Content-Type", "text/plain");
      request.SetRequestHeader("Token", "[Removed]");
      using (UnityWebRequest propRequest = request) {
        yield return propRequest.Send();
      }
    }

    // Helps create a UnityWebRequest from a given url and contentType.
    public static UnityWebRequest GetNewGetRequest(StringBuilder url, string contentType) {
      UnityWebRequest request = new UnityWebRequest(url.ToString());
      request.method = UnityWebRequest.kHttpVerbGET;
      request.SetRequestHeader("Content-Type", contentType);
      request.SetRequestHeader("Token", "[Removed]");
      request.downloadHandler = new DownloadHandlerBuffer();

      if (OAuth2Identity.Instance.HasAccessToken) {
        OAuth2Identity.Instance.Authenticate(request);
      }
      return request;
    }

    public static void CopyStream(Stream input, Stream output) {
      byte[] buffer = new byte[32768];
      int read;
      while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
        output.Write(buffer, 0, read);
      }
    }

    /// <summary>
    ///   BackgroundWork for copying a stream (a zip-file containing a .peltzer/poly file) into memory
    ///   and then sending a callback.
    /// </summary>
    public class CopyStreamWork : BackgroundWork {
      // Optional callbacks.
      private readonly System.Action<byte[]> byteCallback;
      private readonly System.Action<PeltzerFile> peltzerFileCallback;

      private byte[] inputBytes;
      private MemoryStream outputStream;
      private byte[] outputBytes;

      public CopyStreamWork(byte[] inputBytes,
        System.Action<byte[]> byteCallback = null, System.Action<PeltzerFile> peltzerFileCallback = null) {
        this.inputBytes = inputBytes;
        this.byteCallback = byteCallback;
        this.peltzerFileCallback = peltzerFileCallback;

        outputStream = new MemoryStream();
      }

      public void BackgroundWork() {
        using (ZipFile zipFile = new ZipFile(new MemoryStream(inputBytes))) {
          foreach (ZipEntry zipEntry in zipFile) {
            if (zipEntry.Name.EndsWith(".peltzer") || zipEntry.Name.EndsWith(".poly")
              || zipEntry.Name.EndsWith(".blocks")) {
              CopyStream(zipFile.GetInputStream(zipEntry), outputStream);
              outputBytes = outputStream.ToArray();
              break;
            }
          }
        }
      }

      public void PostWork() {
        if (byteCallback != null) {
          byteCallback(outputBytes);
        }
        if (peltzerFileCallback != null) {
          PeltzerFile peltzerFile;
          bool validFile = PeltzerFileHandler.PeltzerFileFromBytes(outputBytes, out peltzerFile);

          if (validFile) {
            peltzerFileCallback(peltzerFile);
          }
        }
      }
    }

    /// <summary>
    ///   BackgroundWork for copying a stream (a zip-file containing a .obj file and a .mtl file) into memory
    ///   and then creating a mesh and sending a callback.
    /// </summary>
    public class CreateMeshFromStreamWork : BackgroundWork {
      DownloadHandler downloadHandler;
      string objFile;
      string mtlFile;
      System.Action<Dictionary<Material, Mesh>> callback;

      public CreateMeshFromStreamWork(DownloadHandler downloadHandler, System.Action<Dictionary<Material, Mesh>> callback) {
        this.downloadHandler = downloadHandler;
        this.callback = callback;
      }

      public void BackgroundWork() {
        // Go through our zip file entries and find the obj and mtl.
        byte[] zippedData = downloadHandler.data;
        using (ZipFile zipFile = new ZipFile(new MemoryStream(zippedData))) {
          foreach (ZipEntry zipEntry in zipFile) {
            if (zipEntry.Name.EndsWith(".obj")) {
              using (MemoryStream unzippedData = new MemoryStream()) {
                CopyStream(zipFile.GetInputStream(zipEntry), unzippedData);
                objFile = System.Text.Encoding.Default.GetString(unzippedData.ToArray());
              }
            } else if (zipEntry.Name.EndsWith(".mtl")) {
              using (MemoryStream unzippedData = new MemoryStream()) {
                CopyStream(zipFile.GetInputStream(zipEntry), unzippedData);
                mtlFile = System.Text.Encoding.Default.GetString(unzippedData.ToArray());
              }
            }
          }
        }
      }

      public void PostWork() {
        // Create meshes
        PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CreateMeshWork(ObjImporter.ImportMaterials(mtlFile),
          objFile, callback));
      }
    }
  }
}
