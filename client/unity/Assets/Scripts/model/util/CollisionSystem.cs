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
using System.Collections.Generic;
using System;
using com.google.apps.peltzer.client.model.core;

namespace com.google.apps.peltzer.client.model.util {

  /// <summary>
  ///   Spatial index for objects based on their Bounds.
  /// </summary>
  public interface CollisionSystem<T> {


    /// <summary>
    ///   Add an item to the CollisionSystem.
    /// </summary>
    /// <param name="item">The item to add</param>
    /// <param name="bounds">The item's initial bounds.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when bounds is not contained by the CollisionSystem's bounds.</exception>
    void Add(T item, Bounds bounds);

    /// <summary>
    ///   Update the bounds of an item.  The item must already exist
    ///   in the index.
    /// </summary>
    /// <param name="item">The item to update.</param>
    /// <param name="bounds">The item's updated bounds.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when the item isn't in the tree.</exception>
    void UpdateItemBounds(T item, Bounds bounds);

    /// <summary>
    ///   Remove an item from the index.
    /// </summary>
    /// <param name="item">Item to remove.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when the item isn't in the tree.</exception>
    void Remove(T item);

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
    bool ContainedBy(Bounds bounds, out HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS);

    /// <summary>
    ///   Find items that intersect the given bounds.
    ///   This method will create a Set when the number of items
    ///   is greater than zero.
    /// </summary>
    /// <param name="bounds">Intersecting bounds.</param>
    /// <param name="items">Set of items found.  Null when this
    /// method returns false.</param>
    /// <param name="limit">Maximum number of items to find.</param>
    /// <returns>true if any items are found.</returns>
    bool IntersectedBy(Bounds bounds, out HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS);
    
    /// <summary>
    ///   Find items that intersect the given bounds.
    ///   This method will create a Set when the number of items
    ///   is greater than zero.
    /// </summary>
    /// <param name="bounds">Intersecting bounds.</param>
    /// <param name="items">Set of items found.  Null when this
    /// method returns false.</param>
    /// <param name="limit">Maximum number of items to find.</param>
    /// <returns>true if any items are found.</returns>
    bool IntersectedByPreallocated(Bounds bounds, ref HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS);

    /// <summary>
    ///   True if the given item is in the tree.
    /// </summary>
    bool HasItem(T item);

    // Return the bounds specified when the item was inserted or updated.
    Bounds BoundsForItem(T item);
  }
}
