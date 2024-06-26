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
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core {

  [TestFixture]
  // Tests for Primitives class.
  public class PrimitivesTest {

    [Test]
    public void TestAxisAlignedBox() {
      // Hard to test that it "looks" like a cube.  So we'll just do some
      // sanity checks.

      MMesh mesh = Primitives.AxisAlignedBox(
        1, new Vector3(1, 2, 3), new Vector3(2, 3, 4), /*material id*/ 2);

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));

      // Make sure there are 6 faces.
      NUnit.Framework.Assert.AreEqual(6, mesh.faceCount);

      HashSet<int> usedVertIds = new HashSet<int>();
      foreach (Face face in mesh.GetFaces()) {
        foreach (int vertId in face.vertexIds) {
          usedVertIds.Add(vertId);
        }
      }
      
      // Make sure there are 8 unique verts.
      NUnit.Framework.Assert.AreEqual(8, usedVertIds.Count);

      foreach (Face face in mesh.GetFaces()) {
        NUnit.Framework.Assert.AreEqual(2, face.properties.materialId);
      }

      // Make sure all vertex ids are in the map correctly.
      foreach (int id in mesh.GetVertexIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetVertex(id).id);
      }
      // And Faces.
      foreach (int id in mesh.GetFaceIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetFace(id).id);
      }

      // Center should be at 1, 2, 3.
      Vector3 middle = mesh.bounds.center;

      // There might be a rounding error.  Make sure it is close.
      NUnit.Framework.Assert.Less(
        Vector3.Distance(middle, new Vector3(1, 2, 3)),
        0.001f);
    }

    [Test]
    public void TestCylinder() {
      const int SLICES = 12; // Same as in code.

      // No holes.
      MMesh mesh = Primitives.AxisAlignedCylinder(
        1, new Vector3(1, 2, 3), Vector3.one, null, /*material id*/ 2);

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));

      //  Should be SLICES + 2 faces.
      NUnit.Framework.Assert.AreEqual(SLICES + 2, mesh.faceCount);

      // And SLICES * 2 Verts.
      NUnit.Framework.Assert.AreEqual(SLICES * 2, mesh.vertexCount);

      // Make sure all vertex ids are in the map correctly.
      foreach (int id in mesh.GetVertexIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetVertex(id).id);
      }
      // And Faces.
      foreach (int id in mesh.GetFaceIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetFace(id).id);
      }

      // Each vertex should be used exactly 3 times.
      foreach (Vertex vertex in mesh.GetVertices()) {
        int vertId = vertex.id;
        int sum = 0;
        foreach (Face face in mesh.GetFaces()) {
          sum += face.vertexIds.Where(x => x == vertId).Count();
        }
        NUnit.Framework.Assert.AreEqual(3, sum);
      }
    }

    [Test]
    public void TestAxisAlignedCone() {
      const int SLICES = 12; // Same as in code.

      MMesh mesh = Primitives.AxisAlignedCone(
        1, new Vector3(1, 2, 3), Vector3.one, /*material id*/ 2);

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));

      // Make sure there are SLICES + 1 faces.
      NUnit.Framework.Assert.AreEqual(SLICES + 1, mesh.faceCount);

      // Make sure there are SLICES + 1 vertices.
      NUnit.Framework.Assert.AreEqual(SLICES + 1, mesh.vertexCount);

      // Make sure all vertex ids are in the map correctly.
      foreach (int id in mesh.GetVertexIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetVertex(id).id);
      }
      // And Faces.
      foreach (int id in mesh.GetFaceIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetFace(id).id);
      }

      // Center should be at 1, 2, 3.
      Vector3 middle = mesh.bounds.center;

      // There might be a rounding error.  Make sure it is close.
      NUnit.Framework.Assert.Less(
        Vector3.Distance(middle, new Vector3(1, 2, 3)),
        0.001f);
    }

    [Test]
    public void TestTriangularPyramid() {
      MMesh mesh = Primitives.TriangularPyramid(
        1, new Vector3(1, 2, 3), Vector3.one, /*material id*/ 2);

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));

      // Make sure there are 4 faces.
      NUnit.Framework.Assert.AreEqual(4, mesh.faceCount);

      // Make sure there are 4 vertices.
      NUnit.Framework.Assert.AreEqual(4, mesh.vertexCount);

      // Make sure all vertex ids are in the map correctly.
      foreach (int id in mesh.GetVertexIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetVertex(id).id);
      }
      // And Faces.
      foreach (int id in mesh.GetFaceIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetFace(id).id);
      }

      // Each vertex should be used exactly 3 times.
      for (int i = 0; i < 4; i++) {
        int sum = 0;
        for (int j = 0; j < 4; j++) {
          sum += mesh.GetFace(j).vertexIds.Where(x => x == i).Count();
        }
        NUnit.Framework.Assert.AreEqual(3, sum);
      }
    }

    [Test]
    public void TestTorus() {
      const int SLICES = 12; // Same as in code.

      MMesh mesh = Primitives.Torus(
        1, new Vector3(1, 2, 3), Vector3.one * 2, /*material id*/ 2);

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));

      // Make sure there are n^2 faces.
      NUnit.Framework.Assert.AreEqual(SLICES * SLICES, mesh.faceCount);

      // Make sure there are n^2 vertices.
      NUnit.Framework.Assert.AreEqual(SLICES * SLICES, mesh.vertexCount);

      // Make sure all vertex ids are in the map correctly.
      foreach (int id in mesh.GetVertexIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetVertex(id).id);
      }
      // And Faces.
      foreach (int id in mesh.GetFaceIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetFace(id).id);
      }

      // Each vertex should be used exactly 4 times.
      for (int i = 0; i < (SLICES * SLICES); i++) {
        int sum = 0;
        for (int j = 0; j < (SLICES * SLICES); j++) {
          sum += mesh.GetFace(j).vertexIds.Where(x => x == i).Count();
        }
        NUnit.Framework.Assert.AreEqual(4, sum);
      }
    }

    [Test]
    public void TestSphere() {
      MMesh mesh = Primitives.AxisAlignedIcosphere(
        1, new Vector3(1, 2, 3), Vector3.one, /*material id*/ 2);

      // Make sure all vertex ids are in the map correctly.
      foreach (int id in mesh.GetVertexIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetVertex(id).id);
      }
      // And Faces.
      foreach (int id in mesh.GetFaceIds()) {
        NUnit.Framework.Assert.AreEqual(id, mesh.GetFace(id).id);
      }

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));
      NUnit.Framework.Assert.AreEqual(80, mesh.faceCount);
    }
  }
}