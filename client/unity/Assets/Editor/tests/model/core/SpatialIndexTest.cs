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
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {
  [TestFixture]
  public class SpatialIndexTest {
    [Test]
    public void TestFindVertex () {
      int centerId = 1;
      int farAwayId = 2;
      MMesh cubeAtCenter = Primitives.AxisAlignedBox(
        centerId, Vector3.zero, /* radius */ Vector3.one, /* material */ 2);
      MMesh cubeFarAway = Primitives.AxisAlignedBox(
        farAwayId, Vector3.one * 3, /* radius */ Vector3.one, /* material */ 2);
      SpatialIndex index = new SpatialIndex(new Bounds(Vector3.zero, Vector3.one * 10));

      // Add a cube at the center and another further away.
      index.AddMesh(cubeAtCenter);
      index.AddMesh(cubeFarAway);

      // Seach for vertices within 1.5 meters from *near* center:
      Vector3 searchLoc = new Vector3(0.1f, 0.2f, 0.3f);
      List<DistancePair<VertexKey>> vertices;
      NUnit.Framework.Assert.IsTrue(index.FindVerticesClosestTo(searchLoc, 3f, out vertices));

      // Should have all vertices of the center cube, none of the other:
      NUnit.Framework.Assert.AreEqual(8, vertices.Count);
      
      foreach(DistancePair<VertexKey> vertex in vertices) {
        NUnit.Framework.Assert.AreEqual(vertex.value.meshId, centerId);
      }

      // Should be sorted by distance from searchLoc
      for (int i = 0; i < (vertices.Count - 1); i++) {
        NUnit.Framework.Assert.LessOrEqual(
          Vector3.Distance(searchLoc, cubeAtCenter.VertexPositionInModelCoords(vertices[i].value.vertexId)),
          Vector3.Distance(searchLoc, cubeAtCenter.VertexPositionInModelCoords(vertices[i + 1].value.vertexId)));
      }

      // Remove the center cube and do the same searc.  SHould get an empty result.
      index.RemoveMesh(cubeAtCenter);
      NUnit.Framework.Assert.IsFalse(index.FindVerticesClosestTo(searchLoc, 3f, out vertices));
    }

    [Test]
    public void TestFindFace() {
      // Add two cubes.  One is bigger.  Have one face from each in the same plane with the same center:
      int smallId = 1;
      int largeId = 2;
      MMesh smallCube = Primitives.AxisAlignedBox(
        smallId, new Vector3(1, 0, 0), /* radius */ Vector3.one, /* material */ 2);
      MMesh largeCube = Primitives.AxisAlignedBox(
        largeId, Vector3.zero, /* radius */ Vector3.one * 2, /* material */ 2);
      SpatialIndex index = new SpatialIndex(new Bounds(Vector3.zero, Vector3.one * 10));
      index.AddMesh(smallCube);
      index.AddMesh(largeCube);

      // For a point (practically) on both faces near their ceneter.
      List<DistancePair<FaceKey>> faces;
      NUnit.Framework.Assert.IsTrue(index.FindFacesClosestTo(new Vector3(2.1f, 0, 0), 0.1f, false, out faces));

      // Should be two faces:
      NUnit.Framework.Assert.AreEqual(2, faces.Count);

      // Expect same results when looking for meshes, since we really just look for faces:
      List<DistancePair<int>> meshIds;
      NUnit.Framework.Assert.IsTrue(index.FindMeshesClosestTo(new Vector3(2.1f, 0, 0), 0.1f, out meshIds));
      NUnit.Framework.Assert.AreEqual(2, meshIds.Count);

      // Move away from the center, outside the smaller bounding box:
      NUnit.Framework.Assert.IsTrue(index.FindFacesClosestTo(new Vector3(2.1f, 1.1f, 1.1f), 0.1f, false, out faces));

      // Should only be the larger cube:
      NUnit.Framework.Assert.AreEqual(1, faces.Count);
      NUnit.Framework.Assert.AreEqual(largeId, faces[0].value.meshId);

      // Remove the larger cube and try again.
      index.RemoveMesh(largeCube);
      // Near center:
      NUnit.Framework.Assert.IsTrue(index.FindFacesClosestTo(new Vector3(2.1f, 0, 0), 0.1f, false, out faces));
      // Outside of small cube:
      NUnit.Framework.Assert.IsFalse(index.FindFacesClosestTo(new Vector3(2.1f, 1.1f, 1.1f), 0.1f, false, out faces));
    }

    [Test]
    public void TestFindEdge() {
      // Add two cubes.  One is bigger.  Have one edge on each on the same line.
      int smallId = 1;
      int largeId = 2;
      MMesh smallCube = Primitives.AxisAlignedBox(
        smallId, new Vector3(1, 1, 0), /* radius */ Vector3.one, /* material */ 2);
      MMesh largeCube = Primitives.AxisAlignedBox(
        largeId, Vector3.zero, /* radius */ Vector3.one * 2, /* material */ 2);
      SpatialIndex index = new SpatialIndex(new Bounds(Vector3.zero, Vector3.one * 10));
      index.AddMesh(smallCube);
      index.AddMesh(largeCube);

      // Find edges near a point that is near the overlappinge edges.
      Vector3 point = new Vector3(2.01f, 2.03f, 0.05f);
      List<DistancePair<EdgeKey>> edges;
      NUnit.Framework.Assert.IsTrue(index.FindEdgesClosestTo(point, 0.25f, false, out edges));

      // Should be two:
      NUnit.Framework.Assert.AreEqual(2, edges.Count);

      // Find a vertex on the other side, near larger cube:
      point = new Vector3(-2.01f, -2.03f, 1.05f);
      NUnit.Framework.Assert.IsTrue(index.FindEdgesClosestTo(point, 0.25f, false, out edges));

      // Should be one:
      NUnit.Framework.Assert.AreEqual(1, edges.Count);

      // Smaller cube should be first:
      NUnit.Framework.Assert.AreEqual(largeId, edges[0].value.meshId);

      // Look somwhere near neither cubes:
      point = new Vector3(-3.01f, 4.03f, 1.05f);
      NUnit.Framework.Assert.IsFalse(index.FindEdgesClosestTo(point, 0.25f, false, out edges));

      // Remove the big cube, should only find stuff near small one.
      index.RemoveMesh(largeCube);
      point = new Vector3(2.01f, 2.03f, 0.05f);
      NUnit.Framework.Assert.IsTrue(index.FindEdgesClosestTo(point, 0.25f, false, out edges));
      NUnit.Framework.Assert.AreEqual(1, edges.Count);
      NUnit.Framework.Assert.AreEqual(smallId, edges[0].value.meshId);
    }
  }
}
