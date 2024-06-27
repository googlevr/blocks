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
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;
using UnityEngine;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.alignment {
  /// <summary>
  ///   A MeshSnapSpace is a coordinate system that an MMesh can be orientated in and snapped to.
  ///   
  ///   A MeshSnapSpace is conceptually the same as the universal coordinate system but with an arbitrary rotation and
  ///   origin. A MeshSnapSpace is used to 'snap to a target mesh' so its properties are defined by the target mesh.
  ///   Its rotation is the mesh's rotation, its origin is the mesh's offset and its axes are the mesh's local right,
  ///   up and forward vectors. 
  ///   
  ///   When an MMesh is snapped to a MeshSnapSpace its rotation is snapped to the nearest 90 degrees of the space's
  ///   rotation and if GridMode is on the MMesh is moved so its bounding box lines up with the MeshSnapGrid. The
  ///   center of the mesh being snapped will also automatically stick to the origin and axes.
  /// </summary>
  public class MeshSnapSpace : SnapSpace {
    private SnapType snapType = SnapType.MESH;
    public Vector3 sourceMeshCenter;
    public Vector3 snappedPosition;
    public Vector3 unsnappedPosition;

    // The id of the mesh that defines this MeshSnapSpace.
    public int targetMeshId { get; private set; }
    private ContinuousAxisStickEffect continuousAxisStickEffect;
    private ContinuousPointStickEffect continuousSourcePointStickEffect;
    private ContinuousPointStickEffect continuousTargetPointStickEffect;
    private bool isAxisSticking;

    public MeshSnapSpace(int targetMeshId) {
      this.targetMeshId = targetMeshId;
      continuousAxisStickEffect = new ContinuousAxisStickEffect();
      continuousSourcePointStickEffect = new ContinuousPointStickEffect();
      continuousTargetPointStickEffect = new ContinuousPointStickEffect();
    }

    /// <summary>
    /// Calculates the origin, rotation and axes of the MeshSnapSpace.
    /// </summary>
    public override void Execute() {
      MMesh targetMesh = PeltzerMain.Instance.model.GetMesh(targetMeshId);

      Axes axes = new Axes(
        targetMesh.rotation * Vector3.right,
        targetMesh.rotation * Vector3.up,
        targetMesh.rotation * Vector3.forward);

      Setup(targetMesh.bounds.center, targetMesh.rotation, axes);

      // We always show the offsets for all mesh stick effects.
      UXEffectManager.GetEffectManager().StartEffect(continuousSourcePointStickEffect);
      UXEffectManager.GetEffectManager().StartEffect(continuousTargetPointStickEffect);
      // The target mesh offset doesn't change throughout a mesh snap.
      continuousTargetPointStickEffect.UpdateFromPoint(origin);
    }

    /// <summary>
    /// Checks if the targetMeshId still exists in the model. If it does the snap is still valid.
    /// </summary>
    /// <returns>Whether the targetMeshId exists still.</returns>
    public override bool IsValid() {
      return PeltzerMain.Instance.model.HasMesh(targetMeshId);
    }

    /// <summary>
    /// Translates a transform into the SnapSpace.
    /// 
    /// A position is snapped to a MeshSnapSpace by:
    ///   1) Trying to stick to the origin (all three axes at once). 
    ///   2) Trying to stick the position to a nearby axis.
    ///   3) Snapping the position to a grid defined by the coordinate system if grid mode is on.
    /// If neither of those conditions are met the position doesn't change.
    /// 
    /// A rotation is snapped to a MeshSnapSpace by snapping the rotation to the nearest 90 degrees of the
    /// MeshSnapSpace rotation.
    /// </summary>
    /// <param name="position">The position of the mesh being snapped.</param>
    /// <param name="rotation">The rotation of the mesh being snapped.</param>
    /// <returns>The snapped position and rotation as a SnapTransform.</returns>
    public override SnapTransform Snap(Vector3 position, Quaternion rotation) {
      unsnappedPosition = position;
      // Try to snap to the origin.
      if (SnapToOrigin(position, out snappedPosition)) {
        if (isAxisSticking) {
          isAxisSticking = false;
          UXEffectManager.GetEffectManager().EndEffect(continuousAxisStickEffect);
        }
      } else {
        if (SnapToAxes(position, PeltzerMain.Instance.peltzerController.isBlockMode, out snappedPosition)) {
          if (!isAxisSticking) {
            isAxisSticking = true;
            UXEffectManager.GetEffectManager().StartEffect(continuousAxisStickEffect);
          }
          continuousAxisStickEffect.UpdateFromAxis(origin, snappedPosition);
        } else {
          if (isAxisSticking) {
            isAxisSticking = false;
            UXEffectManager.GetEffectManager().EndEffect(continuousAxisStickEffect);
          }
          // If we didn't snap to an axes, snap to the grid if grid mode is on or don't do anything.
          if (PeltzerMain.Instance.peltzerController.isBlockMode) {
            SnapToGrid(position, out snappedPosition);
          } else {
            snappedPosition = position;
          }
        }

        continuousSourcePointStickEffect.UpdateFromPoint(snappedPosition);
      }

      // Snap the rotation to the nearest 90 degrees of the MeshSnapSpace's rotation.
      Quaternion snappedRotation = GridUtils.SnapToNearest(rotation, this.rotation, 90f);
      return new SnapTransform(snappedPosition, snappedRotation);
    }

    /// <summary>
    ///   Handles stopping snap logic maintained by the MeshSnapSpace.
    /// </summary>
    public override void StopSnap() {
      if (isAxisSticking) {
        UXEffectManager.GetEffectManager().EndEffect(continuousAxisStickEffect);
      }

      UXEffectManager.GetEffectManager().EndEffect(continuousSourcePointStickEffect);
      UXEffectManager.GetEffectManager().EndEffect(continuousTargetPointStickEffect);
    }

    /// <summary>
    /// Checks if another SnapSpace is equivalent to this space. MeshSnapSpaces are equivalent if they have the same
    /// targetMeshId.
    /// </summary>
    /// <param name="otherSpace">The other SnapSpace.</param>
    /// <returns>Whether they are equal.</returns>
    public override bool Equals(SnapSpace otherSpace) {
      if (otherSpace == null || otherSpace.SnapType != snapType) {
        return false;
      }

      MeshSnapSpace otherFaceSnapSpace = (MeshSnapSpace)otherSpace;
      return targetMeshId == otherFaceSnapSpace.targetMeshId;
    }

    public override SnapType SnapType { get { return snapType; } }
  }
}
