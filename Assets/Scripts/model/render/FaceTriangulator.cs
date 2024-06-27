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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.render {
  public struct Triangle {
    public int vertId0 { get; private set; }
    public int vertId1 { get; private set; }
    public int vertId2 { get; private set; }

    public Triangle(int v1, int v2, int v3) {
      vertId0 = v1;
      vertId1 = v2;
      vertId2 = v3;
    }

    public override string ToString()
    {
      return string.Format("[Triangle: vertId0={0}, vertId1={1}, vertId2={2}]", vertId0, vertId1, vertId2);
    }

    public override bool Equals(object obj)
    {
      return obj is Triangle ? Equals((Triangle)obj) : base.Equals(obj);
    }

    public bool Equals(Triangle other)
    {
      // Same vertices in same winding order.
      return (vertId0 == other.vertId0 && vertId1 == other.vertId1 && vertId2 == other.vertId2) ||
      (vertId0 == other.vertId1 && vertId1 == other.vertId2 && vertId2 == other.vertId0) ||
      (vertId0 == other.vertId2 && vertId1 == other.vertId0 && vertId2 == other.vertId1);
    }

    public override int GetHashCode()
    {
      // 10 bits for each id, ordered by size, beyond which collisions will occur.
      int[] verts = {vertId0, vertId1, vertId2};
      Array.Sort(verts);
      return verts[2] << 20 + verts[1] << 10 + verts[0];
    }
  }

  // Defines an extension to linked list to allow circular next operation and circular previous operation.
  public static class CircularLinkedList {
    public static LinkedListNode<Vertex> CircularNext(
        this LinkedListNode<Vertex> current) {
      if (current == current.List.Last) {
        return current.List.First;
      } else {
        return current.Next;
      }
    }
    public static LinkedListNode<Vertex> CircularPrevious(
        this LinkedListNode<Vertex> current) {
      if (current == current.List.First) {
        return current.List.Last;
      } else {
        return current.Previous;
      }
    }
  }

  public class FaceTriangulator {

    private struct HoleInfo {
      public LinkedList<Vertex> vertList;
      public LinkedListNode<Vertex> bestCandidate;
      public Vector3 bestPos;
      public float bestMagnitude;
    }

    /// <summary>
    ///   Triangulate an entire mesh and return all resulting triangles.
    /// </summary>
    /// <param name="mesh">The mesh to triangulate.</param>
    /// <returns>List of triangles that represent the same geometry as the given mesh.</returns>
    public static List<Triangle> TriangulateMesh(MMesh mesh) {
      List<Triangle> geometry = new List<Triangle>();
      foreach (Face f in mesh.GetFaces()) {
        geometry.AddRange(f.GetTriangulation(mesh));
      }
      return geometry;
    }

    /// <summary>
    /// Triangulates the given face of the given mesh.
    /// </summary>
    /// <param name="mesh">The mesh to which the face belongs.</param>
    /// <param name="face">The face to triangulate.</param>
    /// <returns>The list of triangles that represents the geometry of the face.</returns>
    public static List<Triangle> TriangulateFace(MMesh mesh, Face face) {
      List<Vertex> border = new List<Vertex>(face.vertexIds.Count);
      for (int i = 0; i < face.vertexIds.Count; i++) {
        border.Add(mesh.GetVertex(face.vertexIds[i]));
      }
      return Triangulate(border);
    }
    
    /// <summary>
    /// Triangulates the given face of the given mesh.
    /// </summary>
    /// <param name="mesh">The mesh to which the face belongs.</param>
    /// <param name="face">The face to triangulate.</param>
    /// <returns>The list of triangles that represents the geometry of the face.</returns>
    public static List<Triangle> TriangulateFace(MMesh.GeometryOperation operation, Face face) {

      List<Vertex> border = new List<Vertex>(face.vertexIds.Count);
      for (int i = 0; i < face.vertexIds.Count; i++) {
        border.Add(operation.GetCurrentVertex(face.vertexIds[i]));
      }
      return Triangulate(border);
    }

    /// <summary>
    ///   Triangulate a polygon with holes. The current implementation is a somewhat slow O(n^2)
    ///   ear-clipping algorithm due to its straight-forward implementation.
    /// </summary>
    /// <param name="border">Outside of the polygon -- in clockwise order.</param>
    /// <returns>List of triangles that fully cover the area defined by the border on the
    /// outside and the holes on the inside.</returns>
    public static List<Triangle> Triangulate(List<Vertex> border) {
      List<Triangle> triangles = new List<Triangle>(border.Count);

      // Assuming clockwise wind, take the plane normal of the first three vertices
      // to determine intended face direction.
      Vector3 faceNormal = MeshMath.CalculateNormal(border);

      // Store remaining vertices in a pseudo-circular linked list.
      LinkedList<Vertex> remaining = new LinkedList<Vertex>(border);


      // Initialize the three vertices to check for ears.
      LinkedListNode<Vertex> prev = remaining.Last;
      LinkedListNode<Vertex> current = remaining.First;
      LinkedListNode<Vertex> next = current.Next;

      bool noMoreEars = true;
      while (remaining.Count > 2) {
        if (Math3d.IsConvex(current.Value.loc, prev.Value.loc, next.Value.loc, faceNormal)) {
          bool cut = true;
          // Check that none of the remaining vertices are inside.
          LinkedListNode<Vertex> check = next.CircularNext();
          while (check != prev) {
            // Because of hole patching, the same vertex might be in multiple places in the list.
            // We can safely skip repetitions of the three points.
            if (check.Value.loc == prev.Value.loc ||
                check.Value.loc == current.Value.loc ||
                check.Value.loc == next.Value.loc) {
              check = check.CircularNext();
              continue;
            }
            if (Math3d.TriangleContainsPoint(prev.Value.loc, current.Value.loc, next.Value.loc, check.Value.loc)) {
              cut = false;
              break;
            }
            check = check.CircularNext();
          }

          if (cut) {
            noMoreEars = false;
            triangles.Add(new Triangle(prev.Value.id, current.Value.id, next.Value.id));
            remaining.Remove(current);
            current = next;
            next = next.CircularNext();

            // Skip update logic which was handled above.
            continue;
          }
        }

        // Update node pointers.
        prev = current;
        current = next;
        next = next.CircularNext();

        // If looped around and no ear was cut, fall back to simple. convex implementation when ear-clipping fails.
        // NOTE: This should never happen provided we never have non-coplanar-faces, but we're just playing safe.
        if (current == remaining.First) {
          if (noMoreEars) {
            // Fall back to simple. convex implementation when ear-clipping fails.
            triangles = new List<Triangle>(border.Count);
            for (int i = 2; i < border.Count; i++) {
              triangles.Add(new Triangle(
                border[0].id,
                border[i - 1].id,
                border[i].id));
            }
            return triangles;
          } else {
            noMoreEars = true;
          }
        }
      }
      return triangles;
    }

    public static bool RemoveHoleAtVertex(
        LinkedList<Vertex> border, LinkedList<Vertex> hole, Vertex bestCandidate,
        Vector3 faceNormal, Vector3? origin = null, Vector3? axis = null) {
      // Initialize defaults for optional parameters.
      LinkedListNode<Vertex> bestNode = hole.Find(bestCandidate);
      Vector3 bestPos = bestCandidate.loc;
      if (origin == null || axis == null) {
        origin = bestPos;
        // Take as axis the normal of the vertex to minimize chance of occlusion.
        Vector3 prevPos = bestNode.Previous.Value.loc;
        Vector3 nextPos = bestNode.Next.Value.loc;
        axis = Vector3.Lerp(prevPos - bestPos, nextPos - bestPos, 0.5f);
        // If the vertex on the hole is concave, it's concave wrt the polygon and the axis
        // will be pointing "inward", away from the border. Flip it.
        if (!Math3d.IsConvex(
          bestPos,
          prevPos,
          nextPos,
          faceNormal)) {
          axis *= -1;
        }
      }

      // Find first visible point on arbitrary axis from chosen point in hole.
      // This is probably by far the most computationally intense part of the algorithm.
      LinkedListNode<Vertex> start = border.First;
      LinkedListNode<Vertex> end = start.CircularNext();
      LinkedListNode<Vertex> bestOnBorder = null;
      float bestMagnitude = float.MaxValue;
      Vector3 intersectionPos = Vector3.zero;
      do {
        // Find intersection projection on axis
        Vector3 a, b;
        Vector3 startVec = start.Value.loc;
        Vector3 edge = end.Value.loc - startVec;
        bool intersecting =
          Math3d.ClosestPointsOnTwoLines(out a, out b, startVec, edge, bestPos, axis.Value) &&
          a == b;

        // Record whether intersection occured in the "forward" direction on the ray cast from
        // bestOnHole.
        bool forward = Vector3.Dot(b - bestPos, axis.Value) > 0;

        if (intersecting && forward) {
          // The two points are assumed to be the same but b is technically the one that should be
          // used.
          float coordVal = Vector3.Project(b - origin.Value, axis.Value).magnitude;
          if (coordVal < bestMagnitude) {
            intersectionPos = b;
            bestMagnitude = coordVal;
            // Choose as "best" the vertex that is futher on the axis
            // (to assure no visibility blockage wrt hole since vertex in hole
            // was chosen with maximal on-axis projection magnitude)
            if (Vector3.Project(start.Value.loc - origin.Value, axis.Value).magnitude >
              Vector3.Project(end.Value.loc - origin.Value, axis.Value).magnitude) {
              bestOnBorder = start;
            } else {
              bestOnBorder = end;
            }
          }
        }

        // Update to next pair.
        start = end;
        end = end.CircularNext();
      } while (start != border.First);

      if (bestOnBorder == null) {
        return false;
      }

      // Finally, confirm visibility from chosen best vertices by checking other reflex vertices on
      // the border are not contained
      LinkedListNode<Vertex> onBorder = bestOnBorder.CircularNext();
      LinkedListNode<Vertex> occludingBestOnBorder = null;
      double bestAngle = Math.PI * 2;
      while (onBorder != bestOnBorder) {
        bool convex = Math3d.IsConvex(
          onBorder.Value.loc,
          onBorder.CircularPrevious().Value.loc,
          onBorder.CircularNext().Value.loc,
          faceNormal);
        if (!convex &&
          Math3d.TriangleContainsPoint(
            bestPos, intersectionPos,
            bestOnBorder.Value.loc,
            onBorder.Value.loc)) {
          // Find angle between intersection line and occluding vertex line.
          Vector3 intersectionVec = intersectionPos - bestPos;
          Vector3 occludingVertexVec = onBorder.Value.loc - bestPos;
          float angle = Vector3.Dot(intersectionVec, occludingVertexVec);
          if (angle < bestAngle) {
            occludingBestOnBorder = onBorder;
            bestAngle = angle;
          }
        }

        onBorder = onBorder.CircularNext();
      }

      // Update bestOnBorder with the actual best if occluding vertex was found.
      if (occludingBestOnBorder != null) {
        bestOnBorder = occludingBestOnBorder;
      }

      // Add hole to border by cutting the shape.
      LinkedListNode<Vertex> terminalOnBorder = bestOnBorder.CircularNext();
      LinkedListNode<Vertex> onHole = bestNode;
      do {
        // Holes are wound counter-clockwise so adding them in order is appropriate to preserve
        // overall polygon clockwise winding.
        border.AddBefore(terminalOnBorder, onHole.Value);
        onHole = onHole.CircularNext();
      } while (onHole != bestNode);
      // Two additional vertices are added to complete loop.
      border.AddBefore(terminalOnBorder, bestCandidate);
      border.AddBefore(terminalOnBorder, bestOnBorder.Value);

      return true;
    }

    public static bool RemoveHoles(
        LinkedList<Vertex> border, List<List<Vertex>> holes, Vector3 faceNormal) {
      if (holes == null || holes.Count == 0) {
        return false;
      }

      // Pick an arbitrary vertex as origin and edge as direction
      Vector3 origin = border.First.Value.loc;
      Vector3 axis = border.First.Next.Value.loc - origin;

      List<HoleInfo> holeInfos = new List<HoleInfo>();

      // Record useful meta about each hole.
      foreach (List<Vertex> hole in holes) {
        // Create a list of hole vertices that will later be connected to main list.
        HoleInfo holeInfo;
        holeInfo.vertList = new LinkedList<Vertex>(hole);
        LinkedListNode<Vertex> holeCurrent = holeInfo.vertList.First;
        holeInfo.bestCandidate = null;
        holeInfo.bestMagnitude = float.MinValue;

        // Find maximal offset on the previously chosen arbitrary axis.
        while (holeCurrent != null) {
          float coordVal = Vector3.Project(holeCurrent.Value.loc - origin, axis).magnitude;
          if (coordVal > holeInfo.bestMagnitude) {
            holeInfo.bestMagnitude = coordVal;
            holeInfo.bestCandidate = holeCurrent;
          }
          holeCurrent = holeCurrent.Next;
        }

        holeInfo.bestPos = holeInfo.bestCandidate.Value.loc;
        holeInfos.Add(holeInfo);
      }

      // Order holes by largest magnitude in arbitrary axis.
      holeInfos = holeInfos.OrderByDescending(h => h.bestMagnitude).ToList();

      foreach (HoleInfo hole in holeInfos) {
        RemoveHoleAtVertex(border, hole.vertList, hole.bestCandidate.Value, faceNormal, origin,
          axis);
      }
      return true;
    }
  }
}
