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

using System.Collections.Generic;
using com.google.apps.peltzer.client.model.core;

namespace com.google.apps.peltzer.client.tools.utils
{
    [TestFixture]
    public class GridUtilsTest
    {
        [Test]
        public void TestGridAlignedFloatsDontSnap()
        {
            List<float> noSnapFloats = new List<float> {
        0,
        GridUtils.GRID_SIZE,
        GridUtils.GRID_SIZE * 2
      };
            foreach (float f in noSnapFloats)
                NUnit.Framework.Assert.AreEqual(f, GridUtils.SnapToGrid(f));
        }

        [Test]
        public void TestFloatsSnap()
        {
            NUnit.Framework.Assert.AreEqual(0f, GridUtils.SnapToGrid(0.001f));
            NUnit.Framework.Assert.AreEqual(0f, GridUtils.SnapToGrid(0.00124f));
            NUnit.Framework.Assert.AreEqual(0.01f, GridUtils.SnapToGrid(0.0126f));
            NUnit.Framework.Assert.AreEqual(0.02f, GridUtils.SnapToGrid(0.024f));
            NUnit.Framework.Assert.AreEqual(0.03f, GridUtils.SnapToGrid(0.026f));
            NUnit.Framework.Assert.AreEqual(50.5f, GridUtils.SnapToGrid(50.495f));
        }

        [Test]
        public void TestGridAlignedVectorsDontSnap()
        {
            List<Vector3> noSnapVectors = new List<Vector3> {
        Vector3.zero,
        Vector3.one * GridUtils.GRID_SIZE,
        Vector3.one * GridUtils.GRID_SIZE * 2,
      };
            foreach (Vector3 v in noSnapVectors)
                NUnit.Framework.Assert.AreEqual(v, GridUtils.SnapToGrid(v));
        }

        [Test]
        public void TestVectorsSnap()
        {
            NUnit.Framework.Assert.AreEqual(Vector3.zero, GridUtils.SnapToGrid(new Vector3(0f, 0.001f, -0.001f)));
            NUnit.Framework.Assert.AreEqual(new Vector3(1f, 1f, -1f), GridUtils.SnapToGrid(new Vector3(1.001f, 1f, -1.001f)));
            NUnit.Framework.Assert.AreEqual(new Vector3(0.25f, 0.5f, -0.25f),
              GridUtils.SnapToGrid(new Vector3(0.246f, 0.5001f, -0.2499f)));
        }
    }
}
