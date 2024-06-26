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

namespace com.google.apps.peltzer.client.model.csg {
  [TestFixture]
  public class CsgPolygonTest {
    [Test]
    public void TestBaryCenter() {
      CsgPolygon poly = toPoly(
        new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
      NUnit.Framework.Assert.AreEqual(0f, Vector3.Distance(poly.baryCenter, Vector3.zero), 0.001f);

      poly = toPoly(
         new Vector3(-1, 3, -1), new Vector3(1, 3, -1), new Vector3(1, 3, 1), new Vector3(-1, 3, 1));
      NUnit.Framework.Assert.AreEqual(0f, Vector3.Distance(poly.baryCenter, new Vector3(0, 3, 0)), 0.001f);

      poly = toPoly(new Vector3(-1, 1, -1), new Vector3(1, 1, -1), new Vector3(1, 1, 1));
      NUnit.Framework.Assert.AreEqual(0f, 
        Vector3.Distance(poly.baryCenter, new Vector3(0.3333f, 1.0f, -0.333f)), 0.001f);
    }

    [Test]
    public void TestNormal() {
      CsgPolygon poly = toPoly(
        new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
      NUnit.Framework.Assert.AreEqual(0, Vector3.Distance(poly.plane.normal, new Vector3(0, -1, 0)), 0.001f);

      poly = toPoly(new Vector3(-1, 10, -1), new Vector3(1, 10, -1), new Vector3(1, 10, 1));
      NUnit.Framework.Assert.AreEqual(0, Vector3.Distance(poly.plane.normal, new Vector3(0, -1, 0)), 0.001f);

      poly = toPoly(new Vector3(0, -1, -1), new Vector3(0, 1, -1), new Vector3(0, 0, 1));
      NUnit.Framework.Assert.AreEqual(0, Vector3.Distance(poly.plane.normal, new Vector3(1, 0, 0)), 0.001f);
    }

    private CsgPolygon toPoly(params Vector3[] verts) {
      List<CsgVertex> poly = new List<CsgVertex>();
      foreach (Vector3 vert in verts) {
        poly.Add(new CsgVertex(vert));
      }
      return new CsgPolygon(poly, new core.FaceProperties());
    }

  }
}
