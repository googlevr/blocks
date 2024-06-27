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
using System.Runtime.InteropServices;
using System.Threading;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.util;
using UnityEngine;

/// <summary>
/// Class exposing native functions which implement our collision system.
/// </summary>
public static class NativeSpatialFunction
{
    [DllImport("BlocksNativeLib", EntryPoint = "AllocSpatialPartitioner")]
    public static extern int AllocSpatialPartitioner(Vector3 center, Vector3 size);

    [DllImport("BlocksNativeLib")]
    public static extern void SpatialPartitionerAddItem(int SpatialPartitionerHandle, int itemId,
      Vector3 center, Vector3 extents);

    [DllImport("BlocksNativeLib")]
    public static extern void SpatialPartitionerUpdateItem(int SpatialPartitionerHandle, int itemId,
      Vector3 center, Vector3 extents);

    [DllImport("BlocksNativeLib")]
    public static extern void SpatialPartitionerRemoveItem(int SpatialPartitionerHandle, int itemId);

    [DllImport("BlocksNativeLib")]
    public static extern int SpatialPartitionerContainedBy(int SpatialPartitionerHandle, Vector3 testCenter,
      Vector3 testExtents, int[] returnArray, int returnArrayMaxSize);

    [DllImport("BlocksNativeLib")]
    public static extern int SpatialPartitionerIntersectedBy(int SpatialPartitionerHandle, Vector3 testCenter,
      Vector3 testExtents, int[] returnArray, int returnArrayMaxSize);

    [DllImport("BlocksNativeLib")]
    public static extern int SpatialPartitionerHasItem(int SpatialPartitionerHandle, int itemHandle);
}

/// <summary>
/// Implementation of CollisionSystem that uses brute force implemented in native code.  Even though the
/// algorithmic complexity of this isn't great, we operate on small enough Ns that overhead tends to dominate
/// so implementing this natively is a big win.
/// </summary>
/// <typeparam name="T"></typeparam>
public class NativeSpatial<T> : CollisionSystem<T>
{

    // A unique handle that identifies this collision system to the native code.
    private int spatialPartitionId;
    // Used for super cheap id allocation - we use this id and increment.
    private int nextHandleId = 0;
    // A mapping from the items in this system to their numeric handle used to identify them to native.
    private Dictionary<T, int> itemIds = new Dictionary<T, int>();
    // A mapping of numeric handles to items in the system.
    private Dictionary<int, T> idsToItems = new Dictionary<int, T>();
    // A mapping of items in the system to their Bounds.
    private Dictionary<T, Bounds> itemBounds = new Dictionary<T, Bounds>();
    // A preallocated array for retrieving results from native code.
    private int[] results = new int[SpatialIndex.MAX_INTERSECT_RESULTS];

    // Mutex for controlling concurrent access to this system.
    private Mutex nativeSpatialMutex = new Mutex();

    public NativeSpatial()
    {
        // We call this to ensure that the callback that allows debug statements from native is set up.
        FbxExporter.Setup();
        spatialPartitionId = NativeSpatialFunction.AllocSpatialPartitioner(Vector3.up, Vector3.back);
    }

    /// <summary>
    /// Adds an item to the CollisionSystem.
    /// </summary>
    public void Add(T item, Bounds bounds)
    {
        nativeSpatialMutex.WaitOne();
        int id = nextHandleId++;
        itemIds[item] = id;
        idsToItems[id] = item;
        itemBounds[item] = bounds;
        NativeSpatialFunction.SpatialPartitionerAddItem(spatialPartitionId, id, bounds.center, bounds.extents);
        nativeSpatialMutex.ReleaseMutex();
    }

    /// <summary>
    /// Updates the bounding box of an item already in the collision system.
    /// </summary>
    public void UpdateItemBounds(T item, Bounds bounds)
    {
        nativeSpatialMutex.WaitOne();
        int id = itemIds[item];
        NativeSpatialFunction.SpatialPartitionerUpdateItem(spatialPartitionId, id, bounds.center, bounds.extents);
        nativeSpatialMutex.ReleaseMutex();
    }

    /// <summary>
    /// Remove an item from the system.
    /// </summary>
    /// <param name="item">Item to remove.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when the item isn't in the tree.</exception>
    public void Remove(T item)
    {
        nativeSpatialMutex.WaitOne();
        int id = itemIds[item];
        itemIds.Remove(item);
        idsToItems.Remove(id);
        itemBounds.Remove(item);
        NativeSpatialFunction.SpatialPartitionerRemoveItem(spatialPartitionId, id);
        nativeSpatialMutex.ReleaseMutex();
    }

    /// <summary>
    ///   Find items contained entirely within the given bounds.
    ///   This method will create a set when the number of items
    ///   is greater than zero.
    /// </summary>
    /// <param name="bounds">Containing bounds.</param>
    /// <param name="items">Set of items found.  Null when this
    /// method returns false.</param>
    /// <param name="limit">Maximum number of items to find.</param>
    /// <returns>true if any items are found.</returns>
    public bool ContainedBy(Bounds bounds, out HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS)
    {
        nativeSpatialMutex.WaitOne();
        int numResults = NativeSpatialFunction.SpatialPartitionerContainedBy(spatialPartitionId, bounds.center,
          bounds.extents, results, limit);
        items = new HashSet<T>();
        for (int i = 0; i < numResults; i++)
        {
            items.Add(idsToItems[results[i]]);
        }
        nativeSpatialMutex.ReleaseMutex();
        return items.Count > 0;
    }

    /// <summary>
    /// Returns whether the item is tracked in this system.
    /// </summary>
    public bool HasItem(T item)
    {
        nativeSpatialMutex.WaitOne();
        bool inItems = itemIds.ContainsKey(item);
        nativeSpatialMutex.ReleaseMutex();
        return inItems;
    }

    /// <summary>
    /// Checks whether the supplied Bounds intersects anything in the system, and returns a HashSet
    /// of intersection objects.  Returns true if there were any intersections.
    /// </summary>
    public bool IntersectedBy(Bounds bounds, out HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS)
    {
        nativeSpatialMutex.WaitOne();
        int numResults = NativeSpatialFunction.SpatialPartitionerIntersectedBy(spatialPartitionId, bounds.center,
          bounds.extents, results, limit);
        items = new HashSet<T>();
        for (int i = 0; i < numResults; i++)
        {
            items.Add(idsToItems[results[i]]);
        }
        nativeSpatialMutex.ReleaseMutex();
        return items.Count > 0;
    }

    /// <summary>
    /// Checks whether the supplied Bounds intersects anything in the system, and fills the supplied preallocated Hashset
    /// with intersected items.  Returns true if there were any intersections.
    /// </summary>
    public bool IntersectedByPreallocated(Bounds bounds, ref HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS)
    {
        nativeSpatialMutex.WaitOne();
        int numResults = NativeSpatialFunction.SpatialPartitionerIntersectedBy(spatialPartitionId, bounds.center,
          bounds.extents, results, limit);
        for (int i = 0; i < numResults; i++)
        {
            items.Add(idsToItems[results[i]]);
        }
        nativeSpatialMutex.ReleaseMutex();
        return items.Count > 0;
    }

    public Bounds BoundsForItem(T item)
    {
        return itemBounds[item];
    }
}
