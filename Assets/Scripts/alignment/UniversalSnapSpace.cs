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

using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;
using UnityEngine;

namespace com.google.apps.peltzer.client.alignment {
  /// <summary>
  ///   A UniversalSnapSpace is a coordinate system that an MMesh can be orientated in and snapped to.
  ///   
  ///   A UniversalSnapSpace shares the same characteristics as the universal coordinate system. It has identity
  ///   rotation, its origin is { 0, 0, 0 } and its axes are the unit vectors.
  ///   
  ///   When an MMesh is snapped to a
  ///   UniversalSnapSpace its rotation is snapped to the nearest 90 degrees of Quaternion.Identity and if GridMode is
  ///   on the MMesh is moved so its bounding box lines up with the grid.
  /// </summary>
  public class UniversalSnapSpace : SnapSpace {
    private const SnapType snapType = SnapType.UNIVERSAL;
    // The bounds of the mesh being snapped to this UniversalSnapSpace. When we snap we want to snap the bounds of the
    // mesh to the universal grid so that Universal Snapping mimics GridMode.
    // TODO (bug): Remove the above comment once gridmode is refactored to use universal snapping.
    private Bounds sourceMeshBounds;

    public UniversalSnapSpace(Bounds bounds) {
      sourceMeshBounds = bounds;
    }

    /// <summary>
    /// Sets the origin, rotation and axes of the UniversalSnapSpace to the coordinate system defaults.
    /// </summary>
    public override void Execute() {
      Setup(DEFAULT_ORIGIN, DEFAULT_ROTATION, DEFAULT_AXES);
    }

    public override bool IsValid() {
      // A universal snap is always valid.
      return true;
    }

    /// <summary>
    /// Translates a transform into the SnapSpace.
    /// 
    /// A position is snapped to a UniversalSnapSpace by snapping to a grid defined by the coordinate system if grid
    /// mode is on.
    /// 
    /// A rotation is snapped to a UniversalSnapSpace by snapping the rotation to the nearest 90 degrees of the
    /// UniversalSnapSpace rotation.
    /// </summary>
    /// <param name="position">The position of the mesh being snapped.</param>
    /// <param name="rotation">The rotation of the mesh being snapped.</param>
    /// <returns>The snapped position and rotation as a SnapTransform.</returns>
    public override SnapTransform Snap(Vector3 position, Quaternion rotation) {
      // If grid mode is on snap the bounding box to the grid.
      Vector3 snappedPosition = PeltzerMain.Instance.peltzerController.isBlockMode ?
        GridUtils.SnapToGrid(position, sourceMeshBounds) :
        position;

      // Snap the rotation to the nearest 90 degrees of the universal coordinate system.
      Quaternion snappedRotation = GridUtils.SnapToNearest(rotation, this.rotation, 90f);
      return new SnapTransform(snappedPosition, snappedRotation);
    }

    /// <summary>
    ///   Handles stopping snap logic maintained by the UniversalSnapSpace.
    /// </summary>
    public override void StopSnap() {
      // Does nothing.
    }

    /// <summary>
    /// Checks if another SnapSpace is equivalent to this space. The UniversalSnapSpace has no unique properties so all
    /// UniversalSnapSpaces are equivalent to each other.
    /// </summary>
    /// <param name="otherSpace">The other SnapSpace.</param>
    /// <returns>Whether they are equal.</returns>
    public override bool Equals(SnapSpace otherSpace) {
      return otherSpace != null && otherSpace.SnapType == snapType;
    }

    public override SnapType SnapType { get { return snapType; } }
  }
}
