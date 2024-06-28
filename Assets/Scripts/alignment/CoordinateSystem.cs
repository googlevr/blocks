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
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools.utils;
using UnityEngine;

namespace com.google.apps.peltzer.client.alignment
{
    /// <summary>
    /// Defines an arbitrary coordinate system.
    /// 
    /// A coordinate system has an origin and three axes used to determine the position of points or other
    /// geometric elements in Euclidean space. Coordinate systems exist in model space so that they move and rotate with
    /// scale/world changes.
    /// </summary>
    public class CoordinateSystem
    {
        // Defining thresholds in world space accounts for a user's available precision and their perspective. When a user
        // can see less they are more likely to snap or stick but when a user is zoomed in and working in detail they need
        // to be more precise to snap or stick because they can visualize what they are working on better.

        /// <summary>
        /// The worldspace threshold at which a point will stick to an important position (origin, corner, etc).
        /// </summary>
        public const float STICK_THRESHOLD_WORLDSPACE = 0.03f;

        /// <summary>
        /// The worldspace threshold at which a point will stick to an axis.
        /// </summary>
        protected const float AXIS_STICK_THRESHOLD_WORLDSPACE = 0.04f;

        /// <summary>
        /// The default properties of a coordinate system. The defaults represent the universal coordinate system.
        /// </summary>
        protected readonly Vector3 DEFAULT_ORIGIN = Vector3.zero;
        protected readonly Quaternion DEFAULT_ROTATION = Quaternion.identity;
        protected readonly Axes DEFAULT_AXES = new Axes(Vector3.right, Vector3.up, Vector3.forward);

        /// <summary>
        /// The center or anchor of the coordinate system in model space. In the universal coordinate system this is
        /// Vector3.zero.
        /// </summary>
        protected Vector3 origin { get; private set; }

        /// <summary>
        /// The right, up and forward axes represented as perpendicular unit vectors in model space. In the universal
        /// coordinate system these are: Vector3.right, Vector3.up, Vector3.forward.
        /// </summary>
        protected Axes axes { get; private set; }

        /// <summary>
        /// The rotation of the coordinate system defined as the rotational difference between the axes and the universal
        /// coordinate system axes in model space.
        /// </summary>
        protected Quaternion rotation { get; private set; }

        /// <summary>
        /// The scale of the coordinate system. Set to one for use with Matrix transformations.
        /// </summary>
        protected readonly Vector3 scale = Vector3.one;

        /// <summary>
        /// The transform matrix for this coordinate system.
        /// </summary>
        private Matrix4x4 transformMatrix;

        protected void Setup(Vector3 origin, Quaternion rotation, Axes axes)
        {
            this.origin = origin;
            this.rotation = rotation;
            this.axes = axes;

            transformMatrix = Matrix4x4.TRS(origin, rotation, scale);
        }

        /// <summary>
        /// Snaps a position to the coordinate system and then returns the position in its original space. This allows us
        /// to snap a model space position to an arbritrary grid.
        /// </summary>
        /// <param name="position">The position to snap.</param>
        /// <param name="snappedPosition">The position snapped to the coordinate system in its original space.</param>
        public void SnapToGrid(Vector3 position, out Vector3 snappedPosition)
        {
            // Transform the position into the coordinate system space.
            Vector3 positionInCoordinatesSystemSpace = transformMatrix.MultiplyPoint3x4(position);

            // Snap the transformed position to the coordinate system grid.
            Vector3 snappedPositionInCoordinateSystemSpace = GridUtils.SnapToGrid(positionInCoordinatesSystemSpace);

            // Transform back to the positions original space.
            snappedPosition = transformMatrix.inverse.MultiplyPoint3x4(snappedPositionInCoordinateSystemSpace);
        }

        /// <summary>
        /// Snaps a position to the origin. Returns false if the origin is not within the threshold to stick.
        /// </summary>
        /// <param name="position">The position being snapped.</param>
        /// <param name="snappedPosition">The snapped position, this is either the origin or the original position.</param>
        /// <returns>Whether the position is close enough to the origin to stick.</returns>
        public bool SnapToOrigin(Vector3 position, out Vector3 snappedPosition)
        {
            if (Vector3.Distance(position, origin) < STICK_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale)
            {
                snappedPosition = origin;
                return true;
            }

            snappedPosition = position;
            return false;
        }

        /// <summary>
        /// Snaps a position to the nearest axis. Returns false if the position is not within the threshold to stick.
        /// 
        /// Will either snap the position smoothly onto the nearest axis or to the nearest grid unit from the origin if
        /// gridMode is on.
        /// </summary>
        /// <param name="position">The position to snap to an axis.</param>
        /// <param name="isGridMode">Whether the snap should be smooth or in grid increments.</param>
        /// <param name="snappedPosition">The snapped position.</param>
        /// <returns>Whether the position is close enough to an axis to stick.</returns>
        public bool SnapToAxes(Vector3 position, bool isGridMode, out Vector3 snappedPosition)
        {
            // Find the position snapped to the up axis.
            Vector3 snappedUpPosition;
            float upDelta;
            bool withinUpThreshold = SnapToAxis(Axes.Axis.UP, position, isGridMode, out snappedUpPosition, out upDelta);

            // Find the position snapped to the forward axis.
            Vector3 snappedForwardPosition;
            float forwardDelta;
            bool withinForwardThreshold = SnapToAxis(Axes.Axis.FORWARD, position, isGridMode,
              out snappedForwardPosition, out forwardDelta);

            // Find the position snapped to the right axis.
            Vector3 snappedRightPosition;
            float rightDelta;
            bool withinRightThreshold = SnapToAxis(Axes.Axis.RIGHT, position, isGridMode, out snappedRightPosition,
              out rightDelta);

            // Determine which axis is closest.
            float minDistance = Mathf.Min(upDelta, forwardDelta, rightDelta);

            if (minDistance == upDelta)
            {
                snappedPosition = snappedUpPosition;
                return withinUpThreshold;
            }
            else if (minDistance == forwardDelta)
            {
                snappedPosition = snappedForwardPosition;
                return withinForwardThreshold;
            }
            else
            {
                snappedPosition = snappedRightPosition;
                return withinRightThreshold;
            }
        }

        /// <summary>
        /// Snaps a position to a given axis. Returns false if the position is not within the threshold to snap.
        /// 
        /// Will either snap the position smoothly onto the nearest axis or to the nearest grid unit from the origin if
        /// gridMode is on.
        /// </summary>
        /// <param name="axis">The axis the position is being snapped onto.</param>
        /// <param name="position">The position being snapped.</param>
        /// <param name="isGridMode">Whether the snap should be smooth or in grid increments.</param>
        /// <param name="snappedPosition">The snapped position.</param>
        /// <param name="delta">The distance the position changes when snapped.</param>
        /// <returns>Whether the position is close enough to the axis to snap.</returns>
        public bool SnapToAxis(Axes.Axis axisName, Vector3 position, bool isGridMode, out Vector3 snappedPosition,
          out float delta)
        {

            // Grab the axis that we are snapping to.
            Vector3 axis = Vector3.zero;
            switch (axisName)
            {
                case Axes.Axis.RIGHT:
                    axis = axes.right;
                    break;
                case Axes.Axis.UP:
                    axis = axes.up;
                    break;
                case Axes.Axis.FORWARD:
                    axis = axes.forward;
                    break;
            }

            // Calculate the current model space stick threshold. We use a world space threshold so that we account for a
            // user's available precision and perspective.
            float stickThresholdModelSpace = AXIS_STICK_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale;

            // Project the position onto the axis.
            snappedPosition = isGridMode ?
              GridUtils.ProjectPointOntoLine(position, axis, origin) :
              Math3d.ProjectPointOntoLine(position, axis, origin);

            // Calculate the change.
            delta = Vector3.Distance(position, snappedPosition);

            // Return whether the change is within the allowed threshold.
            return delta < stickThresholdModelSpace;
        }
    }
}
