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
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.csg {
  [TestFixture]
  public class CsgOperationsTest {

    [Test]
    public void TestSubtractFromModel() {
      Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 5);
      Model model = new Model(bounds);

      SpatialIndex spatialIndex = new SpatialIndex(model, bounds);

      int toErase = 1;
      int toIntersect = 2;
      int toIgnore = 3;

      // Add a small cube at the center:
      MMesh meshToAdd = Primitives.AxisAlignedBox(toErase, Vector3.zero, Vector3.one * 0.5f, 1);
      model.AddMesh(meshToAdd);
      spatialIndex.AddMesh(meshToAdd);


      // Add one nearby:
      meshToAdd = Primitives.AxisAlignedBox(toIntersect, Vector3.one, Vector3.one, 1);
      model.AddMesh(meshToAdd);
      spatialIndex.AddMesh(meshToAdd);

      // Add another further away:
      meshToAdd = Primitives.AxisAlignedBox(toIgnore, Vector3.one * 2, Vector3.one * 0.5f, 1);
      model.AddMesh(meshToAdd);
      spatialIndex.AddMesh(meshToAdd);

      // Now subtract a big cube from the model:
      bool subtracted = CsgOperations.SubtractMeshFromModel(
        model, spatialIndex, Primitives.AxisAlignedBox(7, Vector3.zero, Vector3.one, 1));
      NUnit.Framework.Assert.IsTrue(subtracted);

      // Should have completely erased the small one in the center:
      NUnit.Framework.Assert.IsFalse(model.HasMesh(toErase));

      // Other two should still be there:
      NUnit.Framework.Assert.IsTrue(model.HasMesh(toIntersect));
      NUnit.Framework.Assert.IsTrue(model.HasMesh(toIgnore));

      // The one that intersected will end up with more faces.
      NUnit.Framework.Assert.AreEqual(15, model.GetMesh(toIntersect).faceCount);

      // Subtract away from the scene to make sure the method returns false.
      NUnit.Framework.Assert.IsFalse(CsgOperations.SubtractMeshFromModel(model, spatialIndex,
        Primitives.AxisAlignedBox(7, Vector3.one * -3, Vector3.one, 1)));
    }

    [Test]
    public void RaycastTest() {
      CsgPolygon unitSquareNegativeYAtZero = toPoly(
        new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1));
      CsgPolygon unitSquarePositiveYAtZero = toPoly(
        new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1));
      CsgPolygon unitSquarePositiveYAtMinusOne = toPoly(
        new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, -1, -1), new Vector3(-1, -1, -1));
      CsgPolygon unitSquareNegativeYAtMinusOne = toPoly(
        new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1));
      CsgPolygon unitSquareShiftedOneNegativeYAtZero = toPoly(
        new Vector3(0, 0, -1), new Vector3(2, 0, -1), new Vector3(2, 0, 1), new Vector3(0, 0, 1));

      AssertPolyStatus(PolygonStatus.INSIDE, unitSquareNegativeYAtZero, toObj(unitSquareNegativeYAtMinusOne));
      AssertPolyStatus(PolygonStatus.OUTSIDE, unitSquareNegativeYAtZero, toObj(unitSquarePositiveYAtMinusOne));
      AssertPolyStatus(PolygonStatus.SAME, unitSquareNegativeYAtZero, toObj(unitSquareNegativeYAtZero));
      AssertPolyStatus(PolygonStatus.OPPOSITE, unitSquareNegativeYAtZero, toObj(unitSquarePositiveYAtZero));
      AssertPolyStatus(PolygonStatus.SAME, unitSquareNegativeYAtZero,
        toObj(unitSquareNegativeYAtZero, unitSquareShiftedOneNegativeYAtZero));
      AssertPolyStatus(PolygonStatus.SAME, unitSquareNegativeYAtZero,
        toObj(unitSquareShiftedOneNegativeYAtZero, unitSquareNegativeYAtZero));
    }

    [Test]
    public void RaycastCubeWithinCube() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));
      // Small cube inside of a larger cube, both at the origin.
      CsgObject smallCube = CsgOperations.ToCsg(ctx, Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one * 0.5f, 1));
      CsgObject largeCube = CsgOperations.ToCsg(ctx, Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 1));

      // All faces of small cube should be INSIDE the large cube:
      foreach (CsgPolygon poly in smallCube.polygons) {
        AssertPolyStatus(PolygonStatus.INSIDE, poly, largeCube);
      }

      // All faces of large cube should be OUTSIDE small cube.
      foreach (CsgPolygon poly in largeCube.polygons) {
        AssertPolyStatus(PolygonStatus.OUTSIDE, poly, smallCube);
      }
    }

    [Test]
    public void RaycastCubeTouchingCube() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 10f));
      // Two cubes next to each other, touching.
      CsgObject leftCube = CsgOperations.ToCsg(ctx, Primitives.AxisAlignedBox(1, new Vector3(-2, 0, 0), Vector3.one, 1));
      CsgObject rightCube = CsgOperations.ToCsg(ctx, Primitives.AxisAlignedBox(2, Vector3.zero, Vector3.one, 1));

      // Classify all faces in both cubes.
      foreach (CsgPolygon poly in leftCube.polygons) {
        CsgOperations.ClassifyPolygonUsingRaycast(poly, rightCube);
      }
      foreach (CsgPolygon poly in rightCube.polygons) {
        CsgOperations.ClassifyPolygonUsingRaycast(poly, leftCube);
      }

      // We expect all faces to be OUTSIDE except one from each cube.  Those should bother be OPPOSITE.
      CsgPolygon leftPoly = null;
      CsgPolygon rightPoly = null;
      foreach (CsgPolygon poly in leftCube.polygons) {
        if (poly.status == PolygonStatus.OPPOSITE) {
          NUnit.Framework.Assert.Null(leftPoly, "Should be only one poly that is OPPOSITE");
          leftPoly = poly;
        } else {
          NUnit.Framework.Assert.AreEqual(PolygonStatus.OUTSIDE, poly.status);
        }
      }
      foreach (CsgPolygon poly in rightCube.polygons) {
        if (poly.status == PolygonStatus.OPPOSITE) {
          NUnit.Framework.Assert.Null(rightPoly, "Should be only one poly that is OPPOSITE");
          rightPoly = poly;
        } else {
          NUnit.Framework.Assert.AreEqual(PolygonStatus.OUTSIDE, poly.status);
        }
      }

      NUnit.Framework.Assert.NotNull(leftPoly);
      NUnit.Framework.Assert.NotNull(rightPoly);

      // Barycenters should be the same, normals should be opposite
      NUnit.Framework.Assert.AreEqual(0f, Vector3.Distance(leftPoly.baryCenter, rightPoly.baryCenter), 0.001f);
      NUnit.Framework.Assert.AreEqual(-1f, Vector3.Dot(leftPoly.plane.normal, rightPoly.plane.normal), 0.001f);
    }

    [Test]
    public void RaycastCubeOverlappingCube() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));

      // Two cubes next to each other, overlapping.
      CsgObject leftCube = CsgOperations.ToCsg(ctx,
        Primitives.AxisAlignedBox(1, new Vector3(-1, 0, 0), Vector3.one, 1));
      CsgObject rightCube = CsgOperations.ToCsg(ctx, Primitives.AxisAlignedBox(2, Vector3.zero, Vector3.one, 1));

      // Classify all faces in both cubes.
      // Each obj should have 4 polys SAME, 1 OUTSIDE and 1 INSIDE
      int numInside = 0;
      int numOutside = 0;
      int numSame = 0;
      foreach (CsgPolygon poly in leftCube.polygons) {
        CsgOperations.ClassifyPolygonUsingRaycast(poly, rightCube);
        switch (poly.status) {
          case PolygonStatus.INSIDE:
            numInside++;
            break;
          case PolygonStatus.OUTSIDE:
            numOutside++;
            break;
          case PolygonStatus.SAME:
            numSame++;
            break;
          default:
            NUnit.Framework.Assert.Fail("Didn't expect status: " + poly.status);
            break;
        }
      }
      NUnit.Framework.Assert.AreEqual(1, numInside);
      NUnit.Framework.Assert.AreEqual(1, numOutside);
      NUnit.Framework.Assert.AreEqual(4, numSame);

      numInside = 0;
      numOutside = 0;
      numSame = 0;
      foreach (CsgPolygon poly in rightCube.polygons) {
        CsgOperations.ClassifyPolygonUsingRaycast(poly, leftCube);
        switch (poly.status) {
          case PolygonStatus.INSIDE:
            numInside++;
            break;
          case PolygonStatus.OUTSIDE:
            numOutside++;
            break;
          case PolygonStatus.SAME:
            numSame++;
            break;
          default:
            NUnit.Framework.Assert.Fail("Didn't expect status: " + poly.status);
            break;
        }
      }
      NUnit.Framework.Assert.AreEqual(1, numInside);
      NUnit.Framework.Assert.AreEqual(1, numOutside);
      NUnit.Framework.Assert.AreEqual(4, numSame);
    }

    private void AssertPolyStatus(PolygonStatus status, CsgPolygon poly, CsgObject obj) {
      CsgOperations.ClassifyPolygonUsingRaycast(poly, obj);
      NUnit.Framework.Assert.AreEqual(status, poly.status);
    }

    private CsgPolygon toPoly(params Vector3[] verts) {
      List<CsgVertex> poly = new List<CsgVertex>();
      foreach (Vector3 vert in verts) {
        poly.Add(new CsgVertex(vert));
      }
      return new CsgPolygon(poly, new core.FaceProperties());
    }

    private CsgObject toObj(params CsgPolygon[] polys) {
      HashSet<CsgVertex> verts = new HashSet<CsgVertex>();
      foreach (CsgPolygon poly in polys) {
        foreach (CsgVertex vert in poly.vertices) {
          verts.Add(vert);
        }
      }
      return new CsgObject(new List<CsgPolygon>(polys), new List<CsgVertex>(verts));
    }

    [Test]
    public void SubtractCubeWithinCube() {
      // Small cube inside of a larger cube, both at the origin.
      MMesh smallCube = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one * 0.5f, 1);
      MMesh largeCube = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 1);

      // Subtracting large cube from small cube should result in empty space.
      NUnit.Framework.Assert.IsNull(CsgOperations.Subtract(smallCube, largeCube));

      // Subtracting small cube from large cube should result in just the large cube with an invisible hole.
      MMesh results = CsgOperations.Subtract(largeCube, smallCube);
      NUnit.Framework.Assert.AreEqual(12, results.faceCount);

      // Mesh should still be valid:
      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(results));
    }

    [Test]
    public void SubtractCubeOverlappingCube() {
      // Two cubes next to each other, overlapping.
      MMesh result = CsgOperations.Subtract(
        Primitives.AxisAlignedBox(1, new Vector3(-1, 0, 0), Vector3.one, 1),
        Primitives.AxisAlignedBox(2, Vector3.zero, Vector3.one, 1));

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(result, true));
    }

    //[Test] TODO: Uncomment when works :/
    public void SubtractSphereOverlappingCube() {
      // A cube and a sphere, overlapping.
      MMesh result = CsgOperations.Subtract(
        Primitives.AxisAlignedBox(1, new Vector3(-1, -0.7f, -0.3f), Vector3.one, 2),
        Primitives.AxisAlignedIcosphere(2, new Vector3(-.2f, 0, 0), Vector3.one, 1));

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(result, true));
    }

    [Test]
    public void TestOverlappingCubesSplitCorrectly() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));
      CsgObject leftCube = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedBox(1, new Vector3(-1, 0, 0), Vector3.one, 1));
      CsgObject rightCube = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedBox(2, Vector3.zero, Vector3.one, 1));
      TestObjectSplitMaintainsValidObject(ctx, leftCube, rightCube);
    }

    [Test]
    public void TestDiagonalOverlappingCubesSplitCorrectly() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));
      CsgObject leftCube = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedBox(1, new Vector3(-1, -0.7f, -0.3f), Vector3.one, 1));
      CsgObject rightCube = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedBox(2, Vector3.zero, Vector3.one, 1));
      TestObjectSplitMaintainsValidObject(ctx, leftCube, rightCube);
    }

    [Test]
    public void TestDiagonalOverlappingCubeAndSphereSplitCorrectly() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));
      CsgObject leftCube = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedBox(1, new Vector3(-1, -0.7f, -0.3f), Vector3.one, 1));
      CsgObject rightSphere = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedIcosphere(2, new Vector3(-.2f, 0, 0), Vector3.one, 1));
      TestObjectSplitMaintainsValidObject(ctx, leftCube, rightSphere);
    }

    // Same as above, but moved around a little.
    // [Test]  TODO: Uncomment when works :/
    public void TestDiagonalOverlappingCubeAndSphereSplitCorrectly2() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));
      CsgObject leftCube = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedBox(1, new Vector3(-1, -0.7f, -0.3f), Vector3.one, 1));
      CsgObject rightSphere = CsgOperations.ToCsg(
        ctx, Primitives.AxisAlignedIcosphere(2, new Vector3(-.23f, 0.01f, -0.23f), Vector3.one, 1));
      TestObjectSplitMaintainsValidObject(ctx, leftCube, rightSphere);
    }

    public void TestObjectSplitMaintainsValidObject(CsgContext ctx, CsgObject left, CsgObject right) {
      // Make sure they start with valid topology:
      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(
        CsgOperations.FromPolys(1, Vector3.zero, Quaternion.identity, left.polygons), true));
      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(
        CsgOperations.FromPolys(1, Vector3.zero, Quaternion.identity, right.polygons), true));

      // Now split against each other and ensure they are both still valid.
      CsgOperations.SplitObject(ctx, left, right);
      CsgOperations.SplitObject(ctx, right, left);

      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(
        CsgOperations.FromPolys(1, Vector3.zero, Quaternion.identity, left.polygons), true));
      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(
        CsgOperations.FromPolys(1, Vector3.zero, Quaternion.identity, right.polygons), true));

      // Split one last time, because the paper says so :)
      CsgOperations.SplitObject(ctx, left, right);
      NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(
        CsgOperations.FromPolys(1, Vector3.zero, Quaternion.identity, left.polygons), true));
    }
  }
}
