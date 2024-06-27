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
using System.Linq;

namespace com.google.apps.peltzer.client.model.core
{

    [TestFixture]
    // Tests for mesh math.
    public class MeshMathTest
    {

        [Test]
        public void TestCalculateNormalForTriangle()
        {
            List<Vector3> simpleTriangle = new List<Vector3>() { new Vector3(0, 0), new Vector3(0, 1), new Vector3(1, 0) };
            Vector3 normal = new Vector3(0, 0, -1);
            NUnit.Framework.Assert.Less((normal - MeshMath.CalculateNormal(simpleTriangle)).magnitude, .0001f);

            // move the triangle and normal to some crazy place, should still calculate correctly
            Matrix4x4 transformMatrix = Matrix4x4.TRS(new Vector3(3, -2, 7), Quaternion.Euler(13, 7.5f, 20), new Vector3(.4f, 9, 9));
            List<Vector3> transformedTriangle = simpleTriangle.Select(v => transformMatrix.MultiplyPoint(v)).ToList();
            Vector3 transformedNormal = transformMatrix.MultiplyVector(normal).normalized;
            Vector3 newNormal = MeshMath.CalculateNormal(transformedTriangle);

            NUnit.Framework.Assert.Less((transformedNormal - newNormal).magnitude, .0001f);
        }

        [Test]
        public void TestCalculateNormalForConvex()
        {
            // Regular hex inscibed in a cube using midpoints of cube's edges.
            List<Vector3> hex = new List<Vector3>() {
        new Vector3(1, 0, 1),
        new Vector3(0, -1, 1),
        new Vector3(-1, -1, 0),
        new Vector3(-1, 0, -1),
        new Vector3(0, 1, -1),
        new Vector3(1, 1, 0)
      };
            // Conveniently, the normal of this hex is along the diagonal of the cube.
            Vector3 normal = new Vector3(1, -1, -1).normalized;
            NUnit.Framework.Assert.Less((normal - MeshMath.CalculateNormal(hex)).magnitude, .0001f);
        }

        [Test]
        public void TestCalculateNormalForConcave()
        {
            // Bring every other vertex towards the center of the cube, making the shape concave.
            List<Vector3> sortaHex = new List<Vector3>() {
        new Vector3(.1f, 0, .1f),
        new Vector3(0, -1, 1),
        new Vector3(-.1f, -.1f, 0),
        new Vector3(-1, 0, -1),
        new Vector3(0, .1f, -.1f),
        new Vector3(1, 1, 0)
      };
            Vector3 normal = new Vector3(1, -1, -1).normalized;
            // Reflex vertices should not affect resulting normal.
            NUnit.Framework.Assert.Less((normal - MeshMath.CalculateNormal(sortaHex)).magnitude, .0001f);
        }

        [Test]
        public void TestIsCloseToFace()
        {
            const float kFaceClosenessThreshold = 0.0001f;
            const float kVertexDistanceThreshold = 0.0001f;
            List<Vector3> simpleTriangle = new List<Vector3>() { new Vector3(0, 0), new Vector3(0, 1), new Vector3(1, 0) };
            Vector3 onTriangle = new Vector3(.1f, .1f);
            NUnit.Framework.Assert.True(MeshMath.IsCloseToFaceInterior(onTriangle, MeshMath.CalculateNormal(simpleTriangle), simpleTriangle,
              kFaceClosenessThreshold, kVertexDistanceThreshold));

            Vector3 offTriangle = new Vector3(.51f, .51f);
            NUnit.Framework.Assert.False(MeshMath.IsCloseToFaceInterior(offTriangle, MeshMath.CalculateNormal(simpleTriangle), simpleTriangle,
              kFaceClosenessThreshold, kVertexDistanceThreshold));

            Vector3 slightlyAbove = new Vector3(.1f, .1f, .0002f);
            NUnit.Framework.Assert.False(MeshMath.IsCloseToFaceInterior(slightlyAbove, MeshMath.CalculateNormal(simpleTriangle), simpleTriangle,
              kFaceClosenessThreshold, kVertexDistanceThreshold));

            Vector3 closeEnough = new Vector3(.1f, .1f, .00005f);
            NUnit.Framework.Assert.True(MeshMath.IsCloseToFaceInterior(closeEnough, MeshMath.CalculateNormal(simpleTriangle), simpleTriangle,
              kFaceClosenessThreshold, kVertexDistanceThreshold));

            Vector3 onVertex = new Vector3(1f, 0f, 0f);
            NUnit.Framework.Assert.False(MeshMath.IsCloseToFaceInterior(onVertex, MeshMath.CalculateNormal(simpleTriangle), simpleTriangle,
              kFaceClosenessThreshold, kVertexDistanceThreshold));
        }

        [Test]
        public void TestFindCornerVertices()
        {
            List<Vector3> coplanarVertices = new List<Vector3>() {
        new Vector3(1, 0, 0),
        new Vector3(1, 1, 0),
        new Vector3(0, 1, 0),
        new Vector3(-1, 1, 0),
        new Vector3(-1, 0, 0),
        new Vector3(-1, -1, 0),
        new Vector3(0, -1, 0),
        new Vector3(1, -1, 0)};

            List<Vector3> expectedCornerVertices = new List<Vector3>() {
        new Vector3(1, 1, 0),
        new Vector3(-1, 1, 0),
        new Vector3(-1, -1, 0),
        new Vector3(1, -1, 0)};

            Assert.AreEqual(expectedCornerVertices, MeshMath.FindCornerVertices(coplanarVertices));
        }

        [Test]
        public void TestFindClosestEdgeInFace()
        {
            List<Vector3> vertexPositions = new List<Vector3>() {
        new Vector3(-1, -1, -1),
        new Vector3(-1, -1, 1),
        new Vector3(-1, 1, 1),
        new Vector3(-1, 1, -1)};

            Vector3 position = new Vector3(-1, 0.90f, 0f);
            KeyValuePair<Vector3, Vector3> expectedClosestEdge =
              new KeyValuePair<Vector3, Vector3>(new Vector3(-1, 1, 1), new Vector3(-1, 1, -1));
            KeyValuePair<Vector3, Vector3> actualClosestEdge = MeshMath.FindClosestEdgeInFace(position, vertexPositions);
            Assert.AreEqual(expectedClosestEdge, actualClosestEdge);

            position = new Vector3(-1, -0.90f, 0f);
            expectedClosestEdge = new KeyValuePair<Vector3, Vector3>(new Vector3(-1, -1, -1), new Vector3(-1, -1, 1));
            actualClosestEdge = MeshMath.FindClosestEdgeInFace(position, vertexPositions);
            Assert.AreEqual(expectedClosestEdge, actualClosestEdge);

            position = new Vector3(-1, 0f, -.90f);
            expectedClosestEdge = new KeyValuePair<Vector3, Vector3>(new Vector3(-1, 1, -1), new Vector3(-1, -1, -1));
            actualClosestEdge = MeshMath.FindClosestEdgeInFace(position, vertexPositions);
            Assert.AreEqual(expectedClosestEdge, actualClosestEdge);

            position = new Vector3(-1, 0f, .90f);
            expectedClosestEdge = new KeyValuePair<Vector3, Vector3>(new Vector3(-1, -1, 1), new Vector3(-1, 1, 1));
            actualClosestEdge = MeshMath.FindClosestEdgeInFace(position, vertexPositions);
            Assert.AreEqual(expectedClosestEdge, actualClosestEdge);
        }
    }
}
