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

namespace com.google.apps.peltzer.client.model.core {
  [TestFixture]
  public class MeshUtilTest {
    [Test]
    public void TestNonSplitSimple() {
      // Create a three point triangle.
      Vertex v1 = new Vertex(1, new Vector3(0, 0, 0));
      Vertex v2 = new Vertex(2, new Vector3(0, 0, 1));
      Vertex v3 = new Vertex(3, new Vector3(0, 1, 0));

      Vector3 normal = new Vector3(1, 0, 0);

      Face face = new Face(1,
        new List<int>(new int[] { 1, 2, 3 }).AsReadOnly(),
        normal,
        new FaceProperties());

      Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
      vertById[1] = v1;
      vertById[2] = v2;
      vertById[3] = v3;
      Dictionary<int, Face> facesById = new Dictionary<int, Face>();
      facesById[1] = face;

      MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);

      // Face has only three verts, should never be split.
      MMesh meshCopy = mesh.Clone();
      MMesh.GeometryOperation operation = meshCopy.StartOperation();
      operation.ModifyVertexMeshSpace(1, new Vector3(0.1f, 0, 0));
      MeshUtil.SplitFaceIfNeeded(operation, mesh.GetFace(1), 1);
      operation.Commit();
      NUnit.Framework.Assert.AreEqual(1, mesh.faceCount, "Should not have split face.");
    }

    [Test]
    public void TestNonSplit() {
      // Create a square in a plane.
      Vertex v1 = new Vertex(1, new Vector3(0, 0, 0));
      Vertex v2 = new Vertex(2, new Vector3(0, 0, 1));
      Vertex v3 = new Vertex(3, new Vector3(0, 1, 1));
      Vertex v4 = new Vertex(4, new Vector3(0, 1, 0));

      Vector3 normal = new Vector3(1, 0, 0);

      Face face = new Face(1,
        new List<int>(new int[] { 1, 2, 3, 4 }).AsReadOnly(),
        normal,
        new FaceProperties());

      Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
      vertById[1] = v1;
      vertById[2] = v2;
      vertById[3] = v3;
      vertById[4] = v4;
      Dictionary<int, Face> facesById = new Dictionary<int, Face>();
      facesById[1] = face;

      MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);

      // Move first corner, but within the plane.
      MMesh meshCopy = mesh.Clone();
      MMesh.GeometryOperation operation = meshCopy.StartOperation();
      operation.ModifyVertexMeshSpace(1, new Vector3(0, -1, -1));
      MeshUtil.SplitFaceIfNeeded(operation, mesh.GetFace(1), 1);
      operation.Commit();
      NUnit.Framework.Assert.AreEqual(1, mesh.faceCount, "Should not have split face.");
    }

    [Test]
    public void TestSplit() {
      int vertToMove = 1;

      // Create a square in a plane.
      Vertex v1 = new Vertex(vertToMove, new Vector3(0, 0, 0));
      Vertex v2 = new Vertex(2, new Vector3(0, 0, 1));
      Vertex v3 = new Vertex(3, new Vector3(0, 1, 1));
      Vertex v4 = new Vertex(4, new Vector3(0, 1, 0));

      Vector3 normal = new Vector3(1, 0, 0);

      Face face = new Face(1,
        new List<int>(new int[] { 1, 2, 3, 4 }).AsReadOnly(),
        normal,
        new FaceProperties());

      Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
      vertById[vertToMove] = v1;
      vertById[2] = v2;
      vertById[3] = v3;
      vertById[4] = v4;
      Dictionary<int, Face> facesById = new Dictionary<int, Face>();
      facesById[1] = face;

      MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);

      // Move first corner, out of plane.
      MMesh meshCopy = mesh.Clone();
      MMesh.GeometryOperation operation = meshCopy.StartOperation();
      operation.ModifyVertexMeshSpace(1, new Vector3(0.1f, 0, 0));
      MeshUtil.SplitFaceIfNeeded(operation, mesh.GetFace(1), 1);
      operation.Commit();
      
      NUnit.Framework.Assert.AreEqual(2, mesh.faceCount, "Should have split face.");

      // Make sure the vertex was removed from this face.
      Face updatedFace = mesh.GetFace(1);

      string s = updatedFace.id + "   ";
      foreach (int n in updatedFace.vertexIds) {
        s += n + ", ";
      }

      NUnit.Framework.Assert.AreEqual(3, updatedFace.vertexIds.Count);
      NUnit.Framework.Assert.False(updatedFace.vertexIds.Contains(vertToMove), "Vertex should have been removed: " + s);

      // Find the other face, it should contain the vert.
      Face newFace = null;
      foreach (Face f in mesh.GetFaces()) {
        if (f.id != 1) {
          newFace = f;
        }
      }

      NUnit.Framework.Assert.NotNull(newFace);
      NUnit.Framework.Assert.AreEqual(3, newFace.vertexIds.Count);
      NUnit.Framework.Assert.True(newFace.vertexIds.Contains(vertToMove), "Vertex should be in new face.");
    }
  }
}