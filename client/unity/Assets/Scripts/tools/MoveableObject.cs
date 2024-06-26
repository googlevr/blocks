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
using UnityEngine;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   Something outside of the actual model which can be moved with the grab tool. 
  ///   This script expects to be attached to a GameObject representing the grabbable object, and to have a 
  ///   Unity Mesh (as opposed to a Blocks MMesh) representing the object.
  ///   Supports:
  ///   - Hover behaviour
  ///   - Grabbing
  ///   - Releasing
  ///   - Deleting
  ///   - Throwing away
  ///   - Basic grid alignment
  ///   - Scaling
  /// </summary>
  public class MoveableObject : MonoBehaviour {
    /// <summary>
    /// If released with this or more lateral velocity, the object will be thrown away.
    /// </summary>
    private const float THROWING_VELOCITY_THRESHOLD = 2.5f;

    // How close to the quad the user must be to grab the object.
    // In Unity units where 1.0 = 1m.
    public const float HOVER_DISTANCE = 0.05f;

    // The Unity Mesh of the grabbable object, for render and collision detection.
    internal Mesh mesh;
    // The Unity Material of the grabbable object, for render.
    internal Material material;
    // The normal vector of the mesh, in model space.
    private Vector3 meshNormalModelSpace;
    // The position of the mesh vertices in model space.
    private List<Vector3> meshVerticesModelSpace;

    // The position, rotation and scale of the object in model space.
    internal Vector3 positionModelSpace = Vector3.zero;
    internal Quaternion rotationModelSpace = Quaternion.identity;
    internal Vector3 scaleModelSpace = Vector3.one;

    // The position, rotation and scale of the object in model space when it was most-recently grabbed.
    internal Vector3 positionAtStartOfMove;
    internal Quaternion rotationAtStartOfMove;
    internal Vector3 scaleAtStartOfMove;
    // The rotation of the controller in model space when the grab movement began.
    internal Quaternion controllerRotationAtStartOfMove;

    // A basic shatter effect.
    private ParticleSystem shatterPrefab;
    // A basic highlight for a hover effect.
    static Color BLUE_HIGHLIGHT = new Color(1f, 1f, 1.5f);

    private bool thrownAway = false;
    internal bool grabbed = false;
    private bool isSnapping = false;
    private bool hovered = false;

    /// <summary>
    ///   Sets up the object, allowing it to be moved. Must be called for every object.
    ///   We do work here rather than in a constructor as this is a MonoBehavior
    /// </summary>
    public virtual void Setup() {
      PeltzerMain.Instance.controllerMain.ControllerActionHandler += MoveDetector;
      if (shatterPrefab == null) {
        shatterPrefab = Resources.Load<ParticleSystem>("Prefabs/Shatter");
      }
    }

    /// <summary>
    ///   A basic, overridable shatter effect.
    /// </summary>
    internal virtual void Shatter() {
      // Play the shatter effect and noise.
      ParticleSystem shatterEffect = Instantiate(shatterPrefab);
      shatterEffect.transform.position = gameObject.transform.position;
      shatterEffect.startSize = gameObject.transform.localScale.magnitude * 0.3f;
      PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.breakSound, /* pitch */ 0.85f);

      // Delete the object.
      Delete();
    }

    /// <summary>
    ///   A basic, overridable hover effect.
    /// </summary>
    internal virtual void SetHovered() {
      material.color = BLUE_HIGHLIGHT;
    }

    /// <summary>
    ///   The reverse of the above hover effect.
    /// </summary>
    internal virtual void SetUnhovered() {
      material.color = Color.white;
    }

    /// <summary>
    ///   A basic, overridable deletion behavior.
    /// </summary>
    internal virtual void Delete() {
      PeltzerMain.Instance.controllerMain.ControllerActionHandler -= MoveDetector;
    }

    /// <summary>
    ///   Scale behavior, to be implemented by the specific object.
    /// </summary>
    /// <param name="scaleUp">True if scaling up, false if scaling down.</param>
    internal virtual void Scale(bool scaleUp) {
      // Do nothing by default.
    }

    /// <summary>
    ///   A basic, overridable grab behaviour that attaches the moveable object to the controller.
    /// </summary>
    internal virtual void Grab() {
      // Store the values when grab began, to allow us to calculate deltas later.
      positionAtStartOfMove = positionModelSpace;
      rotationAtStartOfMove = rotationModelSpace;
      controllerRotationAtStartOfMove = PeltzerMain.Instance.peltzerController.LastRotationModel;
      scaleAtStartOfMove = scaleModelSpace;

      UpdatePosition();

      transform.SetParent(PeltzerMain.Instance.peltzerController.transform);
      grabbed = true;

      // Disable the menu and palette while something is grabbed.
      PeltzerMain.Instance.restrictionManager.paletteAllowed = false;
      PeltzerMain.Instance.restrictionManager.menuActionsAllowed = false;
    }

    /// <summary>
    ///   A basic, overridable release behaviour that detaches the moveable object from the controller.
    /// </summary>
    internal virtual void Release() {
      transform.SetParent(null);
      grabbed = false;

      // Enable the menu and palette when something is no longer grabbed.
      PeltzerMain.Instance.restrictionManager.paletteAllowed = true;
      PeltzerMain.Instance.restrictionManager.menuActionsAllowed = true;
    }

    /// <summary>
    ///   A basic, overridable behavior for throwing away the object.
    /// </summary>
    /// <param name="velocity">The velocity of the controller throwing the object.</param>
    internal virtual void ThrowAway(Vector3 velocity) {
      // Set the object free.
      transform.SetParent(null);
      grabbed = false;
      thrownAway = true;

      // Apply the force.
      Rigidbody rigidbody = gameObject.GetComponent<Rigidbody>();
      if (rigidbody == null) {
        rigidbody = gameObject.AddComponent<Rigidbody>();
      }

      rigidbody.isKinematic = false;
      rigidbody.AddForce(velocity, ForceMode.VelocityChange);
    }

    /// <summary>
    ///   A basic, overridable behaviour to destroy this object.
    /// </summary>
    internal void Destroy() {
      PeltzerMain.Instance.controllerMain.ControllerActionHandler -= MoveDetector;
      GameObject.Destroy(gameObject);
    }

    /// <summary>
    ///   Update's the object's position and rotation, aligning to the grid if needed.
    /// </summary>
    internal void UpdatePosition() {
      WorldSpace worldSpace = PeltzerMain.Instance.worldSpace;

      // Calculate the new position/rotation in model space.
      positionModelSpace = worldSpace.WorldToModel(transform.position);
      rotationModelSpace = worldSpace.WorldOrientationToModel(transform.rotation);

      // The snap it to the grid and put it back into world-space if needed.
      if (PeltzerMain.Instance.peltzerController.isBlockMode || isSnapping) {
        positionModelSpace = GridUtils.SnapToGrid(positionModelSpace);
        transform.position = PeltzerMain.Instance.worldSpace.ModelToWorld(positionModelSpace);
        Quaternion rotDelta = Quaternion.Inverse(controllerRotationAtStartOfMove)
          * PeltzerMain.Instance.peltzerController.LastRotationModel;
        rotationModelSpace = rotationAtStartOfMove * GridUtils.SnapToNearest(rotDelta, Quaternion.identity, 90f);
        transform.rotation = PeltzerMain.Instance.worldSpace.ModelOrientationToWorld(rotationModelSpace);
      }
    }

    /// <summary>
    ///   Recalculates the vertices and normal of the underlying mesh, to account for changes in position, 
    ///   rotation and scale.
    /// </summary>
    internal void RecalculateVerticesAndNormal() {
      Matrix4x4 mat = Matrix4x4.TRS(positionModelSpace, rotationModelSpace, scaleModelSpace);
      meshVerticesModelSpace = new List<Vector3>(mesh.vertexCount);
      foreach (Vector3 pos in mesh.vertices) {
        meshVerticesModelSpace.Add(mat.MultiplyPoint(pos));
      }
      meshNormalModelSpace = MeshMath.CalculateNormal(meshVerticesModelSpace);
    }

    /// <summary>
    ///   Set whether the object is currently being hovered by the controller.
    /// </summary>
    private void SetHoverStatus() {
      PeltzerController controller = PeltzerMain.Instance.peltzerController;

      if ((controller.mode == ControllerMode.move || controller.mode == ControllerMode.delete)
          && MeshMath.IsCloseToFaceInterior(controller.LastPositionModel,
          meshNormalModelSpace, meshVerticesModelSpace, HOVER_DISTANCE, /* vertexDistanceThreshold */ 0)) {
        if (!hovered) {
          hovered = true;
          SetHovered();
        }
      } else if (hovered) {
        hovered = false;
        SetUnhovered();
      }
    }

    /// <summary>
    /// Returns whether or not the user just did a "throw" gesture with the controller when releasing.
    /// </summary>
    /// <returns>True if and only if the user just did a throw gesture.</returns>
    private bool IsThrowing() {
      return PeltzerMain.Instance.peltzerController.GetVelocity().magnitude > THROWING_VELOCITY_THRESHOLD;
    }

    /// <summary>
    ///   Offers basic interaction with the moveable object: grabbing, releasing, throwing, scaling and deleting.
    /// </summary>
    private void MoveDetector(object sender, ControllerEventArgs args) {
      PeltzerController controller = PeltzerMain.Instance.peltzerController;

      if (args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN) {
        isSnapping = true;
      } else if (args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP) {
        isSnapping = false;
      } else if (controller.mode == ControllerMode.move
        && args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN
        && hovered) {
        Grab();
      } else if (controller.mode == ControllerMode.delete
        && args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN
        && hovered) {
        Delete();
      } else if (controller.mode == ControllerMode.move
        && args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP
        && grabbed) {
        if (IsThrowing()) {
          ThrowAway(controller.GetVelocity());
        } else {
          Release();
        }
      } else if (controller.mode == ControllerMode.move
        && args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && grabbed || hovered) {
        if (args.TouchpadLocation == TouchpadLocation.TOP) {
          Scale(/* scaleUp */ true);
        } else if (args.TouchpadLocation == TouchpadLocation.BOTTOM) {
          Scale(/* scaleUp */ false);
        }
      }
    }

    /// <summary>
    /// Updates the objects transform values in order to keep them consistent with any transformations
    /// applied to the world.
    /// </summary>
    void UpdateTransform() {
      transform.position = PeltzerMain.Instance.worldSpace.ModelToWorld(positionModelSpace);
      transform.rotation = PeltzerMain.Instance.worldSpace.ModelOrientationToWorld(rotationModelSpace);
      transform.localScale = PeltzerMain.Instance.worldSpace.scale * scaleModelSpace;
    }

    void Update() {
      if (thrownAway) {
        if (transform.position.y <= 0.125f) {
          // If the object was thrown and has hit the ground, shatter it.
          Shatter();
        } else {
          // Else, keep updating its position for render.
          UpdatePosition();
        }
        return;
      }

      if (grabbed) {
        // If this object is being grabbed, its position will update by virtue of it being a child of the controller.
        // We do, however, need to update its position and orientation in model space for future operations.
        UpdatePosition();
      } else {
        // If this object is not grabbed, check to see if it is hovered.
        SetHoverStatus();

        // Update the object's transforms in case of any world movement while the object is not grabbed.
        UpdateTransform();
      }
    }

    void LateUpdate() {
      // Render the object in the world. To do that, we transform our model space position/rotation/scale to
      // world space to get an appropriate matrix.
      Matrix4x4 mat = PeltzerMain.Instance.worldSpace.modelToWorld *
        Matrix4x4.TRS(positionModelSpace, rotationModelSpace, scaleModelSpace);
      // Draw in the PolyAssets layer -- won't show up in thumbnails.
      Graphics.DrawMesh(mesh, mat, material, MeshWithMaterialRenderer.DEFAULT_LAYER);
    }
  }
}
