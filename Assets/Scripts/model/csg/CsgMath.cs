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
  public class CsgMath {
    // Floating point slop for testing things like coplanar, etc.
    public const float EPSILON = 0.0001f;

    // Given a Unity Plane, find a point on that plane.
    // public for testing.
    public static Vector3 PointOnPlane(Plane plane) {
      return -(plane.normal * plane.distance);
    }

    // Find the intersection of a ray with a plane.  (Should return a value even if the plane is 'behind' the ray.)
    public static void RayPlaneIntersection(
        out Vector3 intersection, Vector3 rayStart, Vector3 rayNormal, Plane plane) {
      Ray ray = new Ray(rayStart, rayNormal);
      float d;
      plane.Raycast(ray, out d);
      if (Math.Abs(d) > EPSILON) {
        intersection = ray.GetPoint(d);
      } else {
        intersection = rayStart;
      }
    }

    // Is the given point inside the given polygon.  Assumes all points are coplanar.
    // Returns 1 if inside, -1 if outside and 0 if on boundary.
    // public for testing.
    public static int IsInside(CsgPolygon poly, Vector3 point) {
      bool onEdge = false;

      for (int i = 0; i < poly.vertices.Count; i++) {
        Vector3 a = poly.vertices[i].loc;
        Vector3 b = poly.vertices[(i + 1) % poly.vertices.Count].loc;
        Vector3 c = poly.vertices[(i + 2) % poly.vertices.Count].loc;
        int sameSide = SameSide(a, b, point, c);
        if (sameSide < 0) {
          return -1;
        }
        if (sameSide == 0) {
          onEdge = true;
        }
      }
      return onEdge ? 0 : 1;
    }

    // Returns 1 if inside, -1 if outside and 0 if on boundary.
    private static int SameSide(Vector3 a, Vector3 b, Vector3 check, Vector3 reference) {
      Vector3 checkSide = MeshMath.CalculateNormal(a, b, check);
      Vector3 referenceSide = MeshMath.CalculateNormal(a, b, reference);
      if (checkSide.magnitude < EPSILON) {
        return 0;
      }
      // Empirically, I've found that == is too lenient and distance is too restrictive.
      // squared distance is not only more efficient, but it also produces better results.
      return (referenceSide - checkSide).sqrMagnitude < EPSILON ? 1 : -1;
    }
  }
}
