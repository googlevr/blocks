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
  public class OctreeImpl<T> : CollisionSystem<T> {
    // Number of items stored in a Node before we decide to split that node.
    private static readonly int SPLIT_SIZE = 10;
    // Max depth of the tree.  Since the tree divides the axis by two at
    // each level, the size of the smallest node is initial_size/2^MAX_DEPTH.
    // 10 is a reasonable default.  Can be an Octree param if needed.
    private static readonly int MAX_DEPTH = 10;
    private readonly Bounds bounds;
    private readonly OTNode root;
    private readonly Dictionary<T, Bounds> itemBounds =
      new Dictionary<T, Bounds>();
    private readonly Dictionary<T, OTNode> itemNode =
      new Dictionary<T, OTNode>();

    /// <summary>
    ///  Create an empty Octree.
    /// </summary>
    /// <param name="bounds">Bounds of the Octree.
    ///   All items added to this index must be within these bounds.
    /// </param>
    public OctreeImpl(Bounds bounds) {
      this.bounds = bounds;
      root = new OTNode(this, bounds, 0 /* Depth */);
    }

    /// <summary>
    ///   Add an item to the Octree.
    /// </summary>
    /// <param name="item">The item to add</param>
    /// <param name="bounds">The item's initial bounds.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when bounds is not contained by the Octree's bounds.</exception>
    public void Add(T item, Bounds bounds) {
      AssertOrThrow.False(itemBounds.ContainsKey(item),
        "Cannot re-add item using the same key. Use Update to change an item's bounds.");
      itemBounds[item] = bounds;
      OTNode node = root.Add(item, bounds);
      itemNode[item] = node;
    }

    /// <summary>
    ///   Update the bounds of an item.  The item must already exist
    ///   in the index.
    /// </summary>
    /// <param name="item">The item to update.</param>
    /// <param name="bounds">The item's updated bounds.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when the item isn't in the tree.</exception>
    public void UpdateItemBounds(T item, Bounds bounds) {
      OTNode oldNode = itemNode[item];
      oldNode.Remove(item);
      itemBounds[item] = bounds;
      itemNode[item] = root.Add(item, bounds);
    }

    /// <summary>
    ///   Remove an item from the index.
    /// </summary>
    /// <param name="item">Item to remove.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when the item isn't in the tree.</exception>
    public void Remove(T item) {
      AssertOrThrow.True(itemNode.ContainsKey(item),
        "Item is specified for removal but is not in the tree.");
      OTNode oldNode = itemNode[item];
      oldNode.Remove(item);
      itemNode.Remove(item);
      itemBounds.Remove(item);
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
        int limit = SpatialIndex.MAX_INTERSECT_RESULTS) {
      items = null;
      return root.ContainedBy(bounds, ref items, limit);
    }

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
    public bool IntersectedBy(Bounds bounds, out HashSet<T> items,
        int limit = SpatialIndex.MAX_INTERSECT_RESULTS) {
      items = null;
      return root.IntersectedBy(bounds, ref items, limit);
    }

    /// <summary>
    ///   True if the given item is in the tree.
    /// </summary>
    public bool HasItem(T item) {
      return itemNode.ContainsKey(item);
    }

    // Return the bounds specified when the item was inserted or updated.
    public Bounds BoundsForItem(T item) {
      return itemBounds[item];
    }

    private OTNode NodeForItem(T item) {
      return itemNode[item];
    }

    private void UpdateItemNode(T item, OTNode node) {
      itemNode[item] = node;
    }
    
    /// <summary>
    /// Checks whether the supplied Bounds intersects anything in the system, and fills the supplied preallocated Hashset
    /// with intersected items.  Returns true if there were any intersections.
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="items"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    public bool IntersectedByPreallocated(Bounds bounds, ref HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS) {
      return true;
    }

    // Public for testing.
    public static Bounds SubBounds(Bounds parent, int idx) {
      Vector3 childSize = parent.size / 2.0f;
      Vector3 extents = parent.extents / 2.0f;
      Vector3 center = parent.center;
      Vector3 childCenter = new Vector3(
        (idx & 1) > 0 ? center.x - extents.x : center.x + extents.x,
        (idx & 2) > 0 ? center.y - extents.y : center.y + extents.y,
        (idx & 4) > 0 ? center.z - extents.z : center.z + extents.z);
      return new Bounds(childCenter, childSize);
    }

    // VisibleForTesting
    public OTNode GetRootNode() {
      return root;
    }

    // VisibleForTesting
    public Bounds GetBounds() {
      return bounds;
    }


    // Tree structure to contain items.
    public class OTNode {
      private readonly int depth;
      private readonly OctreeImpl<T> tree;
      private readonly Bounds bounds;
      private HashSet<T> items = new HashSet<T>();
      private OTNode[] childNodes = null;

      internal OTNode(OctreeImpl<T> tree, Bounds bounds, int depth) {
        this.tree = tree;
        this.bounds = bounds;
        this.depth = depth;
      }

      internal OTNode Add(T item, Bounds itemBounds) {
        AssertOrThrow.True(Math3d.ContainsBounds(bounds, itemBounds),
          "Item has bounds outside of tree bounds");
        if (childNodes == null) {
          if (items.Count >= SPLIT_SIZE && depth < MAX_DEPTH) {
            SplitNode();
            // Recursively re-call this function, post-split
            return Add(item, itemBounds);
          } else {
            items.Add(item);
            return this;
          }
        } else {
          for (int i = 0; i < 8; i++) {
            if (Math3d.ContainsBounds(SubBounds(bounds, i), itemBounds)) {
              if (childNodes[i] == null) {
                childNodes[i] = new OTNode(
                  tree, SubBounds(bounds, i), depth + 1);
              }
              return childNodes[i].Add(item, itemBounds);
            }
          }
          // Wasn't bounded by any children, add it locally.
          items.Add(item);
          return this;
        }
      }

      internal void SplitNode() {
        childNodes = new OTNode[8];
        // Take all local items, remove them, then re-add them.
        HashSet<T> toAdd = items;
        items = new HashSet<T>();
        foreach (T item in toAdd) {
          OTNode addedTo = Add(item, tree.BoundsForItem(item));
          tree.UpdateItemNode(item, addedTo);
        }
      }

      internal void Remove(T item) {
        AssertOrThrow.True(items.Remove(item),
          "Item is specified for removal but is not in the tree.");
      }

      // Add items to the set from here and below within the tree.
      // The resulting set is created on-demand.  Returns 'true'
      // if items match the query.
      internal bool ContainedBy(
          Bounds container, ref HashSet<T> contained, int limit) {
        bool foundItems = false;

        foreach (T item in items) {
          if (contained != null && contained.Count >= limit) {
            return true;
          }
          if (Math3d.ContainsBounds(container, tree.BoundsForItem(item))) {
            EnsureSet(ref contained);
            foundItems = true;
            contained.Add(item);
          }
        }

        if (childNodes != null) {
          for (int i = 0; i < 8; i++) {
            if (childNodes[i] != null
                && childNodes[i].bounds.Intersects(container)) {
              foundItems = childNodes[i].ContainedBy(
                  container, ref contained, limit)
                || foundItems;
            }
          }
        }

        return foundItems;
      }

      // Add items to the set from here and below within the tree.
      // The resulting set is created on-demand.  Returns 'true'
      // if items match the query.
      internal bool IntersectedBy(
          Bounds intersectBounds, ref HashSet<T> intersected, int limit) {
        bool foundItems = false;

        foreach (T item in items) { 
          if (intersected != null && intersected.Count >= limit) {
            return true;
          }
          if (tree.BoundsForItem(item).Intersects(intersectBounds)) {
            EnsureSet(ref intersected);
            foundItems = true;
            intersected.Add(item);
          }
        }

        if (childNodes != null) {
          for (int i = 0; i < 8; i++) {
            if (childNodes[i] != null
                && childNodes[i].bounds.Intersects(intersectBounds)) {
              foundItems = childNodes[i].IntersectedBy(
                  intersectBounds, ref intersected, limit)
                || foundItems;
            }
          }
        }
        return foundItems;
      }

      private void EnsureSet(ref HashSet<T> set) {
        set = set != null ? set : new HashSet<T>();
      }

      // VisibleForTesting
      public OTNode[] GetChildNodes() {
        return childNodes;
      }
    }
  }
}
