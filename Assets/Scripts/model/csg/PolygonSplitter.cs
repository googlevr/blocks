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
using UnityEngine;
using com.google.apps.peltzer.client.model.core;

namespace com.google.apps.peltzer.client.model.csg {

  /// <summary>
  ///   Constants for segment endpoint type and segment types.
  /// </summary>
  public class Endpoint {
    public const int VERTEX = 1;
    public const int FACE = 2;
    public const int EDGE = 3;

    public const int V_V_V = VERTEX * 100 + VERTEX * 10 + VERTEX;
    public const int V_E_V = VERTEX * 100 + EDGE * 10 + VERTEX;
    public const int V_E_E = VERTEX * 100 + EDGE * 10 + EDGE;
    public const int V_F_V = VERTEX * 100 + FACE * 10 + VERTEX;
    public const int V_F_E = VERTEX * 100 + FACE * 10 + EDGE;
    public const int V_F_F = VERTEX * 100 + FACE * 10 + FACE;
    public const int E_E_V = EDGE * 100 + EDGE * 10 + VERTEX;
    public const int E_E_E = EDGE * 100 + EDGE * 10 + EDGE;
    public const int E_F_V = EDGE * 100 + FACE * 10 + VERTEX;
    public const int E_F_E = EDGE * 100 + FACE * 10 + EDGE;
    public const int E_F_F = EDGE * 100 + FACE * 10 + FACE;
    public const int F_F_V = FACE * 100 + FACE * 10 + VERTEX;
    public const int F_F_E = FACE * 100 + FACE * 10 + EDGE;
    public const int F_F_F = FACE * 100 + FACE * 10 + FACE;

    public static int combine(int a, int b, int c) {
      return a * 100 + b * 10 + c;
    }
  }

  /// <summary>
  ///   Descriptor of segment which is the intersection of two polygons.
  ///   See Fig 5.1.
  /// </summary>
  public class SegmentDescriptor {
    public int start;
    public int middle;
    public int end;

    public int startVertIdx;
    public int endVertIdx;
    public float startDist;
    public float endDist;

    public CsgVertex startVertex;
    public CsgVertex endVertex;

    // Values after trimmed to the splitting poly.
    public int finalStart;
    public int finalMiddle;
    public int finalEnd;

    public CsgVertex finalStartVertex;
    public CsgVertex finalEndVertex;
  }

  /// <summary>
  ///   Code to split a polygon with another one.  The resulting polygons must always be convex without holes.
  /// </summary>
  public class PolygonSplitter {
    // Enable to get debug logging.
    private const bool DEBUG = false;
    // Enable to add polygon split checks.
    private const bool VERIFY_POLY_SPLITS = false;

    ///   Section 6: Split both of the polygons with respect to each other.
    public static bool SplitPolys(CsgContext ctx, CsgObject objA, CsgPolygon polyA, CsgPolygon polyB) {
      float[] distAtoB = DistanceFromVertsToPlane(polyA, polyB.plane);
      if (!CrossesPlane(distAtoB)) {
        // All verts on one side of plane, no intersection.
        return false;
      }
      float[] distBtoA = DistanceFromVertsToPlane(polyB, polyA.plane);
      if (!CrossesPlane(distAtoB)) {
        // All verts on one side of plane, no intersection.
        return false;
      }

      // Get the line that is the intersection of the planes.
      Vector3 linePt;
      Vector3 lineDir;
      if (!PlanePlaneIntersection(out linePt, out lineDir, polyA.plane, polyB.plane)) {
        // Planes are parallel
        return false;
      }

      SegmentDescriptor segA = CalcSegmentDescriptor(ctx, linePt, lineDir, distAtoB, polyA);
      SegmentDescriptor segB = CalcSegmentDescriptor(ctx, linePt, lineDir, distBtoA, polyB);

      if (segA == null || segB == null) {
        return false;
      }

      // If the segments don't overlap, there is no intersection.
      if (segA.endDist <= segB.startDist || segB.endDist <= segA.startDist) {
        return false;
      }

      TrimTo(segA, segB);

      return SplitPolyOnSegment(objA, polyA, segA);
    }

    // Trim segment A to segment B.  This is where polygon A is cut by polygon B.
    private static void TrimTo(SegmentDescriptor segA, SegmentDescriptor segB) {
      segA.finalMiddle = segA.middle;
      if (segB.startDist > segA.startDist && Math.Abs(segA.startDist - segB.startDist) > CsgMath.EPSILON) {
        // Push segA start distance up.
        segA.finalStart = segA.middle;
        segA.finalStartVertex = segB.startVertex;
      } else {
        segA.finalStart = segA.start;
        segA.finalStartVertex = segA.startVertex;
      }
      if (segB.endDist < segA.endDist && Math.Abs(segA.endDist - segB.endDist) > CsgMath.EPSILON) {
        // Pull segA end distance back.
        segA.finalEnd = segA.middle;
        segA.finalEndVertex = segB.endVertex;
      } else {
        segA.finalEnd = segA.end;
        segA.finalEndVertex = segA.endVertex;
      }

      if (segA.finalStartVertex == segA.finalEndVertex) {
        // Trimmed to a single vertex.
        segA.finalStart = Endpoint.VERTEX;
        segA.finalMiddle = Endpoint.VERTEX;
        segA.finalEnd = Endpoint.VERTEX;
      }
    }

    // Given the exact segment where a polygon is split by the other, actually split the polygon.
    // The way the polygon is split depends exactly on how the segment intersects the polygon.
    // See Figure 6.3 for all the gory details.
    // Public for testing.
    public static bool SplitPolyOnSegment(CsgObject obj, CsgPolygon poly, SegmentDescriptor seg) {
      int splitType = Endpoint.combine(seg.finalStart, seg.finalMiddle, seg.finalEnd);

      if (DEBUG) {
        Console.Write("Split type: " + splitType + " seg = " + seg.startVertIdx + ", " + seg.endVertIdx + ", " + poly.vertices.Count);
      }

      // For symmetrical cases, swap everything
      if (splitType == Endpoint.F_F_V || splitType == Endpoint.E_E_V) {
        if (DEBUG) {
          Console.Write("Swapped");
        }
        Swap(seg);
        splitType = Endpoint.combine(seg.finalStart, seg.finalMiddle, seg.finalEnd);
      }

      switch (splitType) {
        case Endpoint.E_E_E: {
            if (Vector3.Distance(seg.finalStartVertex.loc, seg.startVertex.loc) < CsgMath.EPSILON
                && Vector3.Distance(seg.finalEndVertex.loc, seg.endVertex.loc) < CsgMath.EPSILON) {
              return false;
            }
            List<List<CsgVertex>> newPolys = new List<List<CsgVertex>>();
            int startInClockwiseOrder = seg.startVertIdx;
            int endInClockwiseOrder = seg.endVertIdx;
            CsgVertex startVertInClockwiseOrder = seg.finalStartVertex;
            CsgVertex endVertInClockwiseOrder = seg.finalEndVertex;
            bool pointsAreDifferent = startVertInClockwiseOrder != endVertInClockwiseOrder;
            if ((seg.endVertIdx + 1) % poly.vertices.Count == seg.startVertIdx) {
              startInClockwiseOrder = seg.endVertIdx;
              endInClockwiseOrder = seg.startVertIdx;
              startVertInClockwiseOrder = seg.finalEndVertex;
              endVertInClockwiseOrder = seg.finalStartVertex;
            }

            newPolys.Add(new List<CsgVertex>() {
              poly.vertices[(startInClockwiseOrder - 1 + poly.vertices.Count) % poly.vertices.Count],
              poly.vertices[startInClockwiseOrder],
              startVertInClockwiseOrder
            });
            if (pointsAreDifferent) {
              newPolys.Add(new List<CsgVertex>() {
                poly.vertices[(startInClockwiseOrder - 1 + poly.vertices.Count) % poly.vertices.Count],
                startVertInClockwiseOrder,
                endVertInClockwiseOrder
              });
            }
            List<CsgVertex> theRest = new List<CsgVertex>();
            for (int i = endInClockwiseOrder; i != startInClockwiseOrder; i = (i + 1) % poly.vertices.Count) {
              theRest.Add(poly.vertices[i]);
            }
            theRest.Add(endVertInClockwiseOrder);
            newPolys.Add(theRest);
            if (SafeReplacePolys(seg, obj, poly, newPolys.ToArray())) {
              obj.vertices.Add(startVertInClockwiseOrder);
              if (pointsAreDifferent) {
                obj.vertices.Add(endVertInClockwiseOrder);
              }
              return true;
            } else {
              return false;
            }
          }
        case Endpoint.V_V_V: {
            // Fig 6.3 (a)
            seg.finalStartVertex.status = VertexStatus.BOUNDARY;
            return false;
          }
        case Endpoint.V_E_V: {
            // Fig 6.3 (b)
            seg.finalStartVertex.status = VertexStatus.BOUNDARY;
            seg.finalEndVertex.status = VertexStatus.BOUNDARY;
            return false;
          }
        case Endpoint.V_E_E: {
            // Fig 6.3 (c)
            if (seg.endVertex == seg.finalEndVertex && seg.startVertex == seg.finalStartVertex) {
              //Debug.Log("no split is possible because segment is bad");
              return false;
            }
            List<CsgVertex> mainPart = new List<CsgVertex>();
            List<CsgVertex> triPart = new List<CsgVertex>();

            for (int i = (seg.startVertIdx + 1) % poly.vertices.Count; i != seg.startVertIdx; i = (i + 1) % poly.vertices.Count) {
              mainPart.Add(poly.vertices[i]);
            }
            mainPart.Add(seg.finalEndVertex);
            if (seg.startVertIdx == seg.endVertIdx) {
              // I don't think this ever happens.
              triPart.Add(poly.vertices[(seg.startVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count]);
              triPart.Add(poly.vertices[seg.startVertIdx]);
              triPart.Add(seg.finalEndVertex);
            } else if (((seg.endVertIdx + 1) % poly.vertices.Count) == seg.startVertIdx) {
              triPart.Add(seg.finalEndVertex);
              triPart.Add(poly.vertices[seg.startVertIdx]);
              triPart.Add(poly.vertices[(seg.startVertIdx + 1) % poly.vertices.Count]);
            } else if ((seg.startVertIdx + 1) % poly.vertices.Count == seg.endVertIdx) {
              triPart.Add(poly.vertices[(seg.startVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count]);
              triPart.Add(poly.vertices[seg.startVertIdx]);
              triPart.Add(seg.finalEndVertex);
            } else {
              // This should never occur.
              return false;
            }

            if (SafeReplacePolys(seg, obj, poly, mainPart, triPart)) {
              obj.vertices.Add(seg.finalEndVertex);

              poly.vertices[seg.startVertIdx].status = VertexStatus.BOUNDARY;
              return true;
            } else {
              return false;
            }
          }
        case Endpoint.E_E_V: {
            // Fig 6.3 (c)
            List<CsgVertex> mainPart = new List<CsgVertex>();
            List<CsgVertex> triPart = new List<CsgVertex>();

            CsgVertex vertToAdd = null;

            if (seg.startVertIdx == seg.endVertIdx) {
              for (int i = (seg.startVertIdx + 1) % poly.vertices.Count; i != seg.startVertIdx; i = (i + 1) % poly.vertices.Count) {
                mainPart.Add(poly.vertices[i]);
              }
              mainPart.Add(seg.finalEndVertex);
              triPart.Add(poly.vertices[seg.startVertIdx]);
              triPart.Add(poly.vertices[(seg.startVertIdx + 1) % poly.vertices.Count]);
              triPart.Add(seg.finalEndVertex);
              vertToAdd = seg.finalEndVertex;
            } else if (((seg.endVertIdx + 1) % poly.vertices.Count) == seg.startVertIdx) {
              for (int i = seg.startVertIdx; i != seg.endVertIdx; i = (i + 1) % poly.vertices.Count) {
                mainPart.Add(poly.vertices[i]);
              }
              mainPart.Add(seg.finalStartVertex);
              triPart.Add(poly.vertices[(seg.endVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count]);
              triPart.Add(poly.vertices[seg.endVertIdx]);
              triPart.Add(seg.finalStartVertex);
              vertToAdd = seg.finalStartVertex;
            } else if ((seg.startVertIdx + 1) % poly.vertices.Count == seg.endVertIdx) {
              for (int i = (seg.endVertIdx + 1) % poly.vertices.Count; i != seg.endVertIdx; i = (i + 1) % poly.vertices.Count) {
                mainPart.Add(poly.vertices[i]);
              }
              mainPart.Add(seg.finalStartVertex);
              triPart.Add(poly.vertices[seg.endVertIdx]);
              triPart.Add(poly.vertices[(seg.endVertIdx + 1) % poly.vertices.Count]);
              triPart.Add(seg.finalStartVertex);
              vertToAdd = seg.finalStartVertex;
            } else {
              // Should not occur
              return false;
            }

            if (vertToAdd != null) {
              obj.vertices.Add(vertToAdd);
            }
            poly.vertices[seg.endVertIdx].status = VertexStatus.BOUNDARY;

            return SafeReplacePolys(seg, obj, poly, mainPart, triPart);
          }
        case Endpoint.V_F_V: {
            // Fig 6.3 (e)
            List<CsgVertex> topPart = new List<CsgVertex>();
            for (int i = seg.startVertIdx; i != (seg.endVertIdx + 1) % poly.vertices.Count; i = (i + 1) % poly.vertices.Count) {
              topPart.Add(poly.vertices[i]);
            }
            List<CsgVertex> bottomPart = new List<CsgVertex>();
            for (int i = seg.endVertIdx; i != (seg.startVertIdx + 1) % poly.vertices.Count; i = (i + 1) % poly.vertices.Count) {
              bottomPart.Add(poly.vertices[i]);
            }

            if(SafeReplacePolys(seg, obj, poly, topPart, bottomPart)) {
              poly.vertices[seg.startVertIdx].status = VertexStatus.BOUNDARY;
              poly.vertices[seg.endVertIdx].status = VertexStatus.BOUNDARY;
              return true;
            } else {
              return false;
            }
          }
        case Endpoint.E_F_E: {
            // Fig 6.3 (k)
            List<CsgVertex> topPart = new List<CsgVertex>();
            List<CsgVertex> bottomPart = new List<CsgVertex>();
            int vCount = poly.vertices.Count;
            int startI = seg.startVertIdx;
            int endI = seg.endVertIdx;
            CsgVertex startV = seg.finalStartVertex;
            CsgVertex endV = seg.finalEndVertex;
            if ((endI + 1) % vCount == startI) {
              startI = seg.endVertIdx;
              endI = seg.startVertIdx;
              startV = seg.finalEndVertex;
              endV = seg.finalStartVertex;
            }
            for (int i = (startI + 1) % vCount; i != (endI + 1) % vCount; i = (i + 1) % vCount) {
              topPart.Add(poly.vertices[i]);
            }
            topPart.Add(endV);
            topPart.Add(startV);
            for (int i = (endI + 1) % vCount; i != (startI + 1) % vCount; i = (i + 1) % vCount) {
              bottomPart.Add(poly.vertices[i]);
            }
            bottomPart.Add(startV);
            bottomPart.Add(endV);

            if (SafeReplacePolys(seg, obj, poly, topPart, bottomPart)) {
              obj.vertices.Add(seg.finalStartVertex);
              obj.vertices.Add(seg.finalEndVertex);
              return true;
            } else {
              return false;
            }
          }
        case Endpoint.V_F_E: {
            // Fig 6.3 (f)
            List<CsgVertex> topPart = new List<CsgVertex>();
            List<CsgVertex> bottomPart = new List<CsgVertex>();
            topPart.Add(seg.finalEndVertex);
            topPart.Add(seg.finalStartVertex);

            bottomPart.Add(seg.finalStartVertex);
            bottomPart.Add(seg.finalEndVertex);

            bool topHalf = true;
            for (int i = 1; i < poly.vertices.Count; i++) {
              int idx = (seg.startVertIdx + i) % poly.vertices.Count;
              if (topHalf) {
                topPart.Add(poly.vertices[idx]);
              } else {
                bottomPart.Add(poly.vertices[idx]);
              }
              if (idx == seg.endVertIdx) {
                topHalf = false;
              }
            }

            if (SafeReplacePolys(seg, obj, poly, topPart, bottomPart)) {
              obj.vertices.Add(seg.finalStartVertex);
              obj.vertices.Add(seg.finalEndVertex);
              return true;
            } else {
              return false;
            }
          }
        case Endpoint.E_F_V: {
            // Fig 6.3(f)
            List<CsgVertex> topPart = new List<CsgVertex>();
            for (int i = seg.endVertIdx; i != (seg.startVertIdx + 1) % poly.vertices.Count; i = (i + 1) % poly.vertices.Count) {
              topPart.Add(poly.vertices[i]);
            }
            topPart.Add(seg.finalStartVertex);
            List<CsgVertex> bottomPart = new List<CsgVertex>();
            bottomPart.Add(seg.finalStartVertex);
            for (int i = (seg.startVertIdx + 1) % poly.vertices.Count; i != (seg.endVertIdx + 1) % poly.vertices.Count; i = (i + 1) % poly.vertices.Count) {
              bottomPart.Add(poly.vertices[i]);
            }

            obj.vertices.Add(seg.finalStartVertex);
            poly.vertices[seg.endVertIdx].status = VertexStatus.BOUNDARY;

            return SafeReplacePolys(seg, obj, poly, topPart, bottomPart);
          }
        case Endpoint.E_F_F: {
            // Fig 6.3 (l/m)
            List<CsgVertex> topPart = new List<CsgVertex>();
            topPart.Add(seg.finalStartVertex);
            int topEndIdx = seg.end == Endpoint.VERTEX ? (seg.endVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count
                : seg.endVertIdx;
            for (int idx = (seg.startVertIdx + 1) % poly.vertices.Count; idx != topEndIdx;
                idx = (idx + 1) % poly.vertices.Count) {
              topPart.Add(poly.vertices[idx]);
            }
            topPart.Add(poly.vertices[topEndIdx]);
            topPart.Add(seg.finalEndVertex);

            List<CsgVertex> bottomPart = new List<CsgVertex>();
            bottomPart.Add(seg.finalEndVertex);
            for (int idx = (seg.endVertIdx + 1) % poly.vertices.Count; idx != seg.startVertIdx;
                idx = (idx + 1) % poly.vertices.Count) {
              bottomPart.Add(poly.vertices[idx]);
            }
            bottomPart.Add(poly.vertices[seg.startVertIdx]);
            bottomPart.Add(seg.finalStartVertex);

            bool result = false;
            if (seg.end == Endpoint.VERTEX) {
              // Fig 6.3 (l)
              CsgVertex prevVert = poly.vertices[(seg.endVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count];
              CsgVertex nextVert = poly.vertices[(seg.endVertIdx + 1) % poly.vertices.Count];

              result = SafeReplacePolys(seg, obj, poly, topPart, bottomPart,
                new List<CsgVertex>() { seg.finalEndVertex, prevVert, seg.endVertex },
                new List<CsgVertex>() { seg.finalEndVertex, seg.endVertex, nextVert });
            } else {
              // Fig 6.3 (m)
              CsgVertex nextVert = poly.vertices[(seg.endVertIdx + 1) % poly.vertices.Count];
              result = SafeReplacePolys(seg, obj, poly, topPart, bottomPart,
                new List<CsgVertex>() { seg.finalEndVertex, poly.vertices[seg.endVertIdx], nextVert });
            }
            if (result) {
              obj.vertices.Add(seg.finalStartVertex);
              obj.vertices.Add(seg.finalEndVertex);
            }
            return result;
          }
        case Endpoint.F_F_E: {
            List<List<CsgVertex>> newPolys = new List<List<CsgVertex>>();
            newPolys.Add(new List<CsgVertex>() {
              poly.vertices[seg.startVertIdx],
              poly.vertices[(seg.startVertIdx + 1) % poly.vertices.Count],
              seg.finalStartVertex
            });

            List<CsgVertex> topPart = new List<CsgVertex>();
            for (int i = (seg.startVertIdx + 1) % poly.vertices.Count; i != (seg.endVertIdx + 1) % poly.vertices.Count; i = (i + 1) % poly.vertices.Count) {
              topPart.Add(poly.vertices[i]);
            }
            topPart.Add(seg.finalEndVertex);
            topPart.Add(seg.finalStartVertex);
            newPolys.Add(topPart);

            if (seg.start == Endpoint.VERTEX) {
              newPolys.Add(new List<CsgVertex>() {
                poly.vertices[(seg.startVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count],
                poly.vertices[seg.startVertIdx],
                seg.finalStartVertex
              });
              // bottom part goes until right before begin
              List<CsgVertex> bottomPart = new List<CsgVertex>();
              for (int i = (seg.endVertIdx + 1) % poly.vertices.Count; i != seg.startVertIdx; i = (i + 1) % poly.vertices.Count) {
                bottomPart.Add(poly.vertices[i]);
              }
              bottomPart.Add(seg.finalStartVertex);
              bottomPart.Add(seg.finalEndVertex);
              newPolys.Add(bottomPart);
            } else {
              List<CsgVertex> bottomPart = new List<CsgVertex>();
              for (int i = (seg.endVertIdx + 1) % poly.vertices.Count; i != (seg.startVertIdx + 1) % poly.vertices.Count; i = (i + 1) % poly.vertices.Count) {
                bottomPart.Add(poly.vertices[i]);
              }
              bottomPart.Add(seg.finalStartVertex);
              bottomPart.Add(seg.finalEndVertex);
              newPolys.Add(bottomPart);
            }

            obj.vertices.Add(seg.finalStartVertex);
            obj.vertices.Add(seg.finalEndVertex);

            return SafeReplacePolys(seg, obj, poly, newPolys.ToArray());
          }
        case Endpoint.V_F_F: {
            // Fig 6.3 (g/h)
            obj.vertices.Add(seg.finalEndVertex);
            List<CsgVertex> topPart = new List<CsgVertex>();
            int topEndIdx = seg.end == Endpoint.VERTEX ? (seg.endVertIdx - 1 + poly.vertices.Count)
                % poly.vertices.Count : seg.endVertIdx;
            topPart.Add(seg.finalEndVertex);
            for (int idx = seg.startVertIdx; idx != topEndIdx; idx = (idx + 1) % poly.vertices.Count) {
              topPart.Add(poly.vertices[idx]);
            }
            topPart.Add(poly.vertices[topEndIdx]);

            List<CsgVertex> bottomPart = new List<CsgVertex>();
            bottomPart.Add(seg.finalEndVertex);
            for (int idx = (seg.endVertIdx + 1) % poly.vertices.Count; idx != seg.startVertIdx;
                idx = (idx + 1) % poly.vertices.Count) {
              bottomPart.Add(poly.vertices[idx]);
            }
            bottomPart.Add(poly.vertices[seg.startVertIdx]);

            if (seg.end == Endpoint.VERTEX) {
              // Fig 6.3 (g)
              CsgVertex prevVert = poly.vertices[(seg.endVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count];
              CsgVertex nextVert = poly.vertices[(seg.endVertIdx + 1) % poly.vertices.Count];

              return SafeReplacePolys(seg, obj, poly, topPart, bottomPart,
                new List<CsgVertex>() { seg.finalEndVertex, prevVert, seg.endVertex },
                new List<CsgVertex>() { seg.finalEndVertex, seg.endVertex, nextVert });
            } else {
              // Fig 6.3 (h)
              CsgVertex nextVert = poly.vertices[(seg.endVertIdx + 1) % poly.vertices.Count];
              return SafeReplacePolys(seg, obj, poly, topPart, bottomPart,
                new List<CsgVertex>() { seg.finalEndVertex, poly.vertices[seg.endVertIdx], nextVert });
            }
          }
        case Endpoint.F_F_F: {
            List<List<CsgVertex>> newPolys = new List<List<CsgVertex>>();

            List<CsgVertex> interiorPoints = new List<CsgVertex>(); // may just be one point
            interiorPoints.Add(seg.finalEndVertex);
            if (Mathf.Abs(seg.finalEndVertex.loc.x - seg.finalStartVertex.loc.x) > CsgMath.EPSILON
              || Mathf.Abs(seg.finalEndVertex.loc.y - seg.finalStartVertex.loc.y) > CsgMath.EPSILON
              || Mathf.Abs(seg.finalEndVertex.loc.z - seg.finalStartVertex.loc.z) > CsgMath.EPSILON) {
              interiorPoints.Add(seg.finalStartVertex);
            }

            // begin part
            newPolys.Add(new List<CsgVertex>() {
                poly.vertices[seg.startVertIdx],
                poly.vertices[(seg.startVertIdx + 1) % poly.vertices.Count],
                interiorPoints[interiorPoints.Count - 1],
              });
            if (seg.start == Endpoint.VERTEX) {
              newPolys.Add(new List<CsgVertex>() {
                poly.vertices[seg.startVertIdx],
                interiorPoints[interiorPoints.Count - 1],
                poly.vertices[(seg.startVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count],
              });
            }

            // top part
            List<CsgVertex> topPart = new List<CsgVertex>();
            int endIdx = seg.end == Endpoint.VERTEX ? seg.endVertIdx : (seg.endVertIdx + 1) % poly.vertices.Count;
            for (int i = (seg.startVertIdx + 1) % poly.vertices.Count; i != endIdx; i = (i + 1) % poly.vertices.Count) {
              topPart.Add(poly.vertices[i]);
            }
            topPart.AddRange(interiorPoints);
            newPolys.Add(topPart);

            // end part
            newPolys.Add(new List<CsgVertex>() {
                poly.vertices[seg.endVertIdx],
                poly.vertices[(seg.endVertIdx + 1) % poly.vertices.Count],
                interiorPoints[0],
              });
            if (seg.end == Endpoint.VERTEX) {
              newPolys.Add(new List<CsgVertex>() {
                poly.vertices[(seg.endVertIdx - 1 + poly.vertices.Count) % poly.vertices.Count],
                poly.vertices[seg.endVertIdx],
                interiorPoints[0],
              });
            }

            // bottom part
            List<CsgVertex> bottomPart = new List<CsgVertex>();
            endIdx = seg.start == Endpoint.VERTEX ? seg.startVertIdx : (seg.startVertIdx + 1) % poly.vertices.Count;
            for (int i = (seg.endVertIdx + 1) % poly.vertices.Count; i != endIdx; i = (i + 1) % poly.vertices.Count) {
              bottomPart.Add(poly.vertices[i]);
            }
            bottomPart.AddRange(interiorPoints.Reverse<CsgVertex>().ToList());
            newPolys.Add(bottomPart);

            foreach (CsgVertex interiorPoint in interiorPoints) {
              obj.vertices.Add(interiorPoint);
            }

            return SafeReplacePolys(seg, obj, poly, newPolys.ToArray());
          }
        default:
          if (DEBUG) {
            Console.Write("Unimplemented split type: " + splitType);
            List<CsgVertex>[] otherPolys = new List<CsgVertex>[2];
            otherPolys[0] = new List<CsgVertex>() { seg.finalStartVertex, seg.finalEndVertex };
            otherPolys[1] = new List<CsgVertex>() { seg.startVertex, seg.endVertex };
            DumpPolygonsForDebug(poly, otherPolys);
          }
          Debug.Log("!!!!!>>>>>     Unhandled case: " + splitType);
          break;
      }
      return false;
    }

    // Test helper, dumps info about a split to help generate a test case.
    private static void DumpSplitInfo(CsgPolygon poly, SegmentDescriptor seg) {
      Console.Write("\nPoly:");
      foreach (CsgVertex vert in poly.vertices) {
        Console.Write(Str(vert));
      }
      Console.Write("\nSeg:");
      Console.Write("descriptor.start = " + seg.start);
      Console.Write("descriptor.middle = " + seg.middle);
      Console.Write("descriptor.end = " + seg.end);
      Console.Write("descriptor.finalStart = " + seg.finalStart);
      Console.Write("descriptor.finalMiddle = " + seg.finalMiddle);
      Console.Write("descriptor.finalEnd = " + seg.finalEnd);
      Console.Write("descriptor.startVertex = " + Str(seg.startVertex));
      Console.Write("descriptor.endVertex = " + Str(seg.endVertex));
      Console.Write("descriptor.finalStartVertex = " + Str(seg.finalStartVertex));
      Console.Write("descriptor.finalEndVertex = " + Str(seg.finalEndVertex));
      Console.Write("descriptor.startVertIdx = " + seg.startVertIdx);
      Console.Write("descriptor.endVertIdx = " + seg.endVertIdx);
    }

    private static string Str(CsgVertex vert) {
      return vert.loc.x + "f, " + vert.loc.y + "f, " + vert.loc.z + "f";
    }


    //  Replace the polygon in the object with its components.  If the components have zero-area, then skip.  If the
    // result would be identical, don't bother doing anything.
    // SegmentDescriptor param is only here for debugging.
    private static bool SafeReplacePolys(SegmentDescriptor seg, CsgObject obj, CsgPolygon oldPoly,
      params List<CsgVertex>[] newPolyVerts) {

      List<CsgPolygon> newPolys = new List<CsgPolygon>();
      foreach (List<CsgVertex> verts in newPolyVerts) {
        newPolys.Add(new CsgPolygon(verts, oldPoly.faceProperties, oldPoly.plane.normal));
      }

      bool dumpPolygons = DEBUG;
      if (VERIFY_POLY_SPLITS) {
        int numSplitEdges = 0;
        numSplitEdges += seg.finalStart == Endpoint.EDGE ? 1 : 0;
        numSplitEdges += seg.finalEnd == Endpoint.EDGE ? 1 : 0;
        if (numSplitEdges == 2 && seg.finalMiddle == Endpoint.EDGE) {
          // Same edge, so only one edge is split.
          numSplitEdges = 1;
        }
        if (!CsgUtil.IsValidPolygonSplit(oldPoly, newPolys, numSplitEdges)) {
          Console.Write("Invalid split for case: " + Endpoint.combine(seg.finalStart, seg.finalMiddle, seg.finalEnd));
          dumpPolygons = true;
        }
      }

      // Dump polygon and split info out for debugging.
      if (dumpPolygons) {
        List<CsgVertex>[] otherPolys = new List<CsgVertex>[newPolyVerts.Length + 2];
        for (int i = 0; i < newPolyVerts.Length; i++) {
          otherPolys[i] = newPolyVerts[i];
        }
        otherPolys[newPolyVerts.Length] = new List<CsgVertex>() { seg.finalStartVertex, seg.finalEndVertex };
        otherPolys[newPolyVerts.Length + 1] = new List<CsgVertex>() { seg.startVertex, seg.endVertex };
        DumpPolygonsForDebug(oldPoly, otherPolys);
        DumpSplitInfo(oldPoly, seg);
      }

      // If we only had one valid polygon, it will be the same as the original, so do nothing.
      if (newPolys.Count > 1) {
        obj.polygons.Remove(oldPoly);
        obj.polygons.AddRange(newPolys);
        return true;
      }

      return false;
    }
    // Check if a polygon has a non-zero area and more than two vertices.
    private static bool IsValidPolygon(List<CsgVertex> verts) {
      if (verts.Count < 3) {
        return false;
      } else if (verts.Count == 3) {
        // If the dot product is -1 or 1, it is a zero area triangle.
        float dot = Vector3.Dot((verts[1].loc - verts[0].loc).normalized, (verts[2].loc - verts[0].loc).normalized);
        return Mathf.Abs(1 - Mathf.Abs(dot)) > CsgMath.EPSILON;
      } else {
        // Not true in general, but true for the splits we perform.  I think.
        return true;
      }
    }

    // Helper method.  Project the polygons on a plane and then write their 2d coords.
    // Hacky: assumes last "polygon" is the split edge and labels it thusly
    //   (I always wanted to use 'thusly' in a code comment ;)
    public static void DumpPolygonsForDebug(CsgPolygon oldPoly, params List<CsgVertex>[] polys) {
      Quaternion toPlane = Quaternion.Inverse(Quaternion.LookRotation(oldPoly.plane.normal, Vector3.right));
      Console.Write("");
      for (int i = -1; i < polys.Length; i++) {
        string name = "P" + (i + 1);
        if (i == -1) {
          name = "Original";
        } else if (i == (polys.Length - 2)) {
          name = "FinalSegment";
        } else if (i == (polys.Length - 1)) {
          name = "Segment";
        }
        List<CsgVertex> poly = i == -1 ? oldPoly.vertices : polys[i];
        foreach (CsgVertex vert in poly) {
          Vector3 projected = toPlane * vert.loc;
          Console.Write(projected.x + ", " + projected.y + ", " + name);
        }
      }
    }

    // Section 5: Calculate the segment descriptor for a given polygon and a line that splits it.
    // Public for testing.
    public static SegmentDescriptor CalcSegmentDescriptor(CsgContext ctx,
        Vector3 linePt, Vector3 lineDir, float[] distToPlane, CsgPolygon poly) {
      SegmentDescriptor descriptor = new SegmentDescriptor();
      bool foundFirst = false;
      bool foundSecond = false;

      for (int i = 0; i < distToPlane.Length; i++) {
        int j = (i + 1) % distToPlane.Length;
        if (Math.Abs(distToPlane[i]) < CsgMath.EPSILON) {
          if (!foundFirst) {
            descriptor.startVertIdx = i;
            descriptor.start = Endpoint.VERTEX;
            descriptor.startDist = SignedDistance(linePt, lineDir, poly.vertices[i].loc);
            descriptor.startVertex = poly.vertices[i];
            foundFirst = true;
          } else {
            descriptor.endVertIdx = i;
            descriptor.end = Endpoint.VERTEX;
            descriptor.endDist = SignedDistance(linePt, lineDir, poly.vertices[i].loc);
            descriptor.endVertex = poly.vertices[i];
            foundSecond = true;
          }
        } else if (Math.Abs(distToPlane[i]) > CsgMath.EPSILON && Math.Abs(distToPlane[j]) > CsgMath.EPSILON && Mathf.Sign(distToPlane[i]) != Mathf.Sign(distToPlane[j])) {
          // Crosses plane.
          float t = distToPlane[i] / (distToPlane[i] - distToPlane[j]);
          Vector3 midPoint = Vector3.Lerp(poly.vertices[i].loc, poly.vertices[j].loc, t);
          // Project back onto our plane:
          float dist = poly.plane.GetDistanceToPoint(midPoint);
          midPoint -= (dist * poly.plane.normal);
          if (!foundFirst) {
            descriptor.startVertIdx = i;
            descriptor.start = Endpoint.EDGE;
            descriptor.startDist = SignedDistance(linePt, lineDir, midPoint);
            descriptor.startVertex = ctx.CreateOrGetVertexAt(midPoint);
            descriptor.startVertex.status = VertexStatus.BOUNDARY;
            foundFirst = true;
          } else {
            descriptor.endVertIdx = i;
            descriptor.end = Endpoint.EDGE;
            descriptor.endDist = SignedDistance(linePt, lineDir, midPoint);
            descriptor.endVertex = ctx.CreateOrGetVertexAt(midPoint);
            descriptor.endVertex.status = VertexStatus.BOUNDARY;
            foundSecond = true;
          }
        }
      }

      if (!foundFirst) {
        return null;
      }

      if (!foundSecond) {
        descriptor.end = descriptor.start;
        descriptor.endDist = descriptor.startDist;
        descriptor.endVertIdx = descriptor.startVertIdx;
        descriptor.endVertex = descriptor.startVertex;
      }

      // Put the start and end in order of distance from linePt.
      if (descriptor.startDist > descriptor.endDist) {
        int vertIdSave = descriptor.startVertIdx;
        float distSave = descriptor.startDist;
        int typeSave = descriptor.start;
        CsgVertex vertSave = descriptor.startVertex;

        descriptor.startVertIdx = descriptor.endVertIdx;
        descriptor.startDist = descriptor.endDist;
        descriptor.start = descriptor.end;
        descriptor.startVertex = descriptor.endVertex;

        descriptor.endVertIdx = vertIdSave;
        descriptor.endDist = distSave;
        descriptor.end = typeSave;
        descriptor.endVertex = vertSave;
      }

      if (descriptor.startVertIdx == descriptor.endVertIdx) {
        descriptor.middle = Endpoint.VERTEX;
      } else if (descriptor.start == Endpoint.VERTEX && descriptor.end == Endpoint.VERTEX
          && (descriptor.startVertIdx == (descriptor.endVertIdx +1) % distToPlane.Length
          || (descriptor.startVertIdx + 1) % distToPlane.Length == descriptor.endVertIdx)) {
        descriptor.middle = Endpoint.EDGE;
      } else {
        descriptor.middle = Endpoint.FACE;
      }

      // Mark endpoints as boundary.
      descriptor.startVertex.status = VertexStatus.BOUNDARY;
      descriptor.endVertex.status = VertexStatus.BOUNDARY;

      return descriptor;
    }

    // Swap the endpoint descriptors.
    private static void Swap(SegmentDescriptor descriptor) {
      int vertIdSave = descriptor.startVertIdx;
      float distSave = descriptor.startDist;
      int typeSave = descriptor.start;
      int finalTypeSave = descriptor.finalStart;
      CsgVertex vertSave = descriptor.startVertex;
      CsgVertex finalVertSave = descriptor.finalStartVertex;

      descriptor.startVertIdx = descriptor.endVertIdx;
      descriptor.startDist = descriptor.endDist;
      descriptor.start = descriptor.end;
      descriptor.finalStart = descriptor.finalEnd;
      descriptor.startVertex = descriptor.endVertex;
      descriptor.finalStartVertex = descriptor.finalEndVertex;

      descriptor.endVertIdx = vertIdSave;
      descriptor.endDist = distSave;
      descriptor.end = typeSave;
      descriptor.finalEnd = finalTypeSave;
      descriptor.endVertex = vertSave;
      descriptor.finalEndVertex = finalVertSave;
    }

    // Get the signed distance from a ray to a point.
    private static float SignedDistance(Vector3 rayStart, Vector3 rayNormal, Vector3 point) {
      float d = Vector3.Distance(rayStart, point);
      if (Vector3.Dot(rayNormal, point - rayStart) < 0) {
        return -d;
      } else {
        return d;
      }
    }

    // Calculate the ray that is the intersection of two planes.
    private static bool PlanePlaneIntersection(
        out Vector3 rayStart, out Vector3 rayNormal, Plane plane1, Plane plane2) {
      rayStart = Vector3.zero;
      rayNormal = Vector3.Cross(plane1.normal, plane2.normal);
      Vector3 ldir = Vector3.Cross(plane2.normal, rayNormal);

      float denominator = Vector3.Dot(plane1.normal, ldir);

      if (Mathf.Abs(denominator) > CsgMath.EPSILON) {
        Vector3 plane1Position = CsgMath.PointOnPlane(plane1);
        Vector3 plane2Position = CsgMath.PointOnPlane(plane2);
        Vector3 plane1ToPlane2 = plane1Position - plane2Position;
        float t = Vector3.Dot(plane1.normal, plane1ToPlane2) / denominator;
        rayStart = plane2Position + t * ldir;
        return true;
      } else {
        return false;
      }
    }

    // Given the signed distances for a list of points to a plane, does the polygon cross the plane?
    // It has if some points are positive and some are negative.  Also considered true if some points
    // are *on* the plane and others are not.
    private static bool CrossesPlane(float[] dists) {
      bool hasAbove = false;
      bool hasBelow = false;
      bool hasOn = false;
      foreach (float dist in dists) {
        if (dist < 0) {
          hasBelow = true;
        } else if (dist > 0) {
          hasAbove = true;
        } else {
          hasOn = true;
        }
      }

      int count = 0;
      count += hasAbove ? 1 : 0;
      count += hasBelow ? 1 : 0;
      count += hasOn ? 1 : 0;
      return count > 1;
    }

    // Given a polygon, find the distance from each of its vertices to a given plane.
    private static float[] DistanceFromVertsToPlane(CsgPolygon poly, Plane plane) {
      float[] dists = new float[poly.vertices.Count];
      for (int i = 0; i < poly.vertices.Count; i++) {
        float dist = plane.GetDistanceToPoint(poly.vertices[i].loc);
        dists[i] = Mathf.Abs(dist) < CsgMath.EPSILON ? 0 : dist;
      }
      return dists;
    }
  }
}
