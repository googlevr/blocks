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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.menu;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.api_clients.assets_service_client;
using com.google.apps.peltzer.client.model.export;
using System.IO;
using com.google.apps.peltzer.client.entitlement;

namespace com.google.apps.peltzer.client.zandria
{
    public class Creation
    {
        public int index;
        /// <summary>
        ///   The entry for this creation. The entry will change if an existing entry fails to load.
        /// </summary>
        public Entry entry;
        /// <summary>
        /// The preview displayed on the PolyMenu. This is instantiated from a creation prefab.
        /// </summary>
        public GameObject preview;
        /// <summary>
        /// The thumbnail game object. We keep a reference to the game object and not just the sprite so we can easily
        /// activate and deactivate it.
        /// </summary>
        public GameObject thumbnail;
        /// <summary>
        /// The thumbnail to show when the creation fails to load.
        /// </summary>
        public GameObject errorThumbnail;
        /// <summary>
        /// The actual sprite for the thumbnail.
        /// </summary>
        public Sprite thumbnailSprite;
        /// <summary>
        /// The handler script for the creation that handles converting queries into usuable information.
        /// </summary>
        public ZandriaCreationHandler handler;
        /// <summary>
        /// Whether this is a locally-available creation, rather than a cloud-saved creation.
        /// </summary>
        public bool isLocal;
        /// <summary>
        /// Whether this is a creation that was saved in this session.
        /// </summary>
        public bool isSave;

        public Creation(int index, Entry entry, GameObject creationPrefab, bool isLocal, bool isSave)
        {
            this.index = index;
            this.entry = entry;
            preview = creationPrefab;
            thumbnail = creationPrefab.transform.Find("CreationPreview").gameObject.transform.Find("Thumbnail").gameObject;
            errorThumbnail =
              creationPrefab.transform.Find("CreationPreview").gameObject.transform.Find("Error_Thumbnail").gameObject;
            errorThumbnail.SetActive(false);
            preview.GetComponent<SelectableDetailsMenuItem>().creation = this;
            handler = preview.GetComponent<ZandriaCreationHandler>();
            this.isLocal = isLocal;
            this.isSave = isSave;
        }

        internal void SetThumbnailSprite(Sprite thumbnailSprite)
        {
            this.thumbnailSprite = thumbnailSprite;
            thumbnail.GetComponent<SpriteRenderer>().sprite = thumbnailSprite;
        }
    }

    public class Entry
    {
        /// <summary>
        ///   The actual entry that is used to query the store for the model.
        /// </summary>
        public ObjectStoreEntry queryEntry;
        /// <summary>
        ///   A reference to the current load status of this entry. The status is used to indicate whether we have ever
        ///   tried to load this entry so we can find a "fresh" entry to load when we need a new creation.
        /// </summary>
        public ZandriaCreationsManager.LoadStatus loadStatus;

        public Entry(ObjectStoreEntry queryEntry)
        {
            this.queryEntry = queryEntry;
            // On creation set loadStatus to be None. This will indicate that the entry has never been loaded.
            loadStatus = ZandriaCreationsManager.LoadStatus.NONE;
        }
    }

    public class Load
    {
        /// <summary>
        ///   A reference to the list of creation entry metadata for entries waiting to be loaded. This list contains
        ///   the metadata for every creation we can load and is the starting point for loading creations onto the
        ///   PolyMenu. As we attempt to load entries they are removed from this list and attached to a creation.
        /// </summary>
        public List<Entry> entries;
        /// <summary>
        ///   The creations for this load. These are in chronological order.
        /// </summary>
        public List<Creation> creations;
        /// <summary>
        ///  The prefab for a creation that has the correct scripts and size to be attached to the PolyMenu.
        /// </summary>
        public GameObject creationPrefab;
        /// <summary>
        ///   A reference to the load requests we should make during the next Update loop. The index refers to the index of
        ///   a creation in creations that needs to be loaded.
        ///
        ///   Requests are only appended to the list when: An initial call to StartLoad() is called where we make
        ///   NUMBER_OF_CREATIONS_PER_PAGE load requests, an existing load request fails and needs to replace itself, there
        ///   aren't enough loaded previews for a next page request but there are still entries whose status is
        ///   LoadStatus.NONE.
        /// </summary>
        public List<int> pendingModelLoadRequestIndices;
        public List<int> pendingThumbnailLoadRequestIndices;
        /// <summary>
        ///   A reference to the number of root load requests made for a given type. A root load request is an intial
        ///   request made at the start of a load or because of a page request. A load request which is generated as a
        ///   resulting load failure does not count as a root load. totalRootLoads tells us how many pages we are currently
        ///   trying to actively populate.
        /// </summary>
        public int totalRootLoadRequests;
        /// <summary>
        ///   The total number of entries queried for this load.
        /// </summary>
        public int totalNumberOfEntries;
        /// <summary>
        ///  The number of pages we are actively trying to load, we might not actually have enough models to fill all these
        ///  pages.
        /// </summary>
        public int activePageCount;
        /// <summary>
        ///  The total number of pages for this load. This is either the max number of pages allowed or the number of pages
        ///  that can be filled given the number of creations for this load.
        /// </summary>
        public int numberOfPages;

        public Load(GameObject creationPrefab)
        {
            entries = new List<Entry>();
            totalNumberOfEntries = 0;
            this.creationPrefab = creationPrefab;
            creations = new List<Creation>();
            pendingModelLoadRequestIndices = new List<int>();
            pendingThumbnailLoadRequestIndices = new List<int>();
            totalRootLoadRequests = 0;
            numberOfPages = 0;
            activePageCount =
              ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE * ZandriaCreationsManager.NUMBER_OF_PAGES_AT_START;
        }

        /// <summary>
        ///   Takes an entry add forces it to the front of the menu. This is typically used when a user saves and we want
        ///   to maintain the chronological ordering of the menu.
        /// </summary>
        /// <param name="entry">The entry to be parsed into a creation and loaded onto the menu.</param>
        public void AddEntryToStartOfMenu(Entry entry, bool isLocal, bool isSave)
        {
            // Update the total number of entries for this load.
            totalNumberOfEntries += 1;

            // Setup the creation.
            // Create a copy of the prefab for a creation that has the correct size and scripts to be attached to the menu.
            GameObject zandriaCreationHolder = GameObject.Instantiate(creationPrefab);
            // Hide the creation until it's attached to the menu.
            zandriaCreationHolder.SetActive(false);
            // Create a creation from the prefab and the next entry to be loaded.
            creations.Insert(0, (new Creation(0, entry, zandriaCreationHolder, isLocal, isSave)));
            // The following collections index into creations. Given that we've prepended to creations, we must increment
            // each index into creations.
            for (int i = 0; i < pendingModelLoadRequestIndices.Count; i++)
            {
                pendingModelLoadRequestIndices[i] = pendingModelLoadRequestIndices[i] + 1;
            }
            for (int i = 0; i < pendingThumbnailLoadRequestIndices.Count; i++)
            {
                pendingThumbnailLoadRequestIndices[i] = pendingThumbnailLoadRequestIndices[i] + 1;
            }

            int maxCreations =
              ZandriaCreationsManager.MAX_NUMBER_OF_PAGES * ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE;

            // We are going to load this creation no matter what to the front of the list. But that might put the number of
            // creations over the limit and we have to cut one from the end of the list.
            if (creations.Count() > maxCreations)
            {
                int removedIndex = creations.Count() - 1;
                Creation removedCreation = creations.Last();

                // Remove the load from pending loads, this will silently fail if its not in there.
                pendingModelLoadRequestIndices.Remove(removedIndex);
                pendingThumbnailLoadRequestIndices.Remove(removedIndex);

                // The worst case is when the creation is in the middle of being loaded. If this is the case destroying the
                // gameObject won't be threadsafe.
                // TODO (bug): Destroy previews when a creation is removed in a threadsafe way by marking creations for
                // deletion and then deleting them on the main thread once they finish loading.
                if (removedCreation.entry.loadStatus == ZandriaCreationsManager.LoadStatus.LOADING_MODEL)
                {
                    // Set the actual preview inactive.
                    removedCreation.preview.SetActive(false);
                    // Remove any markers in the handler that this preview should be active once it's done loading.
                    removedCreation.handler.isActiveOnMenu = false;
                }
                else
                {
                    // It is not loading, we can destroy it in a threadsafe way.
                    GameObject.Destroy(removedCreation.preview);
                }

                // We only ever add one to the front so we can just remove one from the end.
                creations.RemoveAt(creations.Count() - 1);
                totalRootLoadRequests -= 1;
            }

            // Mark the creation to be loaded.
            pendingThumbnailLoadRequestIndices.Add(0);
            // Increment totalRootLoadRequests.
            totalRootLoadRequests += 1;

            int totalPossiblePages =
              (int)Mathf.Ceil((float)totalNumberOfEntries / ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE);
            numberOfPages = Math.Min(ZandriaCreationsManager.MAX_NUMBER_OF_PAGES, totalPossiblePages);
        }

        /// <summary>
        ///   Takes a list of entries and adds them behind any existing entries in the menu. This is typically used for bulk
        ///   calls to Zandria where the entries are already returned in chronological order. Most of the time there won't
        ///   be any existing creations when this method is called, but it's designed to act like there is to handle the case
        ///   where the bulk call has started, the user quickly saves a model and the call to load that model onto the menu
        ///   is faster than the call to get all the other models.
        /// </summary>
        /// <param name="entries">The entries to be parsed into creations and loaded onto the menu.</param>
        public void AddEntriesToEndOfMenu(List<Entry> entries, bool isLocal, bool isSave)
        {
            // Append all of the given entries to the existing entries.
            this.entries.AddRange(entries);
            totalNumberOfEntries += entries.Count();

            // Determine new number of max creations. This is either still the max allowed if the menu was already full or
            // the new entry count.
            int maxCreations =
              Mathf.Min(ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE * ZandriaCreationsManager.MAX_NUMBER_OF_PAGES,
              totalNumberOfEntries);

            // Determine how many creations we are going to add from the list of entries.
            int numCreationsToAdd = maxCreations - creations.Count();
            int startIndex = creations.Count();

            // Instatiate a creation for the number of creations we want to load onto the menu.
            for (int i = 0; i < numCreationsToAdd; i++)
            {
                // Create a copy of the prefab for a creation that has the correct size and scripts to be attached to the menu.
                GameObject zandriaCreationHolder = GameObject.Instantiate(creationPrefab);
                // Hide the creation until it's attached to the menu.
                zandriaCreationHolder.SetActive(false);
                // Create a creation from the prefab and the next entry to be loaded.
                creations.Add(new Creation(startIndex + i, entries.First(), zandriaCreationHolder, isLocal, isSave));

                // Remove the entry from the list of pending entries now that it's associated with a creation.
                entries.RemoveAt(0);
            }

            // Determine how many creations we want to currently be trying to load onto the menu. This isn't necessarily the
            // max number of creations the menu can hold, but the max number given the number of pages we are actively trying
            // to load.
            int maxActiveCreations =
              Mathf.Min(activePageCount * ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE, creations.Count());

            // The number of load requests to make is equal to the difference between the number of requests we have made and
            // the max number of requests we can make.
            int numLoadRequests = maxActiveCreations - totalRootLoadRequests;
            pendingThumbnailLoadRequestIndices.AddRange(Enumerable.Range(startIndex, numLoadRequests));
            totalRootLoadRequests += numLoadRequests;

            int totalPossiblePages =
              (int)Mathf.Ceil((float)totalNumberOfEntries / ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE);
            numberOfPages = Math.Min(ZandriaCreationsManager.MAX_NUMBER_OF_PAGES, totalPossiblePages);
        }
    }

    /// <summary>
    ///   Loads and holds references to all the Zandria creations on the in VR Zandria Menu.
    /// </summary>
    public class ZandriaCreationsManager : MonoBehaviour
    {
        // The loading status of an entry.
        // NONE: The entry exists but we have never tried to load it.
        // LOADING_THUMBNAIL: A load has been started for this entry's thumbnail.
        // LOADING_MODEL: A load has been started for this entry's model.
        // FAILED: At some point in the loading pipeline this load failed and the pipeline was terminated.
        // SUCCESSFUL: This load is finished and a preview of the creation now exists in previewsByType and can be attached
        // to the PolyMenu.
        public enum LoadStatus { NONE, LOADING_THUMBNAIL, LOADING_MODEL, FAILED, SUCCESSFUL }
        public const int NUMBER_OF_CREATIONS_PER_PAGE = 9;
        public const int MAX_NUMBER_OF_PAGES = 10;
        public const int NUMBER_OF_PAGES_AT_START = 2;
        // The PPU for imported thumbnails from Zandria that will be displayed on the menu. Chosen by eyeballing it.
        // More positive numbers will give smaller thumbnails, and vice-versa.
        private const int THUMBNAIL_IMPORT_PIXELS_PER_UNIT = 300;

        // The number of different types of creations. Currently we are support: Your models, featured, liked.
        private const int NUMBER_OF_CREATION_TYPES = 3;

        // We implement polling for the "Featured" and "Liked" sections in order to show any
        // new models that get featured or liked by the user while Blocks is running.
        private const float POLLING_INTERVAL_SECONDS = 8;

        // WARNING: All dictionaries in ZandriaCreationsManager are private because they are not threadsafe. They must be
        // accessed from within ZandriaCreationsManager and they must be locked before access.
        // WARNING AGAIN: DON'T EVEN THINK ABOUT TOUCHING THESE ON A BACKGROUND THREAD OR IN ANYWAY THAT IS NOT THREADSAFE.
        private Dictionary<PolyMenuMain.CreationType, Load> loadsByType;
        // A reference to load requests that have been made but don't exist in loadsByType until the initial query to get
        // all the entries are complete. We'll use this to manage the state of the PolyMenu until the initial query is
        // complete.
        private HashSet<PolyMenuMain.CreationType> pendingLoadsByType;

        private GameObject creationPrefab;
        private readonly object mutex = new object();
        private WorldSpace identityWorldSpace;

        // When we last polled for updates to the menu.
        private float timeLastPolled;

        public AssetsServiceClient assetsServiceClient;

        public void Setup()
        {
            assetsServiceClient = gameObject.AddComponent<AssetsServiceClient>();
            identityWorldSpace = new WorldSpace(PeltzerMain.DEFAULT_BOUNDS);

            lock (mutex)
            {
                // Load an instance of the ZandriaCreationHolder prefab. This will be used to attach each creation to.
                creationPrefab = (GameObject)Resources.Load("Prefabs/ZandriaCreationHolder");

                loadsByType = new Dictionary<PolyMenuMain.CreationType, Load>(NUMBER_OF_CREATION_TYPES);
                pendingLoadsByType = new HashSet<PolyMenuMain.CreationType>();

                StartLoad(PolyMenuMain.CreationType.FEATURED);
                LoadOfflineModels();
            }
        }

        void Update()
        {
            lock (mutex)
            {
                // Queue thumbnails for loading.
                foreach (KeyValuePair<PolyMenuMain.CreationType, Load> pair in loadsByType)
                {
                    for (int j = 0; j < pair.Value.pendingThumbnailLoadRequestIndices.Count(); j++)
                    {
                        int loadIndex = pair.Value.pendingThumbnailLoadRequestIndices[j];
                        // Find the creation we are trying to load.
                        if (loadIndex >= pair.Value.creations.Count) { continue; } // Preventing bug
                        Creation creation = pair.Value.creations[loadIndex];

                        // Update the progress of the load.
                        creation.entry.loadStatus = LoadStatus.LOADING_THUMBNAIL;
                        // Execute the load.
                        StartCoroutine(LoadThumbnailForCreation(creation, pair.Key, pair.Value, loadIndex));
                    }

                    // Clear pendingThumbnailLoadRequestIndices. We have made a load request for every pending request.
                    pair.Value.pendingThumbnailLoadRequestIndices.Clear();
                }

                foreach (KeyValuePair<PolyMenuMain.CreationType, Load> pair in loadsByType)
                {
                    for (int j = 0; j < pair.Value.pendingModelLoadRequestIndices.Count(); j++)
                    {
                        int loadIndex = pair.Value.pendingModelLoadRequestIndices[j];
                        // Find the creation we are trying to load.
                        if (loadIndex >= pair.Value.creations.Count) { continue; } // Preventing bug
                        Creation creation = pair.Value.creations[loadIndex];

                        // Update the progress of the load.
                        creation.entry.loadStatus = LoadStatus.LOADING_MODEL;
                        // Execute the load.
                        LoadModelForCreation(creation, pair.Key);
                    }

                    // Clear pendingModelLoadRequestIndices. We have made a load request for every pending request.
                    pair.Value.pendingModelLoadRequestIndices.Clear();
                }
            }

            // Poll for new featured or liked models periodically if the menu is open.
            // Note: we don't poll the "Your models" section because (1) it's harder to optimize (it's not ordered
            // by modified time) and (2) that flow is already covered in an ad-hoc way: we update the poly menu
            // manually when the user saves a model.
            if (PeltzerMain.Instance.polyMenuMain.PolyMenuIsActive() && Time.time - timeLastPolled > POLLING_INTERVAL_SECONDS)
            {
                if (loadsByType.ContainsKey(PolyMenuMain.CreationType.FEATURED))
                {
                    Poll(PolyMenuMain.CreationType.FEATURED);
                }
                if (loadsByType.ContainsKey(PolyMenuMain.CreationType.LIKED) && OAuth2Identity.Instance.LoggedIn)
                {
                    Poll(PolyMenuMain.CreationType.LIKED);
                }
                timeLastPolled = Time.time;
            }
        }

        /// <summary>
        ///   Starts a load for a given creation type by making an API query to get the metadata for all such creations.
        ///   Then, establishes all the data structures that enable continuous loading and pagination
        ///   for all the creations of this type.
        ///   A sibling method, StartSingleCreationLoad, exists below.
        /// </summary>
        /// <param name="type">The enum type of the load.</param>
        public void StartLoad(PolyMenuMain.CreationType type)
        {
            lock (mutex)
            {
                pendingLoadsByType.Add(type);
            }

            // Start a coroutine which will create a UnityWebRequest and wait for it to send and return with results.
            GetAssetsServiceSearchResults(type,
            delegate (ObjectStoreSearchResult objectStoreResults)
            {
                if (objectStoreResults.results.Length == 0) { return; }

                // We've successfully called back with results from the query. Parse them into actual entries.
                List<Entry> entries = new List<Entry>();
                for (int i = 0; i < objectStoreResults.results.Length; i++)
                {
                    entries.Add(new Entry(objectStoreResults.results[i]));
                }

                lock (mutex)
                {
                    Load load;
                    if (!loadsByType.TryGetValue(type, out load))
                    {
                        load = new Load(creationPrefab);
                        loadsByType.Add(type, load);
                    }

                    load.AddEntriesToEndOfMenu(entries, /* isLocal */ false, /* isSave */ false);

                    // The load has completed and is now managed in loadsByType so we can remove it from pendingLoads.
                    pendingLoadsByType.Remove(type);

                    // Refresh the PolyMenu now that there are creations available.
                    PeltzerMain.Instance.GetPolyMenuMain().RefreshPolyMenu();
                }
            },
            delegate ()
            {
                // The query was not successful.
                lock (mutex)
                {
                    pendingLoadsByType.Remove(type);
                    PeltzerMain.Instance.GetPolyMenuMain().RefreshPolyMenu();
                }
            });
        }

        public void Poll(PolyMenuMain.CreationType type)
        {
            lock (mutex)
            {
                if (pendingLoadsByType.Contains(type)) { return; }
            }

            // Start a coroutine which will create a UnityWebRequest and wait for it to send and return with results.
            GetAssetsServiceSearchResults(type,
            delegate (ObjectStoreSearchResult objectStoreResults)
            {
                if (objectStoreResults.results.Length == 0) { return; }

                // Success! Load the new models onto the front of the menu.
                lock (mutex)
                {
                    Load load;
                    if (!loadsByType.TryGetValue(type, out load))
                    {
                        load = new Load(creationPrefab);
                        loadsByType.Add(type, load);
                    }

                    for (int i = 0; i < objectStoreResults.results.Length; i++)
                    {
                        load.AddEntryToStartOfMenu(new Entry(objectStoreResults.results[i]),
                    /* isLocal */ false, /* isSave */ false);

                        // The load has completed and is now managed in loadsByType so we can remove it from pendingLoads.
                        pendingLoadsByType.Remove(type);
                    }
                }
                // Refresh the PolyMenu now that there are creations available.
                PeltzerMain.Instance.GetPolyMenuMain().RefreshPolyMenu();
            },
            delegate ()
            {
                // The query was not successful. Do nothing.
            });
        }

        /// <summary>
        ///   Load all of the models in the users OfflineModels directory to the PolyMenu.
        /// </summary>
        public void LoadOfflineModels()
        {
            // Most recent first.
            try
            {
                DirectoryInfo offlineModelsDirectory = new DirectoryInfo(PeltzerMain.Instance.offlineModelsPath);
                if (!offlineModelsDirectory.Exists) return;
                List<DirectoryInfo> directories = offlineModelsDirectory.GetDirectories().ToList();

                // Parse them in reverse order such that we add the newest entries to the start of the menu
                // after we add older entries to the start of the menu.
                for (int i = directories.Count() - 1; i >= 0; i--)
                {
                    DirectoryInfo directory = directories[i];
                    ObjectStoreEntry entry;

                    if (GetObjectStoreEntryFromLocalDirectory(directory, out entry))
                    {
                        StartSingleCreationLoad(PolyMenuMain.CreationType.YOUR, entry, /* isLocal */ true, /* isSave */ false);
                    }
                }
            }
            catch (Exception e)
            {
                // We failed to get offline models, the app can continue, but we'll log the issue.
                Debug.Log("Failed to get offline models: " + e);
            }
        }

        /// <summary>
        ///   Delete the given offline model directory.
        /// </summary>
        public void DeleteOfflineModel(string directoryName)
        {
            try
            {
                DirectoryInfo directory = new DirectoryInfo(
                  Path.Combine(PeltzerMain.Instance.offlineModelsPath, directoryName));
                if (!directory.Exists) return;
                directory.Delete(/* recursive */ true);
            }
            catch (Exception)
            {
                // No big harm in a failure.
            }
        }

        public bool GetObjectStoreEntryFromLocalDirectory(DirectoryInfo directory, out ObjectStoreEntry objectStoreEntry)
        {
            objectStoreEntry = new ObjectStoreEntry();
            if (!directory.Exists) return false;

            try
            {
                FileInfo[] thumbnailFiles = directory.GetFiles("thumbnail.png");
                if (thumbnailFiles.Count() == 0)
                {
                    Debug.Log("No thumbnail file found in offline directory " + directory.FullName);
                    return false;
                }

                FileInfo[] blocksFiles = directory.GetFiles("*.blocks");
                if (blocksFiles.Count() == 0)
                {
                    Debug.Log("No .blocks file found in offline directory " + directory.FullName);
                    return false;
                }

                objectStoreEntry.localThumbnailFile = thumbnailFiles[0].FullName;
                objectStoreEntry.localPeltzerFile = blocksFiles[0].FullName;
                objectStoreEntry.localId = directory.Name;

                return true;
            }
            catch (Exception e)
            {
                // We failed to get offline models, the app can continue, but we'll log the issue.
                Debug.Log("Failed to get offline models from a directory: " + e);
                return false;
            }
        }

        /// <summary>
        ///   As above, but for a single creation specified by an object store entry.
        /// </summary>
        public void StartSingleCreationLoad(PolyMenuMain.CreationType type, ObjectStoreEntry objectStoreEntry,
          bool isLocal, bool isSave)
        {
            Entry entry = new Entry(objectStoreEntry);
            lock (mutex)
            {
                pendingLoadsByType.Add(type);
                Load load;
                // We only hit this case if the load was pending at the start of this call, both the single creation and bulk
                // creation load coroutines were operating at the same time but the single creation call completed first.
                // In this case the single creation call needs to create the entry in loadByType.
                if (!loadsByType.TryGetValue(type, out load))
                {
                    load = new Load(creationPrefab);
                    loadsByType.Add(type, load);
                }

                load.AddEntryToStartOfMenu(entry, isLocal, isSave);

                // The load has completed and is now managed in loadsByType so we can remove it from pendingLoads.
                pendingLoadsByType.Remove(type);

                // Refresh the PolyMenu now that there are creations available.
                PeltzerMain.Instance.GetPolyMenuMain().RefreshPolyMenu();
            }
        }

        /// <summary>
        ///   Removes a given creation of the specified type.
        /// </summary>
        public void RemoveSingleCreationAndRefreshMenu(PolyMenuMain.CreationType creationType, string entryIdToRemove)
        {
            lock (mutex)
            {
                Load load = loadsByType[creationType];
                List<Creation> creations = load.creations;
                for (int i = 0; i < creations.Count; i++)
                {
                    Creation creation = creations[i];
                    if ((creation.isLocal && creation.entry.queryEntry.localId == entryIdToRemove) ||
                         !creation.isLocal && creation.entry.queryEntry.id == entryIdToRemove)
                    {
                        creations.RemoveAt(i);
                        PeltzerMain.Instance.GetPolyMenuMain().RefreshPolyMenu();
                        break;
                    }
                }
            }
            return;
        }

        /// <summary>
        ///   Updates a single cloud-saved creation on the 'your models' section.
        /// </summary>
        public void UpdateSingleCloudCreationOnYourModels(string asset)
        {
            PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAssetBackgroundWork(asset,
              delegate (ObjectStoreEntry objectStoreEntry)
              {
                  UpdateSingleCreationOnYourModels(objectStoreEntry, /* isLocal */ false, /* isSave */ true);
              }, /* hackUrls */ true));
        }

        /// <summary>
        ///   Updates a single locally-saved creation on the 'your models' section.
        /// </summary>
        public void UpdateSingleLocalCreationOnYourModels(DirectoryInfo directory)
        {
            ObjectStoreEntry objectStoreEntry;
            if (GetObjectStoreEntryFromLocalDirectory(directory, out objectStoreEntry))
            {
                UpdateSingleCreationOnYourModels(objectStoreEntry, /* isLocal */ true, /* isSave */ true);
            }
        }

        /// <summary>
        ///   Updates a single creation on the 'your models' section.
        /// </summary>
        private void UpdateSingleCreationOnYourModels(ObjectStoreEntry objectStoreEntry, bool isLocal, bool isSave)
        {
            // We've successfully called back with results from the query. Parse them into an actual entry.
            Entry entry = new Entry(objectStoreEntry);

            lock (mutex)
            {
                // We update an asset by deleting it then loading it to the front of the menu.
                Load load = loadsByType[PolyMenuMain.CreationType.YOUR];
                List<Creation> creations = load.creations;
                for (int i = 0; i < creations.Count; i++)
                {
                    Creation creation = creations[i];
                    if ((!creation.isLocal && creations[i].entry.queryEntry.id == entry.queryEntry.id) ||
                      (creation.isLocal && creations[i].entry.queryEntry.localId == entry.queryEntry.localId))
                    {
                        creations.RemoveAt(i);
                        break;
                    }
                }
                load.AddEntryToStartOfMenu(entry, isLocal, isSave);

                // Refresh the PolyMenu now that there are creations available.
                PeltzerMain.Instance.GetPolyMenuMain().RefreshPolyMenu();
            }
        }

        /// <summary>
        ///   Clears a given load. Typically used when a user signs out. We set all the previews visibly inactive but don't
        ///   destroy them. This would cause some serious problems with our threading if a preview we destroyed in the
        ///   middle of being loaded.
        ///
        ///   TODO (bug): Destroy previews when a load is cleared in a threadsafe way by marking creations for
        ///   deletion and then deleting them on the main thread once they finish loading.
        /// </summary>
        /// <param name="type">The type of load.</param>
        public void ClearLoad(PolyMenuMain.CreationType type)
        {
            lock (mutex)
            {
                Load load;
                if (loadsByType.TryGetValue(type, out load))
                {
                    // Hide every non-local preview.
                    for (int i = load.creations.Count() - 1; i >= 0; i--)
                    {
                        Creation creation = load.creations[i];

                        // Set the actual preview inactive.
                        creation.preview.SetActive(false);
                        // Remove any markers in the handler that this preview should be active once it's done loading.
                        creation.handler.isActiveOnMenu = false;
                    }
                }
                pendingLoadsByType.Remove(type);
                loadsByType.Remove(type);

                PeltzerMain.Instance.GetPolyMenuMain().RefreshPolyMenu();
            }
        }

        /// <summary>
        ///   Makes a query to get creations metadata for the given type.
        /// </summary>
        /// <param name="type">The type for the query.</param>
        /// <param name="callback">Callback function on successful query.</param>
        public void GetAssetsServiceSearchResults(PolyMenuMain.CreationType type,
          Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback)
        {
            switch (type)
            {
                case PolyMenuMain.CreationType.FEATURED:
                    assetsServiceClient.GetFeaturedModels(successCallback, failureCallback);
                    break;
                case PolyMenuMain.CreationType.YOUR:
                    assetsServiceClient.GetYourModels(successCallback, failureCallback);
                    break;
                case PolyMenuMain.CreationType.LIKED:
                    assetsServiceClient.GetLikedModels(successCallback, failureCallback);
                    break;
            }
        }

        /// <summary>
        ///   Makes a query to get creations metadata for the given asset.
        /// </summary>
        /// <param name="assetId">An assets service asset id.</param>
        /// <param name="callback">Callback function on successful query.</param>
        public void GetAssetFromAssetsService(string assetId, Action<ObjectStoreEntry> callback)
        {
            assetsServiceClient.GetAsset(assetId, callback);
        }

        /// <summary>
        ///   Takes the metadata for an creation entry and makes the call to get the model and then starts
        ///   background work to create the GameObject preview of the creation that can be loaded to the PolyMenu.
        /// </summary>
        /// <param name="creation">The creation to be loaded.</param>
        /// <param name="type">The type of entry it is.</param>
        public void LoadModelForCreation(Creation creation, PolyMenuMain.CreationType type)
        {
            ObjectStoreEntry entry = creation.entry.queryEntry;
            if (entry == null || (entry.localPeltzerFile == null &&
              (entry.assets == null || (entry.assets.peltzer == null && entry.assets.peltzer_package == null))))
            {
                OnLoadFailure(creation, type);
                return;
            }

            // Setup the handler script from the prefab which is able to load the actual model.
            creation.handler.Setup(entry);

            // Get the raw file data for the entry.
            ObjectStoreClient.GetRawFileData(entry, delegate (byte[] rawFileData)
            {
                // On failure replace this load attempt with another by generating a pending load request.
                if (rawFileData == null)
                {
                    OnLoadFailure(creation, type);
                }

                // On successful return of the raw byte data for the creation start background work and create the preview
                // for the creation.
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new LoadCreationWork(identityWorldSpace, creation.handler, this,
                  creation, rawFileData, entry, type));
            });
        }

        /// <summary>
        ///   Takes the metadata for an creation entry and makes the call to get the thumbnail and then
        ///   assigns it to the gameObject as a placeholder until the model can be loaded.
        /// </summary>
        /// <param name="creation">The creation to be loaded.</param>
        /// <param name="type">The type of entry it is.</param>
        public IEnumerator LoadThumbnailForCreation(Creation creation, PolyMenuMain.CreationType type, Load load,
          int indexInCreations)
        {
            ObjectStoreEntry entry = creation.entry.queryEntry;
            if (entry == null)
            {
                OnLoadFailure(creation, type);
                yield break;
            }
            // No thumbnail, just go ahead and load the model.
            if (entry.thumbnail == null && entry.localThumbnailFile == null)
            {
                load.pendingModelLoadRequestIndices.Add(indexInCreations);
                yield break;
            }
            // We have a thumbnail, fetch it before loading the model.
            GetThumbnailTexture(entry, delegate (Texture2D tex)
            {
                Sprite thumbnailSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                  new Vector2(0.5f, 0.5f), THUMBNAIL_IMPORT_PIXELS_PER_UNIT);
                creation.SetThumbnailSprite(thumbnailSprite);
                load.pendingModelLoadRequestIndices.Add(indexInCreations);
            });
        }

        private void GetThumbnailTexture(ObjectStoreEntry entry,
          System.Action<Texture2D> thumbnailTextureCallback, bool isRecursion = false)
        {
            if (entry.localThumbnailFile != null)
            {
                Texture2D tex = new Texture2D(192, 192);
                tex.LoadImage(File.ReadAllBytes(entry.localThumbnailFile));
                thumbnailTextureCallback(tex);
            }
            else
            {
                UnityWebRequest request = assetsServiceClient.GetRequest(entry.thumbnail, "image/png");
                PeltzerMain.Instance.webRequestManager.EnqueueRequest(
                  () => { return request; },
                  (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
                    ProcessGetThumbnailTexture(success, responseCode, responseBytes, request, entry,
                    thumbnailTextureCallback, isRecursion)));
            }
        }

        // Deals with the response of a GetThumbnailTexture request, retrying it if an auth token was stale.
        private IEnumerator ProcessGetThumbnailTexture(bool success, int responseCode,
          byte[] responseBytes, UnityWebRequest request, ObjectStoreEntry entry,
          System.Action<Texture2D> thumbnailTextureCallback, bool isRecursion = false)
        {
            if (!success || responseCode == 401 || responseBytes.Length == 0)
            {
                if (isRecursion)
                {
                    Debug.Log(AssetsServiceClient.GetDebugString(request, "Error when fetching a thumbnail for " + entry.id));
                    yield break;
                }
                yield return OAuth2Identity.Instance.Reauthorize();
                GetThumbnailTexture(entry, thumbnailTextureCallback, /* isRecursion */ true);
            }
            else
            {
                Texture2D tex = new Texture2D(192, 192);
                tex.LoadImage(responseBytes);
                thumbnailTextureCallback(tex);
            }
        }

        /// <summary>
        ///   Updates the entry's load status safely once it's successfully loaded from an external script.
        /// </summary>
        /// <param name="creation">The creation that was loaded successfully.</param>
        /// <param name="type">The type of entry.</param>
        public void OnLoadSuccess(Creation creation, PolyMenuMain.CreationType type,
          MeshWithMaterialRenderer mwmRenderer)
        {
            lock (mutex)
            {
                // Add the preview to the list of fully loaded and ready to go previews.
                creation.thumbnail.SetActive(false);
                // Update the status of the load to be finished and successful.
                creation.entry.loadStatus = LoadStatus.SUCCESSFUL;
            }

            // If this creation was generated on the menu because the user just saved in app we want to reuse
            // the preview as the savePreview.
            if (creation.isSave)
            {
                PeltzerMain.Instance.savePreview.SetupPreview(mwmRenderer);
                // Make sure that when the user opens the menu again it opens to the model they just saved.
                PeltzerMain.Instance.polyMenuMain.SwitchToYourModelsSection();
            }

            if (type == PolyMenuMain.CreationType.FEATURED)
            {
                PeltzerMain.Instance.menuHint.AddPreview(mwmRenderer);
            }
        }

        /// <summary>
        ///   Updates the entry's load status safely if it fails to load from an external script or internally since there
        ///   are multiple places a load can fail.
        /// </summary>
        /// <param name="creation">The creation that was not successfully loaded.</param>
        /// <param name="type">The type of entry.</param>
        public void OnLoadFailure(Creation creation, PolyMenuMain.CreationType type)
        {
            lock (mutex)
            {
                // Update the status of the load.
                creation.entry.loadStatus = LoadStatus.FAILED;
                creation.errorThumbnail.SetActive(true);
                creation.thumbnail.SetActive(false);
            }
        }

        /// <summary>
        ///   Gets a range of valid previews of a given type to be loaded a page of the PolyMenu. The page number defines
        ///   the range. This will not return the total number of desired previews if 1) There aren't enough previews
        ///   loaded. 2) upToAndIncluding exceeds the total number of possible previews (it's the last page).
        /// </summary>
        /// <param name="type">The type of preview being requested.</param>
        /// <param name="from">The start index of the range of previews.</param>
        /// <param name="upToNotIncluding">The exclusive end index of the range of previews.</param>
        /// <returns>As many valid previews as possible.</returns>
        public List<GameObject> GetPreviews(PolyMenuMain.CreationType type, int from, int upToNotIncluding)
        {
            List<GameObject> previews = new List<GameObject>(upToNotIncluding - from);

            lock (mutex)
            {
                if (loadsByType.ContainsKey(type))
                {
                    // Check to see if this preview request is for previews on our last loaded (loading) page. If it is we want to
                    // send load requests for the next page so we are always one page ahead of the user.
                    if (upToNotIncluding >= loadsByType[type].totalRootLoadRequests)
                    {
                        int startIndexToLoad = loadsByType[type].totalRootLoadRequests;

                        // Either load a full page, or load the remaining creations on this current page.
                        int numberOfLoads =
                          Mathf.Min(NUMBER_OF_CREATIONS_PER_PAGE, loadsByType[type].creations.Count() - startIndexToLoad);

                        // This is a request to load the next page so these requests count as root requests.
                        loadsByType[type].totalRootLoadRequests =
                          loadsByType[type].totalRootLoadRequests + numberOfLoads;

                        // bug: Somehow numberOfLoads is throwing an ArgumentOutOfRangeException because it is being set to
                        // be less than 0. This shouldn't be happening but we can't repro to figure out what is wrong. Adding a
                        // check so that it doesn't throw an error.
                        if (numberOfLoads >= 0)
                        {
                            loadsByType[type].pendingThumbnailLoadRequestIndices.AddRange(
                              Enumerable.Range(startIndexToLoad, numberOfLoads));
                        }
                    }

                    for (int i = from; i < upToNotIncluding && i < loadsByType[type].creations.Count(); i++)
                    {
                        previews.Add(loadsByType[type].creations[i].preview);
                    }
                }
            }

            return previews;
        }

        /// <summary>
        /// Finds the number of pages for a given creation type. This is either the number of pages there are, or the max
        /// allowed number of pages.
        /// </summary>
        /// <param name="type">The creation type.</param>
        /// <returns>The number of pages.</returns>
        public int GetNumberOfPages(PolyMenuMain.CreationType type)
        {
            bool containsType;

            lock (mutex)
            {
                containsType = loadsByType.ContainsKey(type);
            }

            if (!containsType)
            {
                return 1;
            }
            else
            {
                int numPages;

                lock (mutex)
                {
                    numPages = loadsByType[type].numberOfPages;
                }

                return Math.Max(1, numPages);
            }
        }

        /// <summary>
        /// Whether a load is pending and we don't know if it is valid or invalid or if we are sure a load is valid because
        /// it has non-zero entries.
        /// </summary>
        /// <param name="type">The load type.</param>
        /// <returns>Whether the load is pending or valid.</returns>
        public bool HasPendingOrValidLoad(PolyMenuMain.CreationType type)
        {
            Load load;
            bool hasPendingOrValidLoad;

            lock (mutex)
            {
                hasPendingOrValidLoad = pendingLoadsByType.Contains(type)
                || (loadsByType.TryGetValue(type, out load) && load.totalNumberOfEntries > 0);
            }

            return hasPendingOrValidLoad;
        }

        /// <summary>
        /// Whether or not the creations manager has a load for a given creation type.
        /// </summary>
        /// <param name="type">The type of creation.</param>
        /// <returns>Whether or not a load exists.</returns>
        public bool IsLoadingType(PolyMenuMain.CreationType type)
        {
            bool containsType;

            lock (mutex)
            {
                containsType = loadsByType.ContainsKey(type);
            }

            return containsType;
        }

        /// <summary>
        /// Whether or not a load exists for this type and if the load has valid entries to load.
        /// </summary>
        /// <param name="type">The creation type being loaded.</param>
        /// <returns>Whether the load exists and has more than 0 entries to load.</returns>
        public bool HasValidLoad(PolyMenuMain.CreationType type)
        {
            bool hasValidLoad;

            lock (mutex)
            {
                if (loadsByType.ContainsKey(type))
                {
                    hasValidLoad = loadsByType[type].totalNumberOfEntries > 0;
                }
                else
                {
                    hasValidLoad = false;
                }
            }

            return hasValidLoad;
        }
    }

    /// <summary>
    ///   Class for creating BackgroundWork executed in PeltzerMain. This minimizes how much Zandria creation
    ///   loading we do on the MainThread by getting file data on a background thread and then creating the
    ///   creation's preview on the MainThread in PostWork() when the background work is complete.
    /// </summary>
    public class LoadCreationWork : BackgroundWork
    {
        private readonly ZandriaCreationsManager creationsManager;
        private readonly ZandriaCreationHandler creationHandler;
        private readonly byte[] rawFileData;
        private Creation creation;
        private List<MMesh> meshes;
        private WorldSpace identityWorldSpace;
        private bool isValidCreation;
        private PolyMenuMain.CreationType type;
        private float recommendedRotation;

        public LoadCreationWork(WorldSpace identityWorldSpace, ZandriaCreationHandler creationHandler,
          ZandriaCreationsManager creationsManager, Creation creation, byte[] rawFileData,
          ObjectStoreEntry entry, PolyMenuMain.CreationType type)
        {
            this.creationHandler = creationHandler;
            this.creationsManager = creationsManager;
            this.creation = creation;
            this.rawFileData = rawFileData;
            this.identityWorldSpace = identityWorldSpace;
            this.type = type;
        }

        public void BackgroundWork()
        {
            // Get the actual MMeshes from the peltzer file.
            isValidCreation = creationHandler.GetMMeshesFromPeltzerFile(rawFileData,
              delegate (List<MMesh> meshes, float recommendedRotation)
              {
                  this.meshes = meshes;
                  this.recommendedRotation = recommendedRotation;
              });
        }

        public void PostWork()
        {
            if (!isValidCreation)
            {
                creationsManager.OnLoadFailure(creation, type);
                return;
            }

            if (!Features.showPolyMenuModelPreviews)
            {
                creation.entry.loadStatus = ZandriaCreationsManager.LoadStatus.SUCCESSFUL;

                if (creation.isSave)
                {
                    MeshHelper.GameObjectFromMMeshesForMenu(identityWorldSpace, meshes, delegate (GameObject meshPreview)
                    {
                        MeshWithMaterialRenderer mwmRenderer = meshPreview.GetComponent<MeshWithMaterialRenderer>();

                        // Set it to draw to a new layer, such that we don't see shadows for this preview.
                        mwmRenderer.Layer = MeshWithMaterialRenderer.NO_SHADOWS_LAYER;

                        // Reset the transform so that we only use the parent transform.
                        mwmRenderer.ResetTransform();
                        meshPreview.SetActive(false);

                        PeltzerMain.Instance.savePreview.SetupPreview(mwmRenderer);
                        // Make sure that when the user opens the menu again it opens to the model they just saved.
                        PeltzerMain.Instance.polyMenuMain.SwitchToYourModelsSection();
                    });
                }
                else if (type == PolyMenuMain.CreationType.FEATURED && PeltzerMain.Instance.menuHint.IsPopulating())
                {
                    MeshHelper.GameObjectFromMMeshesForMenu(identityWorldSpace, meshes, delegate (GameObject meshPreview)
                    {
                        MeshWithMaterialRenderer mwmRenderer = meshPreview.GetComponent<MeshWithMaterialRenderer>();

                        // Set it to draw to a new layer, such that we don't see shadows for this preview.
                        mwmRenderer.Layer = MeshWithMaterialRenderer.NO_SHADOWS_LAYER;

                        // Reset the transform so that we only use the parent transform.
                        mwmRenderer.ResetTransform();
                        meshPreview.SetActive(false);

                        PeltzerMain.Instance.menuHint.AddPreview(mwmRenderer);
                    });
                }

                return;
            }

            // Get a preview from the MMeshes found on the background thread.
            MeshHelper.GameObjectFromMMeshesForMenu(identityWorldSpace, meshes, delegate (GameObject meshPreview)
            {
                MeshWithMaterialRenderer mwmRenderer = meshPreview.GetComponent<MeshWithMaterialRenderer>();

                // Set it to draw to a new layer, such that we don't see shadows for this preview.
                mwmRenderer.Layer = MeshWithMaterialRenderer.NO_SHADOWS_LAYER;

                // Reset the transform so that we only use the parent transform.
                mwmRenderer.ResetTransform();

                // We have successfully loaded the creation as a preview so we attach it to the menu.
                if (meshPreview != null)
                {
                    //  Parent the preview to the ZandriaCreationHolder.
                    meshPreview.transform.parent = creation.preview.transform.Find("CreationPreview");
                    meshPreview.transform.localPosition = Vector3.zero;
                    Quaternion newRotation = new Quaternion();
                    newRotation.eulerAngles = new Vector3(0, recommendedRotation, 0);
                    meshPreview.transform.localRotation = newRotation;

                    // Hide the preview from the scene. The PolyMenuMain will be responsible for showing the appropriate
                    // previews.
                    creation.preview.SetActive(creationHandler.isActiveOnMenu);

                    // We've successfully loaded a creation. Pass everything back to the creationsManager to keep track of.
                    creationsManager.OnLoadSuccess(creation, type, mwmRenderer);
                }
                else
                {
                    // Make a new load request if the preview returned null.
                    creationsManager.OnLoadFailure(creation, type);
                }
            });
        }
    }
}
