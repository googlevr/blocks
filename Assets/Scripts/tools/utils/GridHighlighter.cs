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

using System.Collections.Generic;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils {
  /// <summary>
  /// Constructs and renders a grid of points, snapped to the universal grid.
  /// </summary>
  public class GridHighlighter {
    public static readonly int NO_SHADOWS_LAYER = 9; // NoShadowsLayer -- won't cast a shadow

    private Mesh gridMesh;

    public void InitGrid(int numVertsPerRow, int gridSkip = 1) {
      gridMesh = new Mesh();
      gridMesh.Clear();

      List<int> indexList = new List<int>();
      List<Vector3> vertexList = new List<Vector3>();
      int x, y, z;
      float curX, curY, curZ;
      int index = 0;
      for (x = 0; x < numVertsPerRow; x++) {
        curX = (x - (Mathf.FloorToInt(numVertsPerRow / 2))) * GridUtils.GRID_SIZE;
        for (y = 0; y < numVertsPerRow; y++) {
          curY = (y - (Mathf.FloorToInt(numVertsPerRow / 2))) * GridUtils.GRID_SIZE;
          for (z = 0; z < numVertsPerRow; z++) {
            curZ = (z - (Mathf.FloorToInt(numVertsPerRow / 2))) * GridUtils.GRID_SIZE;
            vertexList.Add(new Vector3(curX, curY, curZ));
            indexList.Add(index);
            index++;
          }
        }
      }
      int[] indices = indexList.ToArray();
      Vector3[] vertices = vertexList.ToArray();

      gridMesh.vertices = vertices;
      // Since we're using a point geometry shader we need to set the mesh up to supply data as points.
      gridMesh.SetIndices(indices, MeshTopology.Points, 0 /* submesh id */, true /* recalculate bounds */);
    }

    public void Render(Vector3 unsnappedGridCenter, Matrix4x4 objectToWorld, Material renderMat, int scale) {
      // Scale to the correct grid granularity, then translate to the correct model position, then apply model to
      // world transform. This should result in the correct model->world matrix for the grid's vertices.
      Vector3 gridCenter = GridUtils.SnapToGrid(unsnappedGridCenter/scale) * scale;
      Matrix4x4 gridTransform = objectToWorld * Matrix4x4.TRS(gridCenter, Quaternion.identity, Vector3.one)
        * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, scale));
      Graphics.DrawMesh(gridMesh, gridTransform, renderMat, NO_SHADOWS_LAYER);
    }
  }
}