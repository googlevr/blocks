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
  class Flipper {
    /// <summary>
    /// Enum to indicate which axis to flip over.
    /// </summary>
    private enum FlipAxis { X_AXIS, Y_AXIS, Z_AXIS };

    /// <summary>
    /// Use the lastRotationModel of the controller to gauge if the controller is pointing up,
    /// left, or right and return the appropriate axis to flip over.
    /// </summary>
    /// <param name="lastRotationModel">The rotation of the controller in model space.</param>
    /// <returns>The relevant FlipAxis enum value.</returns>
    private static FlipAxis GetFlipAxis(Quaternion lastRotationModel) {
      Vector3 controllerNormal = lastRotationModel * Vector3.forward;

      float forwardAxisDiff = (controllerNormal - Vector3.forward).magnitude;
      float rightAxisDiff = (controllerNormal - Vector3.right).magnitude;
      float upAxisDiff = (controllerNormal - Vector3.up).magnitude;

      if (Mathf.Min(forwardAxisDiff, rightAxisDiff, upAxisDiff) == forwardAxisDiff) {
        return FlipAxis.X_AXIS;
      } else if (Mathf.Min(forwardAxisDiff, rightAxisDiff, upAxisDiff) == rightAxisDiff) {
        return FlipAxis.Z_AXIS;
      } else {
        return FlipAxis.Y_AXIS;
      }
    }

    /// <summary>
    /// Calculates the result of flipping the given meshes on an axis of symmetry determined by the controller
    /// position. The flip will be pivoted about the centroid of the collection of meshes. This does not modify
    /// the original meshes.
    /// </summary>
    /// <param name="meshesToFlip">A list of MMeshes.</param>
    /// <param name="controllerRotation">The peltzer controller rotation, used to determine the flipping axis.</param>
    /// <param name="result">
    ///   Out parameter. If this method returns true, this will be a list of meshes that represents
    ///   the result of flipping the given meshes. The returned meshes are not added to the model (it's the caller's
    ///   responsibility to do so, if that is desired).
    /// </param>
    /// <returns>True if the operation was successful (in which case the <code>result</code> out param indicates
    /// the resulting flipped meshes). False if the operation failed (in which case <code>result</code> is in an
    /// undefined state and shouldn't be used).</returns>
    public static bool FlipMeshes(List<MMesh> meshesToFlip, Quaternion controllerRotation,
      out List<MMesh> result) {
      result = new List<MMesh>();

      // Find the centroid of the previews, which will be our flip pivot point.
      Vector3 centroid = Math3d.FindCentroid(meshesToFlip);
      FlipAxis flippingAxis = GetFlipAxis(controllerRotation);

      foreach (MMesh originalMesh in meshesToFlip) {
        // First, convert the vertices to model space, flip them about the model centroid over the chosen axis,
        // and then convert back to mesh space.
        Dictionary<int, Vertex> newVerticesById = new Dictionary<int, Vertex>();
        foreach (int vertexId in originalMesh.GetVertexIds()) {
          Vector3 vertexModelSpace = originalMesh.VertexPositionInModelCoords(vertexId);
          Vector3 newLocModelSpace = vertexModelSpace;
          switch (flippingAxis) {
            case FlipAxis.X_AXIS:
              newLocModelSpace.x = centroid.x - (vertexModelSpace.x - centroid.x);
              break;
            case FlipAxis.Y_AXIS:
              newLocModelSpace.y = centroid.y - (vertexModelSpace.y - centroid.y);
              break;
            default:
              newLocModelSpace.z = centroid.z - (vertexModelSpace.z - centroid.z);
              break;
          }
          Vector3 newLocMeshSpace = originalMesh.ModelCoordsToMeshCoords(newLocModelSpace);
          newVerticesById.Add(vertexId, new Vertex(vertexId, newLocMeshSpace));
        }
        // Flip the normals on each face around the axis of symmetry, and reverse the winding of its vertices.
        Dictionary<int, Face> newFacesById = new Dictionary<int, Face>();
        foreach (Face originalFace in originalMesh.GetFaces()) {
          int id = originalFace.id;
          List<int> newVertices = originalFace.vertexIds.Reverse().ToList();

          newFacesById.Add(id, new Face(id, newVertices.AsReadOnly(),
            newVerticesById, originalFace.properties));
        }
        MMesh newMesh = new MMesh(originalMesh.id, originalMesh.offset, originalMesh.rotation,
          newVerticesById, newFacesById, originalMesh.groupId,
          originalMesh.remixIds != null ? new HashSet<string>(originalMesh.remixIds) : null);
        result.Add(newMesh);
      }
      return true;
    }
  }
}
