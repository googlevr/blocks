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

namespace com.google.apps.peltzer.client.model.csg {
  public enum PolygonStatus {
    UNKNOWN,
    INSIDE,
    OUTSIDE,
    SAME,
    OPPOSITE,
  }

  /// <summary>
  ///   A convex, coplanar polygon.
  /// </summary>
  public class CsgPolygon {
    public List<CsgVertex> vertices { get; set; }
    public Plane plane { get; private set; }
    public FaceProperties faceProperties { get; set; }
    public Bounds bounds { get; private set; }
    public PolygonStatus status { get; set; }
    public Vector3 baryCenter;

    public CsgPolygon(List<CsgVertex> vertices, FaceProperties faceProperties, Vector3? normal = null) {
      this.vertices = vertices;
      this.faceProperties = faceProperties;

      if (normal != null) {
        plane = new Plane(normal.Value, vertices[0].loc);
      } else {
        plane = new Plane(vertices[0].loc, vertices[1].loc, vertices[2].loc);
      }

      // Calc bounds and baryCenter
      Bounds bounds = new Bounds(vertices[0].loc, Vector3.zero);
      baryCenter = vertices[0].loc;
      for (int i = 1; i < vertices.Count; i++) {
        bounds.Encapsulate(vertices[i].loc);
        baryCenter += vertices[i].loc;
      }
      baryCenter /= (float)vertices.Count;

      // Expand a bit to cover floating point error.
      bounds.Expand(0.002f);
      this.bounds = bounds;

      // Status is UNKNOWN to start with.
      status = PolygonStatus.UNKNOWN;
    }

    internal CsgPolygon Invert() {
      List<CsgVertex> newVerts = new List<CsgVertex>(vertices);
      newVerts.Reverse();
      return new CsgPolygon(newVerts, faceProperties, -plane.normal);
    }
  }
}
