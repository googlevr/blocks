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
using NUnit.Framework;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.util {

  [TestFixture]
  // Tests for Octree
  public class OctreeTest {

    /*[Test]
    public void TestDepthIsZeroForSmallTree() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 10));

      // Initial tree should have 0 depth.
      NUnit.Framework.Assert.AreEqual(0, Depth(tree),
        "Tree should have 0 depth.");

      // Small tree should have 0 depth.
      tree.Add("Foo", new Bounds(new Vector3(1, 2, 3), Vector3.one));
      tree.Add("Bar", new Bounds(new Vector3(2, 1, 3), Vector3.one));
      tree.Add("Baz", new Bounds(new Vector3(3, 2, 1), Vector3.one));

      NUnit.Framework.Assert.AreEqual(0, Depth(tree),
        "Tree should have 0 depth.");
    }

    [Test]
    public void TestSplitNode() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 10));

      // Initial tree should have 0 depth.
      NUnit.Framework.Assert.AreEqual(0, Depth(tree),
        "Tree should have 0 depth.");

      //  Add 15 objects.  Tree should split once.
      for (float f = 0f; f < 1.5f; f += 0.1f) {
        tree.Add("Foo: " + f, new Bounds(new Vector3(f, 2, 3), Vector3.one));
      }

      NUnit.Framework.Assert.AreEqual(1, Depth(tree),
        "Tree should have 1 depth.");
    }

    [Test]
    public void TestInfiniteRecursion() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 10));

      // Placing more than 10 items of point size at the same location could
      // cause infinite recursion.
      for (int i = 0; i < 20; i++) {
        tree.Add("Foo: " + i,
          new Bounds(new Vector3(1.5f, 2.3f, 3.1f), Vector3.zero));
      }
    }

    [Test]
    public void TestContains() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 20));

      tree.Add("Foo", new Bounds(new Vector3(1, 1, 1), Vector3.one * 0.1f));
      tree.Add("Bar", new Bounds(new Vector3(2, 2, 2), Vector3.one * 0.1f));
      tree.Add("NotFound",
        new Bounds(new Vector3(5, 5, 5), Vector3.one * 0.1f));

      HashSet<string> contains;
      NUnit.Framework.Assert.True(
        tree.ContainedBy(new Bounds(Vector3.zero, Vector3.one * 5.0f),
        out contains));
      NUnit.Framework.Assert.AreEqual(2, contains.Count);
      NUnit.Framework.Assert.Contains("Foo", contains);
      NUnit.Framework.Assert.Contains("Bar", contains);

      // Now move one outside of bounds:
      tree.Update("Bar", new Bounds(new Vector3(6, 6, 6), Vector3.one * 0.5f));
      NUnit.Framework.Assert.True(
        tree.ContainedBy(new Bounds(Vector3.zero, Vector3.one * 3.0f),
        out contains));
      NUnit.Framework.Assert.AreEqual(1, contains.Count);
      NUnit.Framework.Assert.Contains("Foo", contains);
    }

    [Test]
    public void TestIntersects() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 20));

      tree.Add("Foo", new Bounds(new Vector3(1, 1, 1), Vector3.one * 2f));
      tree.Add("Bar", new Bounds(new Vector3(2, 2, 2), Vector3.one * 2f));
      tree.Add("Baz", new Bounds(new Vector3(5, 5, 5), Vector3.one * 2f));

      HashSet<string> intersects;
      NUnit.Framework.Assert.True(tree.IntersectedBy(
        new Bounds(Vector3.zero, Vector3.one * 10.0f), out intersects));
      NUnit.Framework.Assert.AreEqual(3, intersects.Count);
      NUnit.Framework.Assert.Contains("Foo", intersects);
      NUnit.Framework.Assert.Contains("Bar", intersects);
      NUnit.Framework.Assert.Contains("Baz", intersects);

      NUnit.Framework.Assert.True(tree.IntersectedBy(
        new Bounds(Vector3.zero, Vector3.one * 4.0f), out intersects));
      NUnit.Framework.Assert.AreEqual(2, intersects.Count);
      NUnit.Framework.Assert.Contains("Foo", intersects);
      NUnit.Framework.Assert.Contains("Bar", intersects);

      // Now move one outside of bounds:
      tree.Update("Baz", new Bounds(new Vector3(6, 6, 6), Vector3.one * 0.5f));
      NUnit.Framework.Assert.True(tree.IntersectedBy(
        new Bounds(Vector3.zero, Vector3.one * 10.0f), out intersects));
      NUnit.Framework.Assert.AreEqual(2, intersects.Count);
      NUnit.Framework.Assert.Contains("Foo", intersects);
      NUnit.Framework.Assert.Contains("Bar", intersects);
    }

    [Test]
    public void TestOutsideOfRange() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 2));

      try {
        tree.Add("Foo", new Bounds(
          new Vector3(10, 10, 10), Vector3.one * 0.1f));
        NUnit.Framework.Assert.True(false, "Expected exception");
      } catch (Exception) {
        // Expected
      }
    }

    [Test]
    public void TestMoveNonExisting() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 2));

      try {
        tree.Update("Foo", new Bounds(Vector3.zero, Vector3.one * 0.1f));
        NUnit.Framework.Assert.True(false, "Expected exception");
      } catch (Exception) {
        // Expected
      }

      // Now add and then remove something and try again
      tree.Add("Foo", new Bounds(Vector3.zero, Vector3.one * 0.1f));
      tree.Remove("Foo");
      try {
        tree.Update("Foo", new Bounds(Vector3.zero, Vector3.one * 0.1f));
        NUnit.Framework.Assert.True(false, "Expected exception");
      } catch (Exception) {
        // Expected
      }
    }

    [Test]
    public void TestDeleteNonExisting() {
      Octree<string> tree = new Octree<string>(
        new Bounds(Vector3.zero, Vector3.one * 2));

      try {
        tree.Remove("Foo");
        NUnit.Framework.Assert.True(false, "Expected exception");
      } catch (Exception) {
        // Expected
      }

      // Now add and then remove something and try again
      tree.Add("Foo", new Bounds(Vector3.zero, Vector3.one * 0.1f));
      tree.Remove("Foo");
      try {
        tree.Remove("Foo");
        NUnit.Framework.Assert.True(false, "Expected exception");
      } catch (Exception) {
        // Expected
      }
    }

    [Test]
    public void TestSubBounds() {
      Bounds root = new Bounds(Vector3.zero, Vector3.one * 20);
      AssertBounds(Octree<string>.SubBounds(root, 0), new Bounds(new Vector3(5, 5, 5), new Vector3(10, 10, 10)));
      AssertBounds(Octree<string>.SubBounds(root, 7), new Bounds(new Vector3(-5, -5, -5), new Vector3(10, 10, 10)));
      AssertBounds(Octree<string>.SubBounds(root, 1), new Bounds(new Vector3(-5, 5, 5), new Vector3(10, 10, 10)));
    }

    private void AssertBounds(Bounds actual, Bounds expected) {
      NUnit.Framework.Assert.IsTrue(Vector3.Distance(actual.center, expected.center) < 0.01f
        && Vector3.Distance(actual.size, expected.size) < 0.01f, "Expected: " + expected + "  was: " + actual);
    }

    private int Depth(Octree<string> tree) {
      Octree<string>.OTNode root = tree.GetRootNode();
      return DepthOfNode(root);
    }

    private int DepthOfNode(Octree<string>.OTNode node) {
      Octree<string>.OTNode[] children = node.GetChildNodes();
      if (children == null) {
        return 0;
      } else {
        int max = 0;
        foreach(Octree<string>.OTNode child in children) {
          if (child != null) {
            max = Math.Max(max, 1 + DepthOfNode(child));
          }
        }
        return max;
      }
    } */
  } 
}
