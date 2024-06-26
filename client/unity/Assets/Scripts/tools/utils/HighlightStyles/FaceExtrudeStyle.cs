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
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tools.utils {
  /// <summary>
  /// This class exists primarily to hold the static method for RenderFaces when FACE_SELECT is set. It may be possible
  /// to consolidate this with the other Face*Style classes in the future.
  /// </summary>
  public class FaceExtrudeStyle {
    public static Material material;
    private static Mesh faceRenderMesh = new Mesh();
    // Renders vertex highlights.
    // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
    // face geometry frame to frame) - bug
    public static void RenderFaces(Model model,
        HighlightUtils.TrackedHighlightSet<FaceKey> faceHighlights,
        WorldSpace worldSpace) {
      HashSet<FaceKey> keys = faceHighlights.getKeysForStyle((int)FaceStyles.EXTRUDE);
      if (keys.Count == 0) { return; }
      faceRenderMesh.Clear();
      List<int> indices = new List<int>();
      List<Vector3> vertices = new List<Vector3>();
      // Because Unity does not make a "arbitrary data" vertex channel available to us, we're going to abuse the UV
      // channel to pass per-vertex animation state into the shader.
      List<Vector2> selectData = new List<Vector2>();
      List<Vector3> normals = new List<Vector3>();
      List<Vector4> selectPositions = new List<Vector4>();
      List<Color> colors = new List<Color>();

      int curIndex = 0;
      foreach (FaceKey key in keys) {
        if (!model.HasMesh(key.meshId)) { continue; }
        MMesh mesh = model.GetMesh(key.meshId);
        Face curFace;
        if (mesh.TryGetFace(key.faceId, out curFace)) {
          if (!mesh.HasFace(key.faceId)) continue;
          // For each face, add all triangles to the mesh with all per-face data set appropriately.
          Vector4 selectPosition = faceHighlights.GetCustomChannel0(key);
          float animPct = faceHighlights.GetAnimPct(key);
          Vector4 colorV4 = faceHighlights.GetCustomChannel1(key);
          Color faceColor = new Color(colorV4.x, colorV4.y, colorV4.z, colorV4.w);
          List<Triangle> tris = MeshHelper.TriangulateFace(mesh, curFace);
          for (int i = 0; i < tris.Count; i++) {
            // For each triangle in the face, add a vertex to the Mesh
            vertices.Add(mesh.VertexPositionInModelCoords(curFace.vertexIds[tris[i].vertId0]));
            normals.Add(curFace.normal);
            selectData.Add(new Vector2(animPct, 0f));
            indices.Add(curIndex);
            colors.Add(faceColor);
            selectPositions.Add(selectPosition);
            curIndex++;

            vertices.Add(mesh.VertexPositionInModelCoords(curFace.vertexIds[tris[i].vertId1]));
            normals.Add(curFace.normal);
            selectData.Add(new Vector2(animPct, 0f));
            indices.Add(curIndex);
            colors.Add(faceColor);
            selectPositions.Add(selectPosition);
            curIndex++;

            vertices.Add(mesh.VertexPositionInModelCoords(curFace.vertexIds[tris[i].vertId2]));
            normals.Add(curFace.normal);
            selectData.Add(new Vector2(animPct, 0f));
            indices.Add(curIndex);
            colors.Add(faceColor);
            selectPositions.Add(selectPosition);
            curIndex++;
          }
        }
      }
      faceRenderMesh.SetVertices(vertices);
      // These are not actually UVs - we're using the UV channel to pass per-primitive animation data so that edges
      // animate independently.
      faceRenderMesh.SetUVs(/* channel */ 0, selectData);
      faceRenderMesh.SetNormals(normals);
      faceRenderMesh.SetTriangles(indices, /* subMesh */ 0);
      faceRenderMesh.SetColors(colors);
      faceRenderMesh.SetTangents(selectPositions);

      Graphics.DrawMesh(faceRenderMesh, worldSpace.modelToWorld, material, 0);
    }
  }
}
