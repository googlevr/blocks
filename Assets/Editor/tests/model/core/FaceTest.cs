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

namespace com.google.apps.peltzer.client.model.core
{

    [TestFixture]
    // Tests for Face.
    public class FaceTest
    {

        [Test]
        public void TestClone()
        {
            List<int> verts = new List<int>();
            Vector3 norm = new Vector3(0, 1, 0);
            FaceProperties props = new FaceProperties(47);

            verts.Add(0);
            verts.Add(1);

            Face face = new Face(/* faceId */ 2, verts.AsReadOnly(), norm, props);

            Face clone = face.Clone();

            NUnit.Framework.Assert.AreSame(face.normal, clone.normal,
              "Normals are immutable and should be shared.");
            NUnit.Framework.Assert.AreSame(face.vertexIds, clone.vertexIds,
              "Vertices are immutable and should be shared.");
            NUnit.Framework.Assert.AreNotSame(face.properties, clone.properties);

            NUnit.Framework.Assert.AreEqual(
              face.properties.materialId, clone.properties.materialId);
        }
    }
}
