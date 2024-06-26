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

using com.google.apps.peltzer.client.analytics;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.csg;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   A tool responsible for adding meshes to the scene.
  ///   An insert is composed of previewing, (potentially) scaling and/or moving, and triggering.
  /// </summary>
  public class VolumeInserter : MonoBehaviour {
    // The default scale delta.
    private static readonly int DEFAULT_SCALE_DELTA = 2;
    // The default radius of primitives added to the scene. Twice the grid size.
    private static readonly float DEFAULT_PRIMITIVE_SCALE = (GridUtils.GRID_SIZE / 2.0f);
    // The position offset between the controller and the position of the preview;
    public static readonly Vector3 PREVIEW_OFFSET = new Vector3(0f, 0f, 0.25f);
    // How long to wait between scaling events.
    private static readonly float MIN_TIME_BETWEEN_SCALING = 0.2f;
    // The lowest scaleDelta can go, for primitives. This implies a cube of size 2-grid-units.
    private static readonly int MIN_SCALE_DELTA = -4;
    // The highest scaleDelta can go, for primitives. This implies that four (and a bit) such cubes could fit 
    // side-by-side within bounds.
    private static readonly int MAX_SCALE_DELTA = 220;
    // How long after an insert should it take for the preview mesh to once again show at full opacity.
    private static readonly float PREVIEW_RESHOW_DURATION = 1.0f;
    /// <summary>
    /// The number of seconds the user scales an object continuously before we start increasing the rate of the
    /// scaling process.
    /// </summary>
    private const float FAST_SCALE_THRESHOLD = 1f;
    /// <summary>
    ///   If user is scaling for a what we consider a long time, we will increase the scaling rate by this amount.
    /// </summary>
    private const int LONG_TERM_SCALE_FACTOR = 1;

    // Other tools.
    private ControllerMain controllerMain;
    private PeltzerController peltzerController;
    private Model model;
    private SpatialIndex spatialIndex;
    private AudioLibrary audioLibrary;
    private WorldSpace worldSpace;
    private MeshRepresentationCache meshRepresentationCache;
    private float lastInsertTime = 0.0f;

    // We use the selector in copy mode to allow the user to select a set of meshes to use as a primitive.
    private Selector selector;

    // Insertion.
    // The mesh(es) currently being held which will be inserted on a trigger click.
    private HeldMeshes heldMeshes;

    // Scaling.
    // The delta from the default scale of the current preview.
    public int scaleDelta { get; private set; }
    // Whether the user is currently holding the scale button.
    private ScaleType scaleType = ScaleType.NONE;
    // The number of scale events received without the touchpad being released.
    private int continuousScaleEvents = 0;
    /// <summary>
    // The benchmark we set to determine when the user has been scaling for a long time.
    /// </summary>
    private float longTermScaleStartTime = float.MaxValue;
    // Keep track of changes to shape-menu-show-state for nice lerping.
    private bool wasShowingShapesMenuLastFrame;
    // Keep track of whether startSnap was called while the shapes menu was up; if so, start snapping
    // as soon as the new mesh is begun.
    private bool snapStartedWhileShapesMenuUp;
    /// <summary>
    /// Used to determine if we should show the snap tooltip or not. Don't show the tooltip if the user already
    /// showed enough knowledge of how to snap.
    /// </summary>
    private int completedSnaps = 0;
    private const int SNAP_KNOW_HOW_COUNT = 3;

    /// <summary>
    ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
    ///   As such, this setup method must be called before the tool is used for it to have a valid state.
    /// </summary>
    public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController,
        AudioLibrary audioLibrary, WorldSpace worldSpace, SpatialIndex spatialIndex,
        MeshRepresentationCache meshRepresentationCache, Selector selector) {
      this.model = model;
      this.controllerMain = controllerMain;
      this.peltzerController = peltzerController;
      this.audioLibrary = audioLibrary;
      this.worldSpace = worldSpace;
      this.spatialIndex = spatialIndex;
      this.meshRepresentationCache = meshRepresentationCache;
      this.selector = selector;
      this.lastInsertTime = -10.0f;
      controllerMain.ControllerActionHandler += ControllerEventHandler;
      peltzerController.MaterialChangedHandler += MaterialChangeHandler;
      peltzerController.shapesMenu.ShapeMenuItemChangedHandler += ShapeChangedHandler;
      peltzerController.ModeChangedHandler += ModeChangeEventHandler;
      peltzerController.BlockModeChangedHandler += BlockModeChangedHandler;

      scaleDelta = DEFAULT_SCALE_DELTA;

      // Attach the preview mesh to the preview GameObject.
      CreateNewVolumeMesh();
    }

    /// <summary>
    ///   Updates the location of the preview.
    /// </summary>
    private void Update() {
      if (!PeltzerController.AcquireIfNecessary(ref peltzerController)) {
        return;
      }

      // If we are in "copy mode", use the selector to allow the user to hover/select meshes to copy.
      // (we have to check if peltzerController.shapesMenu is null because VolumeInserter might be
      // created before PeltzerController setup is done).
      if (peltzerController.shapesMenu != null &&
          peltzerController.shapesMenu.CurrentItemId == ShapesMenu.COPY_MODE_ID) {
        selector.SelectMeshAtPosition(peltzerController.LastPositionModel, Selector.MESHES_ONLY);
      }

      bool activeMode = (peltzerController.mode == ControllerMode.insertVolume
        || peltzerController.mode == ControllerMode.subtract) 
        && !PeltzerMain.Instance.peltzerController.isPointingAtMenu
        && PeltzerMain.Instance.introChoreographer.introIsComplete;

      if (heldMeshes != null) {
        if (activeMode) {
          heldMeshes.UpdatePositions();
          heldMeshes.Unhide();
        } else {
          heldMeshes.Hide();
        }
      }

      // If we changed from showing the shapes menu to not, scale up from the menu-size to the desired size.
      bool showingShapesMenuThisFrame = peltzerController.shapesMenu.showingShapeMenu;
      if (wasShowingShapesMenuLastFrame && !showingShapesMenuThisFrame) {
        CreateNewVolumeMesh(/* oldScaleToAnimateFrom */ 0);
      }

      wasShowingShapesMenuLastFrame = showingShapesMenuThisFrame;

      //process held mesh fadeout
      float timeSinceLastInsert = Time.time - this.lastInsertTime;
      if (timeSinceLastInsert < PREVIEW_RESHOW_DURATION + 1f) {
        float pctAnim = Mathf.Min(1f, timeSinceLastInsert / PREVIEW_RESHOW_DURATION);
        float curvedAlpha = 0.1f + 0.9f * (pctAnim * pctAnim * pctAnim);
        for (int i = 0; i < heldMeshes.heldMeshes.Count; i++) {
          MeshWithMaterialRenderer renderer = 
            heldMeshes.heldMeshes[i].Preview.GetComponent<MeshWithMaterialRenderer>();
          renderer.fade = 0.3f * curvedAlpha;
        }
      }
    }

    /// <summary>
    ///   Update the positions of the held meshes immediately.
    /// </summary>
    public void UpdateHeldMeshesPositions() {
      heldMeshes.UpdatePositions();
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'start inserting' event. Or, in copy mode,
    ///   it's the "copy" event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is the start of an insert event, false otherwise.</returns>
    private static bool IsStartInsertVolumeOrCopyEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN;
    }

    /// <summary>
    ///   Whether this matches the pattern of an 'end inserting' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is the end of an insert event, false otherwise.</returns>
    private static bool IsEndInsertVolumeEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP;
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'scale' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a scale event, false otherwise.</returns>
    private bool IsScaleEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && (args.TouchpadLocation == TouchpadLocation.BOTTOM || args.TouchpadLocation == TouchpadLocation.TOP)
        && args.TouchpadOverlay != TouchpadOverlay.RESET_ZOOM;
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'stop scaling' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a scale event, false otherwise.</returns>
    private bool IsStopScalingEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.UP
        && scaleType != ScaleType.NONE;
    }

    /// <summary>
    ///   Whether this matches a start snapping event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if the grip is down.</returns>
    private static bool IsStartSnapEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN;
    }

    private static bool IsStartSnapDetectionEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.LIGHT_DOWN;
    }

    private static bool IsStopSnapDetectionEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.LIGHT_UP;
    }

    /// <summary>
    ///   Whether this matches an end snapping event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if the grip is up.</returns>
    private static bool IsEndSnapEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP;
    }

    public bool IsChangeShapeEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && (heldMeshes == null || (!heldMeshes.IsFilling && !heldMeshes.IsInserting))
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && (args.TouchpadLocation == TouchpadLocation.LEFT || args.TouchpadLocation == TouchpadLocation.RIGHT)
        && args.TouchpadOverlay != TouchpadOverlay.RESET_ZOOM;
    }

    /// <summary>
    ///   Whether this event corresponds to a 'toggle mode' pattern.
    /// </summary>
    private bool IsSwitchModeEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.ApplicationMenu
        && args.Action == ButtonAction.DOWN
        && Features.csgSubtractEnabled;
    }

    // Touchpad Hover Tests
    private bool IsSetUpHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && (heldMeshes == null || (!heldMeshes.IsFilling && !heldMeshes.IsInserting))
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.TOP;
    }

    private bool IsSetDownHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && (heldMeshes == null || (!heldMeshes.IsFilling && !heldMeshes.IsInserting))
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.BOTTOM;
    }

    private bool IsSetLeftHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && (heldMeshes == null || (!heldMeshes.IsFilling && !heldMeshes.IsInserting))
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.LEFT;
    }

    private bool IsSetRightHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && (heldMeshes == null || (!heldMeshes.IsFilling && !heldMeshes.IsInserting))
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.RIGHT;
    }

    private static bool IsUnsetAllHoverTooltipsEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.NONE;
    }

    public float GetScaleForScaleDelta(int delta) {
      return (delta + 6) * DEFAULT_PRIMITIVE_SCALE;
    }

    /// <summary>
    ///   Creates a new mesh given the tool's current parameters and attaches it to the preview Game Object.
    /// </summary>
    /// <param name="oldScaleDeltaToAnimateFrom">If not null, this indicates the previous scaleDelta from
    /// which to animate from. Note: this is only honored when there is a single held mesh.</param>
    private void CreateNewVolumeMesh(int? oldScaleDeltaToAnimateFrom = null) {
      Vector3? oldPosToAnimateFrom = null;
      Vector3? oldScaleToAnimateFrom = null;
      if (oldScaleDeltaToAnimateFrom != null && heldMeshes != null && heldMeshes.heldMeshes.Count == 1) {
        // Get the initial pos/scale. We will use these at the end of the method to set up the animation.
        MeshWithMaterialRenderer renderer = 
          heldMeshes.heldMeshes[0].Preview.GetComponent<MeshWithMaterialRenderer>();
        oldPosToAnimateFrom = renderer.GetPositionInModelSpace();
        // Adjust the scale by the current animated scale, if necessary (so that the animation
        // doesn't jump if we rescale the mesh mid-animation).
        Vector3 oldScaleAnimFactor = renderer.GetCurrentAnimatedScale();
        oldScaleToAnimateFrom = oldScaleAnimFactor * GetScaleForScaleDelta(oldScaleDeltaToAnimateFrom.Value);
        // If the animation was from the shapes menu, invert the world-space too.
        if (wasShowingShapesMenuLastFrame) {
          oldScaleToAnimateFrom /= worldSpace.scale;
        }
      }

      // Create the primitive.
      List<MMesh> newMeshes = new List<MMesh>();
      // TODO(bug) Replace pink with wireframe
      int material = peltzerController.mode == ControllerMode.subtract ?
        /* pink wireframe */ MaterialRegistry.PINK_WIREFRAME_ID : peltzerController.currentMaterial;

      Vector3 scale;
      if (peltzerController.shapesMenu.showingShapeMenu) {
        // Scale delta used here is 0 if the shapes menu is open: we want all shapes in the menu the 
        // same size. Further, we invert the worldSpace scale, as the shapes menu is at a constant scale.
        scale = Vector3.one * GetScaleForScaleDelta(0) / worldSpace.scale;
      } else {
        scale = Vector3.one * GetScaleForScaleDelta(scaleDelta);
      }
      Primitives.Shape selectedVolumeShape = (Primitives.Shape)peltzerController.shapesMenu.CurrentItemId;
      MMesh newMesh = Primitives.BuildPrimitive(selectedVolumeShape, scale, /* offset */ Vector3.zero,
        model.GenerateMeshId(), material);

      newMesh.RecalcBounds();
      Vector3 baseBounds = newMesh.bounds.size;

      // If we're in block mode, leave the mesh unrotated (HeldMeshes will deal with it). If not in block
      // mode, start with the controller's rotation (in MODEL space).
      if (!peltzerController.isBlockMode) {
        newMesh.rotation = peltzerController.LastRotationModel * newMesh.rotation;
      }
      newMesh.RecalcBounds();

      if (peltzerController.isBlockMode) {
        // For block mode, we start with the new mesh aligned in the position it would be if the controller
        // were to be at Vector3.zero and pointing straight forward along the Z axis.

        // Note: we don't have to snap the position here if in block mode. Snapping the bounding box of the
        // held meshes is done by the logic in HeldMeshes class. If we were to also do it here, the offset would
        // be incorrect, as it would become a permanent offset of the held meshes (see bug).

        newMesh.offset = PREVIEW_OFFSET * newMesh.bounds.size.magnitude;
      } else {
        // Place the mesh in front of the controller (adjusting the position so that it doesn't overlap the
        // controller -- the bigger the mesh, the further ahead we place it).
        newMesh.offset = peltzerController.LastPositionModel +
          peltzerController.LastRotationModel * (PREVIEW_OFFSET * newMesh.bounds.size.magnitude);
      }

      Dictionary<int, Vector3> meshSize = new Dictionary<int, Vector3>();
      meshSize[newMesh.id] = baseBounds;
      
      // Set the new meshes as held.
      ResetHeldMeshes(new List<MMesh> { newMesh }, meshSize);

      if (oldPosToAnimateFrom != null && oldScaleToAnimateFrom != null && heldMeshes.heldMeshes.Count > 0) {
        // Set up animation.
        MeshWithMaterialRenderer renderer = 
          heldMeshes.heldMeshes[0].Preview.GetComponent<MeshWithMaterialRenderer>();
        renderer.AnimatePositionFrom(oldPosToAnimateFrom.Value);
        // The scale is relative to the mesh's new size, so adjust accordingly:
        float adjustedStartScale = oldScaleToAnimateFrom.Value.magnitude / 
          (Vector3.one * GetScaleForScaleDelta(scaleDelta)).magnitude;
        renderer.AnimateScaleFrom(adjustedStartScale);
      } else {
        // Hide held meshes, they will unhide on next update if we are in the right mode.
        heldMeshes.Hide();
      }
    }

    /// <summary>
    /// Sets up the held meshes.
    /// </summary>
    /// <param name="newMeshes">The new meshes to be held, or null to indicate no held meshes.</param>
    private void ResetHeldMeshes(IEnumerable<MMesh> newMeshes, Dictionary<int, Vector3> sizes = null) {
      if (heldMeshes != null) {
        heldMeshes.DestroyPreviews();
        heldMeshes.HideSnapGuides();
        Destroy(heldMeshes);
        heldMeshes = null;
      }
      if (newMeshes != null) {
        heldMeshes = gameObject.AddComponent<HeldMeshes>();
        heldMeshes.Setup(
          newMeshes,
          peltzerController.LastPositionModel,
          peltzerController.LastRotationModel,
          peltzerController,
          worldSpace,
          meshRepresentationCache,
          peltzerController.isBlockMode ? HeldMeshes.PlacementMode.ABSOLUTE : HeldMeshes.PlacementMode.OFFSET,
          null,
          true, // use preview material
          sizes);

        if (snapStartedWhileShapesMenuUp) {
          snapStartedWhileShapesMenuUp = false;
          heldMeshes.StartSnapping(model, spatialIndex);
        }
      }
    }

    /// <summary>
    ///   Sets the starting controller position, time and insertType to represent the start of an insert.
    /// </summary>
    private void StartInsertMesh() {
      AssertOrThrow.NotNull(heldMeshes, "heldMeshes can't be null");
      heldMeshes.StartInserting(peltzerController.LastPositionModel);
      peltzerController.SetVolumeOverlayActive(false);
    }

    private void EndInsertMesh() {
      AssertOrThrow.NotNull(heldMeshes, "heldMeshes can't be null");
      heldMeshes.FinishInserting();
      peltzerController.SetVolumeOverlayActive(true);
    }

    /// <summary>
    ///   Drops the previewed mesh into the scene at the controller's current location,
    ///   actually updating our data model.
    /// </summary>
    private void InsertVolumeMesh() {
      AssertOrThrow.NotNull(heldMeshes, "heldMeshes can't be null");
      HeldMeshes.HeldMesh heldMesh = heldMeshes.GetFirstHeldMesh();
      MMesh meshToInsert = heldMesh.Mesh;
      meshToInsert.ChangeId(model.GenerateMeshId());
      MeshWithMaterialRenderer meshWithMaterialRenderer = 
        heldMesh.Preview.GetComponent<MeshWithMaterialRenderer>();
      meshToInsert.offset = meshWithMaterialRenderer.GetPositionInModelSpace();
      meshToInsert.rotation = meshWithMaterialRenderer.GetOrientationInModelSpace();
      // Abort entire volume insert on any error.
      if (peltzerController.mode == ControllerMode.insertVolume && !model.CanAddMesh(meshToInsert)) {
        PeltzerMain.Instance.Analytics.FailedOperation("insertVolume");
        audioLibrary.PlayClip(audioLibrary.errorSound);
        peltzerController.TriggerHapticFeedback();
        CreateNewVolumeMesh();
        return;
      }

      if (peltzerController.mode == ControllerMode.insertVolume) {
        bool applyAddMeshEffect = peltzerController.currentMaterial != MaterialRegistry.GEM_ID &&
          peltzerController.currentMaterial != MaterialRegistry.GLASS_ID;
        model.ApplyCommand(new AddMeshCommand(meshToInsert, applyAddMeshEffect));
        lastInsertTime = Time.time;
        audioLibrary.PlayClip(audioLibrary.insertVolumeSound);
        peltzerController.TriggerHapticFeedback(
          HapticFeedback.HapticFeedbackType.FEEDBACK_3, /* durationSeconds */ 0.05f, /* strength */ 0.3f);
        Primitives.Shape selectedShape = (Primitives.Shape)peltzerController.shapesMenu.CurrentItemId;
        PeltzerMain.Instance.Analytics.InsertMesh(Analytics.primitiveTypesToStrings[selectedShape]);
      } else if (peltzerController.mode == ControllerMode.subtract) {
        if (CsgOperations.SubtractMeshFromModel(model, spatialIndex, meshToInsert)) {
          audioLibrary.PlayClip(audioLibrary.deleteSound);
          peltzerController.TriggerHapticFeedback(
            HapticFeedback.HapticFeedbackType.FEEDBACK_3, /* durationSeconds */ 0.05f, /* strength */ 0.3f);
        }
      }

      PeltzerMain.Instance.volumesInserted++;
      CreateNewVolumeMesh();
    }

    /// <summary>
    ///   Explicitly set the scale delta to the given value.
    /// </summary>
    public void SetScaleTo(int delta) {
      scaleDelta = delta;
      CreateNewVolumeMesh();
    }

    /// <summary>
    ///   Increases or decreases the scale of the mesh to be inserted.
    /// </summary>
    /// <param name="scaleUp">Whether to scale up (if false, scale down).</param>
    /// <param name="steps">How many steps to scale up or down.</param>
    public void ChangeScale(bool scaleUp, int steps) {
      AssertOrThrow.NotNull(heldMeshes, "heldMeshes can't be null");
      if ((scaleUp && !PeltzerMain.Instance.restrictionManager.touchpadUpAllowed)
        || (!scaleUp && !PeltzerMain.Instance.restrictionManager.touchpadDownAllowed)) {
        return;
      }

      // Don't allow rapid-scaling in the tutorial, per bug
      if (PeltzerMain.Instance.tutorialManager.TutorialOccurring()) {
        steps = 1;
      }

      // Can't scale if the restriction manager doesn't allow us to.
      if (!PeltzerMain.Instance.restrictionManager.scaleOnVolumeInsertionAllowed) {
        return;
      }

      // Can't scale whilst filling.
      if (heldMeshes.IsFilling) {
        return;
      }

      // The 'scale from' should be 0 (default size) if the menu was open, or the previous size otherwise.
      int oldScaleDelta = peltzerController.shapesMenu.showingShapeMenu ? 0 : scaleDelta;

      // Hide the shapes menu if the user starts scaling.
      peltzerController.shapesMenu.Hide();

      if (IsLongTermScale()) {
        steps += LONG_TERM_SCALE_FACTOR;
      }
      // Just change the delta and regenerate it.
      if (scaleUp) {
        if (scaleDelta == MAX_SCALE_DELTA) {
          // If we are already at the max scale, don't try and scale further.
          StopScaling();
          audioLibrary.PlayClip(audioLibrary.errorSound);
          PeltzerMain.Instance.Analytics.FailedOperation("scaleUpVolume");
          return;
        } else if (scaleDelta + steps > MAX_SCALE_DELTA) {
          // If scaling by the specified amount would take us past the max scale, just scale up to the max scale.
          scaleDelta = MAX_SCALE_DELTA;
          scaleType = ScaleType.SCALE_UP;
          audioLibrary.PlayClip(audioLibrary.incrementSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        } else {
          scaleDelta += steps;
          scaleType = ScaleType.SCALE_UP;
          audioLibrary.PlayClip(audioLibrary.incrementSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        }
      } else {
        if (scaleDelta == MIN_SCALE_DELTA) {
          // If we are already at the min scale, don't try and scale further.
          StopScaling();
          audioLibrary.PlayClip(audioLibrary.errorSound);
          PeltzerMain.Instance.Analytics.FailedOperation("scaleDownVolume");
          return;
        } else if (scaleDelta - steps < MIN_SCALE_DELTA) {
          // If scaling by the specified amount would take us past the min scale, just scale down to the min scale.
          scaleDelta = MIN_SCALE_DELTA;
          scaleType = ScaleType.SCALE_DOWN;
          audioLibrary.PlayClip(audioLibrary.decrementSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        } else {
          scaleDelta -= steps;
          scaleType = ScaleType.SCALE_DOWN;
          audioLibrary.PlayClip(audioLibrary.decrementSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        }
      }
      peltzerController.TriggerHapticFeedback();
      CreateNewVolumeMesh(oldScaleDelta);
      return;
    }

    /// <summary>
    ///   Stop scaling.
    /// </summary>
    private void StopScaling() {
      scaleType = ScaleType.NONE;
      continuousScaleEvents = 0;
      longTermScaleStartTime = float.MaxValue;
    }

    /// <summary>
    ///   Whether scaling has been happening continuously over the threshold set by FAST_SCALE_THRESHOLD.
    /// </summary>
    /// <returns>True if this is a long term scale event, false otherwise.</returns>
    private bool IsLongTermScale() {
      return Time.time > longTermScaleStartTime;
    }

    /// <summary>
    ///   An event handler that listens for controller input and delegates accordingly.
    /// </summary>
    /// <param name="sender">The sender of the controller event.</param>
    /// <param name="args">The controller event arguments.</param>
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      // If we are not in insert or subtract mode, do nothing.
      if ((peltzerController.mode != ControllerMode.insertVolume && peltzerController.mode != ControllerMode.subtract)
        || PeltzerMain.Instance.peltzerController.isPointingAtMenu) {
        return;
      }

      // Check for "change shape" events.
      if (IsChangeShapeEvent(args)) {
        if (!PeltzerMain.Instance.restrictionManager.shapesMenuAllowed) {
          return;
        }

        bool forward = args.TouchpadLocation == TouchpadLocation.RIGHT;
        if (forward) {
          if (!peltzerController.shapesMenu.SelectNextShapesMenuItem()) {
            PeltzerMain.Instance.Analytics.FailedOperation("switchShapeRight");
            audioLibrary.PlayClip(audioLibrary.shapeMenuEndSound);
            peltzerController.TriggerHapticFeedback();
          } else {
            audioLibrary.PlayClip(audioLibrary.swipeRightSound);
          }
          peltzerController.TriggerHapticFeedback();
        } else {
          if (!peltzerController.shapesMenu.SelectPreviousShapesMenuItem()) {
            PeltzerMain.Instance.Analytics.FailedOperation("switchShapeLeft");
            audioLibrary.PlayClip(audioLibrary.shapeMenuEndSound);
            peltzerController.TriggerHapticFeedback();
          } else {
            audioLibrary.PlayClip(audioLibrary.swipeLeftSound);
          }
          peltzerController.TriggerHapticFeedback();
        }
      }

      if (IsStartInsertVolumeOrCopyEvent(args)) {
        // If the shapes menu was open, just close it instead of starting an insertion or copy..
        if (peltzerController.shapesMenu.showingShapeMenu) {
          peltzerController.shapesMenu.Hide();
          return;
        }
        peltzerController.TriggerHapticFeedback();
        StartInsertMesh();

      } else if (IsEndInsertVolumeEvent(args)) {
        if (heldMeshes != null && (heldMeshes.IsInserting || heldMeshes.IsFilling)) {
          heldMeshes.HideSnapGuides();
          InsertVolumeMesh();
          EndInsertMesh();
        }
      } else if (Features.useContinuousSnapDetection && IsStartSnapDetectionEvent(args)) {
        // Show the snap guides if the trigger is slightly pressed.
        heldMeshes.DetectSnap();
      } else if (Features.useContinuousSnapDetection && IsStopSnapDetectionEvent(args)) {
        // If we are previewing the snap guide with a half trigger press and then release the trigger,
        // hide the guide.
        heldMeshes.HideSnapGuides();
      } else if (IsStartSnapEvent(args)) {
        // Close the shapes menu before starting a snap, and note that we started a snap while it was open,
        // as closing the menu triggers heldMeshes to be reset in CreateNewVolumeMesh.
        if (completedSnaps < SNAP_KNOW_HOW_COUNT) {
          PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
        }
        if (peltzerController.shapesMenu.showingShapeMenu) {
          peltzerController.shapesMenu.Hide();
          snapStartedWhileShapesMenuUp = true;
        }
        if (heldMeshes != null) {
          heldMeshes.StartSnapping(model, spatialIndex);
        }
      } else if (IsEndSnapEvent(args)) {
        if (heldMeshes != null) {
          heldMeshes.StopSnapping();
          heldMeshes.HideSnapGuides();
          PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
          completedSnaps++;
        }
      } else if (IsScaleEvent(args)) {
        if (PeltzerMain.Instance.restrictionManager.scaleOnVolumeInsertionAllowed
          && heldMeshes != null) {
          if (scaleType == ScaleType.NONE) {
            longTermScaleStartTime = Time.time + FAST_SCALE_THRESHOLD;
          }
          continuousScaleEvents++;
          ChangeScale(args.TouchpadLocation == TouchpadLocation.TOP, continuousScaleEvents);
        }
      } else if (IsStopScalingEvent(args)) {
        if (heldMeshes != null) {
          StopScaling();
        }
      } else {
        if (args.TouchpadOverlay != TouchpadOverlay.VOLUME_INSERTER) {
          UnsetAllHoverTooltips();
        } else {
          if (IsSetUpHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadUpAllowed) {
            SetHoverTooltip(peltzerController.controllerGeometry.volumeInserterTooltipUp, TouchpadHoverState.UP);
          } else if (IsSetDownHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadDownAllowed) {
            SetHoverTooltip(peltzerController.controllerGeometry.volumeInserterTooltipDown, TouchpadHoverState.DOWN);
          } else if (IsSetLeftHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed) {
            SetHoverTooltip(peltzerController.controllerGeometry.volumeInserterTooltipLeft, TouchpadHoverState.LEFT);
          } else if (IsSetRightHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadRightAllowed) {
            SetHoverTooltip(peltzerController.controllerGeometry.volumeInserterTooltipRight, TouchpadHoverState.RIGHT);
          } else if (IsUnsetAllHoverTooltipsEvent(args)) {
            UnsetAllHoverTooltips();
          }
        }
      }

      if (IsSwitchModeEvent(args)) {
        if (peltzerController.mode == ControllerMode.insertVolume) {
          peltzerController.ChangeMode(ControllerMode.subtract);
          peltzerController.shapesMenu.ChangeShapesMenuMaterial(MaterialRegistry.PINK_WIREFRAME_ID);
        } else if (peltzerController.mode == ControllerMode.subtract) {
          peltzerController.ChangeMode(ControllerMode.insertVolume);
          peltzerController.shapesMenu.ChangeShapesMenuMaterial(peltzerController.currentMaterial);
        }
      }
    }

    private void ModeChangeEventHandler(ControllerMode oldMode, ControllerMode newMode) {
      if (oldMode == ControllerMode.insertVolume || oldMode == ControllerMode.subtract) {
        peltzerController.shapesMenu.Hide();
        UnsetAllHoverTooltips();
      }

      if (newMode == ControllerMode.insertVolume || newMode == ControllerMode.subtract) {
        CreateNewVolumeMesh();

        if (completedSnaps < SNAP_KNOW_HOW_COUNT) {
          PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
        }
      }
    }

    private void MaterialChangeHandler(int newMaterialId) {
      FaceProperties newFaceProperties = new FaceProperties(newMaterialId);
      foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
        foreach (Face face in heldMesh.Mesh.GetFaces()) {
          face.SetProperties(newFaceProperties);
        }
        heldMesh.Preview.GetComponent<MeshWithMaterialRenderer>().OverrideWithNewMaterial(newMaterialId);
      }
    }

    private void ShapeChangedHandler(int newShapeMenuItemId) {
      if (newShapeMenuItemId == ShapesMenu.COPY_MODE_ID) {
        // Start copy mode.
        // TODO(bug): show copy mode affordance (new tool head?).
        ResetHeldMeshes(null);
      } else {
        selector.DeselectAll();
        CreateNewVolumeMesh();
      }
    }

    private void BlockModeChangedHandler(bool isBlockMode) {
      if (peltzerController.mode == ControllerMode.insertVolume || peltzerController.mode == ControllerMode.subtract) {
        CreateNewVolumeMesh();
      }
    }

    public bool IsFilling() {
      return heldMeshes.IsFilling;
    }

    /// <summary>
    ///   Makes only the supplied tooltip visible and ensures the others are off.
    /// </summary>
    /// <param name="tooltip">The tooltip text to activate.</param>
    /// <param name="state">The hover state.</param>
    private void SetHoverTooltip(GameObject tooltip, TouchpadHoverState state) {
      if (!tooltip.activeSelf) {
        UnsetAllHoverTooltips();
        tooltip.SetActive(true);
        peltzerController.SetTouchpadHoverTexture(state);
        peltzerController.TriggerHapticFeedback(
          HapticFeedback.HapticFeedbackType.FEEDBACK_1,
          0.003f,
          0.15f
        );
      }
    }

    /// <summary>
    ///   Unset all of the touchpad hover text tooltips.
    /// </summary>
    private void UnsetAllHoverTooltips() {
      peltzerController.controllerGeometry.volumeInserterTooltipUp.SetActive(false);
      peltzerController.controllerGeometry.volumeInserterTooltipDown.SetActive(false);
      peltzerController.controllerGeometry.volumeInserterTooltipLeft.SetActive(false);
      peltzerController.controllerGeometry.volumeInserterTooltipRight.SetActive(false);
      peltzerController.SetTouchpadHoverTexture(TouchpadHoverState.NONE);
    }

    public void ClearState() {
      scaleDelta = DEFAULT_SCALE_DELTA;
      snapStartedWhileShapesMenuUp = false;
      CreateNewVolumeMesh();
    }
  }
}
