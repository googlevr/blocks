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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;

namespace com.google.apps.peltzer.client.model.util
{
    /// <summary>
    ///   Some 3d math utilities.
    ///   Adapted from: http://wiki.unity3d.com/index.php/3d_Math_functions .
    /// </summary>
    public class Math3d
    {

        public const float MERGE_DISTANCE = 0.008f;
        public const float EPSILON = 0.0001f;

        /// <summary>
        ///   Resize a collection of vertices by using a scale vector.
        /// </summary>
        /// <param name="vertices">Original vertices.</param>
        /// <param name="scale">Scale vector.</param>
        /// <returns>An IEnumerable to enumerate through the scaled vertex collection.</returns>
        public static IEnumerable<Vertex> ScaleVertices(IEnumerable<Vertex> vertices, Vector3 scale)
        {
            return vertices.Select(v => new Vertex(v.id, Vector3.Scale(v.loc, scale)));
        }

        /// <summary>
        ///   Convert a plane defined by 3 points to a plane defined by a vector and a point.
        ///   The plane point is the middle of the triangle defined by the 3 points.
        /// </summary>
        /// <param name="planeNormal">Normal of the output plane.</param>
        /// <param name="planePoint">A point on the output plane.</param>
        /// <param name="pointA">A point on the plane.</param>
        /// <param name="pointB">A point on the plane.</param>
        /// <param name="pointC">A point on the plane.</param>
        public static void PlaneFrom3Points(out Vector3 planeNormal, out Vector3 planePoint,
          Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {

            planeNormal = Vector3.zero;
            planePoint = Vector3.zero;

            //Make two vectors from the 3 input points, originating from point A
            Vector3 AB = pointB - pointA;
            Vector3 AC = pointC - pointA;

            //Calculate the normal
            planeNormal = Vector3.Normalize(Vector3.Cross(AB, AC));

            //Get the points in the middle AB and AC
            Vector3 middleAB = pointA + (AB / 2.0f);
            Vector3 middleAC = pointA + (AC / 2.0f);

            //Get vectors from the middle of AB and AC to the point which is not on that line.
            Vector3 middleABtoC = pointC - middleAB;
            Vector3 middleACtoB = pointB - middleAC;

            //Calculate the intersection between the two lines. This will be the center
            //of the triangle defined by the 3 points.
            //We could use LineLineIntersection instead of ClosestPointsOnTwoLines but due to rounding errors
            //this sometimes doesn't work.
            Vector3 temp;
            ClosestPointsOnTwoLines(out planePoint, out temp, middleAB, middleABtoC, middleAC, middleACtoB);
        }

        /// <summary>
        ///   Two non-parallel lines which may or may not touch each other have a point on each line which are closest
        ///   to each other. This function finds those two points. If the lines are not parallel, the function
        ///   outputs true, otherwise false.
        /// </summary>
        /// <param name="closestPointLine1">Closest point on first line.</param>
        /// <param name="closestPointLine2">Closest point on second line.</param>
        /// <param name="linePoint1">Point on first line.</param>
        /// <param name="lineVec1">Direction of first line.</param>
        /// <param name="linePoint2">Point on second line.</param>
        /// <param name="lineVec2">Direction of second line.</param>
        /// <returns>True if lines are parallel.</returns>
        public static bool ClosestPointsOnTwoLines(out Vector3 closestPointLine1, out Vector3 closestPointLine2,
          Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
        {

            closestPointLine1 = Vector3.zero;
            closestPointLine2 = Vector3.zero;

            float a = Vector3.Dot(lineVec1, lineVec1);
            float b = Vector3.Dot(lineVec1, lineVec2);
            float e = Vector3.Dot(lineVec2, lineVec2);

            float d = a * e - b * b;

            //lines are not parallel
            if (d != 0.0f)
            {

                Vector3 r = linePoint1 - linePoint2;
                float c = Vector3.Dot(lineVec1, r);
                float f = Vector3.Dot(lineVec2, r);

                float s = (b * f - c * e) / d;
                float t = (a * f - c * b) / d;

                closestPointLine1 = linePoint1 + lineVec1 * s;
                closestPointLine2 = linePoint2 + lineVec2 * t;

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        ///   Returns a point which is a projection from a point to a plane.
        /// </summary>
        /// <param name="planeNormal">Plane's normal.</param>
        /// <param name="planePoint">Point on plane.</param>
        /// <param name="point">The point to project.</param>
        /// <returns>The projection.</returns>
        public static Vector3 ProjectPointOnPlane(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
        {
            //First calculate the distance from the point to the plane:
            float distance = SignedDistancePlanePoint(planeNormal, planePoint, point);

            //Reverse the sign of the distance
            distance *= -1;

            //Get a translation vector
            Vector3 translationVector = planeNormal.normalized * distance;

            //Translate the point to form a projection
            return point + translationVector;
        }

        /// <summary>
        ///   Get the shortest distance between a point and a plane. The output is signed so it holds information
        ///   as to which side of the plane normal the point is.
        /// </summary>
        /// <param name="planeNormal">Plane's normal.</param>
        /// <param name="planePoint">Point on plane.</param>
        /// <param name="point">The point.</param>
        /// <returns>The (signed) distance.</returns>
        public static float SignedDistancePlanePoint(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
        {
            return Vector3.Dot(point - planePoint, planeNormal.normalized);
        }

        /// <summary>
        ///   Check if a given polygon's vertex is a convex vertex, i.e. forms an acute
        ///   angle on the side bounding the area.
        /// </summary>
        /// <param name="check">The vertex to check.</param>
        /// <param name="prev">The previous vertex in a clockwise wind.</param>
        /// <param name="next">The next vertex in a clockwise wind.</param>
        /// <param name="faceNormal">The normal vector of the polygon.</param>
        /// <returns>True if vertex is convex, false if it's reflex.</returns>
        public static bool IsConvex(Vector3 check, Vector3 prev, Vector3 next, Vector3 faceNormal)
        {
            return Vector3.Dot(MeshMath.CalculateNormal(prev, check, next), faceNormal) > 0;
        }

        /// <summary>
        ///   Simple check for arbitrary triangle in 3D being instersected by Ray.
        ///   The triangle must be given with the vertices in clockwise order.
        /// </summary>
        /// <param name="ray">The ray to cast at the triangle.</param>
        /// <param name="maxDistance">The max distance for the ray. Minimum is assumed 0</param>
        /// <param name="a">A vertex of the triangle.</param>
        /// <param name="b">A vertex of the triangle.</param>
        /// <param name="c">A vertex of the triangle.</param>
        /// <param name="normal">The triangle's normal.</param>
        /// <returns>True if point is contained (and coplanar), false otherwise.</returns>
        public static bool RayIntersectsTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            float distance;
            if (!new Plane(a, b, c).Raycast(ray, out distance))
            {
                return false;
            }
            // This is the intersection point between the ray and the plane that contains the triangle.
            Vector3 point = ray.origin + ray.direction * distance;
            // Now we have to check if the point is in the triangle.
            // To do that, we check each segment (AB, BC, CA) and verify that the test point is on the same side of
            // the line as the remaining vertex. Since we know the triangle's normal, we can speed up this calculation
            // by doing the math directly inline instead of relying on SameSide(), etc:
            return Vector3.Dot(MeshMath.CalculateNormal(a, b, point), normal) >= 0 &&
              Vector3.Dot(MeshMath.CalculateNormal(b, c, point), normal) >= 0 &&
              Vector3.Dot(MeshMath.CalculateNormal(c, a, point), normal) >= 0;
        }

        /// <summary>
        ///   Simple check for arbitrary triangle in 3D containing an arbitrary point in 3D.
        ///   Uses barycentric coordinated to check that v >= 0, w >=0, and v + w <= 1
        /// </summary>
        /// <param name="a">A vertex of the triangle.</param>
        /// <param name="b">A vertex of the triangle.</param>
        /// <param name="c">A vertex of the triangle.</param>
        /// <param name="check">A vertex to check.</param>
        /// <returns>True if point is contained (and coplanar), false otherwise.</returns>
        public static bool TriangleContainsPoint(Vector3 a, Vector3 b, Vector3 c, Vector3 check)
        {
            Vector3 barycentricCoords = Bary(check, a, b, c);
            return (barycentricCoords.x >= 0.0f
                    && barycentricCoords.y >= 0.0f
                    && (barycentricCoords.x + barycentricCoords.y) <= 1.0f);
        }

        //Barycentric coordinate algorithm from Real Time Collision Detection
        //Barycentric coordinates paramaterize space - in this space with respect to three points of a triangle.
        //The u, v, and w coordinates allow you to calculate a point's position on a plane using the three points of
        //a triangle ABC on that plane P = uA + vB + wC, and have the property that u + v + w = 1.  Additionally, if the
        //barycentric coordinates are such that 0 <= u, v, w <= 1 it implies that the point lies within the triangle.
        //This can also be presented as v >= 0, w >= 0. and v + w <= 1.
        public static Vector3 Bary(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 v0 = b - a;
            Vector3 v1 = c - a;
            Vector3 v2 = point - a;
            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            return new Vector3(v, w, 1.0f - v - w);
        }

        /// <summary>
        ///   Check if a point is inside the border of a convex polygon (point must be coplanar with the
        /// polygon already).
        /// </summary>
        public static bool IsInside(List<Vector3> poly, Vector3 point)
        {
            // This returns incorrect results for very small edges due to floating point precision.  Multiplying by a big
            // number fixes this (or at least makes the degerate case much harder to hit)
            Vector3 p2 = point * 10000;
            Vector3 baseVertex = poly[0] * 10000;
            for (int i = 1; i < poly.Count - 1; i++)
            {
                Vector3 a = poly[i] * 10000;
                Vector3 b = poly[(i + 1)] * 10000;

                // If point is in the triangle, return early bc it is therefore also in the polygon.
                if (TriangleContainsPoint(baseVertex, a, b, p2))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///   Check if three ordered points are colinear.
        /// </summary>
        public static bool AreColinear(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b - a).normalized == (c - a).normalized;
        }

        private static bool SameSide(Vector3 a, Vector3 b, Vector3 check, Vector3 reference)
        {
            // The direction of the cross product is opposite for either side of the a-b line.
            Vector3 checkSide = MeshMath.CalculateNormal(a, b, check);
            Vector3 referenceSide = MeshMath.CalculateNormal(a, b, reference);
            // A point on the line is considered to be in both halves for the purposes of triangle
            // containing a point. However, the refence point being on the line is probably a
            // semantic error.
            return checkSide == Vector3.zero || Mathf.Abs(Vector3.Dot(checkSide, referenceSide) - 1.0f) < EPSILON;
        }

        /// <summary>
        ///   Whether one set of bounds is contained by another.
        /// </summary>
        /// <param name="outer">The presumed outer bounds.</param>
        /// <param name="inner">The presumed inner bounds.</param>
        /// <returns>Whether the inner bounds are entirely contained by the outer bounds.</returns>
        public static bool ContainsBounds(Bounds outer, Bounds inner)
        {
            return outer.Contains(inner.min) && outer.Contains(inner.max);
        }

        // Returns the centroid of a group of vectors.
        public static Vector3 FindCentroid(IEnumerable<Vector3> vectors)
        {
            Vector3 tally = Vector3.zero;
            foreach (Vector3 vec in vectors)
            {
                tally += vec;
            }
            return tally / vectors.Count();
        }

        // Returns the centroid of a list of vectors.
        public static Vector3 FindCentroid(List<Vector3> vectors)
        {
            Vector3 tally = Vector3.zero;
            for (int i = 0; i < vectors.Count; i++)
            {
                tally += vectors[i];
            }
            return tally / vectors.Count();
        }

        // Returns the centroid of a group of MMesh offsets.
        public static Vector3 FindCentroid(IEnumerable<MMesh> meshes)
        {
            Vector3 tally = Vector3.zero;
            foreach (MMesh mesh in meshes)
            {
                tally += mesh.offset;
            }
            return tally / meshes.Count();
        }

        // Returns the centroid of a list of MMesh offsets.
        public static Vector3 FindCentroid(List<MMesh> meshes)
        {
            Vector3 tally = Vector3.zero;
            for (int i = 0; i < meshes.Count; i++)
            {
                tally += meshes[i].offset;
            }
            return tally / meshes.Count();
        }

        public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            Vector3 dir = point - pivot;
            return rotation * dir + pivot;
        }

        // Find the most common rotation of a group of rotations.
        // Ties are broken deterministically by precedence in the passed collection.
        // This method could probably be smarter.
        public static Quaternion MostCommonRotation(IEnumerable<Quaternion> rotations)
        {
            Dictionary<Quaternion, int> rotationCounts = new Dictionary<Quaternion, int>();
            int highestCount = 0;
            Quaternion mostCommonRotation = Quaternion.identity;

            foreach (Quaternion rotation in rotations)
            {
                if (rotationCounts.ContainsKey(rotation))
                {
                    rotationCounts[rotation] = rotationCounts[rotation] + 1;
                }
                else
                {
                    rotationCounts.Add(rotation, 1);
                }

                if (rotationCounts[rotation] > highestCount)
                {
                    highestCount = rotationCounts[rotation];
                    mostCommonRotation = rotation;
                }
            }

            return mostCommonRotation;
        }

        /// <summary>
        ///   Given a position and a list of points finds which point is nearest to position.
        /// </summary>
        /// <param name="position">The position being compared to the points.</param>
        /// <param name="points">The list of possible nearest points.</param>
        /// <returns>The nearest point.</returns>
        public static Vector3 NearestPoint(Vector3 position, List<Vector3> points)
        {
            float nearestDistance = Mathf.Infinity;
            Vector3 nearestPoint = new Vector3();

            foreach (Vector3 point in points)
            {
                float distance = Vector3.SqrMagnitude(point - position);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point;
                }
            }

            return nearestPoint;
        }

        /// <summary>
        ///   Takes a position and projects it onto a line.
        /// </summary>
        /// <param name="toSnap">The point to project onto the line.</param>
        /// <param name="line">The line represented as a vector being projected onto.</param>
        /// <param name="origin">A reference point on the line.</param>
        /// <returns>The toSnap position projected onto the line.</returns>
        public static Vector3 ProjectPointOntoLine(Vector3 toSnap, Vector3 line, Vector3 origin)
        {
            // Find the distance from the origin to the toSnap position.
            float projectedDistance =
              Mathf.Cos(Vector3.Angle(toSnap - origin, line) * Mathf.Deg2Rad) * Vector3.Distance(origin, toSnap);

            return origin + (line.normalized * projectedDistance);
        }

        /// <summary>
        ///   Compares two vectors for equality.
        /// </summary>
        /// <param name="v1">The first vector.</param>
        /// <param name="v2">The second vector.</param>
        /// <param name="epsilon">The floating point error.</param>
        /// <returns>True if the vectors are equal.</returns>
        public static bool CompareVectors(Vector3 v1, Vector3 v2, float epsilon)
        {
            if (!(Mathf.Abs(v1.x - v2.x) < epsilon))
                return false;

            if (!(Mathf.Abs(v1.y - v2.y) < epsilon))
                return false;

            if (!(Mathf.Abs(v1.z - v2.z) < epsilon))
                return false;

            return true;
        }

        /// <summary>
        /// Test if a quaternion is valid for rotation (ie, has a magnitude of 1).
        /// </summary>
        /// <param name="testQuaternion">The quaternion to test</param>
        /// <param name="epsilon">The acceptable amount or error.</param>
        /// <returns>True if the Quaternion is a valid rotation quaternion</returns>
        public static bool QuaternionIsValidRotation(Quaternion testQuaternion, float epsilon = EPSILON)
        {
            return Mathf.Abs(testQuaternion.x * testQuaternion.x
              + testQuaternion.y * testQuaternion.y
              + testQuaternion.z * testQuaternion.z
              + testQuaternion.w * testQuaternion.w - 1.0f) < epsilon;
        }

        /// <summary>
        /// Normalizes a quaternion.
        /// </summary>
        /// <param name="q">The Quaternion to normalize/</param>
        /// <returns>The normalized Quaternion.</returns>
        public static Quaternion Normalize(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }

        public static Vector3 Normalize(Vector3 vec)
        {
            Vector3 scaledVec = 1000000f * vec;
            return scaledVec / scaledVec.magnitude;
        }

        /// <summary>
        ///   Compares two quaternions for equality.
        /// </summary>
        /// <param name="q1">The first quaternion.</param>
        /// <param name="q2">The second quaternion.</param>
        /// <param name="epsilon">The floating point error.</param>
        /// <returns>True if the quaternions are equal.</returns>
        public static bool CompareQuaternions(Quaternion q1, Quaternion q2, float epsilon)
        {
            Vector3 q1Euler = q1.eulerAngles;
            Vector3 q2Euler = q2.eulerAngles;

            if (!(Mathf.Abs(q1Euler.x - q2Euler.x) < epsilon))
                return false;

            if (!(Mathf.Abs(q1Euler.y - q2Euler.y) < epsilon))
                return false;

            if (!(Mathf.Abs(q1Euler.z - q2Euler.z) < epsilon))
                return false;

            return true;
        }

        /// <summary>
        /// Returns a given value on a cubic bezier curve defined by A, B, C and D.
        ///
        /// WARNING: There seems to be unknown constraints for which this function doesn't work. It does work for the
        /// current use cases.
        /// </summary>
        public static float CubicBezierEasing(float A, float B, float C, float D, float t)
        {
            return A + 3.0f * t * (B - A) + 3.0f * t * t * (C - 2.0f * B + A) + t * t * t * (D - 3.0f * C + 3.0f * B - A);
        }
    }
}
