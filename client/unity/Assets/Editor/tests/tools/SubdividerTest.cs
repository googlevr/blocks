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

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.tools {
  [TestFixture]
  public class SubdividerTest {
    [Test]
    public void TestInsertVert() {
      Face triangle = new Face(1,
        new List<int>() { 1, 2, 3 }.AsReadOnly(), Vector3.zero,
        new FaceProperties());

      // Not part of triangle, don't split.
      NUnit.Framework.Assert.AreSame(triangle, Subdivider.MaybeInsertVert(triangle, 7, 8, 23));
      NUnit.Framework.Assert.AreSame(triangle, Subdivider.MaybeInsertVert(triangle, 1, 4, 23));
      NUnit.Framework.Assert.AreSame(triangle, Subdivider.MaybeInsertVert(triangle, 3, 5, 23));

      // Split between 1 and 2
      List<int> indices = Subdivider.MaybeInsertVert(triangle, 1, 2, 7);
      NUnit.Framework.Assert.AreEqual(1, indices[0]);
      NUnit.Framework.Assert.AreEqual(7, indices[1]);
      NUnit.Framework.Assert.AreEqual(2, indices[2]);
      NUnit.Framework.Assert.AreEqual(3, indices[3]);

      // Reverse order
      indices = Subdivider.MaybeInsertVert(triangle, 2, 1, 7);
      NUnit.Framework.Assert.AreEqual(1, indices[0]);
      NUnit.Framework.Assert.AreEqual(7, indices[1]);
      NUnit.Framework.Assert.AreEqual(2, indices[2]);
      NUnit.Framework.Assert.AreEqual(3, indices[3]);

      // At endpoint
      indices = Subdivider.MaybeInsertVert(triangle, 3, 1, 7);
      NUnit.Framework.Assert.AreEqual(1, indices[0]);
      NUnit.Framework.Assert.AreEqual(2, indices[1]);
      NUnit.Framework.Assert.AreEqual(3, indices[2]);
      NUnit.Framework.Assert.AreEqual(7, indices[3]);
    }
  }
}
