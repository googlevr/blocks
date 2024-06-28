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
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;
using System.Linq;
using System.Text;
using UnityEngine;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.core
{
    public class MeshValidator
    {
        /// <summary>
        /// By how much we scale the mesh for validation purposes.
        /// This is for numerical stability and to avoid floating-point errors.
        /// </summary>
        private const float STABILIZATION_SCALE_FACTOR = 1000.0f;

        /// <summary>
        /// Tolerance when checking for intersections between rays and triangles. This makes the triangles we use
        /// in the test slightly bigger than they really are, to avoid degeneracy problems at the edges.
        /// </summary>
        private const float TRIANGLE_INTERSECTION_TOLERANCE = 1.005f;

        /// <summary>
        /// Tests whether or not the given mesh is valid (heuristically).
        ///
        /// For the purposes of Poly, a mesh is defined to be valid if no back faces are exposed. In other words, a mesh
        /// is valid if and only if there is no angle from which an outside observer could see an incorrectly wound
        /// face (a back face). Note that self-intersecting meshes can still be valid! For example, the Freeform tool
        /// can create self intersecting meshes that are valid, because even though they have self intersections, they
        /// DO NOT expose any back faces.
        /// 
        /// This algorithm implements that definition heuristically, checking each face to see if it's exposed.
        /// It's not bullet-proof but the hope is that it's difficult for an average user to accidentally construct a
        /// case where this algorithm doesn't work.
        /// 
        /// In particular, the algorithm is designed in a way that a false positives are more likely than false negatives.
        /// That is, if this algorithm says a mesh is INVALID, then it almost certainly is invalid.
        /// </summary>
        /// <param name="mesh">The mesh to test.</param>
        /// <param name="updatedVerts">The vertices that were updated</param>
        /// <returns>True if and only if the mesh is valid (heuristically).</returns>
        public static bool IsValidMesh(MMesh mesh, HashSet<int> updatedVertIds)
        {
            // Our algorithm is very brittle on tiny meshes because of floating point and numerical stability issues,
            // so for the purposes of validation we work on a scaled-up copy that's more reliable to work with.
            // This fixes some problems related to working with tiny meshes.
            // The cost of this is making a MMesh copy per frame.
            mesh = CloneScaled(mesh, STABILIZATION_SCALE_FACTOR);

            // For now we need to triangulate the whole mesh because the algorithm is hard-coded to triangular faces.
            // The affected faces have to be tested against all other faces, so all faces need to be available as triangles.
            List<Triangle> geometry = FaceTriangulator.TriangulateMesh(mesh);

            // Calculate the normals and vertex positions of each triangle.
            List<TriangleInfo> triangleInfo = CalculateTriangleInfo(mesh, geometry);

            // Test rays we will use during the main loop.
            Ray[] testRays = new Ray[4];
            Vector3[] triangleVerts = new Vector3[3];

            // The main idea of this algorithm is to check each face to see if it's "exposed", which makes the mesh invalid.
            // Checking in a mathematically correct way is hard and doesn't work well in self-intersecting meshes, so our
            // method is approximate.
            //
            // Intuitively, to check if a back-face is "exposed", you could imagine a wide-angle camera placed on the back
            // of the face. Now imagine what you would see in the image. If the mesh is valid, all back faces are "inside"
            // so you would only the inside of the mesh. If the face is exposed (visible from the outside) then in that image
            // you would be able to see a hole from which you could see the sky. So to us a mesh is valid if no back faces
            // can "see the sky".
            //
            // How do we check if a back face can see the sky?
            //
            // For each face, we calculate its anti-normal (negative of the normal), which is a vector that points INWARDS,
            // opposite to the normal of the face. In a correct mesh, the anti-normal will point to the INSIDE of the mesh.
            // So if we start at the center of the face and follow along the anti-normal, we should be inside the mesh. This
            // means that if we continue, we should eventually EXIT the mesh (that is, intersect with the back side of a
            // face). If we never exit the mesh, then it's because that back face is exposed (we can "see the sky"),
            // which makes the mesh invalid.
            //
            // It's not sufficient to check just the center of the face. Technically we should check ALL points on the face
            // and all possible rays to see if there's a ray that doesn't exit the mesh. Naturally checking all possible
            // rays would be pretty expensive, so instead we just check the CENTER and the VERTICES. To increase coverage, the
            // rays we use when checking the vertices point slightly away from the anti-normal, to allow us to catch a bigger
            // field of view.
            for (int i = 0; i < geometry.Count; i++)
            {
                Triangle triangle = geometry[i];
                TriangleInfo thisTriangleInfo = triangleInfo[i];
                Vector3 antiNormal = -thisTriangleInfo.normal;

                // If the triangle wasn't modified, skip it.
                if (!updatedVertIds.Contains(triangle.vertId0) && !updatedVertIds.Contains(triangle.vertId1) &&
                  !updatedVertIds.Contains(triangle.vertId2)) continue;

                // We will now check if the back of this face can "see the sky". To do this, let's figure out our
                // four "test rays": one from the center and one from each vertex. The rays go in the direction of the
                // anti-normal. The rays from each vertex are slightly bent away from the center for bigger "wide angle"
                // coverage.
                triangleVerts[0] = thisTriangleInfo.v1;
                triangleVerts[1] = thisTriangleInfo.v2;
                triangleVerts[2] = thisTriangleInfo.v3;

                // Construct a test ray from (a point close to) each vertex.
                for (int j = 0; j < 3; j++)
                {
                    testRays[j] = new Ray(
                      // Pick a point that's close to the vertex but still inside the face, to avoid degeneracy problems.
                      triangleVerts[j] * 0.95f + thisTriangleInfo.center * 0.05f,
                      // Bend the direction of the ray slightly away from the anti-normal for wider-angle coverage.
                      antiNormal + (triangleVerts[j] - thisTriangleInfo.center).normalized * 0.1f);
                }
                // The last test ray starts at the center and goes along the anti-normal.
                testRays[3] = new Ray(thisTriangleInfo.center, antiNormal);

                // Check all the test rays. If one of them doesn't exit the mesh, it means this back face can see the sky,
                // so the mesh is invalid.
                if (!RaysExitMesh(mesh, geometry, triangleInfo, testRays, i)) return false;
            }
            // We found no reason to suspect the mesh is invalid.
            return true;
        }

        private static bool RaysExitMesh(MMesh mesh, List<Triangle> geometry, List<TriangleInfo> triangleInfo,
            Ray[] rays, int triangleIndexToIgnore)
        {
            for (int i = 0; i < rays.Length; i++)
            {
                if (!RayExitsMesh(mesh, geometry, triangleInfo, rays[i], triangleIndexToIgnore)) return false;
            }
            return true;
        }

        private static bool RayExitsMesh(MMesh mesh, List<Triangle> geometry, List<TriangleInfo> triangleInfo,
            Ray ray, int triangleIndexToIgnore = -1)
        {
            for (int i = 0; i < geometry.Count; i++)
            {
                // Check if we should ignore this triangle.
                if (i == triangleIndexToIgnore) continue;

                // Check if the ray intersects this triangle.
                Triangle thisTriangle = geometry[i];
                TriangleInfo thisTriangleInfo = triangleInfo[i];

                // Use the vertex positions adjusted for tolerance, so we don't miss edges between faces.
                bool intersects = Math3d.RayIntersectsTriangle(ray,
                  thisTriangleInfo.v1WithTolerance, thisTriangleInfo.v2WithTolerance,
                  thisTriangleInfo.v3WithTolerance, thisTriangleInfo.normal);
                if (!intersects) continue;

                // If the dot product is < 0, then the ray is going against the normal, so it's an entry.
                // Otherwise, it's an exit.
                if (Vector3.Dot(ray.direction, thisTriangleInfo.normal) > 0) return true;
            }
            // No exit detected.
            return false;
        }

        private static List<TriangleInfo> CalculateTriangleInfo(MMesh mesh, List<Triangle> geometry)
        {
            List<TriangleInfo> info = new List<TriangleInfo>(geometry.Count);
            for (int i = 0; i < geometry.Count; i++)
            {
                TriangleInfo thisInfo = new TriangleInfo();
                Triangle triangle = geometry[i];
                Vector3 threeMinusOne = mesh.VertexPositionInMeshCoords(triangle.vertId2) -
                  mesh.VertexPositionInMeshCoords(triangle.vertId0);
                Vector3 twoMinusOne = mesh.VertexPositionInMeshCoords(triangle.vertId1) -
                  mesh.VertexPositionInMeshCoords(triangle.vertId0);
                thisInfo.normal = Vector3.Cross(twoMinusOne, threeMinusOne).normalized;

                // For the validity test, we want to be a little bit lenient with triangles because if we use the exact
                // triangles, then we might miss a ray that that passes exactly through an edge between two faces (as we will
                // consider the ray as not having intersected either face).
                // Therefore, we need to use a slightly enlarged triangle so that there's some overlap at the
                // edges and we don't miss any rays.
                thisInfo.v1 = mesh.VertexPositionInMeshCoords(triangle.vertId0);
                thisInfo.v2 = mesh.VertexPositionInMeshCoords(triangle.vertId1);
                thisInfo.v3 = mesh.VertexPositionInMeshCoords(triangle.vertId2);
                thisInfo.center = (thisInfo.v1 + thisInfo.v2 + thisInfo.v3) / 3.0f;
                thisInfo.v1WithTolerance = thisInfo.center + (thisInfo.v1 - thisInfo.center) * TRIANGLE_INTERSECTION_TOLERANCE;
                thisInfo.v2WithTolerance = thisInfo.center + (thisInfo.v2 - thisInfo.center) * TRIANGLE_INTERSECTION_TOLERANCE;
                thisInfo.v3WithTolerance = thisInfo.center + (thisInfo.v3 - thisInfo.center) * TRIANGLE_INTERSECTION_TOLERANCE;

                info.Add(thisInfo);
            }
            AssertOrThrow.True(info.Count == geometry.Count, "# of triangle infos should be same as # of triangles.");
            return info;
        }

        private static MMesh CloneScaled(MMesh mesh, float factor)
        {
            MMesh clone = mesh.Clone();
            MMesh.GeometryOperation cloneScaleOperation = clone.StartOperation();
            foreach (int id in clone.GetVertexIds())
            {
                cloneScaleOperation.ModifyVertexMeshSpace(id, clone.VertexPositionInMeshCoords(id) * factor);
            }
            cloneScaleOperation.Commit();
            return clone;
        }

        private struct TriangleInfo
        {
            // Original position of each vertex of the triangle (in mesh coords).
            public Vector3 v1, v2, v3;
            // Positions of each vertex in the slightly enlarged triangle (used for testing intersections).
            public Vector3 v1WithTolerance, v2WithTolerance, v3WithTolerance;
            // Position of center of triangle (in mesh coords).
            public Vector3 center;
            // Normal of the triangle.
            public Vector3 normal;
        }
    }
}
