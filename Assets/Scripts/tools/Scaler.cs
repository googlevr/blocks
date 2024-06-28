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
using System.Linq;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.tools
{
    /// <summary>
    ///   Scales meshes.
    /// </summary>
    class Scaler
    {
        /// <summary>
        ///   Scales a collection of meshes in place.
        /// </summary>
        /// <param name="meshes">
        ///   The collection of meshes to scale.
        /// </param>
        /// <param name="scaleFactor">The factor used for scaling.</param>
        /// <returns>
        ///   True if the operation succeeded, false if scaling would have made any mesh too small.
        ///   If false, nothing is modified.
        /// </returns>
        public static bool TryScalingMeshes(List<MMesh> meshes, float scaleFactor)
        {
            if (meshes.Count == 0)
            {
                return false;
            }

            // Ensure that scaling down wouldn't take any mesh beneath the minimal grid size.
            if (scaleFactor < 1)
            {
                foreach (MMesh mesh in meshes)
                {
                    float maxSize = Mathf.Max(mesh.bounds.size.x, Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
                    if (maxSize * scaleFactor < GridUtils.GRID_SIZE * 2)
                    {
                        return false;
                    }
                }
            }

            // Scale the vertices of each mesh by expanding or contracting its vertices relative to the center of the mesh.
            Vector3 centroid = Math3d.FindCentroid(meshes);

            foreach (MMesh mesh in meshes)
            {
                MMesh.GeometryOperation scaleOperation = mesh.StartOperation();
                foreach (int vertexId in mesh.GetVertexIds())
                {
                    // Scale the vector, and move it towards/from the mesh origin by half of the size difference.
                    Vector3 loc = mesh.VertexPositionInMeshCoords(vertexId);
                    scaleOperation.ModifyVertexMeshSpace(vertexId, loc * scaleFactor);
                }
                // Uniform scale means this is safe
                scaleOperation.CommitWithoutRecalculation();
                mesh.RecalcBounds();
                Vector3 offsetVector = (mesh.offset - centroid) * scaleFactor;
                mesh.offset = centroid + (offsetVector.normalized * offsetVector.magnitude);
            }

            return true;
        }

        /// <summary>
        ///   Given a list of MMeshes scales them down to fit within a desiredSize.
        ///   This is currently used by the PolyMenu to scale down previews and is model independent.
        /// </summary>
        /// <param name="file">The MMeshes to be scaled.</param>
        /// <returns>The scaled MMeshes.</returns>
        public static List<MMesh> ScaleMeshes(List<MMesh> originalMeshes, float desiredSize)
        {
            Bounds bounds = new Bounds();

            List<MMesh> meshes = new List<MMesh>(originalMeshes.Count);
            // Create a clone of the meshes. We don't want to scale the actual meshes.
            for (int i = 0; i < originalMeshes.Count; i++)
            {
                meshes.Add(originalMeshes[i].Clone());
            }

            List<Vector3> offsets = new List<Vector3>(meshes.Count());
            for (int i = 0; i < meshes.Count; i++)
            {
                offsets.Add(meshes[i].offset);

                // Find the bounds of the meshes so that we can scale them.
                if (bounds.size == Vector3.zero)
                {
                    bounds = meshes[i].bounds;
                }
                else
                {
                    bounds.Encapsulate(meshes[i].bounds);
                }
            }

            Vector3 centroid = Math3d.FindCentroid(offsets);

            float maxSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scaleFactor = desiredSize / maxSize;

            // Scale the meshes to fit into the menu.
            for (int i = 0; i < meshes.Count; i++)
            {
                Vector3 originalOffset = meshes[i].offset;
                MMesh.GeometryOperation scaleOperation = meshes[i].StartOperation();
                foreach (Vertex vertex in meshes[i].GetVertices())
                {
                    // Scale the vector, and move it towards/from the mesh origin by half of the size difference.
                    scaleOperation.ModifyVertexMeshSpace(vertex.id, vertex.loc * scaleFactor);
                }
                // Uniform scale means normals are unaffected
                scaleOperation.CommitWithoutRecalculation();
                Vector3 offsetVector = (originalOffset - centroid) * scaleFactor;
                meshes[i].offset = centroid + (offsetVector.normalized * offsetVector.magnitude);
            }

            return meshes;
        }
    }
}
