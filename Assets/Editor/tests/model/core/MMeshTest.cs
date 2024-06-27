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
using UnityEngine;
using NUnit.Framework;

namespace com.google.apps.peltzer.client.model.core
{

    [TestFixture]
    // Tests for MMesh.
    public class MMeshTest
    {

        [Test]
        public void TestClone()
        {
            MMesh mesh = Primitives.AxisAlignedBox(
              /* meshId */ 2, Vector3.zero, Vector3.one, /* materialId */ 1);

            MMesh clone = mesh.Clone();

            NUnit.Framework.Assert.AreEqual(mesh.id, clone.id);

            NUnit.Framework.Assert.AreNotSame(mesh.GetFaces(), clone.GetFaces());
            NUnit.Framework.Assert.AreNotSame(mesh.GetFace(0), clone.GetFace(0));

            NUnit.Framework.Assert.AreNotSame(mesh.GetVertices(), clone.GetVertices());
            int vertexTestId = mesh.GetVertexIds().GetEnumerator().Current;
            NUnit.Framework.Assert.AreSame(mesh.GetVertex(vertexTestId), clone.GetVertex(vertexTestId),
              "Vertices are immutable.  They should be shared.");
        }

        [Test]
        public void TestBounds()
        {
            MMesh mesh = Primitives.AxisAlignedBox(
              /* meshId */ 2, Vector3.zero, Vector3.one, /* materialId */ 1);

            // Check the mesh bounds.
            Bounds bounds = mesh.bounds;
            AssertClose(bounds.center, Vector3.zero);
            AssertClose(bounds.extents, Vector3.one);

            // Move the mesh and recheck.
            mesh.offset += new Vector3(1, 0, 0);
            mesh.RecalcBounds();
            bounds = mesh.bounds;
            AssertClose(bounds.center, new Vector3(1, 0, 0));
            AssertClose(bounds.extents, Vector3.one);
        }

        private void AssertClose(Vector3 left, Vector3 right)
        {
            NUnit.Framework.Assert.True(Vector3.Distance(left, right) < 0.001f,
              left + " was not similar to " + right);
        }
    }
}
