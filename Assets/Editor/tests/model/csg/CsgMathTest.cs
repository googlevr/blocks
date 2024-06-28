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
using com.google.apps.peltzer.client.model.core;

namespace com.google.apps.peltzer.client.model.csg
{
    [TestFixture]
    public class CsgMathTest
    {

        [Test]
        public void TestPointOnPlane()
        {
            Plane plane = new Plane(new Vector3(-1, 5, -1), new Vector3(1, 5, -1), new Vector3(1, 5, 1));
            Vector3 p = CsgMath.PointOnPlane(plane);
            NUnit.Framework.Assert.AreEqual(p.y, 5f, 0.001f);
        }

        [Test]
        public void TestRayPlaneIntersection()
        {
            Plane plane = new Plane(new Vector3(-1, 5, -1), new Vector3(1, 5, -1), new Vector3(1, 5, 1));
            Vector3 intersection;

            // Plane in front of Ray:
            CsgMath.RayPlaneIntersection(out intersection, Vector3.zero, new Vector3(0, 1, 0), plane);
            NUnit.Framework.Assert.AreEqual(0, Vector3.Distance(intersection, new Vector3(0, 5, 0)), 0.001f);

            // Plane behind Ray:
            CsgMath.RayPlaneIntersection(out intersection, Vector3.zero, new Vector3(0, -1, 0), plane);
            NUnit.Framework.Assert.AreEqual(0, Vector3.Distance(intersection, new Vector3(0, 5, 0)), 0.001f);

            // On ray start:
            CsgMath.RayPlaneIntersection(out intersection, new Vector3(0, 5, 0), new Vector3(0, -1, 0), plane);
            NUnit.Framework.Assert.AreEqual(0, Vector3.Distance(intersection, new Vector3(0, 5, 0)), 0.001f);
        }

        [Test]
        public void TestIsInside()
        {
            AssertInside(Vector3.zero,
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
            AssertInside(new Vector3(0.9f, 0, 0.9f),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
            AssertInside(new Vector3(-0.9f, 0, 0.9f),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));

            AssertNotInside(new Vector3(-1.5f, 0, 0),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
            AssertNotInside(new Vector3(-1.5f, 0, -1.1f),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
            AssertNotInside(new Vector3(-1.5f, 0, 0.9f),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));

            AssertOnBorder(new Vector3(-1, 0, 0),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
            AssertOnBorder(new Vector3(1, 0, 0),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
            AssertOnBorder(new Vector3(-1, 0, -1),
              new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
        }

        private void AssertInside(Vector3 point, params Vector3[] verts)
        {
            NUnit.Framework.Assert.AreEqual(1, CsgMath.IsInside(toPoly(verts), point),
              "Point should be inside polygon");
        }

        private void AssertNotInside(Vector3 point, params Vector3[] verts)
        {
            NUnit.Framework.Assert.AreEqual(-1, CsgMath.IsInside(toPoly(verts), point),
              "Point should not be inside polygon");
        }

        private void AssertOnBorder(Vector3 point, params Vector3[] verts)
        {
            NUnit.Framework.Assert.AreEqual(0, CsgMath.IsInside(toPoly(verts), point),
              "Point should be on polygon border");
        }

        private CsgPolygon toPoly(params Vector3[] verts)
        {
            List<CsgVertex> poly = new List<CsgVertex>();
            foreach (Vector3 vert in verts)
            {
                poly.Add(new CsgVertex(vert));
            }
            return new CsgPolygon(poly, new core.FaceProperties());
        }
    }
}
