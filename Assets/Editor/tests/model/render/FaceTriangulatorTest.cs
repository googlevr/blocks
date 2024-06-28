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

using NUnit.Framework;
using UnityEngine;

using System;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.render
{
    [TestFixture]
    public class FaceTriangulatorTest
    {
        [Test]
        public void TriangleTest()
        {
            // Clockwise winded triangle.
            Vertex v1 = new Vertex(1, new Vector3(5, 0, 0));
            Vertex v2 = new Vertex(2, Vector3.zero);
            Vertex v3 = new Vertex(3, new Vector3(0, 3, 4));
            List<Vertex> border = new List<Vertex>();
            border.Add(v1);
            border.Add(v2);
            border.Add(v3);
            List<Triangle> triangles = FaceTriangulator.Triangulate(border);
            List<Triangle> expected = new List<Triangle>();
            expected.Add(new Triangle(v1.id, v2.id, v3.id));

            // Simple triangle should triangulate to itself.
            Assert.AreEqual(expected, triangles);
        }

        [Test]
        public void ConvexTest()
        {
            // Simple convex shape.
            Vertex v1 = new Vertex(0, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(1, new Vector3(0, 3, 5));
            Vertex v3 = new Vertex(2, new Vector3(0, 5, 3));
            Vertex v4 = new Vertex(3, new Vector3(0, 4, 1));
            Vertex v5 = new Vertex(4, new Vector3(0, 2, 0));
            List<Vertex> border = new List<Vertex>();
            border.Add(v1);
            border.Add(v2);
            border.Add(v3);
            border.Add(v4);
            border.Add(v5);
            List<Triangle> triangles = FaceTriangulator.Triangulate(border);
            HashSet<int> usedVertices = new HashSet<int>();

            // There should be 3 triangles, each with a normal facing in negative x.
            Assert.AreEqual(3, triangles.Count);
            foreach (Triangle t in triangles)
            {
                usedVertices.Add(t.vertId0);
                usedVertices.Add(t.vertId1);
                usedVertices.Add(t.vertId2);

                Assert.Less(new Plane(border[t.vertId0].loc, border[t.vertId1].loc, border[t.vertId2].loc).normal.x, 0);
            }

            // All 5 vertices should be used.
            Assert.AreEqual(5, usedVertices.Count);
        }

        [Test]
        public void ConcaveTest()
        {
            Vertex v1 = new Vertex(1, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(2, new Vector3(0, 3, 5));
            Vertex v3 = new Vertex(3, new Vector3(0, 5, 3));
            Vertex v4 = new Vertex(4, new Vector3(0, 2, 2));
            Vertex v5 = new Vertex(5, new Vector3(0, 2, 0));
            List<Vertex> border = new List<Vertex>();
            border.Add(v1);
            border.Add(v2);
            border.Add(v3);
            border.Add(v4);
            border.Add(v5);
            List<Triangle> triangles = FaceTriangulator.Triangulate(border);
            // The polygon is restrictive enough to enforce only one possible triangulation.
            List<Triangle> expected = new List<Triangle>();
            expected.Add(new Triangle(v1.id, v2.id, v4.id));
            expected.Add(new Triangle(v2.id, v3.id, v4.id));
            expected.Add(new Triangle(v1.id, v4.id, v5.id));
            Assert.AreEqual(3, triangles.Count);
            Assert.Contains(expected[0], triangles);
            Assert.Contains(expected[1], triangles);
            Assert.Contains(expected[2], triangles);
        }

        [Test]
        public void HoleTest()
        {
            // Simple triangle with square inside.
            Vertex v1 = new Vertex(0, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(1, new Vector3(0, 6, 8));
            Vertex v3 = new Vertex(2, new Vector3(0, 10, 0));
            Vertex v4 = new Vertex(3, new Vector3(0, 6, 1));
            Vertex v5 = new Vertex(4, new Vector3(0, 7, 2));
            Vertex v6 = new Vertex(5, new Vector3(0, 5, 5));
            Vertex v7 = new Vertex(6, new Vector3(0, 3.5f, 3));
            List<Vertex> border = new List<Vertex>();
            border.Add(v1);
            border.Add(v2);
            border.Add(v3);
            List<Vertex> hole = new List<Vertex>();
            hole.Add(v4);
            hole.Add(v5);
            hole.Add(v6);
            hole.Add(v7);
            List<Vertex> all = new List<Vertex>(border);
            all.AddRange(hole);
            List<List<Vertex>> holes = new List<List<Vertex>>();
            holes.Add(hole);
            List<Triangle> triangles = FaceTriangulator.Triangulate(border);
            // Verify point in hole is not contained in any of the faces
            foreach (Triangle t in triangles)
            {
                Console.Write(t);
                Assert.False(Math3d.TriangleContainsPoint(
                    all[t.vertId0].loc, all[t.vertId1].loc,
                    all[t.vertId2].loc, new Vector3(0, 5, 3)));
            }
        }

        [Test]
        public void BadIntersectionTest()
        {
            // Simple triangle with square inside.
            Vertex v1 = new Vertex(0, new Vector3(0, -1, 1));
            Vertex v2 = new Vertex(1, new Vector3(-1, -1, 0));
            Vertex v3 = new Vertex(2, new Vector3(0, -1, -1));
            Vertex v4 = new Vertex(3, new Vector3(1, -1, 0));
            Vertex v5 = new Vertex(4, new Vector3(0.5f, -1, 0));
            Vertex v6 = new Vertex(5, new Vector3(0, -1, -0.5f));
            Vertex v7 = new Vertex(6, new Vector3(-0.5f, -1, 0));
            Vertex v8 = new Vertex(7, new Vector3(0, -1, 0.5f));
            List<Vertex> border = new List<Vertex>();
            border.Add(v1);
            border.Add(v2);
            border.Add(v3);
            border.Add(v4);
            List<Vertex> hole = new List<Vertex>();
            hole.Add(v5);
            hole.Add(v6);
            hole.Add(v7);
            hole.Add(v8);
            List<Vertex> all = new List<Vertex>(border);
            all.AddRange(hole);
            List<List<Vertex>> holes = new List<List<Vertex>>();
            holes.Add(hole);
            List<Triangle> triangles = FaceTriangulator.Triangulate(border);
            // Verify point in hole is not contained in any of the faces
            foreach (Triangle t in triangles)
            {
                Console.Write(t);
                Assert.False(Math3d.TriangleContainsPoint(
                    all[t.vertId0].loc, all[t.vertId1].loc,
                    all[t.vertId2].loc, new Vector3(0, -1, 0)));
            }
        }

        [Test]
        public void HoleWithOcclusionTest()
        {
            // Outside is square with occluding indentation.
            Vertex v1 = new Vertex(0, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(1, new Vector3(0, 0, 5));
            Vertex v3 = new Vertex(2, new Vector3(0, 8, 8));
            Vertex v4 = new Vertex(3, new Vector3(0, 0, 10));
            Vertex v5 = new Vertex(4, new Vector3(0, 10, 10));
            Vertex v6 = new Vertex(5, new Vector3(0, 10, 0));
            List<Vertex> border = new List<Vertex>();
            border.Add(v1);
            border.Add(v2);
            border.Add(v3);
            border.Add(v4);
            border.Add(v5);
            border.Add(v6);

            // Hole is simple triangle.
            Vertex v7 = new Vertex(6, new Vector3(0, 4, 4));
            Vertex v8 = new Vertex(7, new Vector3(0, 3.5f, 3));
            Vertex v9 = new Vertex(8, new Vector3(0, 5, 3));
            List<Vertex> hole = new List<Vertex>();
            hole.Add(v7);
            hole.Add(v8);
            hole.Add(v9);
            List<Vertex> all = new List<Vertex>(border);
            all.AddRange(hole);
            List<List<Vertex>> holes = new List<List<Vertex>>();
            holes.Add(hole);

            // If no exception is thrown triangulation was successful.
            List<Triangle> triangles = FaceTriangulator.Triangulate(border);

            // Verify point in hole is not contained in any of the faces
            foreach (Triangle t in triangles)
            {
                Console.Write(t);
                Assert.False(Math3d.TriangleContainsPoint(
                    all[t.vertId0].loc, all[t.vertId1].loc,
                    all[t.vertId2].loc, new Vector3(0, 4, 3.5f)));
            }
        }

        [Test]
        public void MultipleHoleTest()
        {
            // Border is triangle.
            Vertex v1 = new Vertex(0, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(1, new Vector3(0, 6, 8));
            Vertex v3 = new Vertex(2, new Vector3(0, 15, 0));
            List<Vertex> border = new List<Vertex>();
            border.Add(v1);
            border.Add(v2);
            border.Add(v3);
            List<Vertex> all = new List<Vertex>(border);

            // Smaller hole is also a triangle.
            Vertex v4 = new Vertex(3, new Vector3(0, 5, 3.75f));
            Vertex v5 = new Vertex(4, new Vector3(0, 4.5f, 3.5f));
            Vertex v6 = new Vertex(5, new Vector3(0, 5, 3));
            List<Vertex> hole = new List<Vertex>();
            hole.Add(v4);
            hole.Add(v5);
            hole.Add(v6);
            all.AddRange(hole);
            List<List<Vertex>> holes = new List<List<Vertex>>();
            holes.Add(hole);

            // Larger hole is encompassing smaller one, preventing visibility.
            Vertex v7 = new Vertex(6, new Vector3(0, 7, 2));
            Vertex v8 = new Vertex(7, new Vector3(0, 5, 5));
            Vertex v9 = new Vertex(8, new Vector3(0, 3.25f, 3));
            Vertex v10 = new Vertex(9, new Vector3(0, 6.75f, 2));
            Vertex v11 = new Vertex(10, new Vector3(0, 3.75f, 3.25f));
            Vertex v12 = new Vertex(11, new Vector3(0, 5, 4.5f));
            hole = new List<Vertex>();
            hole.Add(v7);
            hole.Add(v8);
            hole.Add(v9);
            hole.Add(v10);
            hole.Add(v11);
            hole.Add(v12);
            all.AddRange(hole);
            holes.Add(hole);

            // If no exception is thrown triangulation was successful.
            List<Triangle> triangles = FaceTriangulator.Triangulate(border);

            // Verify point in hole is not contained in any of the faces
            foreach (Triangle t in triangles)
            {
                Console.Write(t);
                Assert.False(Math3d.TriangleContainsPoint(
                    all[t.vertId0].loc, all[t.vertId1].loc,
                    all[t.vertId2].loc, new Vector3(0, 4.875f, 3.25f)));
                Assert.False(Math3d.TriangleContainsPoint(
                    all[t.vertId0].loc, all[t.vertId1].loc,
                    all[t.vertId2].loc, new Vector3(0, 4, 3)));
            }
        }
    }
}
