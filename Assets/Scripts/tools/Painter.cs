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

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   Tool for painting meshes and faces.
  /// </summary>
  public class Painter : MonoBehaviour {
    public ControllerMain controllerMain;
    /// <summary>
    ///   A reference to a controller capable of issuing paint commands.
    /// </summary>
    private PeltzerController peltzerController;
    /// <summary>
    ///   A reference to the overall model being built.
    /// </summary>
    private Model model;
    /// <summary>
    ///   Selector for detecting which item is hovered.
    /// </summary>
    private Selector selector;
    /// <summary>
    /// Library for playing sounds.
    /// </summary>
    private AudioLibrary audioLibrary;
    /// <summary>
    /// Whether we are currently painting all hovered objects.
    /// </summary>
    private bool isPainting;
    /// <summary>
    /// When we last made a noise and buzzed because of a paint.
    /// </summary>
    private float timeLastPaintFeedbackPlayed;
    /// <summary>
    /// Leave some time between playing paint feedback.
    /// </summary>
    private const float INTERVAL_BETWEEN_PAINT_FEEDBACKS = 0.5f;
    /// <summary>
    /// Already-painted meshes, never paint the same one the same colour twice in the same operation.
    /// </summary>
    private HashSet<int> seenMeshes = new HashSet<int>();
    /// <summary>
    /// Already-painted meshes and faces, never paint the same one the same colour twice in the same operation.
    /// </summary>
    private Dictionary<int, HashSet<int>> seenMeshesAndFaces = new Dictionary<int, HashSet<int>>();
    /// <summary>
    ///   A pre-allocated dictionary of properties by face, used to avoid constructor overhead, initialized to a 
    /// </summary>
    private Dictionary<int, FaceProperties> propsByFace = new Dictionary<int, FaceProperties>(MMesh.MAX_FACES);
    /// <summary>
    ///   The FaceProperties for any face painted by the current tool.
    /// </summary>
    private FaceProperties paintedFaceProperties;
    /// <summary>
    ///   A list of commands to send. 100 commands should be enough for any single update.
    /// </summary>
    private List<Command> paintCommands = new List<Command>(100);
    /// <summary>
    /// Keep track of material changes.
    /// </summary>
    private int lastMaterial = -1;
    /// <summary>
    /// Whether we have shown the snap tooltip for this tool yet. (Show only once because there are no direct
    /// snapping behaviors for Painter and Deleter).
    /// </summary>
    private bool snapTooltipShown = false;

    // All swatches on the colour palette, such that we can play an animation when Painter is first selected.
    private ChangeMaterialMenuItem[] allColourSwatches;

    public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController, Selector selector,
      AudioLibrary audioLibrary) {
      this.model = model;
      this.controllerMain = controllerMain;
      this.peltzerController = peltzerController;
      this.selector = selector;
      this.audioLibrary = audioLibrary;

      controllerMain.ControllerActionHandler += ControllerEventHandler;
      peltzerController.ModeChangedHandler += ModeChangedHandler;

      allColourSwatches = PeltzerMain.Instance.paletteController.transform.GetComponentsInChildren<ChangeMaterialMenuItem>(true);
    }

    public void Update() {
      if (!PeltzerController.AcquireIfNecessary(ref peltzerController) ||
          (peltzerController.mode != ControllerMode.paintFace &&
          peltzerController.mode != ControllerMode.paintMesh)) {
        return;
      }

      // Update the material if the user switched colors.
      if (lastMaterial != peltzerController.currentMaterial) {
        seenMeshes.Clear();
        seenMeshesAndFaces.Clear();
        lastMaterial = peltzerController.currentMaterial;
        paintedFaceProperties = new FaceProperties(peltzerController.currentMaterial);
      }

      // Update the position of the selector.
      // Note that we use MESHES_ONLY_IGNORE_GROUPS because, specifically for the Paint tool, we don't want mesh
      // groups to be honored (so the user can paint individual meshes in groups without ungrouping).
      if (peltzerController.mode == ControllerMode.paintMesh) {
        selector.SelectMeshAtPosition(peltzerController.LastPositionModel, Selector.MESHES_ONLY_IGNORE_GROUPS);
      }
      else {
        selector.SelectAtPosition(peltzerController.LastPositionModel, Selector.FACES_ONLY);
      }

      if (isPainting) {
        if (peltzerController.mode == ControllerMode.paintFace && selector.hoverFace != null) {
          PaintSelectedFace(selector.hoverFace);
        }
        else if (peltzerController.mode == ControllerMode.paintMesh && selector.hoverMeshes.Count > 0) {
          PaintSelectedMeshes();
        }
      } else {
        if (peltzerController.mode == ControllerMode.paintFace && selector.hoverFace != null) {
          PeltzerMain.Instance.highlightUtils.SetFaceStyleToPaint(selector.hoverFace, selector.selectorPosition,
            MaterialRegistry.GetMaterialAndColorById(peltzerController.currentMaterial).color);
        } else if (peltzerController.mode == ControllerMode.paintMesh && selector.hoverMeshes != null) {
          foreach (int meshId in selector.hoverMeshes) {
            PeltzerMain.Instance.highlightUtils.SetMeshStyleToPaint(meshId);
          }
        }
      }
    }

    private void ModeChangedHandler(ControllerMode oldMode, ControllerMode newMode) {
      if (oldMode == ControllerMode.paintFace || oldMode == ControllerMode.paintMesh) {
        UnsetAllHoverTooltips();
      }

      if ((newMode == ControllerMode.paintFace && oldMode != ControllerMode.paintMesh) || 
        (newMode == ControllerMode.paintMesh && oldMode != ControllerMode.paintFace)) {
        if (!PeltzerMain.Instance.HasEverChangedColor || PeltzerMain.Instance.tutorialManager.TutorialOccurring()) {
          StartFullRipple();
        }
        lastMaterial = peltzerController.currentMaterial;
      }
    }

    /// <summary>
    ///  Generates the full ripple effect by triggering the ripple for each individual color swatch.
    /// </summary>
    public void StartFullRipple() {
      foreach (ChangeMaterialMenuItem changeMaterialMenuItem in allColourSwatches) {
        changeMaterialMenuItem.StartRipple();
      }
    }

    /// <summary>
    ///   Makes only the supplied tooltip visible and ensures the others are off.
    /// </summary>
    /// <param name="tooltip">The tooltip text to activate.</param>
    /// <param name="state">The hover state.</param>
    private void SetHoverTooltip(GameObject tooltip, TouchpadHoverState state, TouchpadOverlay currentOverlay) {
      if (!tooltip.activeSelf) {
        UnsetAllHoverTooltips();
        if (currentOverlay != TouchpadOverlay.PAINT) {
          return;
        }
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
      peltzerController.controllerGeometry.paintTooltipLeft.SetActive(false);
      peltzerController.controllerGeometry.paintTooltipRight.SetActive(false);
      peltzerController.SetTouchpadHoverTexture(TouchpadHoverState.NONE);
    }

    public void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (peltzerController.mode != ControllerMode.paintFace && peltzerController.mode != ControllerMode.paintMesh)
        return;

      if (IsStartPaintingEvent(args)) {
        StartPainting();
      } else if (IsFinishPaintingEvent(args)) {
        ClearState();
      } else if (IsSetLeftHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed) {
        SetHoverTooltip(
          peltzerController.controllerGeometry.paintTooltipLeft, TouchpadHoverState.LEFT, args.TouchpadOverlay);
      } else if (IsSetRightHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadRightAllowed) {
        SetHoverTooltip(
          peltzerController.controllerGeometry.paintTooltipRight, TouchpadHoverState.RIGHT, args.TouchpadOverlay);
      } else if (IsSetSnapTriggerTooltipEvent(args) && !snapTooltipShown) {
        // Show tool tip about the snap trigger.
        PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
        snapTooltipShown = true;
      } else if (IsUnsetAllHoverTooltipsEvent(args)) {
        UnsetAllHoverTooltips();
      }
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'start painting' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a start painting event, false otherwise.</returns>
    private bool IsStartPaintingEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN;
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'stop painting' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a stop painting event, false otherwise.</returns>
    private bool IsFinishPaintingEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP;
    }

    public bool IsPainting() {
      return isPainting;
    }

    private static bool IsSetSnapTriggerTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.LIGHT_DOWN;
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

    private void StartPainting() {
      // Just create faceProperties once: technically this means its shared between all faces that
      // get painted but given that it's immutable, we're safe.
      paintedFaceProperties = new FaceProperties(peltzerController.currentMaterial);

      isPainting = true;
    }

    public void ClearState() {
      selector.DeselectAll();

      // Forget the list of 'already-painted' faces.
      seenMeshes.Clear();
      seenMeshesAndFaces.Clear();

      isPainting = false;
    }

    // Test method.
    public void TriggerUpdateForTest() {
      Update();
    }

    private void PaintSelectedMeshes() {
      // Get all the hovered meshes.
      IEnumerable<int> hoveredMeshes = selector.hoverMeshes;

      // Set the same face property for each face.
      foreach (int meshId in hoveredMeshes) {
        // Never paint the same mesh the same colour twice in one operation.
        if (seenMeshes.Add(meshId)) {
          paintCommands.Add(new ChangeFacePropertiesCommand(meshId, paintedFaceProperties));
        }
      }

      if (paintCommands.Count > 0) {
        Command compositeCommand = new CompositeCommand(paintCommands);
        model.ApplyCommand(compositeCommand);
        paintCommands.Clear();
        PlayFeedback();

      }
    }

    private void PaintSelectedFace(FaceKey faceKey) {
      int meshId = faceKey.meshId;
      int faceId = faceKey.faceId;

      HashSet<int> seenFaces;
      if (!seenMeshesAndFaces.TryGetValue(meshId, out seenFaces)) {
        seenFaces = new HashSet<int>();
        seenMeshesAndFaces[meshId] = seenFaces;
      }
      // Never paint the same face the same colour twice in the same operation.
      if (seenFaces.Add(faceId)) {
        model.ApplyCommand(new ChangeFacePropertiesCommand(meshId,
          new Dictionary<int, FaceProperties>() { { faceId, paintedFaceProperties } }));
        PlayFeedback();

      }
    }

    /// <summary>
    ///   Give haptic and audio feedback to the user about their paint operation, but only if it's been a reasonable
    ///   amount of time since feedback was last given.
    /// </summary>
    private void PlayFeedback() {
      if (Time.time - timeLastPaintFeedbackPlayed > INTERVAL_BETWEEN_PAINT_FEEDBACKS) {
        timeLastPaintFeedbackPlayed = Time.time;
        audioLibrary.PlayClip(audioLibrary.paintSound);
        peltzerController.TriggerHapticFeedback();
      }
    }
  }
}
