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
using System.Collections;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{

    [TestFixture]
    // Tests for SnapGrid.
    public class SnapGridTest
    {
        private readonly float EPSILON = 0.0001f;

        [Test]
        public void TestFindForwardAxis()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 1);
            Face face = mesh.GetFace(0);
            List<Vector3> f0Vertices = face.vertexIds.Select(idx => mesh.VertexPositionInModelCoords(idx)).ToList();

            Plane plane = new Plane(f0Vertices[0], f0Vertices[1], f0Vertices[2]);

            Assert.AreEqual(plane.normal, SnapGrid.FindForwardAxisForTest(f0Vertices));
        }

        [Test]
        public void TestFindRightAxis()
        {
            // ClosestEdge in the Primitive from TestFindForwardAxis() for position new Vector3(-1, 0.90f, 0f).
            KeyValuePair<Vector3, Vector3> closestEdge = new KeyValuePair<Vector3, Vector3>(
              new Vector3(-1, 1, 1),
              new Vector3(-1, 1, -1));

            Assert.IsTrue(
              Math3d.CompareVectors(new Vector3(0, 0, 2).normalized, SnapGrid.FindRightAxisForTest(closestEdge), EPSILON));

            closestEdge = new KeyValuePair<Vector3, Vector3>(new Vector3(1, 1, -1), new Vector3(1, 1, 1));
            Assert.IsTrue(
              Math3d.CompareVectors(new Vector3(0, 0, -2).normalized, SnapGrid.FindRightAxisForTest(closestEdge), EPSILON));

            closestEdge = new KeyValuePair<Vector3, Vector3>(new Vector3(1, 1, 1), new Vector3(1, 1, -1));
            Assert.IsTrue(
              Math3d.CompareVectors(new Vector3(0, 0, 2).normalized, SnapGrid.FindRightAxisForTest(closestEdge), EPSILON));
        }

        [Test]
        public void TestFindUpAxis()
        {
            Vector3 forwardAxis = (new Vector3(-1, 0, 0)).normalized;
            Vector3 rightAxis = (new Vector3(0, 0, 2)).normalized;
            Vector3 upAxis = (new Vector3(0, 2, 0)).normalized;

            Assert.IsTrue(
              Math3d.CompareVectors(upAxis, SnapGrid.FindUpAxisForTest(rightAxis, forwardAxis), EPSILON));

            forwardAxis = (new Vector3(1, 0, 0)).normalized;
            rightAxis = (new Vector3(0, 0, -2)).normalized;
            upAxis = (new Vector3(0, 2, 0)).normalized;

            Assert.IsTrue(
              Math3d.CompareVectors(upAxis, SnapGrid.FindUpAxisForTest(rightAxis, forwardAxis), EPSILON));

            forwardAxis = (new Vector3(0, 1, 0)).normalized;
            rightAxis = (new Vector3(0, 0, 2)).normalized;
            upAxis = (new Vector3(2, 0, 0)).normalized;

            Assert.IsTrue(
              Math3d.CompareVectors(upAxis, SnapGrid.FindUpAxisForTest(rightAxis, forwardAxis), EPSILON));
        }

        [Test]
        // Tests to see if face axis are square and correctly related.
        public void TestExampleAxis()
        {
            Vector3 forward = (new Vector3(2, 1, 3)).normalized;
            Vector3 right = (new Vector3(3, 0, -2)).normalized;
            Assert.AreEqual(90.0f, Vector3.Angle(forward, right));

            Vector3 up = SnapGrid.FindUpAxisForTest(right, forward);
            Assert.AreEqual(90.0f, Vector3.Angle(forward, up));
            Assert.AreEqual(90.0f, Vector3.Angle(right, up));
            Assert.AreEqual(90.0f, Vector3.Angle(right, forward));
            Assert.AreEqual(90.0f, Vector3.Angle(forward, right));
            Assert.AreEqual(90.0f, Vector3.Angle(forward, up));

            Assert.IsTrue(Math3d.CompareVectors(up, Vector3.Cross(forward, right), EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(right, Vector3.Cross(up, forward), EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(forward, Vector3.Cross(right, up), EPSILON));

            forward = (new Vector3(-1, 0, 0)).normalized;
            right = (new Vector3(0, 0, 2)).normalized;
            up = (new Vector3(0, 2, 0)).normalized;

            Assert.IsTrue(Math3d.CompareVectors(up, SnapGrid.FindUpAxisForTest(right, forward), EPSILON));
            Assert.AreEqual(90.0f, Vector3.Angle(forward, up));
            Assert.AreEqual(90.0f, Vector3.Angle(right, up));
            Assert.AreEqual(90.0f, Vector3.Angle(right, forward));
            Assert.AreEqual(90.0f, Vector3.Angle(forward, right));
            Assert.AreEqual(90.0f, Vector3.Angle(forward, up));

            Assert.IsTrue(Math3d.CompareVectors(up, Vector3.Cross(forward, right), EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(right, Vector3.Cross(up, forward), EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(forward, Vector3.Cross(right, up), EPSILON));
        }

        [Test]
        // Tests if the rotation found applied to the universal axis returns the face axis.
        public void TestFindRotation()
        {
            Vector3 forward = new Vector3(-1, 0, 0).normalized;
            Vector3 right = new Vector3(0, 0, 2).normalized;
            Vector3 up = new Vector3(0, 2, 0).normalized;

            Quaternion rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareVectors(right, rotation * Vector3.right, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(up, rotation * Vector3.up, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(forward, rotation * Vector3.forward, EPSILON));

            forward = new Vector3(1, 0, 0).normalized;
            right = new Vector3(0, 0, -2).normalized;
            up = new Vector3(0, 2, 0).normalized;

            rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareVectors(right, rotation * Vector3.right, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(up, rotation * Vector3.up, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(forward, rotation * Vector3.forward, EPSILON));

            forward = (new Vector3(2, 1, 3)).normalized;
            right = (new Vector3(3, 0, -2)).normalized;
            up = SnapGrid.FindUpAxisForTest(right, forward);

            rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareVectors(right, rotation * Vector3.right, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(up, rotation * Vector3.up, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(forward, rotation * Vector3.forward, EPSILON));

            forward = new Vector3(0.7055063f, -0.6813365f, -0.195042f).normalized;
            right = new Vector3(-0.5638906f, -0.7063745f, 0.4278581f).normalized;
            up = new Vector3(-0.429288f, -0.1918742f, -0.882551f).normalized;

            rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareVectors(right, rotation * Vector3.right, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(up, rotation * Vector3.up, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(forward, rotation * Vector3.forward, EPSILON));

            forward = new Vector3(0.3194726f, -0.4424828f, -0.8379416f).normalized;
            right = new Vector3(0.9473031f, 0.1710962f, 0.2708191f).normalized;
            up = new Vector3(0.02353583f, -0.880304f, 0.4738259f).normalized;

            rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareVectors(right, rotation * Vector3.right, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(up, rotation * Vector3.up, EPSILON));
            Assert.IsTrue(Math3d.CompareVectors(forward, rotation * Vector3.forward, EPSILON));
        }

        [Test]
        // Tests if face rotation is the same as a meshes rotation given the mesh is unmodified.
        public void TestIfRotationAppliesToFullMesh()
        {
            GameObject testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            testCube.transform.rotation = Quaternion.Euler(new Vector3(30f, 25f, 10f));

            Vector3 forward = testCube.transform.forward;
            Vector3 right = testCube.transform.right;
            Vector3 up = testCube.transform.up;

            Quaternion rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareQuaternions(testCube.transform.rotation, rotation, EPSILON));

            testCube.transform.rotation = Quaternion.Euler(new Vector3(15f, 19f, 72f));

            forward = testCube.transform.forward;
            right = testCube.transform.right;
            up = testCube.transform.up;

            rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareQuaternions(testCube.transform.rotation, rotation, EPSILON));

            testCube.transform.rotation = Quaternion.Euler(new Vector3(0f, 192f, 29f));

            forward = testCube.transform.forward;
            right = testCube.transform.right;
            up = testCube.transform.up;

            rotation = SnapGrid.FindFaceRotationForTest(right, up, forward);

            Assert.IsTrue(Math3d.CompareQuaternions(testCube.transform.rotation, rotation, EPSILON));
        }
    }
}
