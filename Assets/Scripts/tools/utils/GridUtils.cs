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
using com.google.apps.peltzer.client.model.core;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils
{
    public class GridUtils
    {
        /// <summary>
        /// The size of each grid unit. This is in Unity units, where 1.0f = 1 meter by default.
        /// </summary>
        public const float GRID_SIZE = 0.01f;

        /// <summary>
        /// The size of each angle grid unit, in degrees.
        /// </summary>
        public static float ANGLE_GRID_SIZE = 45f;

        /// <summary>
        ///   The threshold for deviating from the snapped axis while center snapping.
        /// </summary>
        public static float CENTER_DEVIATION_THRESHOLD = 0.040f;

        /// <summary>
        ///   The threshold for center snapping at the start of a snap.
        /// </summary>
        public static float CENTER_SNAP_THRESHOLD = 0.075f;

        /// <summary>
        ///   The threshold for center snapping.
        /// </summary>
        public static float CENTER_THRESHOLD = 0.025f;

        /// <summary>
        ///   Finds which axis of a mesh is closest to a given vector.
        /// </summary>
        /// <param name="vector">The vector being compared to the axis.</param>
        /// <param name="meshRotation">The rotation of the mesh.</param>
        /// <returns>The nearest axis as a normalized vector.</returns>
        public static Vector3 FindNearestLocalMeshAxis(Vector3 vector, Quaternion meshRotation)
        {
            Vector3 nearestUnitVector = FindNearestAxis(Quaternion.Inverse(meshRotation) * vector);
            return meshRotation * nearestUnitVector;
        }

        /// <summary>
        ///   Finds the nearest positive universal axis that a vector is closest to.
        /// </summary>
        /// <param name="vector">The vector being used to find the nearest axis.</param>
        /// <returns>The nearest axis represented as a vector.</returns>
        public static Vector3 FindNearestAxis(Vector3 vector)
        {
            float maxDimension = Mathf.Max(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));

            if (maxDimension == Mathf.Abs(vector.x))
            {
                return Vector3.right;
            }
            else if (maxDimension == Mathf.Abs(vector.y))
            {
                return Vector3.up;
            }
            else
            {
                return Vector3.forward;
            }
        }

        /// <summary>
        ///   Snaps a given point centered at a point to the grid.
        /// </summary>
        /// <param name="center">The center of the bounds.</param>
        /// <param name="bounds">The bounds.</param>
        /// <returns>The new center of the snapped bounds.</returns>
        public static Vector3 SnapToGrid(Vector3 center, Bounds bounds)
        {
            float x = SnapPointAndDeltaToGrid(center.x, bounds.extents.x);
            float y = SnapPointAndDeltaToGrid(center.y, bounds.extents.y);
            float z = SnapPointAndDeltaToGrid(center.z, bounds.extents.z);
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Snaps a float representing one axis of a mesh's centerpoint to the grid, by pushing it
        /// along either the positive or negative value of the cardinal axis and snapping to whichever
        /// is already closest to the grid.
        /// Effectively this snaps one axis of the mesh with the minimal amount of movement.
        /// </summary>
        /// <param name="meshAxis">One axis of the mesh's centerpoint.</param>
        /// <param name="boundsExtent">The extent of the mesh's bounding box along the given axis.</param>
        /// <returns>The snapped value of meshAxis.</returns>
        private static float SnapPointAndDeltaToGrid(float meshAxis, float boundsExtent)
        {
            float moveNegativePosition = meshAxis - boundsExtent;
            float moveNegativeSnapped = SnapToGrid(moveNegativePosition);
            float movePositivePosition = meshAxis + boundsExtent;
            float movePositiveSnapped = SnapToGrid(movePositivePosition);

            if (Mathf.Abs(movePositiveSnapped - movePositivePosition) <
              Mathf.Abs(moveNegativeSnapped - moveNegativePosition))
                return movePositiveSnapped - boundsExtent;

            return moveNegativeSnapped + boundsExtent;
        }

        /// <summary>
        ///   Takes a position and projects it onto a line, rounds the resultant vector's length to grid-units,
        ///   and then returns the position moved along the vector. Optionally takes an arbitrary grid-size.
        /// </summary>
        /// <param name="point">The position to project and then snap onto the line.</param>
        /// <param name="lineVector">The vector of the line being snapped to.</param>
        /// <param name="lineOrigin">A reference point on the line being snapped to.</param>
        /// <param name="gridSize">The grid-size to be used (optional).</param>
        public static Vector3 ProjectPointOntoLine(Vector3 point, Vector3 lineVector,
          Vector3 lineOrigin, float gridSize = GRID_SIZE)
        {
            // Find the distance from the origin to the projectedToSnap position.
            float projectedDistance =
              Mathf.Cos(Vector3.Angle(point - lineOrigin, lineVector) * Mathf.Deg2Rad) * Vector3.Distance(lineOrigin, point);
            // Round this distance to grid-units.
            float snappedProjectedDistance = Mathf.Round(projectedDistance / gridSize) * gridSize;
            // Find the position that is snappedProjectedDistance from the origin.
            return lineOrigin + (lineVector.normalized * snappedProjectedDistance);
        }

        /// <summary>
        /// Snaps a given vector onto the nearest point in the grid.
        /// </summary>
        /// <param name="toSnap">The Vector3 to snap.</param>
        /// <returns></returns>
        public static Vector3 SnapToGrid(Vector3 toSnap)
        {
            return new Vector3(
              SnapToGrid(toSnap.x),
              SnapToGrid(toSnap.y),
              SnapToGrid(toSnap.z));
        }

        /// <summary>
        /// Snaps a given vector onto the nearest point in the grid.
        /// </summary>
        /// <param name="toSnap">The Vector3 to snap.</param>
        /// <returns></returns>
        public static Vector3 SnapToGrid(Vector3 toSnap, Vector3 offset)
        {
            return new Vector3(
              SnapToGrid(toSnap.x, offset.x),
              SnapToGrid(toSnap.y, offset.y),
              SnapToGrid(toSnap.z, offset.z));
        }

        /// <summary>
        /// Snaps a float representing a position on a cardinal axis to the grid. 
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static float SnapToGrid(float f)
        {
            return Mathf.Round(f / GRID_SIZE) * GRID_SIZE;
        }

        /// <summary>
        /// Snaps a float representing a position on a cardinal axis to the grid.
        /// </summary>
        /// <param name="f">The float to snap.</param>
        /// <param name="offset">The distance to move the snap to be on the offset grid.</param>
        /// <returns>The snapped and offset float.</returns>
        public static float SnapToGrid(float f, float offset)
        {
            return (Mathf.Round(f / GRID_SIZE) * GRID_SIZE) + offset;
        }

        public static Quaternion SnapToNearest(Quaternion rot, Quaternion referenceRot, float angle)
        {
            Quaternion identityRot = Quaternion.Inverse(referenceRot) * rot;
            identityRot = Quaternion.Euler(SnapAngleToGrid(identityRot.eulerAngles, angle));
            return referenceRot * identityRot;
        }

        /// <summary>
        /// Snaps Euler angles, as a Vector3, to the nearest angle on the grid.
        /// </summary>
        /// <returns>The grid-snapped rotation, as a Vector3.</returns>
        public static Vector3 SnapAngleToGrid(Vector3 r)
        {
            return new Vector3(SnapAngleToGrid(r.x), SnapAngleToGrid(r.y), SnapAngleToGrid(r.z));
        }

        /// <summary>
        /// Snaps Euler angles, as a Vector3, to the nearest angle on a given grid.
        /// </summary>
        /// <returns>The grid-snapped rotation, as a Vector3.</returns>
        public static Vector3 SnapAngleToGrid(Vector3 r, float gridSize)
        {
            return new Vector3(
              SnapAngleToGrid(r.x, gridSize),
              SnapAngleToGrid(r.y, gridSize),
              SnapAngleToGrid(r.z, gridSize));
        }

        /// <summary>
        /// Snaps a single angle, as a float, to the nearest angle on the grid.
        /// </summary>
        /// <returns>The grid-snapped angle, as a float.</returns>
        public static float SnapAngleToGrid(float f)
        {
            return SnapAngleToGrid(f, ANGLE_GRID_SIZE);
        }

        /// <summary>
        ///   Snaps a single angle, as a float, to the nearest angle on a given grid.
        /// </summary>
        /// <returns>The grid-snapped angle, as a float.</returns>
        public static float SnapAngleToGrid(float f, float gridSize)
        {
            return Mathf.Round(f / gridSize) * gridSize;
        }
    }
}
