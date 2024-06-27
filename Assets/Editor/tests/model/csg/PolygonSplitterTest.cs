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
  public class PolygonSplitterTest {
    [Test]
    public void TestSegmentDescriptor() {
      SegmentDescriptor descriptor;
      CsgPolygon poly = toPoly(
        new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0));
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));

      descriptor = PolygonSplitter.CalcSegmentDescriptor(
        ctx, Vector3.zero, new Vector3(1, 0, 0),
        new float[] { 0, 0, 1, 1 },
        poly);
      AssertDescriptor(descriptor, Endpoint.VERTEX, Endpoint.EDGE, Endpoint.VERTEX);

      // TODO: is this case right?
      //descriptor = PolygonSplitter.CalcSegmentDescriptor(
      //  ctx, Vector3.zero, new Vector3(1, 0, 0),
      //  new float[] { -1, 1, -1, 1 },
      //  poly);
      //AssertDescriptor(descriptor, Endpoint.EDGE, Endpoint.EDGE, Endpoint.EDGE);

      descriptor = PolygonSplitter.CalcSegmentDescriptor(
        ctx, Vector3.zero, new Vector3(1, 0, 0),
        new float[] { -1, 1, 1, -1 },
        poly);
      AssertDescriptor(descriptor, Endpoint.EDGE, Endpoint.FACE, Endpoint.EDGE);

      descriptor = PolygonSplitter.CalcSegmentDescriptor(
        ctx, Vector3.zero, new Vector3(1, 0, 0),
        new float[] { 0, 1, 1, 0 },
        poly);
      AssertDescriptor(descriptor, Endpoint.VERTEX, Endpoint.EDGE, Endpoint.VERTEX);

      descriptor = PolygonSplitter.CalcSegmentDescriptor(
        ctx, Vector3.zero, new Vector3(1, 0, 0),
        new float[] { 0, 1, 0, 1 },
        poly);
      AssertDescriptor(descriptor, Endpoint.VERTEX, Endpoint.FACE, Endpoint.VERTEX);
    }

    private void AssertDescriptor(SegmentDescriptor descriptor, int start, int middle, int end) {
      NUnit.Framework.Assert.AreEqual(start, descriptor.start);
      NUnit.Framework.Assert.AreEqual(middle, descriptor.middle);
      NUnit.Framework.Assert.AreEqual(end, descriptor.end);
    }

    [Test]
    public void TestSplitAlignedPerpendicularFaces() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));

      CsgObject plane1 = toObj(toPoly(
        new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0)));
      CsgObject plane2 = toObj(toPoly(
        new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1)));

      CsgPolygon original = plane1.polygons[0];
      CsgPolygon splitBy = plane2.polygons[0];
      PolygonSplitter.SplitPolys(ctx, plane1, original, splitBy);

      // Should be split into exactly 2 polys.
      NUnit.Framework.Assert.AreEqual(2, plane1.polygons.Count);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(original, plane1.polygons, 2));

      // Resplitting should do nothing:
      foreach (CsgPolygon poly in plane1.polygons) {
        PolygonSplitter.SplitPolys(ctx, plane1, poly, splitBy);
        NUnit.Framework.Assert.AreEqual(2, plane1.polygons.Count);
      }
    }

    [Test]
    public void TestSplitUnalignedPerpendicularFaces() {
      CsgContext ctx = new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f));

      CsgObject plane1 = toObj(toPoly(
        new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0)));
      CsgObject plane2 = toObj(toPoly(
        new Vector3(-0.5f, 0, -0.5f), new Vector3(1, 0, -0.5f), new Vector3(1, 0, 1), new Vector3(-1, 0, 1)));

      CsgPolygon original = plane1.polygons[0];
      CsgPolygon splitBy = plane2.polygons[0];
      PolygonSplitter.SplitPolys(ctx, plane1, original, splitBy);

      // Should be split into exactly 3 polys.
      NUnit.Framework.Assert.AreEqual(3, plane1.polygons.Count);
      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(original, plane1.polygons, 1));

      // Resplitting should do nothing:
      foreach (CsgPolygon poly in plane1.polygons) {
        PolygonSplitter.SplitPolys(ctx, plane1, poly, splitBy);
        NUnit.Framework.Assert.AreEqual(3, plane1.polygons.Count);
      }
    }

    /// <summary>
    ///  Diagram for this test.  The polygon is split at E to X.  For the first case,
    ///  the split happens between edge B-D.  In the second the split happens on vertex Y.
    ///
    /// A                         B
    /// +-------------------------+
    /// |                         |
    /// + E         + X           | (Y)
    /// |                         |
    /// +-------------------------+
    /// C                         D
    /// </summary>
    [Test]
    public void TestSplit_VertexFaceFace() {
      CsgVertex vertA = new CsgVertex(new Vector3(-1, -1, 0));
      CsgVertex vertB = new CsgVertex(new Vector3(1, -1, 0));
      CsgVertex vertC = new CsgVertex(new Vector3(-1, 1, 0));
      CsgVertex vertD = new CsgVertex(new Vector3(1, 1, 0));
      CsgVertex vertE = new CsgVertex(new Vector3(-1, 0, 0));
      CsgVertex vertX = new CsgVertex(new Vector3(0, 0, 0));
      CsgVertex vertY = new CsgVertex(new Vector3(1, 0, 0));

      // Case 6.3h
      CsgPolygon poly = toPoly(vertE, vertA, vertB, vertD, vertC);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = descriptor.finalStart = Endpoint.VERTEX;
      descriptor.middle = descriptor.finalMiddle = Endpoint.FACE;
      descriptor.end = Endpoint.EDGE;
      descriptor.finalEnd = Endpoint.FACE;
      descriptor.startVertex = descriptor.finalStartVertex = vertE;
      descriptor.endVertex = vertB;
      descriptor.finalEndVertex = vertX;
      descriptor.startVertIdx = 0;
      descriptor.endVertIdx = 2;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.AreEqual(3, obj.polygons.Count);
      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));

      // Case 6.3g
      poly = toPoly(vertE, vertA, vertB, vertY, vertD, vertC);
      obj = toObj(poly);

      descriptor.start = descriptor.finalStart = Endpoint.VERTEX;
      descriptor.middle = descriptor.finalMiddle = Endpoint.FACE;
      descriptor.end = Endpoint.VERTEX;
      descriptor.finalEnd = Endpoint.FACE;
      descriptor.startVertex = descriptor.finalStartVertex = vertE;
      descriptor.endVertex = vertY;
      descriptor.finalEndVertex = vertX;
      descriptor.startVertIdx = 0;
      descriptor.endVertIdx = 3;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);
      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));
    }

    [Test]
    public void TestSplit_EdgeEdgeVertex() {
      CsgVertex vertA = new CsgVertex(new Vector3(-2, -1, -1));
      CsgVertex vertB = new CsgVertex(new Vector3(0, -1, -1));
      CsgVertex vertC = new CsgVertex(new Vector3(0, -1, 1));
      CsgVertex vertD = new CsgVertex(new Vector3(-2, -1, 1));
      CsgVertex vertX = new CsgVertex(new Vector3(-1, -1, -1));

      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.VERTEX;
      descriptor.middle = Endpoint.EDGE;
      descriptor.end = Endpoint.VERTEX;
      descriptor.finalStart = Endpoint.EDGE;
      descriptor.finalMiddle = Endpoint.EDGE;
      descriptor.finalEnd = Endpoint.VERTEX;
      descriptor.startVertex = vertA;
      descriptor.endVertex = vertB;
      descriptor.finalStartVertex = vertX;
      descriptor.finalEndVertex = vertB;
      descriptor.startVertIdx = 0;
      descriptor.endVertIdx = 1;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);
      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 1));
    }

    [Test]
    public void TestSplit_VertexEdgeEdge() {
      CsgVertex vertA = new CsgVertex(new Vector3(0, 0.3f, -1.3f));
      CsgVertex vertB = new CsgVertex(new Vector3(0, 0.3f, 0.7f));
      CsgVertex vertC = new CsgVertex(new Vector3(0, -1, 0.7f));
      CsgVertex vertD = new CsgVertex(new Vector3(0, -1, -1));
      CsgVertex vertX = new CsgVertex(new Vector3(0, 0.3f, -1));

      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();

      descriptor.start = Endpoint.VERTEX;
      descriptor.middle = Endpoint.EDGE;
      descriptor.end = Endpoint.EDGE;
      descriptor.finalStart = Endpoint.VERTEX;
      descriptor.finalMiddle = Endpoint.EDGE;
      descriptor.finalEnd = Endpoint.EDGE;
      descriptor.startVertex = vertD;
      descriptor.endVertex = vertX;
      descriptor.finalStartVertex = vertD;
      descriptor.finalEndVertex = vertX;
      descriptor.startVertIdx = 3;
      descriptor.endVertIdx = 0;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);
      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 1));
    }

    /// <summary>
    /// Tests case for a split which occurs along an edge.
    /// </summary>
    [Test]
    public void TestSplit_EdgeEdgeEdge_Fig_i() {
      CsgVertex vertA = new CsgVertex(new Vector3(-1, -1, 0));
      CsgVertex vertB = new CsgVertex(new Vector3(-1.2f, 0, 0));
      CsgVertex vertC = new CsgVertex(new Vector3(-1, 1, 0));
      CsgVertex vertD = new CsgVertex(new Vector3(1, 1, 0));
      CsgVertex vertE = new CsgVertex(new Vector3(1.2f, 0, 0));
      CsgVertex vertF = new CsgVertex(new Vector3(1, -1, 0));

      CsgVertex vertN = new CsgVertex(new Vector3(-.2f, -1, 0));
      CsgVertex vertM = new CsgVertex(new Vector3(-.1f, -1, 0));

      // Case 6.3i
      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD, vertE, vertF);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.EDGE;
      descriptor.middle = Endpoint.EDGE;
      descriptor.end = Endpoint.EDGE;

      descriptor.finalStart = Endpoint.EDGE;
      descriptor.finalMiddle = Endpoint.EDGE;
      descriptor.finalEnd = Endpoint.EDGE;

      descriptor.startVertex = vertF;
      descriptor.finalStartVertex = vertM;
      descriptor.endVertex = vertA;
      descriptor.finalEndVertex = vertN;
      descriptor.startVertIdx = 5;
      descriptor.endVertIdx = 0;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 1));
    }

    /// <summary>
    ///  Diagram for this test. Fig 6.3(n)
    ///
    ///    +----------+
    ///   / \        / \
    ///  /   \      /   \
    /// +B    +N   +M    +E
    ///  \   /      \   /
    ///   \ /        \ /
    ///    +----------+
    ///
    /// </summary>
    [Test]
    public void TestSplit_FaceFaceFace() {
      CsgVertex vertA = new CsgVertex(new Vector3(-1, -1, 0));
      CsgVertex vertB = new CsgVertex(new Vector3(-1.2f, 0, 0));
      CsgVertex vertC = new CsgVertex(new Vector3(-1, 1, 0));
      CsgVertex vertD = new CsgVertex(new Vector3(1, 1, 0));
      CsgVertex vertE = new CsgVertex(new Vector3(1.2f, 0, 0));
      CsgVertex vertF = new CsgVertex(new Vector3(1, -1, 0));

      CsgVertex vertN = new CsgVertex(new Vector3(0, 0, 0));
      CsgVertex vertM = new CsgVertex(new Vector3(1, 0, 0));

      // Case 6.3n
      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD, vertE, vertF);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.VERTEX;
      descriptor.middle = Endpoint.FACE;
      descriptor.end = Endpoint.VERTEX;

      descriptor.finalStart = Endpoint.FACE;
      descriptor.finalMiddle = Endpoint.FACE;
      descriptor.finalEnd = Endpoint.FACE;

      descriptor.startVertex = vertB;
      descriptor.finalStartVertex = vertN;
      descriptor.endVertex = vertE;
      descriptor.finalEndVertex = vertM;
      descriptor.startVertIdx = 1;
      descriptor.endVertIdx = 4;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));
    }

    /// <summary>
    ///  Diagram for this test. Fig 6.3(n)
    ///
    ///    +----------+
    ///   / \        /|
    ///  /   \      / |
    /// +B    +N   +M |
    ///  \   /      \ |
    ///   \ /        \|
    ///    +----------+
    ///
    /// </summary>
    [Test]
    public void TestSplit_FaceFaceFace_Fig_o() {
      CsgVertex vertA = new CsgVertex(new Vector3(-1, -1, 0));
      CsgVertex vertB = new CsgVertex(new Vector3(-1.2f, 0, 0));
      CsgVertex vertC = new CsgVertex(new Vector3(-1, 1, 0));
      CsgVertex vertD = new CsgVertex(new Vector3(1, 1, 0));
      CsgVertex vertF = new CsgVertex(new Vector3(1, -1, 0));

      CsgVertex vertN = new CsgVertex(new Vector3(0, 0, 0));
      CsgVertex vertM = new CsgVertex(new Vector3(.3f, 0, 0));
      CsgVertex endVertexOnEdge = new CsgVertex(new Vector3(1, 0, 0));

      // Case 6.3n
      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD, vertF);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.VERTEX;
      descriptor.middle = Endpoint.FACE;
      descriptor.end = Endpoint.EDGE;

      descriptor.finalStart = Endpoint.FACE;
      descriptor.finalMiddle = Endpoint.FACE;
      descriptor.finalEnd = Endpoint.FACE;

      descriptor.startVertex = vertB;
      descriptor.finalStartVertex = vertN;
      descriptor.endVertex = endVertexOnEdge;
      descriptor.finalEndVertex = vertM;
      descriptor.startVertIdx = 1;
      descriptor.endVertIdx = 3;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));
    }

    /// <summary>
    ///  Diagram for this test. Fig 6.3(n)
    ///
    ///    +----------+
    ///    |\        / \
    ///    | \      /   \
    ///    |  +N   +M    +E
    ///    | /      \   /
    ///    |/        \ /
    ///    +----------+
    ///
    /// </summary>
    [Test]
    public void TestSplit_FaceFaceFace_Fig_p() {
      CsgVertex vertA = new CsgVertex(new Vector3(-1, -1, 0));
      CsgVertex vertC = new CsgVertex(new Vector3(-1, 1, 0));
      CsgVertex vertD = new CsgVertex(new Vector3(1, 1, 0));
      CsgVertex vertE = new CsgVertex(new Vector3(1.2f, 0, 0));
      CsgVertex vertF = new CsgVertex(new Vector3(1, -1, 0));

      CsgVertex vertN = new CsgVertex(new Vector3(0, 0, 0));
      CsgVertex vertM = new CsgVertex(new Vector3(1, 0, 0));

      // Case 6.3n
      CsgPolygon poly = toPoly(vertA, vertC, vertD, vertE, vertF);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.EDGE;
      descriptor.middle = Endpoint.FACE;
      descriptor.end = Endpoint.VERTEX;

      descriptor.finalStart = Endpoint.FACE;
      descriptor.finalMiddle = Endpoint.FACE;
      descriptor.finalEnd = Endpoint.FACE;

      descriptor.startVertex = new CsgVertex(new Vector3(-1, 0, 0));
      descriptor.finalStartVertex = vertN;
      descriptor.endVertex = vertE;
      descriptor.finalEndVertex = vertM;
      descriptor.startVertIdx = 0;
      descriptor.endVertIdx = 3;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));
    }

    [Test]
    public void TestSplit_FaceFaceFace_ExtraCases_FFF() {
      CsgVertex vertA = new CsgVertex(new Vector3(-1, -1, 0));
      CsgVertex vertB = new CsgVertex(new Vector3(-1, 1, 0));
      CsgVertex vertC = new CsgVertex(new Vector3(1, 1, 0));
      CsgVertex vertD = new CsgVertex(new Vector3(1, -1, 0));

      CsgVertex vertN = new CsgVertex(new Vector3(-.9f, .1f, 0));
      CsgVertex vertM = new CsgVertex(new Vector3(-.8f, .2f, 0));

      // Case 6.3n
      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.FACE;
      descriptor.middle = Endpoint.FACE;
      descriptor.end = Endpoint.FACE;

      descriptor.finalStart = Endpoint.FACE;
      descriptor.finalMiddle = Endpoint.FACE;
      descriptor.finalEnd = Endpoint.FACE;

      descriptor.startVertex = new CsgVertex(new Vector3(-1, 0, 0));
      descriptor.finalStartVertex = vertN;
      descriptor.endVertex = new CsgVertex(new Vector3(0, 1, 0));
      descriptor.finalEndVertex = vertM;
      descriptor.startVertIdx = 0;
      descriptor.endVertIdx = 1;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));
    }

    [Test]
    public void TestSplit_FaceFaceFace_ExtraCases_FFF2() {
      CsgVertex vertA = new CsgVertex(new Vector3(-1, -1, 0));
      CsgVertex vertB = new CsgVertex(new Vector3(-1, 1, 0));
      CsgVertex vertC = new CsgVertex(new Vector3(1, 1, 0));
      CsgVertex vertD = new CsgVertex(new Vector3(1, -1, 0));

      CsgVertex vertN = new CsgVertex(new Vector3(-.8f, -.2f, 0));
      CsgVertex vertM = new CsgVertex(new Vector3(-.9f, -.1f, 0));

      // Case 6.3n
      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.FACE;
      descriptor.middle = Endpoint.FACE;
      descriptor.end = Endpoint.FACE;

      descriptor.finalStart = Endpoint.FACE;
      descriptor.finalMiddle = Endpoint.FACE;
      descriptor.finalEnd = Endpoint.FACE;

      descriptor.startVertex = new CsgVertex(new Vector3(0, -1, 0));
      descriptor.finalStartVertex = vertN;
      descriptor.endVertex = new CsgVertex(new Vector3(-1, 0, 0));
      descriptor.finalEndVertex = vertM;
      descriptor.startVertIdx = 3;
      descriptor.endVertIdx = 0;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));
    }

    [Test]
    public void TestSplit_FaceFaceFace_ExtraCases_VFF() {
      CsgVertex vertA = new CsgVertex(new Vector3(-0.8349411f, 0.1900979f, 0.7f));
      CsgVertex vertB = new CsgVertex(new Vector3(-2f, -1.7f, 0.7f));
      CsgVertex vertC = new CsgVertex(new Vector3(0f, -1.7f, 0.7f));

      CsgVertex vertN = new CsgVertex(new Vector3(-0.8349411f, 0.1900979f, 0.7f));
      CsgVertex vertM = new CsgVertex(new Vector3(-0.8349411f, -0.1900979f, 0.7f));

      // Case 6.3n
      CsgPolygon poly = toPoly(vertA, vertB, vertC);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = Endpoint.VERTEX;
      descriptor.middle = Endpoint.FACE;
      descriptor.end = Endpoint.EDGE;

      descriptor.finalStart = Endpoint.VERTEX;
      descriptor.finalMiddle = Endpoint.FACE;
      descriptor.finalEnd = Endpoint.FACE;

      descriptor.startVertex = new CsgVertex(new Vector3(-0.8349411f, 0.1900979f, 0.7f));
      descriptor.finalStartVertex = vertN;
      descriptor.endVertex = new CsgVertex(new Vector3(-0.834941f, -1.7f, 0.7f));
      descriptor.finalEndVertex = vertM;
      descriptor.startVertIdx = 0;
      descriptor.endVertIdx = 1;

      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 0));
    }

    [Test]
    public void TestSplit_VertexEdgeEdge_NarrowPoly() {

      CsgVertex vertA = new CsgVertex(new Vector3(-0.6395259f, 0.3f, -1.063093f));
      CsgVertex vertB = new CsgVertex(new Vector3(-0.6942508f, 0.3f, -1.051798f));
      CsgVertex vertC = new CsgVertex(new Vector3(-0.73f, 0.3f, -1.039017f));
      CsgVertex vertD = new CsgVertex(new Vector3(-0.7515792f, 0.3f, -1.021583f));
      CsgVertex vertX = new CsgVertex(new Vector3(-0.6992299f, 0.3f, -1.05077f));

      CsgPolygon poly = toPoly(vertA, vertB, vertC, vertD);
      CsgObject obj = toObj(poly);

      SegmentDescriptor descriptor = new SegmentDescriptor();
      descriptor.start = 1;
      descriptor.middle = 3;
      descriptor.end = 1;
      descriptor.finalStart = 1;
      descriptor.finalMiddle = 3;
      descriptor.finalEnd = 3;
      descriptor.startVertex = vertC;
      descriptor.endVertex = vertB;
      descriptor.finalStartVertex = vertC;
      descriptor.finalEndVertex = vertX;
      descriptor.startVertIdx = 2;
      descriptor.endVertIdx = 1;
      PolygonSplitter.SplitPolyOnSegment(obj, poly, descriptor);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly, obj.polygons, 1));
    }


    [Test]
    public void TestTwoPolygons() {
      CsgPolygon poly1 = toPoly(
        new CsgVertex(new Vector3(-2f, 0.3f, -1.3f)),
        new CsgVertex(new Vector3(-2f, 0.3f, 0.7f)),
        new CsgVertex(new Vector3(0f, 0.3f, 0.7f)),
        new CsgVertex(new Vector3(0f, 0.3f, -1.3f))
      );
      CsgPolygon poly2 = toPoly(
        new CsgVertex(new Vector3(-1.050651f, 0f, 0.5257311f)),
        new CsgVertex(new Vector3(-0.7f, 0.309017f, 0.809017f)),
        new CsgVertex(new Vector3(-1.009017f, 0.5f, 0.309017f))
      );

      CsgObject obj = toObj(poly1);
      PolygonSplitter.SplitPolys(new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f)), obj, poly1, poly2);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly1, obj.polygons, 1));
    }

    //[TEST]
    public void TestTwoPolygons_EFF() {
      CsgPolygon poly1 = toPoly(
        new CsgVertex(new Vector3(0.1f, 0.9f, 0.4f)),
        new CsgVertex(new Vector3(0.1f, 1.1f, 0.4f)),
        new CsgVertex(new Vector3(0.1f, 1.1f, 0.6f)),
        new CsgVertex(new Vector3(0.1f, 0.9f, 0.6f))
      );
      CsgPolygon poly2 = toPoly(
        new CsgVertex(new Vector3(0.0812017f, 1.131002f, 0.5002f)),
        new CsgVertex(new Vector3(0.1312017f, 1.1001f, 0.5192983f)),
        new CsgVertex(new Vector3(0.1003f, 1.081002f, 0.4692983f))
      );

      CsgObject obj = toObj(poly1);
      PolygonSplitter.SplitPolys(new CsgContext(new Bounds(Vector3.zero, Vector3.one * 5f)), obj, poly1, poly2);

      NUnit.Framework.Assert.IsTrue(CsgUtil.IsValidPolygonSplit(poly1, obj.polygons, 1));
    }

    private CsgPolygon toPoly(params CsgVertex[] verts) {
      return new CsgPolygon(new List<CsgVertex>(verts), new core.FaceProperties());
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
  }
}
