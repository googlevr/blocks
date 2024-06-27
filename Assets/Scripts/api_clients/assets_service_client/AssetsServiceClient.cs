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

using ICSharpCode.SharpZipLibUnityPort.Zip.Compression;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Text;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.entitlement;
using com.google.apps.peltzer.client.zandria;
using com.google.apps.peltzer.client.menu;

namespace com.google.apps.peltzer.client.api_clients.assets_service_client {

  public class AssetsServiceClientWork : MonoBehaviour, BackgroundWork {
    private AssetsServiceClient assetsServiceClient;
    private string assetId;
    private HashSet<string> remixIds;
    private SaveData saveData;
    private byte[] objMultiPartBytes;
    private byte[] triangulatedObjMultiPartBytes;
    private byte[] mtlMultiPartBytes;
    private byte[] fbxMultiPartBytes;
    private byte[] blocksMultiPartBytes;
    private byte[] thumbnailMultiPartBytes;
    private bool publish;
    private bool saveSelected;

    public void Setup(AssetsServiceClient assetsServiceClient, string assetId, HashSet<string> remixIds,
      SaveData saveData, bool publish, bool saveSelected) {
      this.assetsServiceClient = assetsServiceClient;
      this.assetId = assetId;
      this.remixIds = remixIds;
      this.saveData = saveData;
      this.publish = publish;
      this.saveSelected = saveSelected;
    }

    public void BackgroundWork() {
      saveData.GLTFfiles.root.multipartBytes = assetsServiceClient.MultiPartContent(saveData.GLTFfiles.root.fileName,
        saveData.GLTFfiles.root.mimeType, saveData.GLTFfiles.root.bytes);
      foreach (FormatDataFile file in saveData.GLTFfiles.resources) {
        file.multipartBytes = assetsServiceClient.MultiPartContent(file.fileName, file.mimeType, file.bytes);
      }

      objMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.OBJ_FILENAME, "text/plain", saveData.objFile);
      triangulatedObjMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.TRIANGULATED_OBJ_FILENAME,
        "text/plain", saveData.triangulatedObjFile);
      mtlMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.MTL_FILENAME, "text/plain", saveData.mtlFile);
      fbxMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.FBX_FILENAME, "application/octet-stream",
        saveData.fbxFile);
      blocksMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.BLOCKS_FILENAME, "application/octet-stream",
        saveData.blocksFile);
      thumbnailMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.THUMBNAIL_FILENAME, "image/png",
        saveData.thumbnailBytes);
    }

    public void PostWork() {
      if (assetId == null || saveSelected) {
        StartCoroutine(assetsServiceClient.UploadModel(remixIds, objMultiPartBytes, saveData.objPolyCount,
          triangulatedObjMultiPartBytes, saveData.triangulatedObjPolyCount, mtlMultiPartBytes, saveData.GLTFfiles,
          fbxMultiPartBytes, blocksMultiPartBytes, thumbnailMultiPartBytes, publish, saveSelected));
      } else {
        StartCoroutine(assetsServiceClient.UpdateModel(assetId, remixIds, objMultiPartBytes, saveData.objPolyCount,
          triangulatedObjMultiPartBytes, saveData.triangulatedObjPolyCount, mtlMultiPartBytes, saveData.GLTFfiles,
          fbxMultiPartBytes, blocksMultiPartBytes, thumbnailMultiPartBytes, publish));
      }
    }
  }

  public class ParseAssetsBackgroundWork : BackgroundWork {
    private string response;
    private PolyMenuMain.CreationType creationType;
    private System.Action<ObjectStoreSearchResult> successCallback;
    private System.Action failureCallback;
    private bool hackUrls;

    private bool success;
    private ObjectStoreSearchResult objectStoreSearchResult;

    public ParseAssetsBackgroundWork(string response, PolyMenuMain.CreationType creationType,
      System.Action<ObjectStoreSearchResult> successCallback,
      System.Action failureCallback, bool hackUrls = false) {
      this.response = response;
      this.creationType = creationType;
      this.successCallback = successCallback;
      this.failureCallback = failureCallback;
      this.hackUrls = hackUrls;
    }

    public void BackgroundWork() {
      success = AssetsServiceClient.ParseReturnedAssets(response, creationType, out objectStoreSearchResult, hackUrls);
    }

    public void PostWork() {
      if (success) {
        successCallback(objectStoreSearchResult);
      } else {
        failureCallback();
      }
    }
  }

  public class ParseAssetBackgroundWork : BackgroundWork {
    private string response;
    private System.Action<ObjectStoreEntry> callback;
    private bool hackUrls;

    private bool success;
    private ObjectStoreEntry objectStoreEntry;

    public ParseAssetBackgroundWork(string response, System.Action<ObjectStoreEntry> callback, bool hackUrls = false) {
      this.response = response;
      this.callback = callback;
      this.hackUrls = hackUrls;
    }

    public void BackgroundWork() {
      success = AssetsServiceClient.ParseAsset(response, out objectStoreEntry, hackUrls);
    }

    public void PostWork() {
      if (success) {
        callback(objectStoreEntry);
      }
    }
  }

  public class AssetsServiceClient : MonoBehaviour {
    // The base for API requests to the assets service.
    public static string AUTOPUSH_BASE_URL = "[Removed]";
    public static string PROD_BASE_URL = "[Removed]";
    public static string BaseUrl() { return Features.useZandriaProd ? PROD_BASE_URL : AUTOPUSH_BASE_URL; }
    // The base for the URL to be opened in a user's browser if they wish to publish.
    public static string AUTOPUSH_PUBLISH_URL_BASE = "[Removed]";
    public static string PROD_DEFAULT_PUBLISH_URL_BASE = "[Removed]";
    public static string PublishUrl() { return Features.useZandriaProd ? PROD_DEFAULT_PUBLISH_URL_BASE : AUTOPUSH_PUBLISH_URL_BASE; }
    // The base for the URL to be opened in a user's browser if they have saved.
    // Also used as the target for the "Your models" desktop menu
    public static string AUTOPUSH_SAVE_URL = "[Removed]";
    public static string PROD_DEFAULT_SAVE_URL = "[Removed]";
    public static string SaveUrl() { return Features.useZandriaProd ? PROD_DEFAULT_SAVE_URL : AUTOPUSH_SAVE_URL; }

    // Poly's application key for the assets service/
    public const string POLY_KEY = "[Removed]";

    // Search request strings corresponding to ListAssetRequest protos, see point of call for details.
    private static string FeaturedModelsSearchUrl() {
      int pageSize = ZandriaCreationsManager.MAX_NUMBER_OF_PAGES * ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE;
      return String.Format("{0}/v1/assets?key={1}&filter=format_type:BLOCKS,admin_tag:blocksgallery,license:CREATIVE_COMMONS_BY" +
        "&order_by=create_time%20desc&page_size={2}", BaseUrl(), POLY_KEY, pageSize);
    }

    private static string LikedModelsSearchUrl() {
      int pageSize = ZandriaCreationsManager.MAX_NUMBER_OF_PAGES * ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE;

      return String.Format("{0}/v1/assets?key={1}&filter=format_type:BLOCKS,liked:true,license:CREATIVE_COMMONS_BY" +
        "&order_by=liked_time%20desc&page_size={2}", BaseUrl(), POLY_KEY, pageSize);
    }
    private static string YourModelsSearchUrl() {
      int pageSize = ZandriaCreationsManager.MAX_NUMBER_OF_PAGES * ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE;

      return String.Format("{0}/v1/accounts/me/assets?key={1}&filter=format_type:BLOCKS&access_level=PRIVATE" +
        "&order_by=create_time%20desc&page_size={2}", BaseUrl(), POLY_KEY, pageSize);
    }

    // Some regex.
    private const string BOUNDARY = "!&!Peltzer12!&!Peltzer34!&!Peltzer56!&!";
    private const string ASSET_ID_MATCH = "assetId\": \"(.+?)\"";
    private const string ELEMENT_ID_MATCH = "elementId\": \"(.+?)\"";

    // Most recent asset IDs we have seen in the "Featured" and "Liked" sections.
    // Used for polling economically (so we know which part of the results is new and which part isn't).
    public static string mostRecentFeaturedAssetId;
    public static string mostRecentLikedAssetId;

    // Some state around an upload.
    public enum UploadState { IN_PROGRESS, FAILED, SUCCEEDED }
    private string assetId;
    private Dictionary<string, string> elementIds = new Dictionary<string, string>();
    private Dictionary<string, UploadState> elementUploadStates = new Dictionary<string, UploadState>();
    private bool assetCreationSuccess;
    private bool resourceUploadSuccess;
    private bool hasSavedSuccessfully;

    private bool compressResourceUpload = true;
    private readonly object deflateMutex = new object();
    private byte[] tempDeflateBuffer = new byte[65536 * 4];

    /// <summary>
    ///   Takes a string, representing the ListAssetsResponse proto, and fills objectStoreSearchResult with
    ///   relevant fields from the response and returns true, if the response is of the expected format.
    /// </summary>
    public static bool ParseReturnedAssets(string response, PolyMenuMain.CreationType type, 
      out ObjectStoreSearchResult objectStoreSearchResult, bool hackUrls = false) {
      objectStoreSearchResult = new ObjectStoreSearchResult();

      // Try and actually parse the string.
      JObject results = JObject.Parse(response);
      IJEnumerable<JToken> assets = results["asset"].AsJEnumerable();
      if (assets == null) {
        return false;
      }

      // Build accountId to name map first
      Dictionary<string, string> authorNamesById = new Dictionary<string, string>();
      JToken accounts = results["account"];
      if (accounts != null) {
        foreach (JToken account in accounts) {
          string id = account.First["accountId"].ToString();
          string name = "";
          JToken displayName = account.First["displayName"];
          if (displayName != null) {
            name = displayName.ToString();
          }
          authorNamesById.Add(id, name);
        }
      }

      // Then parse the assets.
      List<ObjectStoreEntry> objectStoreEntries = new List<ObjectStoreEntry>();

      string firstAssetId = null;
      foreach (JToken asset in assets) {
        ObjectStoreEntry objectStoreEntry;
        string author = null;
        var accountId = asset["accountId"];
        if (accountId != null) {
          authorNamesById.TryGetValue(accountId.ToString(), out author);
        }

        if (type == PolyMenuMain.CreationType.FEATURED || type == PolyMenuMain.CreationType.LIKED) {
          string assetId = asset["assetId"].ToString();
          if (firstAssetId == null) {
            firstAssetId = assetId;
          }
          // Once we've seen an ID we've seen before, no need to continue through the list. This helps with polling
          // regularly. This assumes new items always appear at the top of the list; we explicitly ask Zandria to sort by
          // featured/liked time, descending.
          if ((type == PolyMenuMain.CreationType.FEATURED && mostRecentFeaturedAssetId == assetId) 
            || (type == PolyMenuMain.CreationType.LIKED && mostRecentLikedAssetId == assetId)) {
            break;
          }
        }
        if (ParseAsset(asset, out objectStoreEntry, hackUrls)) {
          objectStoreEntry.author = author;
          objectStoreEntries.Add(objectStoreEntry);
        }
      }

      if (type == PolyMenuMain.CreationType.FEATURED) {
        mostRecentFeaturedAssetId = firstAssetId;
      } else if (type == PolyMenuMain.CreationType.LIKED) {
        mostRecentLikedAssetId = firstAssetId;
      }
      objectStoreSearchResult.results = objectStoreEntries.ToArray();
      return true;
    }

    /// <summary>
    ///   Parses a single asset as defined in vr/assets/v1/asset.proto
    /// </summary>
    /// <returns></returns>
    public static bool ParseAsset(JToken asset, out ObjectStoreEntry objectStoreEntry, bool hackUrls) {
      objectStoreEntry = new ObjectStoreEntry();

      if (asset["accessLevel"] == null) {
        Debug.Log("Asset had no access level set");
        return false;
      }
      objectStoreEntry.isPrivateAsset = asset["accessLevel"].ToString() == "PRIVATE";

      objectStoreEntry.id = asset["assetId"].ToString();
      JToken thumbnailRoot = asset["thumbnail"];
      if (thumbnailRoot != null) {
        IJEnumerable<JToken> thumbnailElements = asset["thumbnail"].AsJEnumerable();
        foreach (JToken thumbnailElement in thumbnailElements) {
          objectStoreEntry.thumbnail = thumbnailElement["typeInfo"]["imageInfo"]["fifeUrl"].ToString();
          break;
        }
      }
      List<string> tags = new List<string>();
      IJEnumerable<JToken> assetTags = asset["tag"].AsJEnumerable();
      if (assetTags != null) {
        foreach (JToken assetTag in assetTags) {
          tags.Add(assetTag.ToString());
        }
        if (tags.Count > 0) {
          objectStoreEntry.tags = tags.ToArray();
        }
      }
      ObjectStoreObjectAssetsWrapper entryAssets = new ObjectStoreObjectAssetsWrapper();
      ObjectStorePeltzerAssets blocksAsset = new ObjectStorePeltzerAssets();
      // 7 is the enum for Blocks in ElementType
      // A bit ugly: we simply take one arbitrary entry (we assume only one entry exists, as we only ever upload one).
      blocksAsset.rootUrl = asset["formatList"]["7"]["format"][0]["root"]["dataUrl"].ToString();

      blocksAsset.baseFile = "";
      entryAssets.peltzer = blocksAsset;
      objectStoreEntry.assets = entryAssets;
      objectStoreEntry.title = asset["displayName"].ToString();
      objectStoreEntry.createdDate = DateTime.Parse(asset["createTime"].ToString());
      objectStoreEntry.cameraForward = GetCameraForward(asset["cameraParams"]);
      return true;
    }

    /// <summary>
    /// Parse the camera parameter matrix from Zandria to extract the camera's forward, if available.
    /// </summary>
    /// <param name="cameraParams">A 4x4 matrix holding information about the camera's position and
    /// rotation:
    /// Row major
    /// * * Fx Px
    /// * * Fy Py
    /// * * Fz Pz
    /// 0 0 0 1</param>
    /// <returns>A string of three float values separated by spaces that represent the camera forward.</returns>
    private static Vector3 GetCameraForward(JToken cameraParams) {
      JToken cameraMatrix = cameraParams["matrix4x4"];
      if (cameraMatrix == null) return Vector3.zero;
      // We want the third column, which holds the camera's forward.
      Vector3 cameraForward = new Vector3();
      cameraForward.x = float.Parse(cameraMatrix[2].ToString());
      cameraForward.y = float.Parse(cameraMatrix[6].ToString());
      cameraForward.z = float.Parse(cameraMatrix[10].ToString());
      return cameraForward;
    }

    // As above, accepting a string response (such that we can parse on a background thread).
    public static bool ParseAsset(string response, out ObjectStoreEntry objectStoreEntry, bool hackUrls) {
      return ParseAsset(JObject.Parse(response), out objectStoreEntry, hackUrls);
    }

    /// <summary>
    ///   Fetch a list of featured models, together with their metadata, from the assets service.
    ///   Only searches for models with CC-BY licensing to avoid any complicated questions around non-remixable models.
    ///   Requests a create-time-descending ordering.
    /// </summary>
    /// <param name="callback">A callback to which to pass the results.</param>
    /// <param name="isRecursion">Whether this is not the first call to this function.</param>
    public void GetFeaturedModels(System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback,
      bool isRecursion = false) {
      // We wrap in a for loop so we can re-authorise if access tokens have become stale.
      UnityWebRequest request = GetRequest(FeaturedModelsSearchUrl(), "text/text");
      PeltzerMain.Instance.webRequestManager.EnqueueRequest(
      () => { return request; },
      (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
        ProcessGetFeaturedModelsResponse(
          success, responseCode, responseBytes, request, successCallback, failureCallback)), 
          maxAgeMillis: WebRequestManager.CACHE_NONE);
    }

    // Deals with the response of a GetFeaturedModels request, retrying it if an auth token was stale.
    private IEnumerator ProcessGetFeaturedModelsResponse(bool success, int responseCode, byte[] responseBytes,
      UnityWebRequest request, System.Action<ObjectStoreSearchResult> successCallback, 
      System.Action failureCallback, bool isRecursion = false) {
      if (!success || responseCode == 401) {
        if (isRecursion) {
          Debug.Log(GetDebugString(request, "Failed to get featured models"));
          yield break;
        }
        yield return OAuth2Identity.Instance.Reauthorize();
        GetFeaturedModels(successCallback, failureCallback, /* isRecursion */ true);
      } else {
        PeltzerMain.Instance.DoPolyMenuBackgroundWork(
          new ParseAssetsBackgroundWork(Encoding.UTF8.GetString(responseBytes),
          PolyMenuMain.CreationType.FEATURED, successCallback, failureCallback));
      }
    }

    /// <summary>
    ///   Fetch a list of the authenticated user's models, together with their metadata, from the assets service.
    ///   Requests a create-time-descending ordering.
    /// </summary>
    /// <param name="callback">A callback to which to pass the results.</param>
    public void GetYourModels(System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback,
      bool isRecursion = false) {
      UnityWebRequest request = GetRequest(YourModelsSearchUrl(), "text/text");
      PeltzerMain.Instance.webRequestManager.EnqueueRequest(
        () => { return request; },
        (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
          ProcessGetYourModelsResponse(
            success, responseCode, responseBytes, request, successCallback, failureCallback)),
        maxAgeMillis: WebRequestManager.CACHE_NONE);
    }

    // Deals with the response of a GetYourModels request, retrying it if an auth token was stale.
    private IEnumerator ProcessGetYourModelsResponse(bool success, int responseCode, byte[] responseBytes,
      UnityWebRequest request, System.Action<ObjectStoreSearchResult> successCallback, 
      System.Action failureCallback, bool isRecursion = false) {
      if (!success || responseCode == 401) {
        if (isRecursion) {
          Debug.Log(GetDebugString(request, "Failed to get your models"));
          yield break;
        }
        yield return OAuth2Identity.Instance.Reauthorize();
        GetYourModels(successCallback, failureCallback);
      } else {
        PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAssetsBackgroundWork(
          Encoding.UTF8.GetString(responseBytes), PolyMenuMain.CreationType.YOUR, successCallback, 
          failureCallback, hackUrls: true));
      }
    }

    /// <summary>
    ///   Fetch a list of models authenticated user has liked, together with their metadata, from the assets service.
    ///   Only searches for models with CC-BY licensing to avoid any complicated questions around non-remixable models.
    ///   Requests a create-time-descending ordering.
    /// </summary>
    /// <param name="callback">A callback to which to pass the results.</param>
    public void GetLikedModels(System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback) {
      UnityWebRequest request = GetRequest(LikedModelsSearchUrl(), "text/text");
      PeltzerMain.Instance.webRequestManager.EnqueueRequest(
        () => { return request; },
        (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
          ProcessGetLikedModelsResponse(
            success, responseCode, responseBytes, request, successCallback, failureCallback)),
        maxAgeMillis: WebRequestManager.CACHE_NONE);
    }

    // Deals with the response of a GetLikedModels request, retrying it if an auth token was stale.
    private IEnumerator ProcessGetLikedModelsResponse(bool success, int responseCode, byte[] responseBytes,
      UnityWebRequest request, System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback,
      bool isRecursion = false) {
      if (!success || responseCode == 401) {
        if (isRecursion) {
          Debug.Log(GetDebugString(request, "Failed to get liked models"));
          yield break;
        }
        yield return OAuth2Identity.Instance.Reauthorize();
        GetLikedModels(successCallback, failureCallback);
      } else {
        PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAssetsBackgroundWork(
          Encoding.UTF8.GetString(responseBytes), PolyMenuMain.CreationType.LIKED, successCallback, failureCallback));
      }
    }

    /// <summary>
    ///   Fetch a specific asset.
    /// </summary>
    /// <param name="callback">A callback to which to pass the results.</param>
    public void GetAsset(string assetId, System.Action<ObjectStoreEntry> callback) {
      string url = String.Format("{0}/v1/assets/{1}?key={2}", BaseUrl(), assetId, POLY_KEY);
      UnityWebRequest request = GetRequest(url, "text/text");
      PeltzerMain.Instance.webRequestManager.EnqueueRequest(
        () => { return request; },
        (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
          ProcessGetAssetResponse(success, responseCode, responseBytes, request, assetId, callback)),
        maxAgeMillis: WebRequestManager.CACHE_NONE);
    }

    // Deals with the response of a GetAsset request, retrying it if an auth token was stale.
    private IEnumerator ProcessGetAssetResponse(bool success, int responseCode, byte[] responseBytes,
      UnityWebRequest request, string assetId, System.Action<ObjectStoreEntry> callback, bool isRecursion = false) {
      if (!success || responseCode == 401) {
        if (isRecursion) {
          Debug.Log(GetDebugString(request, "Failed to fetch an asset with id " + assetId));
          yield break;
        }
        yield return OAuth2Identity.Instance.Reauthorize();
        GetAsset(assetId, callback);
      } else {
        PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAssetBackgroundWork(
          Encoding.UTF8.GetString(responseBytes), callback, hackUrls: true));
      }
    }

    /// <summary>
    ///   Uploads all the resources for a model to the Assets Service (in parallel).
    ///   If every upload was successful, creates an asset out of them, and calls PeltzerMain to handle success.
    ///   Else, calls PeltzerMain to handle failure.
    ///   Whilst propagating success/failure as a return type might be more idiomatic, it's a pain here, so we
    ///   avoid it. /shrug.
    /// </summary>
    /// <param name="objFile">The bytes of an OBJ file representing this model</param>
    /// <param name="objPolyCount">The poly count of the OBJ file</param>
    /// <param name="triangulatedObjFile">The bytes of a triangulated OBJ file representing this model</param>
    /// <param name="triangulatedObjPolyCount">The poly count of the triangulated OBJ file</param>
    /// <param name="mtlFile">The bytes of an MTL file to pair with the OBJ file</param>
    /// <param name="gltfData">All data required for the glTF files representing this model</param>
    /// <param name="fbxFile">The bytes of a .fbx file representing this model</param>
    /// <param name="blocksFile">The bytes of a PeltzerFile representing this model</param>
    /// <param name="thumbnailFile">The bytes of an image file giving a thumbnail view of this model</param>
    /// <param name="publish">If true, opens the 'publish' dialog on a user's browser after successful creation</param>
    /// <param name="saveSelected">If true, only the currently selected content is saved.</param>
    public IEnumerator UploadModel(HashSet<string> remixIds, byte[] objFile, int objPolyCount,
      byte[] triangulatedObjFile, int triangulatedObjPolyCount, byte[] mtlFile, FormatSaveData gltfData,
      byte[] fbxFile, byte[] blocksFile, byte[] thumbnailFile, bool publish, bool saveSelected) {

      // Upload the resources.
      yield return UploadResources(objFile, triangulatedObjFile, mtlFile, gltfData, fbxFile,
          blocksFile, thumbnailFile, saveSelected);

      // Create an asset if all uploads succeded.
      if (resourceUploadSuccess) {
        yield return CreateNewAsset(gltfData, objPolyCount, triangulatedObjPolyCount, remixIds, saveSelected);
      }

      // Show a toast informing the user that they uploaded to Zandria (or that there was an error.)
      PeltzerMain.Instance
        .HandleSaveComplete(/* success */ assetCreationSuccess, assetCreationSuccess ? "Saved" : "Save failed");
      if (assetCreationSuccess) {
        PeltzerMain.Instance.LoadSavedModelOntoPolyMenu(assetId, publish);
      }

      if (assetCreationSuccess) {
        // If we are only saving the selected content, then we don't want to overwrite the LastSavedAssetId
        // as the id we are currently using is meant to be temporary.
        if (!saveSelected) {
          PeltzerMain.Instance.LastSavedAssetId = assetId;
        }
        if (publish) {
          OpenPublishUrl(assetId);
          PeltzerMain.Instance.Analytics.SuccessfulOperation("publish");
        } else {
          PeltzerMain.Instance.Analytics.SuccessfulOperation("saveLoggedIn");
          // Don't prompt to publish if the tutorial is active or if we are only saving a selected
          // subset of the model.
          if (!PeltzerMain.Instance.tutorialManager.TutorialOccurring() && !saveSelected) {
            // Encourage users to publish their creation.
            PeltzerMain.Instance.SetPublishAfterSavePromptActive();
          }
          if (!hasSavedSuccessfully) {
            // On the first successful save to Zandria we want to open up the browser to the users models so that they
            // understand that we save to the cloud and shows them where they can find their models.
            hasSavedSuccessfully = true;
            OpenSaveUrl();
          }
        }
      } else {
        PeltzerMain.Instance.Analytics.FailedOperation("saveLoggedIn");
      }
    }

    /// <summary>
    ///   Updates an existing asset after uploading the new resources for it.
    /// </summary>
    public IEnumerator UpdateModel(string assetId, HashSet<string> remixIds, byte[] objFile, int objPolyCount,
      byte[] triangulatedObjFile, int triangulatedObjPolyCount, byte[] mtlFile, FormatSaveData gltfData, 
      byte[] fbxFile, byte[] blocksFile, byte[] thumbnailFile, bool publish) {

      // Upload the resources.
      yield return UploadResources(objFile, triangulatedObjFile, mtlFile, gltfData, fbxFile,
          blocksFile, thumbnailFile, saveSelected:false);

      // Update the asset if all uploads succeded.
      if (resourceUploadSuccess) {
        yield return UpdateAsset(assetId, gltfData, objPolyCount, triangulatedObjPolyCount, remixIds);
      }

      // Show a toast informing the user that they uploaded to Zandria, or that there was an error.
      PeltzerMain.Instance
        .HandleSaveComplete(/* success */ assetCreationSuccess, assetCreationSuccess ? "Saved" : "Save failed");
      if (assetCreationSuccess) {
        PeltzerMain.Instance.LastSavedAssetId = assetId;
        if (publish) {
          OpenPublishUrl(assetId);
          PeltzerMain.Instance.Analytics.SuccessfulOperation("publish");
        } else {
          PeltzerMain.Instance.Analytics.SuccessfulOperation("saveLoggedIn");
        }
      } else {
        PeltzerMain.Instance.Analytics.FailedOperation("saveLoggedIn");
      }
    }

    public static void OpenPublishUrl(string assetId) {
      string publishUrl = PublishUrl() + assetId;
      string emailAddress = OAuth2Identity.Instance.Profile == null ? null : OAuth2Identity.Instance.Profile.email;
      string urlToOpen = emailAddress == null ? publishUrl :
        string.Format("https://accounts.google.com/AccountChooser?Email={0}&continue={1}", emailAddress, publishUrl);
      PeltzerMain.Instance.paletteController.SetPublishDialogActive();
      System.Diagnostics.Process.Start(urlToOpen);
    }

    private void OpenSaveUrl() {
      if (PeltzerMain.Instance.HasOpenedSaveUrlThisSession) {
        return;
      }
      string emailAddress = OAuth2Identity.Instance.Profile == null ? null : OAuth2Identity.Instance.Profile.email;
      string urlToOpen = emailAddress == null ? SaveUrl() :
        string.Format("https://accounts.google.com/AccountChooser?Email={0}&continue={1}", emailAddress, SaveUrl());
      System.Diagnostics.Process.Start(urlToOpen);
      PeltzerMain.Instance.HasOpenedSaveUrlThisSession = true;
    }

    /// <summary>
    ///   Upload all required resources for a creation/overwrite request.
    /// </summary>
    private IEnumerator UploadResources(byte[] objFile, byte[] triangulatedObjFile,
      byte[] mtlFile, FormatSaveData gltfData, byte[] fbxFile, byte[] blocksFile, byte[] thumbnailFile,
      bool saveSelected) {
      StartCoroutine(AddResource(ExportUtils.OBJ_FILENAME, "text/plain", objFile, "obj"));
      StartCoroutine(AddResource(ExportUtils.TRIANGULATED_OBJ_FILENAME, "text/plain", triangulatedObjFile,
        "triangulated-obj"));
      StartCoroutine(AddResource(ExportUtils.MTL_FILENAME, "text/plain", mtlFile, "mtl"));
      StartCoroutine(AddResource(ExportUtils.FBX_FILENAME, "application/octet-stream", fbxFile, "fbx"));
      StartCoroutine(AddResource(gltfData.root.fileName, gltfData.root.mimeType, gltfData.root.multipartBytes,
        gltfData.root.tag));

      for (int i = 0; i < gltfData.resources.Count; i++) {
        FormatDataFile file = gltfData.resources[i];
        StartCoroutine(AddResource(file.fileName, file.mimeType, file.multipartBytes, file.tag + i));
      }

      StartCoroutine(AddResource(ExportUtils.BLOCKS_FILENAME, "application/octet-stream", blocksFile, "blocks"));
     if (!saveSelected){
        StartCoroutine(AddResource(ExportUtils.THUMBNAIL_FILENAME, "image/png", thumbnailFile, "png"));
     }

      // Wait for all uploads to complete (or fail);
      UploadState overallState = UploadState.IN_PROGRESS;
      while (overallState == UploadState.IN_PROGRESS) {
        bool allSucceeded = true;
        foreach (KeyValuePair<string, UploadState> pair in elementUploadStates) {
          switch (pair.Value) {
            case UploadState.FAILED:
              Debug.Log("Failed to upload " + pair.Key);
              allSucceeded = false;
              overallState = UploadState.FAILED;
              resourceUploadSuccess = false;
              break;
            case UploadState.IN_PROGRESS:
              allSucceeded = false;
              break;
          }
        }
        if (allSucceeded) {
          overallState = UploadState.SUCCEEDED;
          resourceUploadSuccess = true;
        }
        yield return null;
      }
    }

    /// <summary>
    ///   Create a new asset from the uploaded files.
    /// </summary>
    private IEnumerator CreateNewAsset(FormatSaveData saveData, int objPolyCount, int triangulatedObjPolyCount,
      HashSet<string> remixIds, bool saveSelected) {
      string json = CreateJsonForAssetResources(saveData, remixIds, objPolyCount, triangulatedObjPolyCount, 
        /* displayName */ "(Untitled)", saveSelected);
      string url = String.Format("{0}/v1/assets?key={1}", BaseUrl(), POLY_KEY);
      UnityWebRequest request = new UnityWebRequest();

      // We wrap in a for loop so we can re-authorise if access tokens have become stale.
      for (int i = 0; i < 2; i++) {
        request = PostRequest(url, "application/json", Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.Send();

        if (request.responseCode == 401 || request.isNetworkError) {
          yield return OAuth2Identity.Instance.Reauthorize();
          continue;
        } else {
          assetId = null;
          Regex regex = new Regex(ASSET_ID_MATCH);
          Match match = regex.Match(request.downloadHandler.text);
          if (match.Success) {
            assetId = match.Groups[1].Captures[0].Value;
            // Only update the global AssetId if the user has not hit 'new model' or opened a model
            // since this save began, and if we are not only saving selected content, as the id used
            // is meant to be temporary.
            if (!PeltzerMain.Instance.newModelSinceLastSaved && !saveSelected) {
              PeltzerMain.Instance.AssetId = assetId;
            }
            assetCreationSuccess = true;
          } else {
            Debug.Log("Failed to save to Assets Store. Response: " + request.downloadHandler.text);
          }
          yield break;
        }
      }

      Debug.Log(GetDebugString(request, "Failed to save to asset store"));
    }

    /// <summary>
    ///   Update an existing asset.
    /// </summary>
    private IEnumerator UpdateAsset(string assetId, FormatSaveData saveData, int objPolyCount, 
      int triangulatedObjPolyCount, HashSet<string> remixIds) {
      string json = CreateJsonForAssetResources(saveData, remixIds, objPolyCount, triangulatedObjPolyCount, 
        /* displayName */ null, saveSelected:false);
      string url = String.Format("{0}/v1/assets/{1}:updateData?key={2}", BaseUrl(), assetId, POLY_KEY);
      UnityWebRequest request = new UnityWebRequest();

      // We wrap in a for loop so we can re-authorise if access tokens have become stale.
      for (int i = 0; i < 2; i++) {
        request = Patch(url, "application/json", Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.Send();

        if (request.responseCode == 401 || request.isNetworkError) {
          yield return OAuth2Identity.Instance.Reauthorize();
          continue;
        } else {
          assetId = null;
          Regex regex = new Regex(ASSET_ID_MATCH);
          Match match = regex.Match(request.downloadHandler.text);
          if (match.Success) {
            assetId = match.Groups[1].Captures[0].Value;
            PeltzerMain.Instance.UpdateCloudModelOntoPolyMenu(request.downloadHandler.text);
            assetCreationSuccess = true;
          } else {
            Debug.Log("Failed to update " + assetId + " in Assets Store. Response: " + request.downloadHandler.text);
          }
          yield break;
        }
      }
      Debug.Log(GetDebugString(request, "Failed to save to asset store"));
    }

    private string CreateJsonForAssetResources(FormatSaveData saveData, HashSet<string> remixIds, 
      int objPolyCount, int triangulatedObjPolyCount, string displayName, bool saveSelected) {
      List<String> gltfResourceFiles = new List<string>();
      for (int i = 0; i < saveData.resources.Count; i++) {
        FormatDataFile dataFile = saveData.resources[i];
        gltfResourceFiles.Add(String.Format("\"resource_id\": \"{0}\"", elementIds[dataFile.tag + i]));
      }

      string gltfFormatComplexity = "";
      if (saveData.triangleCount > 0) {
        gltfFormatComplexity = String.Format("\"format_complexity\": {{ \"triangle_count\": {0} }},", 
          saveData.triangleCount);
      }
      string objFormatComplexity = String.Format("\"format_complexity\": {{ \"triangle_count\": {0} }},", 
        objPolyCount);
      string triangulatedObjFormatComplexity = String.Format("\"format_complexity\": {{ \"triangle_count\": {0} }},",
        triangulatedObjPolyCount);

      // Create asset using the uploaded components.
      // Newtonsoft library doesn't like repeated keys, so we do it by hand.
      string gltfResources = String.Join(",", gltfResourceFiles.ToArray());
      string gltfBlock = String.Format("\"format\": [ {{ \"root_id\": \"{0}\", {1} " +
        "{2} }} ]", elementIds[saveData.root.tag], gltfFormatComplexity, gltfResources);

      string remixBlock;
      // Note: we have to include the remix_info section even if it's empty, because its absence would
      // mean "keep the existing remix IDs" (incorrect), not "there are no remix IDs" (correct).
      StringBuilder remixBlockBuilder = new StringBuilder("\"remix_info\": { ");
      foreach (string remixId in remixIds) {
        remixBlockBuilder.Append("\"source_asset\": \"").Append(remixId).Append("\", ");
      }
      remixBlockBuilder.Append("}");
      remixBlock = remixBlockBuilder.ToString();

      string prelude = displayName == null ? "{ " : String.Format("{{ \"display_name\": \"{0}\",", displayName);

      string json;
      if (!saveSelected) {
        json = String.Format(
          "{0}\"thumbnail_id\": \"{1}\"," +
            "\"format\": [ {{ \"format_type\": \"FORMAT_WAVEFRONT_OBJ\", \"root_id\": \"{2}\", {3} \"resource_id\": \"{4}\" }} ]," +
            "\"format\": [ {{ \"format_type\": \"FORMAT_WAVEFRONT_OBJ_TRIANGULATED\", \"root_id\": \"{5}\", {6} \"resource_id\": \"{7}\" }} ]," +
            "\"format\": [ {{ \"format_type\": \"FORMAT_AUTODESK_FBX\", \"root_id\": \"{8}\" }} ]," +
            "\"format\": [ {{ \"format_type\": \"FORMAT_BLOCKS\", \"root_id\": \"{9}\" }} ], {10}, {11} }}",
          prelude, elementIds["png"],
          elementIds["obj"], objFormatComplexity, elementIds["mtl"],
          elementIds["triangulated-obj"], triangulatedObjFormatComplexity, elementIds["mtl"],
          elementIds["fbx"],
          elementIds["blocks"], gltfBlock, remixBlock);
      }
      else {
        json = String.Format(
          "{0}" +
            "\"format\": [ {{ \"format_type\": \"FORMAT_WAVEFRONT_OBJ\", \"root_id\": \"{1}\", {2} \"resource_id\": \"{3}\" }} ]," +
            "\"format\": [ {{ \"format_type\": \"FORMAT_WAVEFRONT_OBJ_TRIANGULATED\", \"root_id\": \"{4}\", {5} \"resource_id\": \"{6}\" }} ]," +
            "\"format\": [ {{ \"format_type\": \"FORMAT_AUTODESK_FBX\", \"root_id\": \"{7}\" }} ]," +
            "\"format\": [ {{ \"format_type\": \"FORMAT_BLOCKS\", \"root_id\": \"{8}\" }} ], {9}, {10} }}",
          prelude,
          elementIds["obj"], objFormatComplexity, elementIds["mtl"],
          elementIds["triangulated-obj"], triangulatedObjFormatComplexity, elementIds["mtl"],
          elementIds["fbx"],
          elementIds["blocks"], gltfBlock, remixBlock);
      }
      return json;
    }

    /// <summary>
    ///   Add a resource to the existing asset.
    /// </summary>
    private IEnumerator AddResource(string filename, string mimeType, byte[] data, string key) {
      elementUploadStates.Add(key, UploadState.IN_PROGRESS);
      string url = string.Format("{0}/uploads", BaseUrl());
      UnityWebRequest request = new UnityWebRequest();

      // We wrap in a for loop so we can re-authorise if access tokens have become stale.
      for (int i = 0; i < 2; i++) {
        request = PostRequest(url, "multipart/form-data; boundary=" + BOUNDARY, data, compressResourceUpload);
        request.SetRequestHeader("X-Google-Project-Override", "apikey");
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.Send();

        if (request.responseCode == 401 || request.isNetworkError) {
          yield return OAuth2Identity.Instance.Reauthorize();
          continue;
        } else {
          Regex regex = new Regex(ELEMENT_ID_MATCH);
          Match match = regex.Match(request.downloadHandler.text);
          if (match.Success) {
            elementIds[key] = match.Groups[1].Captures[0].Value;
            elementUploadStates[key] = UploadState.SUCCEEDED;
          } else {
            Debug.Log(GetDebugString(request, "Failed to save " + filename + " to Assets Store."));
            elementUploadStates[key] = UploadState.FAILED;
          }
          yield break;
        }
      }

      elementUploadStates[key] = UploadState.FAILED;
      Debug.Log(GetDebugString(request, "Failed to save " + filename + " to asset store"));
    }

    /// <summary>
    ///   Returns a debug string for an upload.
    /// </summary>
    /// <returns></returns>
    public static string GetDebugString(UnityWebRequest request, string preface) {
      StringBuilder debugString = new StringBuilder(preface).AppendLine()
        .Append("Response: ").AppendLine(request.downloadHandler.text)
        .Append("Response Code: ").AppendLine(request.responseCode.ToString())
        .Append("Error Message: ").AppendLine(request.error);

      foreach (KeyValuePair<string, string> header in request.GetResponseHeaders()) {
        debugString.Append(header.Key).Append(" : ").AppendLine(header.Value);
      }
      return debugString.ToString();
    }

    /// <summary>
    ///   Build the binary multipart content manually, since Unity's multipart stuff is borked.
    /// </summary>
    public byte[] MultiPartContent(string filename, string mimeType, byte[] data) {
      MemoryStream stream = new MemoryStream();
      StreamWriter sw = new StreamWriter(stream);

      // Write the media part of the request from the data.
      sw.Write("--" + BOUNDARY);
      sw.Write(string.Format(
        "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{0}\"\r\nContent-Type: {1}\r\n\r\n",
        filename, mimeType));
      sw.Flush();
      stream.Write(data, 0, data.Length);
      sw.Write("\r\n--" + BOUNDARY + "--\r\n");
      sw.Close();

      return stream.ToArray();
    }

    /// <summary>
    ///   Compress bytes using deflate.
    ///   </summary>
    private byte[] Deflate(byte[] data) {
      Deflater deflater = new Deflater(Deflater.DEFLATED, true);
      deflater.SetInput(data);
      deflater.Finish();

      using (var ms = new MemoryStream()) {
        lock (deflateMutex) {
          while (!deflater.IsFinished) {
            var read = deflater.Deflate(tempDeflateBuffer);
            ms.Write(tempDeflateBuffer, 0, read);
          }
          deflater.Reset();
        }
        return ms.ToArray();
      }
    }

    /// <summary>
    ///   Delete the specified asset.
    /// </summary>
    public IEnumerator DeleteAsset(string assetId) {
      string url = String.Format("{0}/v1/assets/{1}?key={2}", BaseUrl(), assetId, POLY_KEY);
      UnityWebRequest request = new UnityWebRequest();

      // We wrap in a for loop so we can re-authorise if access tokens have become stale.
      for (int i = 0; i < 2; i++) {
        request = DeleteRequest(url, "application/json");

        yield return request.Send();

        if (request.responseCode == 401 || request.isNetworkError) {
          yield return OAuth2Identity.Instance.Reauthorize();
          continue;
        } else {
          yield break;
        }
      }

      Debug.Log(GetDebugString(request, "Failed to delete " + assetId));
    }

    /// <summary>
    ///   Forms a GET request from a HTTP path.
    /// </summary>
    public UnityWebRequest GetRequest(string path, string contentType) {
      // The default constructor for a UnityWebRequest gives a GET request.
      UnityWebRequest request = new UnityWebRequest(path);
      request.SetRequestHeader("Content-type", contentType);
      if (OAuth2Identity.Instance.HasAccessToken) {
        OAuth2Identity.Instance.Authenticate(request);
      }
      return request;
    }

    /// <summary>
    ///   Forms a DELETE request from a HTTP path.
    /// </summary>
    public UnityWebRequest DeleteRequest(string path, string contentType) {
      UnityWebRequest request = new UnityWebRequest(path, UnityWebRequest.kHttpVerbDELETE);
      request.SetRequestHeader("Content-type", contentType);
      if (OAuth2Identity.Instance.HasAccessToken) {
        OAuth2Identity.Instance.Authenticate(request);
      }
      return request;
    }

    /// <summary>
    ///   Forms a POST request from a HTTP path, contentType and the data.
    /// </summary>
    public UnityWebRequest PostRequest(string path, string contentType, byte[] data, bool compressData = false) {
      // Create the uploadHandler.
      UploadHandler uploader = null;
      if (data.Length != 0) {
        uploader = new UploadHandlerRaw(compressData ? Deflate(data) : data);
        uploader.contentType = contentType;
      }

      // Create the request.
      UnityWebRequest request =
        new UnityWebRequest(path, UnityWebRequest.kHttpVerbPOST, new DownloadHandlerBuffer(), uploader);
      request.SetRequestHeader("Content-type", contentType);
      if (compressData) {
        request.SetRequestHeader("Content-Encoding", "deflate");
      }
      if (OAuth2Identity.Instance.HasAccessToken) {
        OAuth2Identity.Instance.Authenticate(request);
      }
      return request;
    }

    /// <summary>
    ///   Forms a PATCH request from a HTTP path, contentType and the data.
    /// </summary>
    public UnityWebRequest Patch(string path, string contentType, byte[] data) {
      // Create the uploadHandler.
      UploadHandler uploader = null;
      if (data.Length != 0) {
        uploader = new UploadHandlerRaw(data);
        uploader.contentType = contentType;
      }

      // Create the request.
      UnityWebRequest request = new UnityWebRequest(path);
      request.downloadHandler = new DownloadHandlerBuffer();
      request.method = "PATCH";
      request.uploadHandler = uploader;
      request.SetRequestHeader("Content-type", contentType);
      if (OAuth2Identity.Instance.HasAccessToken) {
        OAuth2Identity.Instance.Authenticate(request);
      }
      request.downloadHandler = new DownloadHandlerBuffer();
      return request;
    }
  }
}
