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
using System;
using System.Linq;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.model.render;
using UnityEngine.UI;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   Tool which handles moving an entire mesh.  Since "cloning" is a very similar operation
  ///   to moving, we also support "insertVolume" commands where the ShapeType is COPY.
  /// </summary>
  public class Mover : MonoBehaviour, IMeshRenderOwner {
    // Enums for Type of move.
    // MOVE = hide and then move.
    // CLONE = clone and then move.
    // CREATE = create new and then move. This is used for Zandria models.
    public enum MoveType { NONE, MOVE, CLONE, CREATE }
    // Possible actions that the "group" button can perform, depending on the selected meshes.
    enum GroupButtonAction { NONE, GROUP, UNGROUP };

    // Colours for trackpad icons, to show whether they're enabled or disabled.
    private readonly Color ENABLED_COLOR = new Color(1, 1, 1, 1);
    private readonly Color DISABLED_COLOR = new Color(1, 1, 1, 70f / 255f); // aka 70/255.

    // Parameters for varying the shatter sound.
    private const float SHATTER_PITCH_MAX = 1.3f;
    private const float SHATTER_PITCH_MIN = 0.35f;
    private const float SHATTER_STEP_MAX = 2f;
    private const float SHATTER_STEP_MIN = 0.03f;

    /// <summary>
    ///   Whether or not the user has performed the GROUP action at least once.
    /// </summary>
    public bool userHasPerformedGroupAction = false;

    /// <summary>
    ///   The maximum size of any mesh's bounding box in Grid units, to provide a reasonable
    ///   upper bound for scale operations. Assuming a bounding box maximum of 15metres, and a grid
    ///   unit of 1cm, this is 500 aka 5metres or 1/3 of the bounding box.
    /// </summary>
    private const int MAX_MESH_SIZE_IN_GRID_UNITS = 500;
    /// <summary>
    ///   How long an object should remain in the scene after being thrown away.
    /// </summary>
    private const float OBJECT_LIFETIME_AFTER_THROWING = 0.6f;
    /// <summary>
    ///   Adjusts the size of the shatter particle effect.
    /// </summary>
    private const float SHATTER_SCALE = 0.8f;
    /// <summary>
    ///   If released with this or more lateral velocity, an object will be thrown away.
    /// </summary>
    private const float THROWING_VELOCITY_THRESHOLD = 2.5f;
    /// <summary>
    /// The number of seconds the user scales an object continuously before we start increasing the rate of the
    /// scaling process.
    /// </summary>
    private const float FAST_SCALE_THRESHOLD = 1f;
    /// <summary>
    ///   If user is scaling for a what we consider a long time, we will increase the scaling rate by this amount.
    /// </summary>
    private const int LONG_TERM_SCALE_FACTOR = 1;
    /// <summary>
    /// Number of times to show the group tooltip.
    /// </summary>
    private const int SHOW_GROUP_TOOLTIP_COUNT = 2;
    public ControllerMain controllerMain;
    /// <summary>
    ///   A reference to a controller capable of issuing move commands.
    /// </summary>
    private PeltzerController peltzerController;
    /// <summary>
    ///   A reference to the overall model being built.
    /// </summary>
    private Model model;
    /// <summary>
    ///   The spatial index of the model.
    /// </summary>
    private SpatialIndex spatialIndex;
    /// <summary>
    ///   Selector for detecting which item is hovered or selected.
    /// </summary>
    private Selector selector;
    /// <summary>
    ///   Library for playing sounds.
    /// </summary>
    private AudioLibrary audioLibrary;
    /// <summary>
    ///   A utility to transform from model space to world space.
    /// </summary>
    private WorldSpace worldSpace;
    /// <summary>
    ///   The volume insertion tool.
    /// </summary>
    private VolumeInserter volumeInserter;
    /// <summary>
    ///   The meshes currently being held as a part of a move or copy operation.
    /// </summary>
    private HeldMeshes heldMeshes;
    /// <summary>
    ///   Whether, in the current move operation, we have modified any meshes. We need to know this in order to decide,
    ///   post-move, whether to just move them or replace them.
    /// </summary>
    private bool modifiedAnyMeshesDuringMove;
    /// <summary>
    ///   Whether we are doing a 'move' a 'clone and move' or a 'create and move'.
    /// </summary>
    public MoveType currentMoveType;
    /// <summary>
    ///   The user has pulled the trigger in 'copy' mode.
    /// </summary>
    private bool isCopyGrabbing;
    /// <summary>
    ///   A queue of pairs that contain objects that have been thrown away and the size of the original mesh.
    /// </summary>
    private  Queue<KeyValuePair<GameObject, float>> objectsToDelete = new Queue<KeyValuePair<GameObject, float>> ();
    /// <summary>
    /// The TextMesh component of the left tooltip (so we can modify the text at runtime).
    /// </summary>
    private TextMesh tooltipLeftTextMesh;
    /// <summary>
    ///   The Particle System object used to imitate a shatter effect.
    /// </summary>
    private ParticleSystem shatterPrefab;
    /// <summary>
    /// Number of times the group tooltip has been shown.
    /// </summary>
    private int groupTooltipShownCount = 0;
    /// <summary>
    /// Number of times the ungroup tooltip has been shown.
    /// </summary>
    private int ungroupTooltipShownCount = 0;

    // Detection for trigger down & straight back up, vs trigger down and hold -- either of which
    // begins a move or a copy.
    private bool triggerUpToRelease;
    private float triggerDownTime;
    private bool waitingToDetermineReleaseType;

    // Detection for how long to show the tooltip that explains using the half trigger to preview snapping.
    float timeStartedHalfTriggerDown = 0f;
    private const float HALF_TRIGGER_TOOLTIP_DURATION = 2f;
    private bool halfTriggerTooltipShown = false;

    // Whether the user is currently holding the scale button.
    private ScaleType scaleType = ScaleType.NONE;
    // The number of scale events received without the touchpad being released.
    private int continuousScaleEvents = 0;

    /// <summary>
    // The benchmark we set to determine when the user has been scaling for a long time.
    /// </summary>
    private float longTermScaleStartTime = float.MaxValue;

    private MeshRepresentationCache meshRepresentationCache;

    /// <summary>
    ///   Holds a cached reference to the initial material for the grab tool.
    /// </summary>
    private Material defaultGrabMaterial;
    /// <summary>
    ///   A reference to the current instance of the toolhead's surface for changing the material.
    /// </summary>
    private GameObject copyToolheadSurface;

    /// <summary>
    ///   Whether the user has ever multi-selected in this session
    /// </summary>
    private bool userHasMultiSelected = false;
    
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
      PaletteController paletteController, Selector selector, VolumeInserter volumeInserter, AudioLibrary audioLibrary,
      WorldSpace worldSpace, SpatialIndex spatialIndex, MeshRepresentationCache meshRepresentationCache) {
      this.model = model;
      this.controllerMain = controllerMain;
      this.peltzerController = peltzerController;
      this.selector = selector;
      this.volumeInserter = volumeInserter;
      this.audioLibrary = audioLibrary;
      this.spatialIndex = spatialIndex;
      this.worldSpace = worldSpace;
      this.meshRepresentationCache = meshRepresentationCache;
      this.shatterPrefab = Resources.Load<ParticleSystem>("Prefabs/Shatter");
      controllerMain.ControllerActionHandler += ControllerEventHandler;
      peltzerController.ModeChangedHandler += ModeChangeEventHandler;
      peltzerController.shapesMenu.ShapeMenuItemChangedHandler += ShapeChangedEventHandler;
    }

    /// <summary>
    ///   An event handler that listens for controller input and delegates accordingly.
    /// </summary>
    /// <param name="sender">The sender of the controller event.</param>
    /// <param name="args">The controller event arguments.</param>
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (peltzerController.mode != ControllerMode.move)
        return;

      // get state for tool mode.
      UpdateTooltip();

      if (IsBeginOperationEvent(args)) {
        // If the user is clicking in free space, moving and grabbing is enabled, else we are in selection mode.
        // Selection mode is only ever enabled if click to select is enabled. While selection is enabled, a click
        // in free space will allow moving and cloning.
        int? nearestMesh = null;
        if (Features.clickToSelectEnabled && 
          spatialIndex.FindNearestMeshTo(peltzerController.LastPositionModel, 0.1f / worldSpace.scale, out nearestMesh)) {
          // Check for a nearest mesh in the case that the user is clicking from the inside of a mesh's bounding box.
          // 0.1f is the threshold above which a mesh is considered too far away to select. This is in Unity units, 
          // where 1.0f = 1 meter by default.
          selector.SelectMeshAtPosition(peltzerController.LastPositionModel, Selector.MESHES_ONLY, /* forceSelection = */ true);
        } else {
          triggerUpToRelease = false;
          waitingToDetermineReleaseType = true;
          triggerDownTime = Time.time;
          MaybeStartMoveOrClone();
        }
      } else if (IsCompleteSingleClickEvent(args)) {
        waitingToDetermineReleaseType = false;
        triggerUpToRelease = false;
      } else if (IsReleaseEvent(args)) {
        heldMeshes.HideSnapGuides();
        CompleteMove();
      } else if (Features.useContinuousSnapDetection && IsStartSnapDetectionEvent(args) && heldMeshes != null) {
        // Show the snap guides if the trigger is slightly pressed.
        heldMeshes.DetectSnap();
        // Show half trigger down tooltip
        if (timeStartedHalfTriggerDown == 0f && !halfTriggerTooltipShown) {
          timeStartedHalfTriggerDown = Time.time;
          PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
          PeltzerMain.Instance.paletteController.ShowHoldTriggerHalfwaySnapTooltip();
        } else if (Time.time - timeStartedHalfTriggerDown >= HALF_TRIGGER_TOOLTIP_DURATION) {
          // The user has held down the half trigger for enough time to indicate they understand it. Stop
          // showing the tooltip.
          PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
          halfTriggerTooltipShown = true;
        }
      } else if (Features.useContinuousSnapDetection && IsStopSnapDetectionEvent(args) && heldMeshes != null) {
        // If we are previewing the snap guide with a half trigger press and then release the trigger,
        // hide the guide.
        heldMeshes.HideSnapGuides();
        if (!halfTriggerTooltipShown) {
          // Restart the timer tracking half trigger use.
          timeStartedHalfTriggerDown = 0f;
        }
      } else if (IsStartSnapEvent(args) && heldMeshes != null) {
        heldMeshes.DetectSnap();
        heldMeshes.StartSnapping(model, spatialIndex);
        if (!halfTriggerTooltipShown) {
          // Restart the timer tracking half trigger use.
          timeStartedHalfTriggerDown = 0f;
        }
        if (completedSnaps < SNAP_KNOW_HOW_COUNT) {
          PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
        }
      } else if (IsEndSnapEvent(args) && heldMeshes != null) {
        heldMeshes.StopSnapping();
        heldMeshes.HideSnapGuides();
        completedSnaps++;
        PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
      } else if (IsScaleEvent(args)) {
        if (scaleType == ScaleType.NONE) {
          longTermScaleStartTime = Time.time + FAST_SCALE_THRESHOLD;
        }
        continuousScaleEvents++;
        ScaleMeshes(args.TouchpadLocation == TouchpadLocation.TOP, continuousScaleEvents);
      } else if (IsStopScalingEvent(args)) {
        StopScaling();
      } else if (IsToggleCopyEvent(args) && PeltzerMain.Instance.restrictionManager.copyingAllowed) {
        if (PeltzerMain.Instance.restrictionManager.copyingAllowed && currentMoveType != MoveType.CREATE) {
          PeltzerMain.Instance.restrictionManager.movingMeshesAllowed = true;
        }
        if (currentMoveType != MoveType.CLONE && heldMeshes == null) {
          // If the user was previously not in clone mode, toggle to clone mode and try cloning immediately.
          currentMoveType = MoveType.CLONE;
          MaybeStartMoveOrClone();
        } else if (heldMeshes == null) {
          // If the user was previously in clone mode and is not in the middle of an operation, toggle to move mode.
          currentMoveType = MoveType.MOVE;
        }
      } else if (IsGroupEvent(args)) {
        // Perform the appropriate group action (contextual, depending on the selected meshes).
        PerformGroupButtonAction();
      } else if (IsFlipEvent(args)) {
        FlipMeshes();
      } else if (IsSetUpHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadUpAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.moverTooltipUp, TouchpadHoverState.UP);
      } else if (IsSetDownHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadDownAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.moverTooltipDown, TouchpadHoverState.DOWN);
      } else if (IsSetLeftHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed
        && currentMoveType != MoveType.CREATE) {
        // We don't show the 'copy' tooltip when a user is importing from the PolyMenu, per bug
        SetHoverTooltip(peltzerController.controllerGeometry.moverTooltipLeft, TouchpadHoverState.LEFT);
      } else if (IsSetRightHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadRightAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.moverTooltipRight, TouchpadHoverState.RIGHT);
      } else if (IsUnsetAllHoverTooltipsEvent(args)) {
        UnsetAllHoverTooltips();
      }
    }

    private bool IsBeginOperationEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN
        && heldMeshes == null;
    }

    private bool IsCompleteSingleClickEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP
        && waitingToDetermineReleaseType;
    }

    private bool IsReleaseEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && ((args.Action == ButtonAction.UP && triggerUpToRelease)
        || (args.Action == ButtonAction.DOWN && !triggerUpToRelease))
        && heldMeshes != null;
    }

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

    private static bool IsEndSnapEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP;
    }

    private static bool IsGroupEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.ApplicationMenu
        && args.Action == ButtonAction.DOWN;
    }

    private static bool IsFlipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN && args.TouchpadLocation == TouchpadLocation.RIGHT
        && !PeltzerMain.Instance.Zoomer.Zooming;
    }

    private bool IsScaleEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && (args.TouchpadLocation == TouchpadLocation.BOTTOM || args.TouchpadLocation == TouchpadLocation.TOP)
        && (args.TouchpadOverlay == TouchpadOverlay.MOVE)
        && !PeltzerMain.Instance.Zoomer.Zooming;
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

    private static bool IsToggleCopyEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN && args.TouchpadLocation == TouchpadLocation.LEFT
        && !PeltzerMain.Instance.Zoomer.Zooming;
    }

    // Touchpad Hover Tests
    private static bool IsSetUpHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.TOP;
    }

    private static bool IsSetDownHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.BOTTOM;
    }

    private static bool IsSetLeftHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.LEFT;
    }

    private static bool IsSetRightHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.RIGHT;
    }

    private static bool IsUnsetAllHoverTooltipsEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.NONE;
    }

    /// <summary>
    ///   Called when the controller mode changes to allow for any setup that may be necessary.
    /// </summary>
    /// <param name="mode">The current mode of the controller.</param>
    private void ModeChangeEventHandler(ControllerMode oldMode, ControllerMode newMode) {
      if (oldMode == ControllerMode.move) {
        ClearState();
      }

      if (newMode == ControllerMode.move && completedSnaps < SNAP_KNOW_HOW_COUNT) {
        PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
      }
    }

    private void ShapeChangedEventHandler(int newMenuItemId) {
      ClearState();
    }

    public void InvalidateCachedMaterial() {
      defaultGrabMaterial = null;
    }

    // Start a move or clone operation if any meshes were hovered or selected when the trigger went down.
    private bool MaybeStartMoveOrClone() {
      if (currentMoveType != MoveType.CREATE) {
        // There might be some selected/hovered meshes, if so, set up a move/clone, else do nothing.
        IEnumerable<int> meshes = selector.SelectedOrHoveredMeshes();
        if (meshes.Count() > 0) {
          IEnumerable<MMesh> selectedMeshes = meshes.Select(m => model.GetMesh(m));
          StartMove(selectedMeshes);
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Updates the grab/copy tooltip
    /// </summary>
    public void UpdateTooltip() {
      peltzerController.controllerGeometry.grabTooltips.SetActive(peltzerController.mode == ControllerMode.move);

      // Set the grab hand to be green wireframe or regular, depending if we're in clone mode or not.
      if (copyToolheadSurface != null) {
        if (defaultGrabMaterial == null) {
          defaultGrabMaterial = copyToolheadSurface.GetComponent<Renderer>().material;
        }
        if (PeltzerMain.Instance.restrictionManager.IsControllerModeAllowed(ControllerMode.move)) {
          if (currentMoveType == MoveType.CLONE) {
            copyToolheadSurface.GetComponent<Renderer>().material =
                MaterialRegistry.GetMaterialAndColorById(MaterialRegistry.GREEN_WIREFRAME_ID).material;
          } else {
            copyToolheadSurface.GetComponent<Renderer>().material = defaultGrabMaterial;
          }
        }
      } else {
        copyToolheadSurface = peltzerController.attachedToolHead.transform.Find(
          "grabTool_Geo/GrabHandFBX3/Hand_object").gameObject;
      }

      // Find the current tool state.
      bool inProgress = IsMoving();
      bool hoveringOrSelected =
        selector.selectedMeshes.Count > 0 || selector.hoverMeshes.Count > 0;

      if (!PeltzerMain.Instance.restrictionManager.touchpadHighlightingAllowed) {
        Overlay moveOverlay = peltzerController.controllerGeometry.moveOverlay.GetComponent<Overlay>();

        // Set resize & flip active if we're hovering, selected, or in progress.
        moveOverlay.upIcon.color = inProgress || hoveringOrSelected ? ENABLED_COLOR : DISABLED_COLOR;
        moveOverlay.downIcon.color = inProgress || hoveringOrSelected ? ENABLED_COLOR : DISABLED_COLOR;

        // flip
        moveOverlay.rightIcon.color = inProgress || hoveringOrSelected ? ENABLED_COLOR : DISABLED_COLOR;

        // Set copy active if we're hovering, selected, or neither of those but are in copy-mode.
        moveOverlay.leftIcon.color =
          hoveringOrSelected || (!hoveringOrSelected && currentMoveType == MoveType.CLONE) ?
           ENABLED_COLOR : DISABLED_COLOR;
      }

      // Figure out what the "group" button should do and update UI to reflect the action.
      GroupButtonAction groupButtonAction = GetGroupButtonAction(GetSelectedGrabbedOrHoveredMeshes());

      if (groupButtonAction != GroupButtonAction.NONE) {
        peltzerController.SetApplicationButtonOverlay(ButtonMode.ACTIVE);
      } else {
        peltzerController.SetApplicationButtonOverlay(ButtonMode.WAITING);
      }

      Overlay overlay = peltzerController.controllerGeometry.moveOverlay.GetComponent<Overlay>();
      // We want the group icon on if the user can group or when the button is waiting to show the user
      // what the button would do.
      overlay.onIcon.gameObject.SetActive(groupButtonAction != GroupButtonAction.UNGROUP);
      overlay.offIcon.gameObject.SetActive(groupButtonAction == GroupButtonAction.UNGROUP);

      GameObject groupTooltip = peltzerController.handedness == Handedness.RIGHT ?
        peltzerController.controllerGeometry.groupLeftTooltip : peltzerController.controllerGeometry.groupRightTooltip;
      GameObject ungroupTooltip = peltzerController.handedness == Handedness.RIGHT ?
        peltzerController.controllerGeometry.ungroupLeftTooltip : peltzerController.controllerGeometry.ungroupRightTooltip;

      if (groupButtonAction == GroupButtonAction.GROUP
        && groupTooltipShownCount < SHOW_GROUP_TOOLTIP_COUNT
        && !PeltzerMain.Instance.tutorialManager.TutorialOccurring()
        && !PeltzerMain.Instance.HasDisabledTooltips) {
        peltzerController.controllerGeometry.groupTooltipRoot.SetActive(true);
        groupTooltip.SetActive(true);
        ungroupTooltip.SetActive(false);
      } else if (groupButtonAction == GroupButtonAction.UNGROUP
        && ungroupTooltipShownCount < SHOW_GROUP_TOOLTIP_COUNT
        && !PeltzerMain.Instance.tutorialManager.TutorialOccurring()
        && !PeltzerMain.Instance.HasDisabledTooltips) {
        peltzerController.controllerGeometry.groupTooltipRoot.SetActive(true);
        ungroupTooltip.SetActive(true);
        groupTooltip.SetActive(false);
      } else {
        peltzerController.controllerGeometry.groupTooltipRoot.SetActive(false);
        // Only increment the 'times shown' count when deselected, or else may count as separate times if the tooltip
        // that was still active was "set" again.
        if (groupTooltip.activeSelf) {
          groupTooltip.SetActive(false);
          groupTooltipShownCount++;
        } else if (ungroupTooltip.activeSelf) {
          ungroupTooltip.SetActive(false);
          ungroupTooltipShownCount++;
        }
      }

      // Set the state of the Grab Toolhead tooltip which happens to be the first child - index 0.
      // We wish to show this tooltip if:
      // - There are at least two meshes in the scene.
      // - The user has not multi-selected in this session.
      if (Features.showMultiselectTooltip) {
        userHasMultiSelected |= PeltzerMain.Instance.GetSelector().selectedMeshes.Count > 1;
        bool showTooltip = PeltzerMain.Instance.peltzerController.mode == ControllerMode.move
          && !userHasMultiSelected
          && !PeltzerMain.Instance.HasDisabledTooltips
          && PeltzerMain.Instance.model.GetNumberOfMeshes() > 1;
        peltzerController.attachedToolHead.transform.GetChild(0).gameObject.SetActive(showTooltip);
      }
    }

    /// <summary>
    ///   Makes only the supplied tooltip visible and ensures the others are off.
    /// </summary>
    /// <param name="tooltip">The tooltip text to activate.</param>
    /// <param name="state">The hover state.</param>
    private void SetHoverTooltip(GameObject tooltip, TouchpadHoverState state) {
      if (IsMoving() || selector.selectedMeshes.Count > 0 || selector.hoverMeshes.Count > 0) {
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
      } else {
        UnsetAllHoverTooltips();
      }
    }

    /// <summary>
    ///   Unset all of the touchpad hover text tooltips.
    /// </summary>
    private void UnsetAllHoverTooltips() {
      peltzerController.controllerGeometry.moverTooltipUp.SetActive(false);
      peltzerController.controllerGeometry.moverTooltipDown.SetActive(false);
      peltzerController.controllerGeometry.moverTooltipLeft.SetActive(false);
      peltzerController.controllerGeometry.moverTooltipRight.SetActive(false);
      peltzerController.SetTouchpadHoverTexture(TouchpadHoverState.NONE);
    }

    /// <summary>
    ///   Add a shatter effect to thrown objects.
    /// </summary>
    /// <param name="objectToDelete">The GameObject to shatter.</param>
    /// <param name="scaleFactor">The magnitude of the GameObject's meshes bounds size.</param>
    private void ShatterObject(GameObject objectToDelete, float scaleFactor) {
      ParticleSystem shatterEffect = Instantiate(shatterPrefab);
      shatterEffect.transform.position = objectToDelete.transform.position;
      
      ParticleSystem.MainModule mainModule = shatterEffect.main;
      mainModule.startSize = scaleFactor * SHATTER_SCALE;

      float step = Mathf.Clamp(Mathf.SmoothStep(SHATTER_STEP_MIN, SHATTER_STEP_MAX, scaleFactor), 0f, 1f);
      float pitch = Mathf.Lerp(SHATTER_PITCH_MAX, SHATTER_PITCH_MIN, step);
      audioLibrary.PlayClip(audioLibrary.breakSound, pitch);

      int materialId = objectToDelete.GetComponent<MeshWithMaterialRenderer>().meshes[0].materialAndColor.matId;
      shatterEffect.GetComponent<ParticleSystemRenderer>().material =
        MaterialRegistry.GetMaterialWithAlbedoById(materialId);
    }

    /// <summary>
    ///   Each frame, if a mesh is currently held, update its position in world-space relative
    ///   to its original position, and the delta between the controller's position at world-start
    ///   and the controller's current position.
    /// </summary>
    private void Update() {
      // We need to clean up any 'thrown away' objects when their time comes (even if the user has changed mode).
      while (objectsToDelete.Count > 0 && (objectsToDelete.Peek().Key.transform.position.y <= 0.125f)) {
        KeyValuePair<GameObject, float> pairToDelete = objectsToDelete.Dequeue(); 

        ShatterObject(pairToDelete.Key, pairToDelete.Value);
        DestroyImmediate(pairToDelete.Key);
      }

      if (!PeltzerController.AcquireIfNecessary(ref peltzerController)
        || peltzerController.mode != ControllerMode.move) {
        return;
      }

      UpdateTooltip();

      // If we have not grabbed any meshes let the selector find meshes to grab.
      if (heldMeshes == null) {
        selector.SelectMeshAtPosition(peltzerController.LastPositionModel, Selector.MESHES_ONLY);
      } else {
        // If a move is in progress, and the trigger has been down for longer than WAIT_THRESHOLD, then this is
        // a hold-trigger-and-drag operation which can be completed by raising the trigger.
        if (waitingToDetermineReleaseType && Time.time - triggerDownTime > PeltzerController.SINGLE_CLICK_THRESHOLD) {
          waitingToDetermineReleaseType = false;
          triggerUpToRelease = true;
        }

        heldMeshes.UpdatePositions();
      }

      peltzerController.heldMeshes = heldMeshes;
    }

    public void StartMove(IEnumerable<MMesh> selectedMeshes) {
      if (!PeltzerMain.Instance.restrictionManager.movingMeshesAllowed) {
        return;
      }

      selector.EndMultiSelection();

      // Hide the overlay.
      peltzerController.HideTooltips();

      // A move is being started, inform the user.
      peltzerController.TriggerHapticFeedback();
      audioLibrary.PlayClip(currentMoveType == MoveType.MOVE ?
        audioLibrary.grabMeshSound : audioLibrary.copySound);

      // We'll generate a preview and a copy of the original mesh, for each selected mesh.
      heldMeshes = gameObject.AddComponent<HeldMeshes>();
      heldMeshes.Setup(selectedMeshes, peltzerController.LastPositionModel, peltzerController.LastRotationModel,
        peltzerController, worldSpace, meshRepresentationCache);

      // If we are copying, then we unhide the meshes now, as the original meshes will not be affected.
      // If we are moving, we don't bother unhiding the original meshes, as Mover will be using previews.
      if (currentMoveType == MoveType.MOVE) {
        foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
          model.ClaimMesh(heldMesh.Mesh.id, this);
        }
      } else {
        selector.DeselectAll();
      }
    }

    public bool IsMoving() {
      return heldMeshes != null;
    }

    /// <summary>
    ///   Throw an object away and mark it for deletion.
    /// </summary>
    /// <param name="unlovedObject">The object to delete.</param>
    /// <param name="velocity">The velocity with which to release it.</param>
    /// <param name="meshMagnitude">The magnitude of the mesh's bounds, used to scale the shatter size.</param>
    private void ThrowObjectAway(GameObject unlovedObject, Vector3 velocity, float meshMagnitude) {
      MeshWithMaterialRenderer mwmRenderer = unlovedObject.GetComponent<MeshWithMaterialRenderer>();
      unlovedObject.transform.position = mwmRenderer.GetPositionInWorldSpace();
      mwmRenderer.UseGameObjectPosition = true;

      // Apply the force.
      Rigidbody rigidbody = unlovedObject.GetComponent<Rigidbody>();
      if (rigidbody == null) {
        rigidbody = unlovedObject.AddComponent<Rigidbody>();
      }
      rigidbody.isKinematic = false;
      rigidbody.AddForce(velocity, ForceMode.VelocityChange);

      // Schedule for deletion.
      objectsToDelete.Enqueue(new KeyValuePair<GameObject, float>(unlovedObject, meshMagnitude));
    }

    /// <summary>
    /// Returns whether or not the user just did a "throw" gesture with the controller, thus indicating their disdain
    /// for the currently held meshes and a corresponding desire to be rid of them.
    /// </summary>
    /// <returns>True if and only if the user just did a throw gesture.</returns>
    private bool IsThrowing() {
      return peltzerController.GetVelocity().magnitude > THROWING_VELOCITY_THRESHOLD
        && PeltzerMain.Instance.restrictionManager.throwAwayAllowed;
    }

    /// <summary>
    ///   Throws away the currently grabbed meshes, applying a force to them and scheduling them for deletion.
    /// </summary>
    private void ThrowGrabbedMeshes(out List<Command> commands) {
      commands = new List<Command>(heldMeshes.heldMeshes.Count);
      foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
        ThrowObjectAway(heldMesh.Preview, peltzerController.GetVelocity(), heldMesh.Mesh.bounds.size.magnitude);
        if (currentMoveType == MoveType.MOVE) {
          commands.Add(new DeleteMeshCommand(heldMesh.Mesh.id));
          model.RelinquishMesh(heldMesh.Mesh.id, this);
        }
      }
      heldMeshes.heldMeshes.Clear();
    }

    /// <summary>
    ///   Moves the currently grabbed meshes.
    /// </summary>
    /// <param name="commands">Out param. Returns the list of model commands that resulted from the move.</param>
    /// <returns>True if there were any invalid meshes, false if all meshes were valid.</returns>
    private bool MoveGrabbedMeshes(out List<Command> commands) {
      bool anyInvalidMeshes = false;
      commands = new List<Command>(heldMeshes.heldMeshes.Count);

      foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
        MMesh mesh = heldMesh.Mesh;
        GameObject preview = heldMesh.Preview;

        if (modifiedAnyMeshesDuringMove) {

          MeshWithMaterialRenderer meshRenderer = preview.GetComponent<MeshWithMaterialRenderer>();
          // If we modified any meshes, we delete and re-add any affected meshes, but first, we need to update their
          // position/rotation to match the preview.
          mesh.offset = meshRenderer.GetPositionInModelSpace();
          mesh.rotation = meshRenderer.GetOrientationInModelSpace();
          if (model.CanAddMesh(mesh)) {
            commands.Add(new ReplaceMeshCommand(mesh.id, mesh));
          } else {
            anyInvalidMeshes = true;
          }
        } else {
          // If we didn't modify any meshes, we issue a command to move any affected meshes, by their respective
          // positional and rotational deltas.
          MMesh originalMesh = model.GetMesh(mesh.id);
          MeshWithMaterialRenderer meshRenderer = preview.GetComponent<MeshWithMaterialRenderer>();
          Quaternion rotationDelta = Quaternion.Inverse(originalMesh.rotation)
            * meshRenderer.GetOrientationInModelSpace();
          Vector3 positionDelta = meshRenderer.GetPositionInModelSpace() - originalMesh.offset;
          if (model.CanMoveMesh(originalMesh, positionDelta, rotationDelta)) {
            commands.Add(new MoveMeshCommand(originalMesh.id, positionDelta, rotationDelta));
          } else {
            anyInvalidMeshes = true;
          }
        }
        model.RelinquishMesh(mesh.id, this);
      }

      DestroyImmediate(heldMeshes);

      return anyInvalidMeshes;
    }

    /// <summary>
    /// Creates (inserts into the model for the first time) the currently grabbed meshes.
    /// </summary>
    /// <param name="commands">Out param. Returns the list of model commands that resulted from the create.</param>
    /// <returns>True if there were any invalid meshes, false if all meshes were valid.</returns>
    private bool CreateGrabbedMeshes(out List<Command> commands) {
      bool anyInvalidMeshes = false;
      commands = new List<Command>(heldMeshes.heldMeshes.Count);

      foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
        MMesh mesh = heldMesh.Mesh;
        GameObject preview = heldMesh.Preview;

        MeshWithMaterialRenderer renderer = preview.GetComponent<MeshWithMaterialRenderer>();
        mesh.offset = renderer.GetPositionInModelSpace();
        mesh.rotation = renderer.GetOrientationInModelSpace();
        if (model.CanAddMesh(mesh)) {
          commands.Add(new AddMeshCommand(mesh));
        } else {
          anyInvalidMeshes = true;
        }
        DestroyImmediate(preview);
      }


      return anyInvalidMeshes;
    }

    /// <summary>
    /// Copies the currently grabbed meshes.
    /// </summary>
    /// <param name="commands">Out param. Returns the list of model commands that resulted from the copy.</param>
    /// <returns>True if there were any invalid meshes, false if all meshes were valid.</returns>
    private bool CopyGrabbedMeshes(out List<Command> commands) {
      bool anyInvalidMeshes = false;
      commands = new List<Command>(heldMeshes.heldMeshes.Count);

      // To clone groups correctly, we must generate a new group ID for each group ID in the meshes to copy, and use
      // the new group IDs for the copy. That way, a copy of a group will be a new group. This dictionary stores a
      // mapping from old group ID to new group ID. We populate it lazily, generating a new group ID for each
      // old group ID that we find.
      Dictionary<int, int> groupMapping = new Dictionary<int, int>();

      // GROUP_NONE just maps to GROUP_NONE.
      groupMapping[MMesh.GROUP_NONE] = MMesh.GROUP_NONE;

      foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
        MMesh mesh = heldMesh.Mesh;
        GameObject preview = heldMesh.Preview;

        // Generate a new group ID if we haven't seen this group before.
        if (mesh.groupId != MMesh.GROUP_NONE && !groupMapping.ContainsKey(mesh.groupId)) {
          groupMapping[mesh.groupId] = model.GenerateGroupId();
        }

        MMesh copy = mesh.CloneWithNewIdAndGroup(model.GenerateMeshId(), groupMapping[mesh.groupId]);
        MeshWithMaterialRenderer renderer = preview.GetComponent<MeshWithMaterialRenderer>();
        copy.offset = renderer.GetPositionInModelSpace();
        copy.rotation = renderer.GetOrientationInModelSpace();
        if (model.CanAddMesh(copy)) {
          commands.Add(new CopyMeshCommand(mesh.id, copy));
        } else {
          anyInvalidMeshes = true;
        }
        DestroyImmediate(preview);
      }


      return anyInvalidMeshes;
    }

    private void CompleteMove() {
      // If we weren't moving anything, calling this function is redundant, return.
      if (heldMeshes == null) {
        return;
      }
      bool throwAway = IsThrowing();

      // Get the list of commands for move/copy and find out if this would be an invalid operation.
      List<Command> commands = new List<Command>();
      bool anyInvalidMeshes = false;
      if (throwAway) {
        ThrowGrabbedMeshes(out commands);
      } else if (currentMoveType == MoveType.MOVE) {
        anyInvalidMeshes = MoveGrabbedMeshes(out commands);
      } else if (currentMoveType == MoveType.CREATE) {
        anyInvalidMeshes = CreateGrabbedMeshes(out commands);
      } else {
        anyInvalidMeshes = CopyGrabbedMeshes(out commands);
      }

      if (!anyInvalidMeshes) {
        if (commands.Count > 0) {
          model.ApplyCommand(new CompositeCommand(commands));
        }
        // TODO(bug): Add audio for throwing away.
        audioLibrary.PlayClip(currentMoveType == MoveType.MOVE ?
          audioLibrary.releaseMeshSound : audioLibrary.pasteMeshSound);
        peltzerController.TriggerHapticFeedback();
        PeltzerMain.Instance.movesCompleted++;
      } else {
        audioLibrary.PlayClip(audioLibrary.errorSound);
        peltzerController.TriggerHapticFeedback();
      }

      if (!throwAway) {
        heldMeshes.DestroyPreviews();
      }

      if (currentMoveType == MoveType.CREATE) {
        // Close the details menu after a user imports. 
        // This is a potentially useful UX action for the user, but more importantly it allows
        // us to avoid making a copy of all meshes here, as we're actually grabbing the meshes from the details panel
        // to import into the scene. If the user (for some reason) wants to import the same mesh again, they'll have to
        // re-open the details panel, thereby re-generating the meshes.
        PeltzerMain.Instance.GetPolyMenuMain().SetActiveMenu(menu.PolyMenuMain.Menu.TOOLS_MENU);
      }
      ClearState();
    }

    // Reset everything to a clean, default state.
    public void ClearState() {
      isCopyGrabbing = false;
      modifiedAnyMeshesDuringMove = false;
      waitingToDetermineReleaseType = false;

      if (heldMeshes != null) {
        if (currentMoveType == MoveType.MOVE) {
          foreach (int meshId in heldMeshes.GetMeshIds()) {
            model.RelinquishMesh(meshId, this);
          }
        }
      }

      DestroyImmediate(heldMeshes);
      peltzerController.ShowTooltips();

      selector.DeselectAll();
      currentMoveType = MoveType.MOVE;
    }

    /// <summary>
    /// Claim responsibility for rendering a mesh from this class.
    /// This should only be called by Model, as otherwise Model's knowledge of current ownership will be incorrect.
    /// </summary>
    public int ClaimMesh(int meshId, IMeshRenderOwner fosterRenderer) {
      for (int i = 0; i < heldMeshes.heldMeshes.Count; i++) {
        if (heldMeshes.heldMeshes[i].Mesh.id == meshId) {
          if (heldMeshes.heldMeshes[i].Preview != null) {
            DestroyImmediate(heldMeshes.heldMeshes[i].Preview);
          }
          heldMeshes.heldMeshes.RemoveAt(i);
          return meshId;
        }
      }
      return -1;
    }

    private void FlipMeshes() {
      if (heldMeshes != null && heldMeshes.IsFilling) {
        return;
      }

      List<MMesh> meshesToFlip = GetMeshesToFlipOrScale();
      List<int> originallySelectedMeshIds = new List<int>(selector.selectedMeshes);

      if (meshesToFlip.Count() == 0)
        return;

      // Now compute the flipped the meshes. This doesn't alter the model, it just returns a new set of
      // meshes that represents the result of the flipping.
      List<MMesh> flippedMeshes;

      if (!Flipper.FlipMeshes(meshesToFlip, PeltzerMain.Instance.peltzerController.LastRotationModel, out flippedMeshes)) {
        return;
      }

      if (heldMeshes != null) {
        // If we are currently in the middle of a move/create operation, then we don't update the model.
        // Instead, we just update the previews and continue the grab operation. The model will be updated later when
        // the grab ends.
        if (currentMoveType == MoveType.MOVE) {
          heldMeshes.DestroyPreviews();
          heldMeshes.SetupWithNoCloneOrCache(flippedMeshes, peltzerController.LastPositionModel, peltzerController,
            worldSpace);
        } else {
          heldMeshes.DestroyPreviews();
          heldMeshes.SetupWithNoCloneOrCache(flippedMeshes, peltzerController.LastPositionModel, peltzerController, worldSpace);
        }

        // Keep track of the fact that meshes were modified during the move (so we know to replace them when the
        // move is complete).
        modifiedAnyMeshesDuringMove = true;
      } else {
        // Not grabbing, so we can update the model directly, replacing the original meshes with their corrresponding
        // flipped mesh.
        List<Command> commands = new List<Command>();
        foreach (MMesh mesh in flippedMeshes) {
          // Claim to stop any other tool from previewing
          model.ClaimMesh(mesh.id, this);
          commands.Add(new ReplaceMeshCommand(mesh.id, mesh));
          model.RelinquishMesh(mesh.id, this);
        }
        model.ApplyCommand(new CompositeCommand(commands));

        // Restore the original selection, if there was one.
        foreach (MMesh mesh in meshesToFlip) {
          if (originallySelectedMeshIds.Contains(mesh.id)) {
            selector.SelectMesh(mesh.id);
          }
        }
      }


      // Make some noise.
      // TODO(bug): replace with flip sound.
      audioLibrary.PlayClip(audioLibrary.groupSound);
      peltzerController.TriggerHapticFeedback();
    }

    private IEnumerable<int> GetSelectedGrabbedOrHoveredMeshes() {
      if (heldMeshes != null) {
        return heldMeshes.heldMeshes.Select(hm => hm.Mesh.id);
      } else if (selector.selectedMeshes.Count > 0) {
        return selector.selectedMeshes;
      } else {
        List<MMesh> meshList = new List<MMesh>();
        return selector.hoverMeshes;
      }
    }

    // Performs the correct action for the "group" button. The action is decided
    // contextually depending on the selection.
    private void PerformGroupButtonAction() {
      IEnumerable<int> meshes = GetSelectedGrabbedOrHoveredMeshes();

      // Figure out what the button should do.
      GroupButtonAction action = GetGroupButtonAction(meshes);

      SetMeshGroupsCommand command;
      string operationName;
      AudioClip audioClip;

      switch (action) {
        case GroupButtonAction.GROUP:
          // Group the meshes together.
          operationName = "groupMeshes";
          command = SetMeshGroupsCommand.CreateGroupMeshesCommand(model, meshes);
          audioClip = audioLibrary.groupSound;
          userHasPerformedGroupAction = true;
          break;
        case GroupButtonAction.UNGROUP:
          // Ungroup the meshes.
          operationName = "ungroupMeshes";
          command = SetMeshGroupsCommand.CreateUngroupMeshesCommand(model, meshes);
          audioClip = audioLibrary.ungroupSound;
          break;
        default:
          // Nothing to do.
          return;
      }

      // Apply the command and log to Google Analytics.
      model.ApplyCommand(command);

      if (heldMeshes != null) {
        // Since the held meshes are clones of the model's meshes, we need to update the group IDs of
        // the held meshes to reflect the changes in the model.
        foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
          heldMesh.Mesh.groupId = model.GetMesh(heldMesh.Mesh.id).groupId;
        }
      } else {
        // Unselect everything (because it's unintuitive/inconvenient for things to remain selected after
        // grouping or ungrouping).
        selector.DeselectAll();
      }

      // Make some noise.
      audioLibrary.PlayClip(audioClip);
      peltzerController.TriggerHapticFeedback();
    }

    /// <summary>
    /// Returns the action that the "group" button should perform, based on which meshes are
    /// selected.
    /// </summary>
    /// <param name="selectedOrGrabbedMeshes">The list of meshes currently selected or grabbed.
    /// </param>
    /// <returns>The action that the "group" button should perform.</returns>
    private GroupButtonAction GetGroupButtonAction(IEnumerable<int> selectedOrGrabbedMeshes) {
      // If there are fewer than 2 meshes selected, there is no group action.
      if (selectedOrGrabbedMeshes.Count() < 2) {
        return GroupButtonAction.NONE;
      }
      // If all the grabbed meshes belong to the same group, the relevant action is "ungroup".
      if (model.AreMeshesInSameGroup(selectedOrGrabbedMeshes)) {
        return GroupButtonAction.UNGROUP;
      }
      // Meshes are in different groups or are ungrouped. So the action is "group".
      return GroupButtonAction.GROUP;
    }

    /// <summary>
    /// Calculates the scaling factor by which the meshes should be scaled in order to make them one grid unit
    /// bigger or smaller. This helps ensure that meshes line up with the grid when scaled up/down.
    /// </summary>
    /// <param name="meshes">The meshes to be scaled.</param>
    /// <param name="scaleUp">If true, scale up (increase size). If false, scale down.</param>
    /// <param name="numSteps">The number of steps: 2 steps means scale twice as much as 1 step.</param>
    /// <param name="result">Out param that indicates the scale factor to use. Only defined if this method
    /// returns true. If the method returns false, the result is undefined.</param>
    /// <returns>True if the scaling factor was calculated successfully, false on failure.</returns>
    private bool CalculateGridFriendlyScaleFactor(IEnumerable<MMesh> meshes, bool scaleUp, int numSteps,
      out float result) {
      result = 1.0f;

      // Figure out the bounding box of all the meshes.
      Bounds? boundsOfAll = null;
      foreach (MMesh mesh in meshes) {
        if (null != boundsOfAll) {
          boundsOfAll.Value.Encapsulate(mesh.bounds);
        } else {
          boundsOfAll = mesh.bounds;
        }
      }

      // If there were no meshes, give up.
      if (null == boundsOfAll) {
        return false;
      }

      if (IsLongTermScale()) {
        numSteps += LONG_TERM_SCALE_FACTOR;
      }

      // The logic we use here is: we take the bounding box of the selected meshes and figure out how large that
      // box is in grid units. Then we compute the scale factor such that this box will increase/decrease in size
      // by 1 grid unit (or, more precisely, such that the longest edge of the bounding box will increase/decrease
      // by 1 grid unit). So, for example, if the longest edge currently measures 4 grid units and we are scaling
      // up, then we want it to be 5 grid units long, so the scale factor should be 1.25 (since 4 * 1.25 = 5).

      // How many grid units does the largest side measure?
      float largestSide = Mathf.Max(boundsOfAll.Value.size.x, boundsOfAll.Value.size.y, boundsOfAll.Value.size.z);
      int gridUnits = Mathf.RoundToInt(largestSide / GridUtils.GRID_SIZE);

      if (largestSide < 0.001f) {
        // We can't operate on meshes that have zero or negligible size.
        // The result would be undefined.
        return false;
      }

      // Add some min/max checks:
      if (!scaleUp) {
        if (gridUnits <= 2) {
          // Do not scale down further.
          return false;
        }
        if (gridUnits - numSteps < 2) {
          // Enforce a minimum size.
          gridUnits = 2;
        } else {
          gridUnits -= numSteps;
        }
      } else {
        if (gridUnits + numSteps > MAX_MESH_SIZE_IN_GRID_UNITS) {
          gridUnits = MAX_MESH_SIZE_IN_GRID_UNITS;
        } else {
          gridUnits += numSteps;
        }
      }

      // Now we want to compute the scale factor such that the largest size is that many grid units.
      float desiredLargestSide = GridUtils.GRID_SIZE * gridUnits;
      result = desiredLargestSide / largestSide;
      return true;
    }

    /// <summary>
    ///   Retrieve a list of meshes to flip or scale. Returns grabbed, selected, or hovered meshes.
    /// </summary>
    /// <returns></returns>
    private List<MMesh> GetMeshesToFlipOrScale() {
      List<MMesh> meshes;
      List<int> originallySelectedMeshIds = new List<int>(selector.selectedMeshes);
      if (heldMeshes != null) {
        // Get the meshes that are being held.
        Dictionary<MMesh, GameObject> heldMeshesAndPreviews = heldMeshes.GetHeldMeshesAndPreviews();
        meshes = new List<MMesh>(heldMeshesAndPreviews.Count());
        foreach (KeyValuePair<MMesh, GameObject> pair in heldMeshesAndPreviews) {
          // Until now, the GameObject has held the state of the mesh's offset and rotation. Update it here
          // such that we can pass the right information to Flipper/Scaler.
          MMesh mesh = pair.Key;
          MeshWithMaterialRenderer renderer = pair.Value.GetComponent<MeshWithMaterialRenderer>();
          mesh.offset = renderer.GetPositionInModelSpace();
          mesh.rotation = renderer.GetOrientationInModelSpace();
          meshes.Add(mesh);
        }
      } else {
        // Get the selected or hovered meshes.
        IEnumerable<int> meshIds = selector.SelectedOrHoveredMeshes();
        meshes = new List<MMesh>(meshIds.Count());
        foreach (int meshId in meshIds) {
          MMesh mesh = model.GetMesh(meshId);
          meshes.Add(mesh);
        }
      }

      return meshes;
    }

    /// <summary>
    ///   Scales all held or hovered meshes by expanding or contracting their vertices relative to the center of the
    ///   mesh.
    /// </summary>
    private void ScaleMeshes(bool scaleUp, int numSteps) {
      if (heldMeshes != null && heldMeshes.IsFilling) {
        return;
      }

      if ((scaleUp && !PeltzerMain.Instance.restrictionManager.touchpadUpAllowed)
        || (!scaleUp && !PeltzerMain.Instance.restrictionManager.touchpadDownAllowed)) {
        return;
      }

      scaleType = scaleUp ? ScaleType.SCALE_UP : ScaleType.SCALE_DOWN;

      List<MMesh> meshesToScale = GetMeshesToFlipOrScale();
      List<int> originallySelectedMeshIds = new List<int>(selector.selectedMeshes);

      float scaleFactor;

      // Abort if we have no meshes to scale or if we failed to compute the scale factor.
      if (meshesToScale.Count == 0 ||
          !CalculateGridFriendlyScaleFactor(meshesToScale, scaleUp, numSteps, out scaleFactor)) {
        return;
      }

      // Keep track of the fact that this move operation has scaled meshes.
      modifiedAnyMeshesDuringMove = true;

      if (heldMeshes != null) {
        // Try and scale the meshes as they are.
        if (!Scaler.TryScalingMeshes(meshesToScale, scaleFactor)) {
          audioLibrary.PlayClip(audioLibrary.errorSound);
          return;
        }

        // If we were moving before, we should still be moving now. We take the simplest approach of destroying old
        // previews and generating new ones, rather than trying to move and scale the previews in addition to the
        // meshes.
        if (currentMoveType == MoveType.MOVE) {
          heldMeshes.DestroyPreviews();
          heldMeshes.SetupWithNoCloneOrCache(meshesToScale, peltzerController.LastPositionModel,
            peltzerController, worldSpace);
          foreach (HeldMeshes.HeldMesh heldMesh in heldMeshes.heldMeshes) {
            model.ClaimMesh(heldMesh.Mesh.id, this);
          }
        } else {
          heldMeshes.DestroyPreviews();
          heldMeshes.SetupWithNoCloneOrCache(meshesToScale, peltzerController.LastPositionModel,
            peltzerController, worldSpace);
        }
        audioLibrary.PlayClip(scaleUp ? audioLibrary.incrementSound : audioLibrary.decrementSound);
      } else {
        // If we are not currently moving, we'll actually manipulate the model.

        // Try and scale clones of the meshes, abort on error.
        List<MMesh> clonedMeshes = new List<MMesh>(meshesToScale.Count);
        foreach (MMesh mesh in meshesToScale) {
          clonedMeshes.Add(mesh.Clone());
        }

        if (!Scaler.TryScalingMeshes(clonedMeshes, scaleFactor)) {
          audioLibrary.PlayClip(audioLibrary.errorSound);
          return;
        }

        // First check that every operation is valid.
        foreach (MMesh mesh in clonedMeshes) {
          if (!model.CanAddMesh(mesh)) {
            audioLibrary.PlayClip(audioLibrary.errorSound);
            return;
          }
        }

        List<Command> commands = new List<Command>();
        foreach (MMesh mesh in clonedMeshes) {
          // Claim to stop any other tool from previewing
          model.ClaimMesh(mesh.id, this);
          commands.Add(new ReplaceMeshCommand(mesh.id, mesh));
          model.RelinquishMesh(mesh.id, this);
        }
        model.ApplyCommand(new CompositeCommand(commands));
        peltzerController.TriggerHapticFeedback();

        // Restore the original selection, if there was one.
        foreach (MMesh mesh in clonedMeshes) {
          if (originallySelectedMeshIds.Contains(mesh.id)) {
            selector.SelectMesh(mesh.id);
          }
        }
      }
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
  }
}
