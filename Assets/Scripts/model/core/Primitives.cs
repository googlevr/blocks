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

using com.google.apps.peltzer.client.model.util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core
{

    /// <summary>
    ///   Helper methods that generate MMeshes for geometric primitives.
    /// </summary>
    public class Primitives
    {
        // Note: the Shape enum is used to index things, so it should always start at 0 and count up
        // without skipping numbers. Do not define items to have arbitrary values. You will have a bad time.
        public enum Shape { CONE, SPHERE, CUBE, CYLINDER, TORUS, ICOSAHEDRON };
        private const int LINES_OF_LATITUDE = 8;
        private const int LINES_OF_LONGITUDE = 12;
        public static readonly int NUM_SHAPES = Enum.GetValues(typeof(Shape)).Length;

        private static readonly int[,] CUBE_POINTS = {
      { 0, 4, 6, 2 },  // left
      { 1, 3, 7, 5 },  // right
      { 0, 1, 5, 4 },  // bottom
      { 2, 6, 7, 3 },  // top
      { 0, 2, 3, 1},   // front
      { 4, 5, 7, 6}};  // back

        private static readonly float GOLDEN_RATIO_SCALED = 0.1618033988749895f;

        private static readonly Vector3[] ICOSAHEDRON_POINTS = {
            new Vector3(-0.1f, GOLDEN_RATIO_SCALED, 0),
            new Vector3(0.1f, GOLDEN_RATIO_SCALED, 0),
            new Vector3(-0.1f, -GOLDEN_RATIO_SCALED, 0),
            new Vector3(0.1f, -GOLDEN_RATIO_SCALED, 0),

            new Vector3(0, -0.1f, GOLDEN_RATIO_SCALED),
            new Vector3(0, 0.1f, GOLDEN_RATIO_SCALED),
            new Vector3(0, -0.1f, -GOLDEN_RATIO_SCALED),
            new Vector3(0, 0.1f, -GOLDEN_RATIO_SCALED),

            new Vector3(GOLDEN_RATIO_SCALED, 0, -0.1f),
            new Vector3(GOLDEN_RATIO_SCALED, 0, 0.1f),
            new Vector3(-GOLDEN_RATIO_SCALED, 0, -0.1f),
            new Vector3(-GOLDEN_RATIO_SCALED, 0, 0.1f)
        };

        private static readonly List<int>[] ICOSAHEDRON_FACES = {
            // Faces around point 0.
            new List<int> { 0, 11, 5 },
            new List<int> { 0, 5, 1 },
            new List<int> { 0, 1, 7 },
            new List<int> { 0, 7, 10 },
            new List<int> { 0, 10, 11 },

            // Faces adjacent to point 0.
            new List<int> { 1, 5, 9 },
            new List<int> { 5, 11, 4 },
            new List<int> { 11, 10, 2 },
            new List<int> { 10, 7, 6 },
            new List<int> { 7, 1, 8 },

            // Faces around point 3.
            new List<int> { 3, 9, 4 },
            new List<int> { 3, 4, 2 },
            new List<int> { 3, 2, 6 },
            new List<int> { 3, 6, 8 },
            new List<int> { 3, 8, 9 },

            // Faces adjacent to point 3.
            new List<int> { 4, 9, 5 },
            new List<int> { 2, 4, 11 },
            new List<int> { 6, 2, 10 },
            new List<int> { 8, 6, 7 },
            new List<int> { 9, 8, 1 }
        };

        public static MMesh BuildPrimitive(Shape shape, Vector3 scale, Vector3 offset, int id, int material)
        {
            switch (shape)
            {
                case Shape.CONE:
                    return AxisAlignedCone(id, offset, scale, material);
                case Shape.CUBE:
                    return AxisAlignedBox(id, offset, scale, material);
                case Shape.CYLINDER:
                    return AxisAlignedCylinder(id, offset, scale, /* holeRadius */ null, material);
                case Shape.SPHERE:
                    return AxisAlignedUVSphere(LINES_OF_LONGITUDE, LINES_OF_LATITUDE, id, offset, scale, material);
                case Shape.TORUS:
                    return Torus(id, offset, scale, material);
                case Shape.ICOSAHEDRON:
                    return AxisAlignedIcosphere(id, offset, scale, material, 0);
                default:
                    return AxisAlignedBox(id, offset, scale, material);
            }
        }

        /// <summary>
        ///   Create an axis-aligned box.
        /// </summary>
        /// <param name="id">Id for the mesh.</param>
        /// <param name="center">Center of the box.</param>
        /// <param name="scale">Scale of box.</param>
        /// <param name="materialId">Material id for the mesh.</param>
        /// <returns>An MMesh that renders a box.</returns>
        public static MMesh AxisAlignedBox(
          int id, Vector3 center, Vector3 scale, int materialId)
        {
            FaceProperties faceProperties = new FaceProperties(materialId);

            List<Vertex> corners = new List<Vertex>(8);

            // First make the vertices.  Use the first 3 binary bits of an int
            // for the direction on each axis.
            for (int i = 0; i < /* Corners in a cube. */ 8; i++)
            {
                float x = (i & 1) == 0 ? -1 : 1;
                float y = (i & 2) == 0 ? -1 : 1;
                float z = (i & 4) == 0 ? -1 : 1;
                corners.Add(new Vertex(i, new Vector3(x, y, z)));
            }

            corners = new List<Vertex>(Math3d.ScaleVertices(corners, scale));
            Dictionary<int, Vertex> vertices = corners.ToDictionary(c => c.id);
            // Create the faces based on our template.
            List<Face> faces = new List<Face>();
            for (int i = 0; i < /* Faces in a cube. */ 6; i++)
            {
                List<int> verts = new List<int>();
                for (int j = 0; j < /* Verts per face (i.e. rectangle). */ 4; j++)
                {
                    verts.Add(CUBE_POINTS[i, j]);
                }
                faces.Add(new Face(i, verts.AsReadOnly(), vertices, faceProperties));
            }
            return new MMesh(id, center, Quaternion.identity, vertices, faces.ToDictionary(f => f.id));
        }

        /// <summary>
        ///   Create an axis-aligned cylinder, with 'height' on the y-axis and 'radius' on the x and z axes.
        ///   The ids for the start vertices will be from 0 to SLICES-1 and for the end, SLICES to SLICES*2-1.
        ///   Faces have ids from 0 to SLICES-1 for the outside, SLICES for the start and SLICES+1 for end.
        /// </summary>
        /// <param name="id">Id for the mesh.</param>
        /// <param name="center">Center of the cylinder.</param>
        /// <param name="scale">Scale of cylinder.</param>
        /// <param name="holeRadius">Radius of hole inside cylinder.</param>
        /// <param name="materialId">Material id for the mesh.</param>
        /// <returns>An MMesh that renders a cylinder.</returns>
        public static MMesh AxisAlignedCylinder(int id, Vector3 center, Vector3 scale, float? holeRadius,
            int materialId)
        {
            // Controls the smoothness of the cylinder, we could make this a parameter later if we wanted.
            const int SLICES = 12;
            const int START_OFFSET = 0;
            const int END_OFFSET = SLICES;
            const int INNER_START_OFFSET = SLICES * 2;
            const int INNER_END_OFFSET = SLICES * 3;

            FaceProperties faceProperties = new FaceProperties(materialId);

            Dictionary<int, Vertex> vertices = new Dictionary<int, Vertex>();
            Dictionary<int, Face> faces = new Dictionary<int, Face>();

            // Here we force 'height' to the y axis around the center.
            Vector3 startLocation = new Vector3(0, -1, 0);
            Vector3 endLocation = new Vector3(0, 1, 0);

            // This'll be useful when we want to add cylinders aligned to X or Z axes.
            Vector3 ray = endLocation - startLocation;

            Vector3 axisZ = ray.normalized;
            bool isY = (Mathf.Abs(axisZ.y) > 0.5);
            Vector3 axisX = Vector3.Cross(new Vector3(isY ? 1 : 0, !isY ? 1 : 0, 0), axisZ).normalized;
            Vector3 axisY = Vector3.Cross(axisX, axisZ).normalized;

            // Go around the cylinder and create all the vertices
            for (int i = 0; i < SLICES; i++)
            {
                float radians = (i / (float)SLICES) * 2 * Mathf.PI;

                Vector3 outt = axisX * Mathf.Cos(radians) + axisY * Mathf.Sin(radians);
                Vector3 start = startLocation + ray + outt;
                Vector3 end = startLocation + outt;

                vertices[i + START_OFFSET] = new Vertex(i + START_OFFSET, start);
                vertices[i + END_OFFSET] = new Vertex(i + END_OFFSET, end);

                if (holeRadius.HasValue)
                {
                    start = startLocation + ray + outt * holeRadius.Value;
                    end = startLocation + outt * holeRadius.Value;
                    vertices[i + INNER_START_OFFSET] = new Vertex(i + INNER_START_OFFSET, start);
                    vertices[i + INNER_END_OFFSET] = new Vertex(i + INNER_END_OFFSET, end);
                }
            }

            vertices =
              new Dictionary<int, Vertex>(Math3d.ScaleVertices(vertices.Values, scale).ToDictionary(v => v.id));

            // Go around the cylinder again and create the outside and inside faces.
            for (int i = 0; i < SLICES; i++)
            {
                List<int> faceVerts = new List<int> {
          i + START_OFFSET,
          (i + 1) % SLICES + START_OFFSET,
          (i + 1) % SLICES + END_OFFSET,
          i + END_OFFSET };

                faces[i] = new Face(i, faceVerts.AsReadOnly(), vertices, faceProperties);

                if (holeRadius.HasValue)
                {
                    faceVerts = new List<int> {
            i + INNER_START_OFFSET,
            i + INNER_END_OFFSET,
            (i + 1) % SLICES + INNER_END_OFFSET,
            (i + 1) % SLICES + INNER_START_OFFSET };

                    faces[i + SLICES] =
                      new Face(i + SLICES, faceVerts.AsReadOnly(), vertices, faceProperties);
                }
            }

            // Go around one last time and create the caps.
            List<int> startVerts = new List<int>();
            List<Vector3> startNorms = new List<Vector3>();
            List<int> startHole = new List<int>();
            List<Vector3> startHoleNorms = new List<Vector3>();
            List<int> endVerts = new List<int>();
            List<Vector3> endNorms = new List<Vector3>();
            List<int> endHole = new List<int>();
            List<Vector3> endHoleNorms = new List<Vector3>();
            for (int i = 0; i < SLICES; i++)
            {
                startVerts.Add(SLICES - i - 1 + START_OFFSET);   // Reverse-order.
                startNorms.Add(ray.normalized);
                endVerts.Add(i + END_OFFSET);
                endNorms.Add(-ray.normalized);

                if (holeRadius.HasValue)
                {
                    startHole.Add(i + INNER_START_OFFSET);
                    startHoleNorms.Add(ray.normalized);
                    endHole.Add(SLICES - i - 1 + INNER_END_OFFSET);  // Reverse-order.
                    endHoleNorms.Add(-ray.normalized);
                }
            }

            List<Hole> startHoles = new List<Hole>();
            List<Hole> endHoles = new List<Hole>();

            if (holeRadius.HasValue)
            {
                startHoles.Add(new Hole(startHole.AsReadOnly(), startHoleNorms.AsReadOnly()));
                endHoles.Add(new Hole(endHole.AsReadOnly(), endHoleNorms.AsReadOnly()));
            }

            int faceIdOff = faces.Count;
            faces[faceIdOff] = new Face(
              faceIdOff, startVerts.AsReadOnly(), vertices, faceProperties);
            faces[faceIdOff + 1] = new Face(
              faceIdOff + 1, endVerts.AsReadOnly(), vertices, faceProperties);

            return new MMesh(id, center, Quaternion.identity, vertices, faces);
        }

        /// <summary>
        ///   Create an axis-aligned icosphere.
        /// </summary>
        /// <param name="id">Id for the mesh.</param>
        /// <param name="center">Center of the sphere.</param>
        /// <param name="scale">Scale of sphere.</param>
        /// <param name="materialId">Material id for the mesh.</param>
        /// <param name="recursionLevel">How many times to recursively split triangles on the original icosphere.</param>
        /// <returns>An MMesh that renders an icosphere.</returns>
        public static MMesh AxisAlignedIcosphere(int id, Vector3 center, Vector3 scale, int materialId, int recursionLevel = 1)
        {
            // We won't go straight to Vertex or Face as we're going to subdivide points first.
            List<Vector3> vertexLocations = new List<Vector3>();
            List<List<int>> vertexIndicesForFaces = new List<List<int>>();

            // Set up outputs.
            List<Vertex> vertices = new List<Vertex>();
            List<Face> faces = new List<Face>();
            FaceProperties faceProperties = new FaceProperties(materialId);

            // Create the vertices of the icosahedron.
            foreach (Vector3 icosahedronPoint in ICOSAHEDRON_POINTS)
            {
                vertexLocations.Add(icosahedronPoint.normalized);
            }

            // Create the initial faces of the icosahedron.
            vertexIndicesForFaces.AddRange(ICOSAHEDRON_FACES);

            // Repeatedly subdivide the faces to get a smoother mesh.
            Dictionary<long, int> middlePointIndexCache = new Dictionary<long, int>();
            for (int i = 0; i < recursionLevel; i++)
            {
                List<List<int>> vertexIndicesForSubdividedFaces = new List<List<int>>();
                foreach (List<int> face in vertexIndicesForFaces)
                {
                    // Split each triangle into 4 smaller triangles.
                    int a = getMiddlePoint(face[0], face[1], ref vertexLocations, ref middlePointIndexCache, 1);
                    int b = getMiddlePoint(face[1], face[2], ref vertexLocations, ref middlePointIndexCache, 1);
                    int c = getMiddlePoint(face[2], face[0], ref vertexLocations, ref middlePointIndexCache, 1);

                    vertexIndicesForSubdividedFaces.Add(new List<int> { face[0], a, c });
                    vertexIndicesForSubdividedFaces.Add(new List<int> { face[1], b, a });
                    vertexIndicesForSubdividedFaces.Add(new List<int> { face[2], c, b });
                    vertexIndicesForSubdividedFaces.Add(new List<int> { a, b, c });
                }
                vertexIndicesForFaces = vertexIndicesForSubdividedFaces;
            }

            // Convert to our Vertex type.
            foreach (Vector3 vertexLocation in vertexLocations)
            {
                vertices.Add(new Vertex(vertices.Count, vertexLocation));
            }

            vertices = new List<Vertex>(Math3d.ScaleVertices(vertices, scale));

            Dictionary<int, Vertex> vertsById = vertices.ToDictionary(v => v.id);
            // Convert to our Face type.
            foreach (List<int> vertexIndices in vertexIndicesForFaces)
            {
                AddFace(id, vertexIndices, ref faces, vertsById, faceProperties);
            }

            return new MMesh(id, center, Quaternion.identity, vertsById, faces.ToDictionary(f => f.id));
        }

        /// <summary>
        ///   Create an axis-aligned UV sphere.
        /// </summary>
        /// <param name="numLon">The number of vertical lines on the UV sphere.</param>
        /// <param name="numLat">The number of horizontal lines on the UV sphere.</param>
        /// <param name="id">Id for the mesh.</param>
        /// <param name="center">Center of the sphere.</param>
        /// <param name="scale">Scale of sphere.</param>
        /// <param name="materialId">Material id for the mesh.</param>
        /// <returns>An MMesh that renders a UV sphere.</returns>
        public static MMesh AxisAlignedUVSphere(int numLon, int numLat, int id, Vector3 center, Vector3 scale,
          int materialId)
        {
            // Find the number of vertices that will be on the UV sphere. This is equal to the number of intersections
            // between lines of longitude and latitude plus the north and south poles.
            int numVerts = (numLon * numLat) + 2;
            List<List<int>> vertexIndicesForFaces = new List<List<int>>();
            List<Vertex> vertices = new List<Vertex>(numVerts);

            // Add the poles vertices.
            vertices.Add(new Vertex(0, Vector3.up));
            vertices.Add(new Vertex(numVerts - 1, -Vector3.up));

            // Find the position of all the other vertices.
            for (int lat = 0; lat < numLat; lat++)
            {
                float latAngle = Mathf.PI * (float)(lat + 1) / (numLat + 1);

                for (int lon = 0; lon < numLon; lon++)
                {
                    float lonAngle = 2 * Mathf.PI * (float)(lon == numLon ? 0 : lon) / numLon;
                    int vertexId = (lon + 1) + (lat * numLon);

                    vertices.Add(new Vertex(vertexId, new Vector3(
                        Mathf.Sin(latAngle) * Mathf.Cos(lonAngle),
                        Mathf.Cos(latAngle),
                        Mathf.Sin(latAngle) * Mathf.Sin(lonAngle))));
                }
            }

            // Determine the vertex indices for the faces that make up the top cap.
            for (int lon = 1; lon <= numLon; lon++)
            {
                vertexIndicesForFaces.Add(new List<int> { 0, lon == numLon ? 1 : lon + 1, lon });
            }

            // Determine the vertex indices for the faces that make up the middle of the sphere.
            for (int lat = 0; lat < numLat - 1; lat++)
            {
                for (int lon = 1; lon <= numLon; lon++)
                {
                    int current = lon + (lat * numLon);
                    int next = lon == numLon ? 1 + (lat * numLon) : current + 1;

                    vertexIndicesForFaces.Add(new List<int> { current, next, next + numLon, current + numLon });
                }
            }

            // Find the index for the south pole or final vertex.
            int final = numVerts - 1;

            // Determine the vertex indices for the faces that make up the bottom cap.
            for (int lon = 0; lon < numLon; lon++)
            {
                vertexIndicesForFaces.Add(new List<int> {
          (final - numLon) + lon,
          lon == numLon - 1 ? final - numLon : (final - numLon) + (lon + 1),
          final});
            }

            // Scale the vertices.
            vertices = new List<Vertex>(Math3d.ScaleVertices(vertices, scale));

            // Set up the properties to convert to our face type.
            List<Face> faces = new List<Face>();
            FaceProperties faceProperties = new FaceProperties(materialId);
            Dictionary<int, Vertex> vertsById = vertices.ToDictionary(v => v.id);

            // Convert to our Face type.
            foreach (List<int> vertexIndices in vertexIndicesForFaces)
            {
                AddFace(id, vertexIndices, ref faces, vertsById, faceProperties);
            }

            return new MMesh(id, center, Quaternion.identity, vertsById, faces.ToDictionary(f => f.id));
        }

        /// <summary>
        ///   Create a Face from the given constituents.
        /// </summary>
        /// <param name="vertexIds">A list of indices into 'vertices' representing the vertices of this face.</param>
        /// <param name="vertices">A master list of vertices into which 'vertexIds' will index.</param>
        /// <param name="faces">A list of faces to which the new list will be appended.</param>
        /// <param name="faceProperties">Properties for this face.</param>
        private static void AddFace(int meshId, List<int> vertexIds,
          ref List<Face> faces, Dictionary<int, Vertex> vertsById, FaceProperties faceProperties)
        {
            faces.Add(new Face(faces.Count, vertexIds.AsReadOnly(), vertsById, faceProperties));
        }

        /// <summary>
        ///   Either adds to the given list of vertices, or finds in a cache, the middle point between two given points,
        ///   and returns the index of this midpoint.
        /// </summary>
        /// <param name="p1">An index into 'vertices' representing a point on the icosphere.</param>
        /// <param name="p2">A second index into 'vertices' representing a different point on the icosphere.</param>
        /// <param name="vertices">The master list of vertices in the icosphere.</param>
        /// <param name="cache">A cache of points, for quick lookup.</param>
        /// <param name="radius">The radius of the icosphere being created.</param>
        /// <returns>An index into 'vertices' pointing to the midpoint.</returns>
        private static int getMiddlePoint(int p1, int p2, ref List<Vector3> vertices,
          ref Dictionary<long, int> cache, float radius)
        {
            // First try the cache.
            bool firstIsSmaller = p1 < p2;
            long smallerIndex = firstIsSmaller ? p1 : p2;
            long greaterIndex = firstIsSmaller ? p2 : p1;
            long key = (smallerIndex << 32) + greaterIndex;
            int ret;
            if (cache.TryGetValue(key, out ret))
            {
                return ret;
            }

            // If not found, calculate it.
            Vector3 point1 = vertices[p1];
            Vector3 point2 = vertices[p2];
            Vector3 middle = new Vector3(
                (point1.x + point2.x) / 2f,
                (point1.y + point2.y) / 2f,
                (point1.z + point2.z) / 2f);

            // Add the midpoint, ensuring it is on the sphere's circumference.
            int i = vertices.Count;
            vertices.Add(middle.normalized * radius);

            // Add it to the cache and return.
            cache.Add(key, i);
            return i;
        }

        /// <summary>
        ///   Make an axis-aligned cone.
        /// </summary>
        /// <param name="id">ID for newly created mesh.</param>
        /// <param name="center">Center of cone.</param>
        /// <param name="scale">Scale of cone.</param>
        /// <param name="materialId">Cone's material.</param>
        /// <returns>A new mesh.</returns>
        public static MMesh AxisAlignedCone(int id, Vector3 center, Vector3 scale, int materialId)
        {
            const int SLICES = 12;
            const int TOP_VERT_ID = SLICES;

            FaceProperties properties = new FaceProperties(materialId);

            Vector3 top = new Vector3(0, 1, 0);
            Vector3 bottomCenter = -top;

            Dictionary<int, Vertex> vertices = new Dictionary<int, Vertex>();
            Dictionary<int, Face> faces = new Dictionary<int, Face>();

            // Go around circle, add points.
            for (int i = 0; i < SLICES; i++)
            {
                float radians = (i / (float)SLICES) * 2 * Mathf.PI;
                vertices[i] = new Vertex(i,
                  new Vector3(Mathf.Cos(radians), -1, Mathf.Sin(radians)));
            }
            vertices[TOP_VERT_ID] = new Vertex(TOP_VERT_ID, top);

            vertices =
              new Dictionary<int, Vertex>(Math3d.ScaleVertices(vertices.Values, scale).ToDictionary(v => v.id));

            // Go around again and create faces. Each face is a triangle.
            for (int i = 0; i < SLICES; i++)
            {
                List<int> vertIds = new List<int>() { TOP_VERT_ID, (i + 1) % SLICES, i };
                faces[i] = new Face(i, vertIds.AsReadOnly(), vertices, properties);
            }

            // Go around one last time and create the bottom face.
            List<int> baseVertIds = new List<int>();
            for (int i = 0; i < SLICES; i++)
            {
                baseVertIds.Add(i);
            }
            faces[SLICES + 1] = new Face(SLICES + 1, baseVertIds.AsReadOnly(), vertices, properties);

            return new MMesh(id, center, Quaternion.identity, vertices, faces);
        }

        /// <summary>
        ///   Create an axis-aligned triangle.
        /// </summary>
        /// <param name="id">Id for newly created mesh.</param>
        /// <param name="center">Center of triangle.</param>
        /// <param name="scale">Scale of triangular pyramid.</param>
        /// <param name="materialId">Material id for mesh.</param>
        /// <returns>A new mesh.</returns>
        public static MMesh TriangularPyramid(int id, Vector3 center, Vector3 scale, int materialId)
        {
            const int topId = 0;
            const int bottomLeftId = 1;
            const int bottomRightId = 2;
            const int bottomPointId = 3;

            FaceProperties properties = new FaceProperties(materialId);

            Vector3 top = new Vector3(0, 1, 0);
            Vector3 bottomCenter = -top;

            Dictionary<int, Vertex> vertices = new Dictionary<int, Vertex>();
            Dictionary<int, Face> faces = new Dictionary<int, Face>();

            float r = 1;
            float twoPiRadOverThree = Mathf.PI / 1.5f;
            vertices[topId] = new Vertex(topId, new Vector3(0, 1, 0));
            vertices[bottomLeftId] = new Vertex(bottomLeftId,
              new Vector3(Mathf.Cos(twoPiRadOverThree) * r, -1, Mathf.Sin(twoPiRadOverThree) * r));
            vertices[bottomRightId] = new Vertex(bottomRightId,
              new Vector3(Mathf.Cos(twoPiRadOverThree * 2) * r, -1, Mathf.Sin(twoPiRadOverThree * 2) * r));
            vertices[bottomPointId] = new Vertex(bottomPointId,
              new Vector3(Mathf.Cos(0) * r, -1, Mathf.Sin(0) * r));

            vertices = new Dictionary<int, Vertex>(Math3d.ScaleVertices(vertices.Values, scale).ToDictionary(v => v.id));

            List<Vector3> dummyNormals = new List<Vector3>() { Vector3.one, Vector3.one, Vector3.one };


            List<List<int>> faceIndices = new List<List<int>>() {
        new List<int>() { 0, 2, 1 },
        new List<int>() { 0, 1, 3 },
        new List<int>() { 0, 3, 2 },
        new List<int>() { 1, 2, 3 }
      };

            for (int i = 0; i < faceIndices.Count; i++)
            {
                faces[i] = new Face(i,
                  faceIndices[i].AsReadOnly(),
                  vertices,
                  properties);
            }

            return new MMesh(id, center, Quaternion.identity, vertices, faces);
        }


        /// <summary>
        ///   Make a torus-shaped mesh.  All of the faces an ids are generated by mapping an x axis
        ///   around the torus and y axis around a slice of the torus.
        /// </summary>
        /// <param name="id">The new mesh's id.</param>
        /// <param name="center">Center of the torus.</param>
        /// <param name="scale">Scale of the torus.</param>
        /// <param name="materialId">Material for the mesh.</param>
        /// <returns>A new mesh.</returns>
        public static MMesh Torus(int id, Vector3 center, Vector3 scale, int materialId)
        {
            const int SLICES = 12;

            Vector3 up = new Vector3(0, 1, 0);

            float outerRadius = 1;
            float innerRadius = 0.5f;

            float donutRadius = (outerRadius - innerRadius) / 2f;
            float centerlineRadius = outerRadius - donutRadius;

            FaceProperties properties = new FaceProperties(materialId);

            Dictionary<int, Vertex> vertices = new Dictionary<int, Vertex>();
            Dictionary<int, Face> faces = new Dictionary<int, Face>();

            // Generate all of the surface points and normals.
            List<Vector3> normals = new List<Vector3>(SLICES * SLICES);
            for (int i = 0; i < SLICES; i++)
            {
                float outerRads = (i / (float)SLICES) * 2 * Mathf.PI;
                for (int j = 0; j < SLICES; j++)
                {
                    float innerRads = (j / (float)SLICES) * 2 * Mathf.PI;

                    Vector3 centerLinePoint = new Vector3(
                      Mathf.Cos(outerRads) * centerlineRadius, 0, Mathf.Sin(outerRads) * centerlineRadius);
                    Vector3 dir = (centerLinePoint - Vector3.zero).normalized;

                    Vector3 surfacePoint = centerLinePoint +
                      up * Mathf.Cos(innerRads) * donutRadius +
                      dir * Mathf.Sin(innerRads) * donutRadius;

                    vertices[i * SLICES + j] = new Vertex(i * SLICES + j, surfacePoint);
                    normals.Add((surfacePoint - centerLinePoint).normalized);
                }
            }

            // Scale.
            vertices = new Dictionary<int, Vertex>(Math3d.ScaleVertices(vertices.Values, scale).ToDictionary(v => v.id));

            // Now generate the faces.
            for (int i = 0; i < SLICES; i++)
            {
                for (int j = 0; j < SLICES; j++)
                {
                    int idx1 = i * SLICES + j;
                    int idx2 = ((i + 1) % SLICES) * SLICES + j;
                    int idx3 = ((i + 1) % SLICES) * SLICES + (j + 1) % SLICES;
                    int idx4 = i * SLICES + (j + 1) % SLICES;

                    faces[idx1] = new Face(idx1,
                      new List<int>() { idx1, idx2, idx3, idx4 }.AsReadOnly(),
                      vertices,
                      properties);
                }
            }

            return new MMesh(id, center, Quaternion.identity, vertices, faces);
        }
    }
}
