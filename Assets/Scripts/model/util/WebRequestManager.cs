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
using UnityEngine.Networking;
using System.Collections.Generic;
using System;
using System.Collections;
using System.IO;

namespace com.google.apps.peltzer.client.model.util {
  /// <summary>
  /// Manages web requests, limiting how many can happen simultaneously at any given time and re-using
  /// buffers as much as possible to avoid reallocation and garbage collection.
  /// 
  /// Not *all* web requests must be routed through this class. Small, infrequent web requests can be made directly
  /// via UnityWebRequest without using this class. However, larger or frequent requests should use this, since this
  /// will avoid the expensive allocation of numerous download buffers (a typical UnityWebRequest allocates many
  /// small buffers for temporary transfer and a larger buffer to contain the download, and they all become garbage
  /// that the GC has to clean up).
  /// </summary>
  [ExecuteInEditMode]
  public class WebRequestManager : MonoBehaviour {
    /// <summary>
    /// Constant passed to EnqueueRequest to mean that the request should not be retrieved from cache.
    /// </summary>
    public const long CACHE_NONE = 0;

    /// <summary>
    /// Constant passed to EnqueueRequest to mean that a cached copy is acceptable regardless of its age.
    /// </summary>
    public const long CACHE_ANY_AGE = -1;

    /// <summary>
    /// Maximum number of concurrent downloads to allow. This indicates how many download buffers we should keep.
    /// </summary>
    private const int MAX_CONCURRENT_DOWNLOADS = 8;

    /// <summary>
    /// Initial size of the pre-allocated data buffer.
    /// The data buffer is re-allocated when needed, but we want to avoid doing that because it's
    /// expensive and will happen in the UI thread, so we need to start out with a reasonably large
    /// size to handle the data we will download.
    /// </summary>
    private const int DATA_BUFFER_INIT_SIZE = 128 * 1024 * 1024;  // 128MB

    /// <summary>
    /// Size of the temporary buffer used to receive data. This is for a temporary buffer used
    /// by Unity to transfer data to us.
    /// </summary>
    private const int TEMP_BUFFER_SIZE = 2 * 1024 * 1024;  // 2MB

    /// <summary>
    /// Delegate that creates a UnityWebRequest. This is used by client code to set up a UnityWebRequest
    /// with the desired parameters.
    /// </summary>
    /// <returns>The UnityWebRequest.</returns>
    public delegate UnityWebRequest CreationCallback();

    /// <summary>
    /// Delegate that processes the completion of a web request. We call this delegate to inform the client that
    /// a web request has completed.
    /// </summary>
    /// <param name="completedRequest">The UnityWebRequest that was just completed.</param>
    public delegate void CompletionCallback(bool success, int responseCode, byte[] responseBytes);

    /// <summary>
    /// Represents the desired configuration parameters for the WebRequestManager.
    /// </summary>
    public class WebRequestManagerConfig {
      /// <summary>
      /// The API key to use (mandatory).
      /// </summary>
      public string apiKey;

      /// <summary>
      /// Whether or not to use caching for web requests (recommended).
      /// </summary>
      public bool cacheEnabled = true;

      /// <summary>
      /// Maximum cache size, in megabytes.
      /// </summary>
      public int maxCacheSizeMb = 1024;

      /// <summary>
      /// Maximum number of cache entries.
      /// </summary>
      public int maxCacheEntries = 4096;

      /// <summary>
      /// If not null, this is the path that will be used to store the cache.
      /// If null, the default path will be used.
      /// </summary>
      public string cachePathOverride = null;

      public WebRequestManagerConfig(string apiKey) {
        this.apiKey = apiKey;
      }
    }

    /// <summary>
    /// Represents a pending request that we have in the queue.
    /// </summary>
    private class PendingRequest {
      /// <summary>
      /// The creation callback. When this request's turn arrives, we will call this to create the UnityWebRequest.
      /// </summary>
      public CreationCallback creationCallback;
      /// <summary>
      /// Completion callback. We will call this when the web request completes.
      /// </summary>
      public CompletionCallback completionCallback;
      /// <summary>
      /// Maximum age of the cached copy, in milliseconds.
      /// NO_CACHE means we will not use the cache.
      /// ANY_AGE means any age is OK.
      /// </summary>
      public long maxAgeMillis;

      public PendingRequest(CreationCallback creationCallback, CompletionCallback completionCallback,
          long maxAgeMillis) {
        this.creationCallback = creationCallback;
        this.completionCallback = completionCallback;
        this.maxAgeMillis = maxAgeMillis;
      }
    }

    /// <summary>
    /// Holds buffers for an active web request. Each concurrent active web request must own its own BufferHolder,
    /// which is where it stores data. Web requests are implemented as coroutines, so this is the same as saying
    /// that each of our active coroutines owns one BufferHolder.
    /// </summary>
    private class BufferHolder {
      // Temporary buffer used by Unity to transfer data to us.
      public byte[] tempBuffer = new byte[TEMP_BUFFER_SIZE];
      // Permanent buffer in which we accumulate data as we receive.
      public byte[] dataBuffer = new byte[DATA_BUFFER_INIT_SIZE];
    }

    /// <summary>
    /// Requests that are pending execution. This is a concurrent queue because requests may come in from any
    /// thread. Requests are serviced on the main thread.
    /// </summary>
    private ConcurrentQueue<PendingRequest> pendingRequests = new ConcurrentQueue<PendingRequest>();

    /// <summary>
    /// List of BufferHolders that are idle (not being used by any download coroutine).
    /// BufferHolders are returned to this list when coroutines finish.
    /// </summary>
    private List<BufferHolder> idleBuffers = new List<BufferHolder>();

    /// <summary>
    /// Cache for web responses.
    /// </summary>
    private PersistentBlobCache cache;

    public void Setup(WebRequestManagerConfig config) {
      // Create all the buffer holders. They are all initially idle.
      for (int i = 0; i < MAX_CONCURRENT_DOWNLOADS; i++) {
        idleBuffers.Add(new BufferHolder());
      }

      if (config.cacheEnabled) {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string defaultCachePath = Path.Combine(Path.Combine(Path.Combine(
          appDataPath, Application.companyName), Application.productName), "WebRequestCache");

        string cachePath = config.cachePathOverride ?? defaultCachePath;
        // Note: Directory.CreateDirectory creates all directories in the path.
        Directory.CreateDirectory(cachePath);

        cache = gameObject.AddComponent<PersistentBlobCache>();
        cache.Setup(cachePath, config.maxCacheEntries, config.maxCacheSizeMb * 1024 * 1024);
      }
    }

    /// <summary>
    /// Enqueues a request. Can be called from any thread.
    /// </summary>
    /// <param name="creationCallback">The callback that creates the UnityWebRequest. This callback will
    /// be called when the request is ready to be serviced.</param>
    /// <param name="completionCallback">The callback to call when the request is complete. Will be called
    /// when the request completes.</param>
    /// <param name="maxAgeMillis">Indicates the cache strategy. If this is NO_CACHE, the cache will
    /// not be used, if it's a positive value, it indicates what is the maximum age of the cached
    /// copy that is considered acceptable. If it's ANY_AGE, any cached copy regardless of age
    /// will be considered acceptable.</param>
    public void EnqueueRequest(CreationCallback creationCallback, CompletionCallback completionCallback,
        long maxAgeMillis = CACHE_ANY_AGE) {
      // Your call is very important to us.
      // Please stay on the line and your request will be handled by the next available operator.
      pendingRequests.Enqueue(new PendingRequest(creationCallback, completionCallback, maxAgeMillis));

      // If we are running in the editor, we don't have an update loop, so we have to manually
      // start pending requests here.
      if (!Application.isPlaying) {
        StartPendingRequests();
      }
    }

    /// <summary>
    /// Clears the local web cache. This is asynchronous (the cache will be cleared in the background).
    /// </summary>
    public void ClearCache() {
      if (cache != null) {
        cache.RequestClear();
      }
    }

    private void Update() {
      StartPendingRequests();
    }

    private void StartPendingRequests() {
      // Start pending web requests if we have idle buffers.
      PendingRequest pendingRequest;
      while (idleBuffers.Count > 0 && pendingRequests.Dequeue(out pendingRequest)) {
        // Service the request.
        // Fetch an idle BufferHolder. We will own that BufferHolder for the duration of the coroutine.
        BufferHolder bufferHolder = idleBuffers[idleBuffers.Count - 1];
        // Remove it from the idle list because it's now in use. It will be returned to the pool
        // by HandleWebRequest, when it's done with it.
        idleBuffers.RemoveAt(idleBuffers.Count - 1);
        // Start the coroutine that will handle this web request. When the coroutine is done,
        // it will return the buffer to the pool.
        StartCoroutine(HandleWebRequest(pendingRequest, bufferHolder));
      }
    }

    /// <summary>
    /// Co-routine that services one PendingRequest. This method must be called with StartCoroutine.
    /// </summary>
    /// <param name="request">The request to service.</param>
    private IEnumerator HandleWebRequest(PendingRequest request, BufferHolder bufferHolder) {
      // NOTE: This method runs on the main thread, but never blocks -- the blocking part of the work is
      // done by yielding the UnityWebRequest, which releases the main thread for other tasks while we
      // are waiting for the web request to complete (by the miracle of coroutines).

      // Let the caller create the UnityWebRequest, configuring it as they want. The caller can set the URL,
      // method, headers, anything they want. The only thing they can't do is call Send(), as we're in charge
      // of doing that.
      UnityWebRequest webRequest = request.creationCallback();

      bool cacheAllowed = cache != null && webRequest.method == "GET" && request.maxAgeMillis != CACHE_NONE;

      // Check the cache (if it's a GET request and cache is enabled).
      if (cacheAllowed) {
        bool cacheHit = false;
        byte[] cacheData = null;
        bool cacheReadDone = false;
        cache.RequestRead(webRequest.url, request.maxAgeMillis, (bool success, byte[] data) => {
          cacheHit = success;
          cacheData = data;
          cacheReadDone = true;
        });
        while (!cacheReadDone) {
          yield return null;
        }
        if (cacheHit) {
          request.completionCallback(/* success */ true, /* responseCode */ 200, cacheData);

          // Return the buffer to the pool for reuse.
          CleanUpAfterWebRequest(bufferHolder);

          yield break;
        }
      }

      // Use a download handler with our preallocated buffers for the request (this is what avoids the
      // allocation of numerous tiny buffers).
      // Note that we can't re-use the CustomDownloadHandler objects, as Unity doesn't like it when we do,
      // DownloadHandlers must be fresh for every new request. But that's ok because the bulk of the garbage
      // is the buffers, and we're not re-allocating those.
      CustomDownloadHandler handler = new CustomDownloadHandler(bufferHolder);
      webRequest.downloadHandler = handler;

      // We need to asset that we actually succeeded in setting the download handler, because this can fail
      // if, for example, the creation callback mistakenly called Send(), or if we (because of an unthinkable
      // programming error -- the horror!) are trying to use a disposed CustomDownloadHandler.
      AssertOrThrow.True(webRequest.downloadHandler == handler,
        "Couldn't set download handler. It's either disposed of, or the creation callback mistakenly called Send().");

      // Start the web request. This will suspend this coroutine until the request is done.
      yield return webRequest.Send();

      // Request is finished. Call user-supplied callback.
      request.completionCallback(!webRequest.isNetworkError, (int)webRequest.responseCode, webRequest.downloadHandler.data);

      // Cache the result, if applicable.
      if (!webRequest.isNetworkError && cacheAllowed) {
        byte[] data = webRequest.downloadHandler.data;
        if (data != null && data.Length > 0) {
          byte[] copy = new byte[data.Length];
          Buffer.BlockCopy(data, 0, copy, 0, data.Length);
          cache.RequestWrite(webRequest.url, copy);
        }
      }

      // Clean up.
      webRequest.Dispose();
      handler.CleanUpAndDispose();
      CleanUpAfterWebRequest(bufferHolder);
    }

    private void CleanUpAfterWebRequest(BufferHolder bufferHolder) {
      // Return the buffer to the pool for reuse.
      idleBuffers.Add(bufferHolder);

      // If we are running in the editor, we don't have an update loop, so we have to manually
      // start pending requests here.
      if (!Application.isPlaying) {
        StartPendingRequests();
      }
    }

    /// <summary>
    /// Our custom download handler that uses pre-allocated buffers to reduce garbage collection.
    /// As recommended in:
    /// https://docs.unity3d.com/540/Documentation/Manual/UnityWebRequest.html
    /// </summary>
    private class CustomDownloadHandler : DownloadHandlerScript {
      /// <summary>
      /// Indicates if the download is complete.
      /// </summary>
      private bool downloadComplete;

      /// <summary>
      /// Data buffer, where we store the data we received so far. Not all of this buffer is valid
      /// data. It might be larger than the data contained in it. The dataLength variable indicates
      /// what part of it is valid.
      /// </summary>
      private BufferHolder bufferHolder;

      /// <summary>
      /// Indicates the length of valid data in the dataBuffer buffer.
      /// </summary>
      private int dataLength;

      /// <summary>
      /// The data in the buffer, decoded as UTF8 text.
      /// This is a lazy cache -- we fill it in when we convert the data to text and keep it around for further
      /// requests.
      /// IMPORTANT: we can't use the variable name "text" here because it hides the "text" property of the
      /// base class (which in turn calls GetText()) and leads to subtle bugs.
      /// </summary>
      private string cachedText;

      /// <summary>
      /// True if this instance was disposed of and can no longer be used.
      /// </summary>
      private bool isDisposed;

      /// <summary>
      /// Create a new PreallocatedDownloadBuffer that will own and use the given BufferHolder.
      /// </summary>
      /// <param name="bufferHolder">The BufferHolder to own and use. </param>
      public CustomDownloadHandler(BufferHolder bufferHolder) : base(bufferHolder.tempBuffer) {
        this.bufferHolder = bufferHolder;
      }

      /// <summary>
      /// Returns a copy of the downloaded data.
      /// Can only be called when the download is complete.
      /// </summary>
      /// <returns>The downloaded data. The caller is the owner of the buffer, as it's a copy of this
      /// instance's internal state.</returns>
      protected override byte[] GetData() {
        // NOTE: we have to make a copy of the buffer because the caller assumes that they own the buffer and can
        // do whatever they want with it. In particular, the caller can (and does, in our code) ship the buffer to
        // a background thread and then calls Reset() on this object to use it to download something else.
        AssertOrThrow.True(!isDisposed, "This object is disposed.");
        AssertOrThrow.True(downloadComplete != null, "Data not ready to be read. Download needs to be completed first.");
        byte[] result = new byte[dataLength];
        Buffer.BlockCopy(/* src */ bufferHolder.dataBuffer, /* srcOffset */ 0,
          /* dst */ result, /* dstOffset */ 0, dataLength);
        return result;
      }

      /// <summary>
      /// Returns the downloaded data interpreted as UTF-8 text. This can only be called after the download
      /// is complete, otherwise an exception will be thrown.
      /// </summary>
      /// <returns>The downloaded data as UTF-8 text.</returns>
      protected override string GetText() {
        AssertOrThrow.True(!isDisposed, "This object is disposed.");
        AssertOrThrow.True(downloadComplete != null, "Text not ready to be read. Download needs to be completed first.");
        if (cachedText == null) {
          // Note that we are careful about only using dataBuffer[0..dataLength-1], since the buffer might be
          // larger than the content (particularly if we are reusing the buffer from a previous operation).
          cachedText = System.Text.Encoding.UTF8.GetString(bufferHolder.dataBuffer, 0, dataLength);
        }
        return cachedText;
      }

      /// <summary>
      /// Called by Unity when a new chunk of data is received.
      /// </summary>
      /// <param name="newData">The new data that was received.</param>
      /// <param name="newDataLength">The length of the new data received.</param>
      /// <returns></returns>
      protected override bool ReceiveData(byte[] newData, int newDataLength) {
        AssertOrThrow.True(!isDisposed, "This object is disposed.");
        int capacityNeeded = dataLength + newDataLength;

        // If our buffer capacity will be exceeded, reallocate to fit.
        if (capacityNeeded > bufferHolder.dataBuffer.Length) {
          // Reallocate buffer. Pre-allocate twice the needed capacity in order to cover future needs.
          // This is a standard buffer resizing strategy which ensures that for N inserts, at most O(log N)
          // buffer resizes will be done.
          byte[] newBuffer = new byte[capacityNeeded * 2];
          Buffer.BlockCopy(
            /* src */ bufferHolder.dataBuffer, /* srcOffset */ 0,
            /* dst */ newBuffer, /* dstOffset */ 0,
            /* count */ dataLength);
          bufferHolder.dataBuffer = newBuffer;
        }

        // Append the new data to our current data.
        Buffer.BlockCopy(
            /* src */ newData, /* srcOffset */ 0,
            /* dst */ bufferHolder.dataBuffer, /* dstOffset */ dataLength,
            /* count */ newDataLength);
        dataLength += newDataLength;
        return true;
      }

      /// <summary>
      /// Called by Unity when the download is complete.
      /// </summary>
      protected override void CompleteContent() {
        AssertOrThrow.True(!isDisposed, "This object is disposed.");
        downloadComplete = true;
      }

      public void CleanUpAndDispose() {
        Dispose();
        isDisposed = true;
        bufferHolder = null;
      }
    }
  }
}