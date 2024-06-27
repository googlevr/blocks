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
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.model.csg {

  public class CsgOperations {
    private const float COPLANAR_EPS = 0.001f;

    /// <summary>
    ///   Subtract a mesh from all intersecting meshes in a model.
    /// </summary>
    /// <returns>true if the subtract brush intersects with meshes in the scene.</returns>
    public static bool SubtractMeshFromModel(Model model, SpatialIndex spatialIndex, MMesh toSubtract) {
      Bounds bounds = toSubtract.bounds;

      List<Command> commands = new List<Command>();
      HashSet<int> intersectingMeshIds;
      if (spatialIndex.FindIntersectingMeshes(toSubtract.bounds, out intersectingMeshIds)) { 
        foreach (int meshId in intersectingMeshIds) {
          MMesh mesh = model.GetMesh(meshId);
          MMesh result = Subtract(mesh, toSubtract);
          commands.Add(new DeleteMeshCommand(mesh.id));
          // If the result is null, it means the mesh was entirely erased.  No need to add a new version back.
          if (result != null) {
            if (model.CanAddMesh(result)) {
              commands.Add(new AddMeshCommand(result));
            } else {
              // Abort everything if an invalid mesh would be generated.
              return false;
            }
          }
        }
      }
      if (commands.Count > 0) {
        model.ApplyCommand(new CompositeCommand(commands));
        return true;
      }
      return false;
    }

    /// <summary>
    ///   Subtract a mesh from another.  Returns a new MMesh that is the result of the subtraction.
    ///   If the result is an empty space, returns null.
    /// </summary>
    public static MMesh Subtract(MMesh subtrahend, MMesh minuend) {
      // If the objects don't overlap, just bail out:

      if (!subtrahend.bounds.Intersects(minuend.bounds)) {
        return subtrahend.Clone();
      }

      // Our epsilons aren't very good for operations that are either very small or very big,
      // so translate and scale the two csg shapes so they're centered around the origin
      // and reasonably sized. This prevents a lot of floating point error in the ensuing maths.
      //
      // Here's a good article for comparing floating point numbers:
      // https://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/
      Vector3 operationalCenter = (subtrahend.bounds.center + minuend.bounds.center) / 2.0f;
      float averageRadius = (subtrahend.bounds.extents.magnitude + minuend.bounds.extents.magnitude) / 2.0f;
      Vector3 operationOffset = -operationalCenter;
      float operationScale = 1.0f / averageRadius;
      if (operationScale < 1.0f) {
        operationScale = 1.0f;
      }

      Bounds operationBounds = new Bounds();
      foreach (int vertexId in subtrahend.GetVertexIds()) {
        operationBounds.Encapsulate((subtrahend.VertexPositionInModelCoords(vertexId) + operationOffset) * operationScale);
      }
      foreach (int vertexId in minuend.GetVertexIds()) {
        operationBounds.Encapsulate((minuend.VertexPositionInModelCoords(vertexId) + operationOffset) * operationScale);
      }
      operationBounds.Expand(0.01f);

      CsgContext ctx = new CsgContext(operationBounds);

      CsgObject leftObj = ToCsg(ctx, subtrahend, operationOffset, operationScale);
      CsgObject rightObj = ToCsg(ctx, minuend, operationOffset, operationScale);
      List<CsgPolygon> result = CsgSubtract(ctx, leftObj, rightObj);
      if (result.Count > 0) {
        HashSet<string> combinedRemixIds = null;
        if (subtrahend.remixIds != null || minuend.remixIds != null) {
          combinedRemixIds = new HashSet<string>();
          if (subtrahend.remixIds != null) combinedRemixIds.UnionWith(subtrahend.remixIds);
          if (minuend.remixIds != null) combinedRemixIds.UnionWith(minuend.remixIds);
        }
        return FromPolys(
          subtrahend.id,
          subtrahend.offset,
          subtrahend.rotation,
          result,
          operationOffset,
          operationScale,
          combinedRemixIds);
      } else {
        return null;
      }
    }

    /// <summary>
    ///   Perform the subtract on CsgObjects.  The implementation follows the paper:
    ///   http://vis.cs.brown.edu/results/videos/bib/pdf/Laidlaw-1986-CSG.pdf
    /// </summary>
    private static List<CsgPolygon> CsgSubtract(CsgContext ctx, CsgObject leftObj, CsgObject rightObj) {
      SplitObject(ctx, leftObj, rightObj);
      SplitObject(ctx, rightObj, leftObj);
      SplitObject(ctx, leftObj, rightObj);
      ClassifyPolygons(leftObj, rightObj);
      ClassifyPolygons(rightObj, leftObj);

      FaceProperties facePropertiesForNewFaces = leftObj.polygons[0].faceProperties;
      List<CsgPolygon> polys = SelectPolygons(leftObj, false, null, PolygonStatus.OUTSIDE, PolygonStatus.OPPOSITE);
      polys.AddRange(SelectPolygons(rightObj, true, facePropertiesForNewFaces, PolygonStatus.INSIDE));

      return polys;
    }

    /// <summary>
    ///   Select all of the polygons in the object with any of the given statuses.
    /// </summary>
    private static List<CsgPolygon> SelectPolygons(CsgObject obj, bool invert, FaceProperties? overwriteFaceProperties, params PolygonStatus[] status) {
      HashSet<PolygonStatus> selectedStatus = new HashSet<PolygonStatus>(status);
      List<CsgPolygon> polys = new List<CsgPolygon>();

      foreach(CsgPolygon poly in obj.polygons) {
        if (selectedStatus.Contains(poly.status)) {
          CsgPolygon polyToAdd = poly;
          if (invert) {
            polyToAdd = poly.Invert();
          }
          if (overwriteFaceProperties.HasValue) {
            polyToAdd.faceProperties = overwriteFaceProperties.Value;
          }
          polys.Add(polyToAdd);
        }
      }

      return polys;
    }

    // Section 7:  Classify all polygons in the object.
    private static void ClassifyPolygons(CsgObject obj, CsgObject wrt) {
      // Set up adjacency information.
      foreach(CsgPolygon poly in obj.polygons) {
        for(int i = 0; i < poly.vertices.Count; i++) {
          int j = (i + 1) % poly.vertices.Count;
          poly.vertices[i].neighbors.Add(poly.vertices[j]);
          poly.vertices[j].neighbors.Add(poly.vertices[i]);
        }
      }

      // Classify polys.
      foreach(CsgPolygon poly in obj.polygons) {
        if (HasUnknown(poly) || AllBoundary(poly)) {
          ClassifyPolygonUsingRaycast(poly, wrt);
          if (poly.status == PolygonStatus.INSIDE || poly.status == PolygonStatus.OUTSIDE) {
            VertexStatus newStatus = poly.status == PolygonStatus.INSIDE ? VertexStatus.INSIDE : VertexStatus.OUTSIDE;
            foreach (CsgVertex vertex in poly.vertices) {
              PropagateVertexStatus(vertex, newStatus);
            }
          }
        } else {
          // Use the status of the first vertex that is inside or outside.
          foreach(CsgVertex vertex in poly.vertices) {
            if (vertex.status == VertexStatus.INSIDE) {
              poly.status = PolygonStatus.INSIDE;
              break;
            }
            if (vertex.status == VertexStatus.OUTSIDE) {
              poly.status = PolygonStatus.OUTSIDE;
              break;
            }
          }
          AssertOrThrow.True(poly.status != PolygonStatus.UNKNOWN, "Should have classified polygon.");
        }
      }
    }

    // Fig 8.1: Propagate vertex status.
    private static void PropagateVertexStatus(CsgVertex vertex, VertexStatus newStatus) {
      if (vertex.status == VertexStatus.UNKNOWN) {
        vertex.status = newStatus;
        foreach(CsgVertex neighbor in vertex.neighbors) {
          PropagateVertexStatus(neighbor, newStatus);
        }
      }
    }

    // Fig 7.2: Classify a given polygon by raycasting from its barycenter into the faces of the other object.
    // Public for testing.
    public static void ClassifyPolygonUsingRaycast(CsgPolygon poly, CsgObject wrt) {
      Vector3 rayStart = poly.baryCenter;
      Vector3 rayNormal = poly.plane.normal;
      CsgPolygon closest = null;
      float closestPolyDist = float.MaxValue;

      bool done;
      int count = 0;
      do {
        done = true;  // Done unless we hit a special case.
        foreach(CsgPolygon otherPoly in wrt.polygons) {
          float dot = Vector3.Dot(rayNormal, otherPoly.plane.normal);
          bool perp = Mathf.Abs(dot) < CsgMath.EPSILON;
          bool onOtherPlane = Mathf.Abs(otherPoly.plane.GetDistanceToPoint(rayStart)) < CsgMath.EPSILON;
          Vector3 projectedToOtherPlane = Vector3.zero;
          float signedDist = -1f;
          if (!perp) {
            CsgMath.RayPlaneIntersection(out projectedToOtherPlane, rayStart, rayNormal, otherPoly.plane);
            float dist = Vector3.Distance(projectedToOtherPlane, rayStart);
            signedDist = dist * Mathf.Sign(Vector3.Dot(rayNormal, (projectedToOtherPlane - rayStart)));
          }

          if (perp && onOtherPlane) {
            done = false;
            break;
          } else if(perp && !onOtherPlane) {
            // no intersection
          } else if(!perp && onOtherPlane) {
            int isInside = CsgMath.IsInside(otherPoly, projectedToOtherPlane);
            if (isInside >= 0) {
              closestPolyDist = 0;
              closest = otherPoly;
              break;
            }
          } else if (!perp && signedDist > 0) {
            if (signedDist < closestPolyDist) {
              int isInside = CsgMath.IsInside(otherPoly, projectedToOtherPlane);
              if (isInside > 0) {
                closest = otherPoly;
                closestPolyDist = signedDist;
              } else if (isInside == 0) {
                // On segment, perturb and try again.
                done = false;
                break;
              }
            }
          }
        }
        if (!done) {
          // Perturb the normal and try again.
          rayNormal += new Vector3(
            UnityEngine.Random.Range(-0.1f, 0.1f),
            UnityEngine.Random.Range(-0.1f, 0.1f),
            UnityEngine.Random.Range(-0.1f, 0.1f));
          rayNormal = rayNormal.normalized;
        }
        count++;
      } while (!done && count < 5) ;

      if (closest == null) {
        // Didn't hit any polys, we are outside.
        poly.status = PolygonStatus.OUTSIDE;
      } else {
        float dot = Vector3.Dot(poly.plane.normal, closest.plane.normal);
        if (Mathf.Abs(closestPolyDist) < CsgMath.EPSILON) {
          poly.status = dot < 0 ? PolygonStatus.OPPOSITE : PolygonStatus.SAME;
        } else {
          poly.status = dot < 0 ? PolygonStatus.OUTSIDE : PolygonStatus.INSIDE;
        }
      }
    }

    private static bool HasUnknown(CsgPolygon poly) {
      foreach (CsgVertex vertex in poly.vertices) {
        if (vertex.status == VertexStatus.UNKNOWN) {
          return true;
        }
      }
      return false;
    }

    private static bool AllBoundary(CsgPolygon poly) {
      foreach (CsgVertex vertex in poly.vertices) {
        if (vertex.status != VertexStatus.BOUNDARY) {
          return false;
        }
      }
      return true;
    }

    // Public for testing.
    public static void SplitObject(CsgContext ctx, CsgObject toSplit, CsgObject splitBy) {
      bool splitPoly;
      int count = 0;
      HashSet<CsgPolygon> alreadySplit = new HashSet<CsgPolygon>();
      do {
        splitPoly = false;
        // Temporary guard to prevent infinite loops while there are bugs.
        // TODO(bug) figure out why csg creates so many rejected splits.
        count++;
        if (count > 100) {
          // This usually occurs when csg keeps trying to do the same invalid split over and over.
          // If the algorithm has reached this point, it usually means that the two meshes are
          // split enough to perform a pretty good looking csg subtraction. More investigation
          // should be done on bug and we may be able to remove this guard.
          return;
        }
        foreach (CsgPolygon toSplitPoly in toSplit.polygons) {
          if (alreadySplit.Contains(toSplitPoly)) {
            continue;
          }
          alreadySplit.Add(toSplitPoly);
          if (toSplitPoly.bounds.Intersects(splitBy.bounds)) {
            foreach (CsgPolygon splitByPoly in splitBy.polygons) {
              if (toSplitPoly.bounds.Intersects(splitByPoly.bounds)
                  && !Coplanar(toSplitPoly.plane, splitByPoly.plane)) {
                splitPoly = PolygonSplitter.SplitPolys(ctx, toSplit, toSplitPoly, splitByPoly);
                if (splitPoly) {
                  break;
                }
              }
            }
          }
          if (splitPoly) {
            break;
          }
        }
      } while (splitPoly);
    }

    private static bool Coplanar(Plane plane1, Plane plane2) {
      return Mathf.Abs(plane1.distance - plane2.distance) < COPLANAR_EPS
        && Vector3.Distance(plane1.normal, plane2.normal) < COPLANAR_EPS;
    }

    // Make an MMesh from a set of CsgPolys.  Each unique CsgVertex should be a unique vertex in the MMesh.
    // Public for testing.
    public static MMesh FromPolys(int id, Vector3 offset, Quaternion rotation, List<CsgPolygon> polys,
      Vector3? csgOffset = null, float? scale = null, HashSet<string> remixIds = null) {
      if (!csgOffset.HasValue) {
        csgOffset = Vector3.zero;
      }
      if (!scale.HasValue) {
        scale = 1.0f;
      }
      Dictionary<CsgVertex, int> vertexToId = new Dictionary<CsgVertex, int>();
      MMesh newMesh = new MMesh(id, Vector3.zero, Quaternion.identity,
        new Dictionary<int, Vertex>(), new Dictionary<int, Face>(), MMesh.GROUP_NONE, remixIds);
      MMesh.GeometryOperation constructionOperation = newMesh.StartOperation();
      foreach (CsgPolygon poly in polys) {
        List<int> vertexIds = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        foreach(CsgVertex vertex in poly.vertices) {
          int vertId;
          if (!vertexToId.TryGetValue(vertex, out vertId)) {


            Vertex meshVertex = constructionOperation.AddVertexMeshSpace(Quaternion.Inverse(rotation) *
              ((vertex.loc / scale.Value - csgOffset.Value) - offset));
            vertId = meshVertex.id;
            vertexToId[vertex] = vertId;
          }
          vertexIds.Add(vertId);
          normals.Add(poly.plane.normal);
        }
        constructionOperation.AddFace(vertexIds, poly.faceProperties);
      }
      constructionOperation.Commit();
      newMesh.offset = offset;
      newMesh.rotation = rotation;

      return newMesh;
    }

    // Convert an MMesh into a CsgObject.
    // Public for testing.
    public static CsgObject ToCsg(CsgContext ctx, MMesh mesh, Vector3? offset = null, float? scale = null) {
      if (!offset.HasValue) {
        offset = Vector3.zero;
      }
      if (!scale.HasValue) {
        scale = 1.0f;
      }
      Dictionary<int, CsgVertex> idToVert = new Dictionary<int, CsgVertex>();
      foreach(int vertexId in mesh.GetVertexIds()) {
        idToVert[vertexId] = ctx.CreateOrGetVertexAt((mesh.VertexPositionInModelCoords(vertexId) + offset.Value) * scale.Value);
      }

      List<CsgPolygon> polys = new List<CsgPolygon>();
      foreach(Face face in mesh.GetFaces()) {
        GeneratePolygonsForFace(polys, idToVert, mesh, face);
      }

      return new CsgObject(polys, new List<CsgVertex>(idToVert.Values));
    }

    // Generate CsgPolygons for a Face.  CsgPolygons should be convex and have no holes.
    private static void GeneratePolygonsForFace(
        List<CsgPolygon> polys, Dictionary<int, CsgVertex> idToVert, MMesh mesh, Face face) {
      List<CsgVertex> vertices = new List<CsgVertex>();
      foreach (int vertexId in face.vertexIds) {
        vertices.Add(idToVert[vertexId]);
      }
      CsgPolygon poly = new CsgPolygon(vertices, face.properties);
      polys.Add(poly);
    }
  }
}
