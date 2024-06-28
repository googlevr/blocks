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
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;
using System;
using System.Linq;
using System.Collections.ObjectModel;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   Utilities for modifing MMeshes.
    /// </summary>
    public class MeshUtil
    {
        /// <summary>
        /// Maximum distance from a vertex to the face's plane for the vertex to be considered coplanar with the face.
        /// If the distance between a face's vertex and the face's plane is more than this, the face is considered
        /// to be non-coplanar.
        /// </summary>
        public const float MAX_COPLANAR_DISTANCE = 0.001f;

        /// <summary>
        ///   Split a face at a given vertex if needed.  It is needed if the Face is not coplanar.
        /// </summary>
        /// <param name="operation">The current operation on the mutated mesh.</param>
        /// <param name="face">The face to split if necessary.</param>
        /// <param name="vertId">The id of the moved vertex.</param>
        /// <returns>Whether a face was split.</returns>
        public static bool SplitFaceIfNeeded(MMesh.GeometryOperation operation, Face face, int vertId)
        {
            bool mutated = false;

            // Check if face is coplanar.
            if (IsFaceCoplanar(operation, face))
            {
                // No need to split, just update the normals.

                Face replacementFace = Face.FaceWithPendingNormal(face.id, face.vertexIds, face.properties);
                Vector3 newNormal = MeshMath.CalculateNormal(replacementFace, operation);
                if (face.normal != newNormal)
                {
                    operation.ModifyFace(face.id, face.vertexIds, face.properties);
                }
                return false;
            }

            // Face is not coplanar. Split the face at the vertex that was moved.
            SplitFaceAt(operation, face, vertId);

            return true;
        }

        /// <summary>
        /// Determines whether or not a face is coplanar, that is, if all the vertices on the face lie on the same plane.
        /// </summary>
        /// <param name="operation">The current operation on the mutated mesh.</param>
        /// <param name="face">The face to check.</param>
        /// <returns>True iff the face is coplanar (all vertices are on the same plane).</returns>
        public static bool IsFaceCoplanar(MMesh.GeometryOperation operation, Face face)
        {
            return AreVerticesCoplanar(operation, face.vertexIds);
        }

        /// <summary>
        /// Determines whether or not the given vertices are all coplanar. Optionally, computes the unique plane
        /// to which they belong.
        /// </summary>
        /// <param name="mesh">The mesh to which the vertices belong.</param>
        /// <param name="vertexIds">The ID of the vertices to check.</param>
        /// <returns>True iff all the given vertices are coplanar.</returns>
        public static bool AreVerticesCoplanar(MMesh mesh, ReadOnlyCollection<int> vertexIds)
        {
            // Easy case: if there are 3 or fewer vertices, they are coplanar.
            if (vertexIds.Count <= 3) return true;

            Plane facePlane;
            int indexOfThird;
            if (!CalculateCommonPlane(mesh, vertexIds, out facePlane, out indexOfThird))
            {
                return false;
            }

            // Now we need to check the remaining vertices to see if they are on the same plane.
            // We start at i == 2 because we know 0 and 1 are on the plane (as they were part of the definition).
            for (int i = 2; i < vertexIds.Count; i++)
            {
                // The third vertex that was used to define the plane is also coplanar by definition, so skip it for
                // a mild performance gain.
                if (i == indexOfThird) continue;

                Vector3 pos = mesh.VertexPositionInMeshCoords(vertexIds[i]);

                // To check if it's coplanar, we just calculate the distance from the vertex to the plane.
                // If that's not close to 0, then the vertex is not on the face's plane.
                if (Mathf.Abs(facePlane.GetDistanceToPoint(pos)) > MAX_COPLANAR_DISTANCE)
                {
                    return false;
                }
            }

            // If we got here, we didn't find any vertices that were too far away from the face's plane, so we have
            // determined that the face is coplanar.
            return true;
        }

        /// <summary>
        /// Determines whether or not the given vertices are all coplanar in an ongoing GeometryOperation. 
        /// </summary>
        /// <param name="operation">The current operation on the mutated mesh.</param>
        /// <param name="vertexIds">The ID of the vertices to check.</param>

        /// <returns>True iff all the given vertices are coplanar.</returns>
        public static bool AreVerticesCoplanar(MMesh.GeometryOperation operation, ReadOnlyCollection<int> vertexIds)
        {
            // Easy case: if there are 3 or fewer vertices, they are coplanar.
            if (vertexIds.Count <= 3) return true;

            Plane facePlane;
            int indexOfThird;
            if (!CalculateCommonPlane(operation, vertexIds, out facePlane, out indexOfThird))
            {
                return false;
            }

            // Now we need to check the remaining vertices to see if they are on the same plane.
            // We start at i == 2 because we know 0 and 1 are on the plane (as they were part of the definition).
            for (int i = 2; i < vertexIds.Count; i++)
            {
                // The third vertex that was used to define the plane is also coplanar by definition, so skip it for
                // a mild performance gain.
                if (i == indexOfThird) continue;

                Vector3 pos = operation.GetCurrentVertexPositionMeshSpace(vertexIds[i]);

                // To check if it's coplanar, we just calculate the distance from the vertex to the plane.
                // If that's not close to 0, then the vertex is not on the face's plane.
                if (Mathf.Abs(facePlane.GetDistanceToPoint(pos)) > MAX_COPLANAR_DISTANCE)
                {
                    return false;
                }
            }

            // If we got here, we didn't find any vertices that were too far away from the face's plane, so we have
            // determined that the face is coplanar.
            return true;
        }

        /// <summary>
        /// Calculates the common plane of all the provided vertices.
        /// If the vertices are not all coplanar, returns the (somewhat arbitrarily picked) plane that is defined
        /// by the first two vertices and a third vertex that's not colinear with the first two.
        /// </summary>
        /// <param name="mesh">The mesh to which the vertices belong.</param>
        /// <param name="vertexIds">The vertices to process.</param>
        /// <param name="commonPlane">The common plane of the vertices.</param>
        /// <param name="indexOfThird">The index of the third vertex used to define the plane (the first two
        /// are vertices [0] and [1]).</param>
        /// <returns>True if the common plane could be calculated (in which case commonPlane and
        /// indexOfThird are valid), or false if it could not (in which case commonPlane and indexOfThird
        /// have undefined contents).</returns>
        public static bool CalculateCommonPlane(MMesh mesh, ReadOnlyCollection<int> vertexIds,
            out Plane commonPlane, out int indexOfThird)
        {
            commonPlane = new Plane();
            indexOfThird = -1;

            // We need at least 3 vertices to get a plane.
            if (vertexIds.Count < 3) return false;

            // We need three non-colinear vertices to define the face's plane.
            // We will use vertices 0, 1 and search for another one that's not colinear with them.
            Vector3 first = mesh.VertexPositionInMeshCoords(vertexIds[0]);
            Vector3 second = mesh.VertexPositionInMeshCoords(vertexIds[1]);
            for (int i = 2; i < vertexIds.Count; i++)
            {
                Vector3 third = mesh.VertexPositionInMeshCoords(vertexIds[i]);
                if (!Math3d.AreColinear(first, second, third))
                {
                    // Found it. Vertices 0, 1 and i are non-colinear so we will use them to define a plane.
                    commonPlane = new Plane(first, second, third);
                    indexOfThird = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Calculates the common plane of all the provided vertices.
        /// If the vertices are not all coplanar, returns the (somewhat arbitrarily picked) plane that is defined
        /// by the first two vertices and a third vertex that's not colinear with the first two.
        /// </summary>
        /// <param name="operation">The current operation on the mutated mesh.</param>
        /// <param name="vertexIds">The vertices to process.</param>
        /// <param name="commonPlane">The common plane of the vertices.</param>
        /// <param name="indexOfThird">The index of the third vertex used to define the plane (the first two
        /// are vertices [0] and [1]).</param>
        /// <returns>True if the common plane could be calculated (in which case commonPlane and
        /// indexOfThird are valid), or false if it could not (in which case commonPlane and indexOfThird
        /// have undefined contents).</returns>
        public static bool CalculateCommonPlane(MMesh.GeometryOperation operation, ReadOnlyCollection<int> vertexIds,
          out Plane commonPlane, out int indexOfThird)
        {
            commonPlane = new Plane();
            indexOfThird = -1;

            // We need at least 3 vertices to get a plane.
            if (vertexIds.Count < 3) return false;

            // We need three non-colinear vertices to define the face's plane.
            // We will use vertices 0, 1 and search for another one that's not colinear with them.
            Vector3 first = operation.GetCurrentVertexPositionMeshSpace(vertexIds[0]);
            Vector3 second = operation.GetCurrentVertexPositionMeshSpace(vertexIds[1]);
            for (int i = 2; i < vertexIds.Count; i++)
            {
                Vector3 third = operation.GetCurrentVertexPositionMeshSpace(vertexIds[i]);
                if (!Math3d.AreColinear(first, second, third))
                {
                    // Found it. Vertices 0, 1 and i are non-colinear so we will use them to define a plane.
                    commonPlane = new Plane(first, second, third);
                    indexOfThird = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///   Split the given Face at the given vertex. Handles concanve and convex vertices.
        ///   If this is a convex vertex, the face will be split such that the vertex and its
        ///   neighbors form a new triangular face. If the vertex is concave, the face will be
        ///   triangulated.
        /// </summary>
        private static void SplitFaceAt(MMesh.GeometryOperation operation, Face face, int vertId)
        {
            // First let's check if the vertex is convex.
            int vertIndex = face.vertexIds.IndexOf(vertId);
            AssertOrThrow.True(vertIndex >= 0, "Vertex not found in face.");
            int prevIndex = (vertIndex - 1 + face.vertexIds.Count) % face.vertexIds.Count;
            int nextIndex = (vertIndex + 1) % face.vertexIds.Count;
            Vector3 vertexPos = operation.GetCurrentVertexPositionMeshSpace(vertId);
            Vector3 prevPos = operation.GetCurrentVertexPositionMeshSpace(face.vertexIds[prevIndex]);
            Vector3 nextPos = operation.GetCurrentVertexPositionMeshSpace(face.vertexIds[nextIndex]);

            Vector3 faceNormal = MeshMath.CalculateNormal(face, operation);
            if (Math3d.IsConvex(vertexPos, prevPos, nextPos, faceNormal))
            {
                // Vertex is convex, so we can use the simple approach of just splitting out a new triangle with
                // the vertex and its neighbors.
                SplitFaceAtConvexVertex(operation, face, vertId);
            }
            else
            {
                // Vertex is concave. For now, for simplicity, just split the whole face into triangles.
                SplitFaceIntoTriangles(operation, face);
            }
        }

        /// <summary>
        ///   Split the given Face at the given vertex. This assumes that the vertex is CONVEX.
        ///   To split the face, we remove the vertex from the original face
        ///   and create a new Face containing that vertex and the two neighboring ones.  The
        ///   new face will have "flat" normals.
        /// </summary>
        private static void SplitFaceAtConvexVertex(MMesh.GeometryOperation operation, Face face, int vertId)
        {
            List<int> vertIds = new List<int>(face.vertexIds);

            int indexOf = vertIds.FindIndex(x => x == vertId);
            // Due to hole slicing, the same vertex that was moved may appear in multiple places but it
            // should be in at least one.
            AssertOrThrow.True(indexOf >= 0, "Internal error.  Should have found matching vertex.");

            // Each appearance will need its own slice so just run the slicing code multiple times. Each
            // iteration will remove one appearance and create the appropriate geometry.
            do
            {
                // Create a new face with exactly 3 vertices, including the one we are splitting at.
                List<int> newVertIds = new List<int>();
                List<Vector3> newNormals = new List<Vector3>();
                for (int i = -1; i < 2; i++)
                {
                    newVertIds.Add(vertIds[(indexOf + i + vertIds.Count) % vertIds.Count]);
                }

                Face newFace = operation.AddFace(newVertIds, face.properties);

                // Remove the vert from the existing face.
                vertIds.RemoveAt(indexOf);
                operation.ModifyFace(new Face(face.id, vertIds.AsReadOnly(), face.normal, face.properties));
                indexOf = vertIds.FindIndex(x => x == vertId);
            } while (indexOf >= 0);
        }

        /// <summary>
        /// Splits the given face into its constituent triangles.
        /// </summary>
        /// <param name="mesh">The mesh to which the face belongs.</param>
        /// <param name="face">The face to split.</param>
        private static void SplitFaceIntoTriangles(MMesh.GeometryOperation operation, Face face)
        {
            List<Triangle> triangles = face.GetTriangulation(operation);
            operation.DeleteFace(face.id);

            for (int i = 0; i < triangles.Count; i++)
            {
                Triangle tri = triangles[i];
                operation.AddFace(new List<int>() { tri.vertId0, tri.vertId1, tri.vertId2 }, face.properties);
            }
        }

        /// <summary>
        /// Returns whether or not the face contains an edge connecting the two given vertices.
        /// The edge can be in any order (1 - 2 or 2 - 1).
        /// </summary>
        /// <param name="face">The face to check.</param>
        /// <param name="vertexId1">The ID of the first vertex.</param>
        /// <param name="vertexId2">The ID of the second vertex.</param>
        /// <returns>True iff the given vertices define an edge of the given face.</returns>
        public static bool FaceContainsEdge(Face face, int vertexId1, int vertexId2)
        {
            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                int next = (i + 1 >= face.vertexIds.Count) ? 0 : i + 1;
                if (vertexId1 == face.vertexIds[i] && vertexId2 == face.vertexIds[next]) return true;
                if (vertexId2 == face.vertexIds[i] && vertexId1 == face.vertexIds[next]) return true;
            }
            return false;
        }

        public static String Vector3ToString(Vector3 v3)
        {
            return "<" + v3.x.ToString("0.00000000") + ", " + v3.y.ToString("0.00000000") + ", " +
                   v3.z.ToString("0.00000000") + ">";
        }

        public static void PrintBounds(Bounds bounds, int indent)
        {
            String indentString = new String(' ', indent);
            Debug.Log(indentString + "center: " + Vector3ToString(bounds.center));
            Debug.Log(indentString + "extents: " + Vector3ToString(bounds.extents));
            Debug.Log(indentString + "max: " + Vector3ToString(bounds.max));
            Debug.Log(indentString + "min: " + Vector3ToString(bounds.min));
            Debug.Log(indentString + "size: " + Vector3ToString(bounds.size));
        }

        /// <summary>
        /// Computes a dictionary from edge IDs to the list of face IDs that edge connects.
        /// </summary>
        public static Dictionary<EdgeKey, List<int>> ComputeEdgeKeysToFaceIdsMap(MMesh mesh)
        {
            Dictionary<EdgeKey, List<int>> edgeKeysToFaceIds = new Dictionary<EdgeKey, List<int>>();
            foreach (Face face in mesh.GetFaces())
            {
                int previousVertexId = face.vertexIds[face.vertexIds.Count - 1];
                for (int i = 0; i < face.vertexIds.Count; i++)
                {
                    int currentVertexId = face.vertexIds[i];
                    EdgeKey edgeKey = new EdgeKey(mesh.id, currentVertexId, previousVertexId);
                    List<int> faceIds;
                    if (!edgeKeysToFaceIds.TryGetValue(edgeKey, out faceIds))
                    {
                        faceIds = new List<int>();
                        edgeKeysToFaceIds.Add(edgeKey, faceIds);
                    }
                    faceIds.Add(face.id);
                    previousVertexId = currentVertexId;
                }
            }
            return edgeKeysToFaceIds;
        }
    }
}
