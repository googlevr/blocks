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

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.csg {
  public class FaceDecomposer {
    /// <summary>
    ///   Decompose a face into convex polygons.  Ideally this should produce as few pieces as possible.
    ///   The current implementation either returns the border -- if it is convex without holes -- or
    ///   uses the FaceTriangulator to split it into triangles.
    /// </summary>
    /// <param name="border">Outside of the polygon -- in clockwise order.</param>
    /// <param name="holes">Holes in the polygon -- each in counterclockwise order.</param>
    /// <returns>A list of polygons, each represented as a list of vertex ids.</returns>
    public static List<List<int>> Decompose(List<Vertex> border, List<List<Vertex>> holes) {

      // If there are no holes and the face is convex, just return the border as the poly.
      if (holes.Count == 0) {
        bool convex = true;
        // If it has 3 vertices it will always be convex.
        if (border.Count > 3) {
          Vector3 faceNormal = MeshMath.CalculateNormal(border);
          for (int i = 0; i < border.Count; i++) {
            convex = Math3d.IsConvex(
              border[(i + 1) % border.Count].loc,
              border[i].loc,
              border[(i + 2) % border.Count].loc,
              faceNormal);
            if (!convex) {
              break;
            }
          }
        }
        if (convex) {
          List<int> poly = new List<int>(border.Select(v => v.id));
          return new List<List<int>> { poly };
        }
      }

      // Otherwise, blow the face apart into triangles using the FaceTriangulator.
      List<Triangle> triangles = FaceTriangulator.Triangulate(border);
      return new List<List<int>>(triangles.Select(t => new List<int>() { t.vertId0, t.vertId1, t.vertId2 }));
    }
  }
}
