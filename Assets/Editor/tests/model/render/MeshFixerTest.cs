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
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{
    public class MeshFixerTest
    {

        [Test]
        public void TestJoinCorners()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, /* materialId */ 1);

            // Join a coner on the box with one diagonal across the face.
            List<Vertex> moves = new List<Vertex>();
            moves.Add(new Vertex(0, mesh.VertexPositionInMeshCoords(3)));

            MeshFixer.MoveVerticesAndMutateMeshAndFix(mesh.Clone(), mesh, moves, /* forPreview */ false);

            // Should have removed one vertex and added one face.
            NUnit.Framework.Assert.AreEqual(7, mesh.vertexCount);
            NUnit.Framework.Assert.AreEqual(7, mesh.faceCount);

            NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));
        }

        [Test]
        public void TestJoinEdges()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, /* materialId */ 1);

            // Join an edge on the box with another across the face.
            List<Vertex> moves = new List<Vertex>();
            moves.Add(new Vertex(1, mesh.VertexPositionInMeshCoords(3)));
            moves.Add(new Vertex(0, mesh.VertexPositionInMeshCoords(2)));

            MeshFixer.MoveVerticesAndMutateMeshAndFix(mesh.Clone(), mesh, moves, /* forPreview */ false);

            // Should have removed two vertices and one face.
            NUnit.Framework.Assert.AreEqual(6, mesh.vertexCount);
            NUnit.Framework.Assert.AreEqual(5, mesh.faceCount);

            NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));
        }

        [Test]
        public void TestJoinFaces()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, /* materialId */ 1);

            // Join the top face with the bottom face.
            List<Vertex> moves = new List<Vertex>();
            moves.Add(new Vertex(0, mesh.VertexPositionInMeshCoords(2)));
            moves.Add(new Vertex(1, mesh.VertexPositionInMeshCoords(3)));
            moves.Add(new Vertex(5, mesh.VertexPositionInMeshCoords(7)));
            moves.Add(new Vertex(4, mesh.VertexPositionInMeshCoords(6)));

            MeshFixer.MoveVerticesAndMutateMeshAndFix(mesh.Clone(), mesh, moves, /* forPreview */ false);

            // Should have removed four vertices and four faces.
            NUnit.Framework.Assert.AreEqual(4, mesh.vertexCount);
            NUnit.Framework.Assert.AreEqual(2, mesh.faceCount);

            NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));
        }

        [Test]
        public void TestJoinDuplicateVertices()
        {
            int vertToDelete = 0;

            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, /* materialId */ 1);

            // Should start with 8 verts.
            NUnit.Framework.Assert.AreEqual(8, mesh.vertexCount);

            // Move vertex 0 to where vertex 3 is
            Vector3 loc = mesh.VertexPositionInMeshCoords(3);
            Vertex updated = new Vertex(vertToDelete, loc);
            MMesh.GeometryOperation operation = mesh.StartOperation();
            operation.ModifyVertex(updated);
            operation.Commit();

            HashSet<int> moves = new HashSet<int>() { vertToDelete };

            MeshFixer.JoinDuplicateVertices(mesh, moves);

            // Should be down to 7 verts
            NUnit.Framework.Assert.AreEqual(7, mesh.vertexCount);

            // Make sure vert 0 is not anywhere in the mesh.
            NUnit.Framework.Assert.IsFalse(mesh.HasVertex(vertToDelete));

            foreach (Face face in mesh.GetFaces())
            {
                NUnit.Framework.Assert.IsFalse(face.vertexIds.Contains(vertToDelete));
            }
        }

        [Test]
        public void TestRemoveZeroLengthSegments()
        {
            CheckRemoveZeroLengthSegments(new List<int> { 1, 2, 3, 4 }, new List<int> { 1, 2, 3, 4 });
            CheckRemoveZeroLengthSegments(new List<int> { 1, 1, 3, 4 }, new List<int> { 1, 3, 4 });
            CheckRemoveZeroLengthSegments(new List<int> { 1, 1, 1, 4 }, new List<int> { 1, 4 });
            CheckRemoveZeroLengthSegments(new List<int> { 4, 2, 3, 4 }, new List<int> { 4, 2, 3 });
            CheckRemoveZeroLengthSegments(new List<int> { 1, 2, 2, 4 }, new List<int> { 1, 2, 4 });
            CheckRemoveZeroLengthSegments(new List<int> { 1, 2, 2, 4, 5, 6, 6, 6, 9, 1 }, new List<int> { 1, 2, 4, 5, 6, 9 });
        }

        private void CheckRemoveZeroLengthSegments(List<int> before, List<int> expected)
        {
            Face face = new Face(1, before.AsReadOnly(), Vector3.zero, new FaceProperties());
            Dictionary<int, Face> faces = new Dictionary<int, Face>();
            faces[1] = face;
            MMesh mesh = new MMesh(2, Vector3.zero, Quaternion.identity, new Dictionary<int, Vertex>(), faces);
            HashSet<int> candidateFaces = new HashSet<int>();
            candidateFaces.UnionWith(mesh.GetFaceIds());
            MeshFixer.RemoveZeroLengthSegments(mesh, candidateFaces);

            NUnit.Framework.CollectionAssert.AreEqual(
              new List<int>(mesh.GetFace(1).vertexIds),
              expected);
        }

        [Test]
        public void TestRemoveZeroAreaSegments()
        {
            CheckRemoveZeroAreaSegments(new List<int> { 1, 2, 3, 4 }, new List<int> { 1, 2, 3, 4 });
            CheckRemoveZeroAreaSegments(new List<int> { 1, 2, 3, 2, 5, 6, 5 }, new List<int> { 1, 2, 5 });
            CheckRemoveZeroAreaSegments(new List<int> { 1, 2, 3, 1, 7 }, new List<int> { 2, 3, 1 });
            CheckRemoveZeroAreaSegments(new List<int> { 1, 2, 3, 1, 2, 7 }, new List<int> { 1, 2, 3, 1, 2, 7 });
            CheckRemoveZeroAreaSegments(new List<int> { 1, 2, 3, 2 }, new List<int> { 1, 2 });
        }

        private void CheckRemoveZeroAreaSegments(List<int> before, List<int> expected)
        {
            Face face = new Face(1, before.AsReadOnly(), Vector3.zero, new FaceProperties());
            Dictionary<int, Face> faces = new Dictionary<int, Face>();
            faces[1] = face;
            MMesh mesh = new MMesh(2, Vector3.zero, Quaternion.identity, new Dictionary<int, Vertex>(), faces);
            mesh.RecalcReverseTable();
            HashSet<int> allFaces = new HashSet<int>();
            allFaces.UnionWith(mesh.GetFaceIds());

            MeshFixer.RemoveZeroAreaSegments(mesh, allFaces);

            NUnit.Framework.CollectionAssert.AreEqual(
              new List<int>(mesh.GetFace(1).vertexIds),
              expected);
        }

        [Test]
        public void TestRemoveInvalidFaces()
        {
            CheckRemoveInvalidFaces(new List<int> { 1, 2, 3, 4 }, false);
            CheckRemoveInvalidFaces(new List<int> { 1, 2, 3, 4, 5 }, false);
            CheckRemoveInvalidFaces(new List<int> { 1, 2 }, true);
            CheckRemoveInvalidFaces(new List<int> { 1 }, true);
            CheckRemoveInvalidFaces(new List<int> { }, true);
        }

        private void CheckRemoveInvalidFaces(List<int> before, bool shouldRemove)
        {
            Face face = new Face(1, before.AsReadOnly(), Vector3.zero, new FaceProperties());
            Dictionary<int, Face> faces = new Dictionary<int, Face>();
            faces[1] = face;
            MMesh mesh = new MMesh(2, Vector3.zero, Quaternion.identity, new Dictionary<int, Vertex>(), faces);
            HashSet<int> candidateFaces = new HashSet<int>();
            candidateFaces.UnionWith(mesh.GetFaceIds());
            MeshFixer.RemoveInvalidFacesAndHoles(mesh, candidateFaces);

            NUnit.Framework.Assert.AreEqual(!mesh.HasFace(1), shouldRemove);
        }
    }
}