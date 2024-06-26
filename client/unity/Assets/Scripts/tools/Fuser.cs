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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools {
  class Fuser {
    /// <summary>
    /// Fuses all the given meshes into a single mesh. This is different from grouping because this
    /// does not preserve the original components and CAN'T BE UNDONE -- once the meshes are fused,
    /// they can never be taken apart.
    /// 
    /// NOTE: This is used for debug purposes only for now. We don't expose any tools that fuse meshes.
    /// </summary>
    /// <param name="meshes">The meshes to fuse together.</param>
    /// <param name="fusedMeshId">The ID of the fused mesh.</param>
    /// <returns>The fused mesh.</returns>
    public static MMesh FuseMeshes(IEnumerable<MMesh> meshes, int fusedMeshId) {
      // Set up the new MMesh's data.
      Vector3 offset = Vector3.zero;
      foreach (MMesh mesh in meshes) {
        offset += mesh.offset;
      }
      offset /= meshes.Count();
      Quaternion rotation = Math3d.MostCommonRotation(meshes.Select(m => m.rotation));
      Dictionary<int, Vertex> verticesById = new Dictionary<int, Vertex>();
      Dictionary<int, Face> facesById = new Dictionary<int, Face>();
      HashSet<string> allRemixIds = new HashSet<string>();

      // Collapse each MMesh into the new data above.
      int nextVertexId = 0;
      int nextFaceId = 0;
      foreach (MMesh mesh in meshes) {
        if (mesh.remixIds != null) {
          allRemixIds.UnionWith(mesh.remixIds);
        }

        // Copy each vertex with a new id.
        Dictionary<int, int> originalVertexIdsToNewVertexIds = new Dictionary<int, int>();
        foreach (Vertex originalVertex in mesh.GetVertices()) {
          originalVertexIdsToNewVertexIds.Add(originalVertex.id, nextVertexId);
          Vector3 newVertexLoc = Quaternion.Inverse(rotation) *
            ((mesh.rotation * originalVertex.loc) + mesh.offset - offset);
          Vertex newVertex = new Vertex(nextVertexId, newVertexLoc);
          verticesById.Add(nextVertexId, newVertex);
          nextVertexId++;
        }

        // Copy each face with a new id, referencing into the new vertex ids.
        foreach (Face originalFace in mesh.GetFaces()) {
          List<int> vertexIds = new List<int>();
          foreach (int originalVertexId in originalFace.vertexIds) {
            vertexIds.Add(originalVertexIdsToNewVertexIds[originalVertexId]);
          }
          
          // Can't use original normal because vertices may have been rotated.
          Face newFace = new Face(nextFaceId, vertexIds.AsReadOnly(), verticesById,
            originalFace.properties);
          facesById.Add(nextFaceId, newFace);
          nextFaceId++;
        }
      }

      // Create a new MMesh out of the collapsed data.
      return new MMesh(fusedMeshId, offset, rotation, verticesById, facesById, MMesh.GROUP_NONE,
        allRemixIds.Count > 0 ? allRemixIds : null);
    }
  }
}