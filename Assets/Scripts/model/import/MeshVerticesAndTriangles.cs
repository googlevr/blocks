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

namespace com.google.apps.peltzer.client.model.import {
  /// <summary>
  ///   A helper class to hold vertices and triangles for a mesh, and convert them to a Mesh if needed.
  /// </summary>
  public class MeshVerticesAndTriangles {
    public Vector3[] meshVertices;
    public int[] triangles;

    public MeshVerticesAndTriangles(Vector3[] meshVertices, int[] triangles) {
      this.meshVertices = meshVertices;
      this.triangles = triangles;
    }

    // Must be called on main thread.
    public Mesh ToMesh() {
      Mesh mesh = new Mesh();
      mesh.vertices = meshVertices;
      mesh.triangles = triangles;
      mesh.RecalculateBounds();
      mesh.RecalculateNormals();
      return mesh;
    }
  }
}