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
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   A tool responsible for extruding faces of meshes in the scene. Implemented as MonoBehaviour so we can have an
  ///   Update() loop.
  ///   An extrusion is composed of grabbing a face, (potentially) pulling out the face, and releasing. The face
  ///   itself is moved, and new faces are created to link the moved face back to the mesh.
  ///  
  /// </summary>
  public class Extruder : MonoBehaviour {
    // TODO(bug): refactor this to use background mesh validation like Reshaper.

    public ControllerMain controllerMain;
    /// <summary>
    ///   A reference to a controller capable of issuing extrude commands.
    /// </summary>
    private PeltzerController peltzerController;
    /// <summary>
    ///   A reference to the overall model being built.
    /// </summary>
    private Model model;
    /// <summary>
    ///   Selector for detecting which face is hovered or selected.
    /// </summary>
    private Selector selector;
    /// <summary>
    ///   Library for playing sounds.
    /// </summary>
    private AudioLibrary audioLibrary;
    /// <summary>
    ///   The controller position in model space where the extrusion began.
    /// </summary>
    private Vector3 extrusionBeginPosition;
    /// <summary>
    ///   The controller orientation in model space where the extrusion began.
    /// </summary>
    private Quaternion extrusionBeginOrientation;
    /// <summary>
    ///   In-flight extrusions.
    /// </summary>
    private List<ExtrusionOperation> extrusions = new List<ExtrusionOperation>();
    /// <summary>
    ///   Temporary re-paint commands for in-flight extrusions.
    /// </summary>
    private List<Command> temporaryHeldFaceMaterialCommands = new List<Command>();

    private WorldSpace worldSpace;

    // Detection for trigger down & straight back up, vs trigger down and hold -- either of which 
    // begins an extrusion.
    private bool triggerUpToRelease;
    private float triggerDownTime;
    private bool waitingToDetermineReleaseType;
    
    /// <summary>
    /// Used to determine if we should show the snap tooltip or not. Don't show the tooltip if the user already
    /// showed enough knowledge of how to snap.
    /// </summary>
    private int completedSnaps = 0;
    private const int SNAP_KNOW_HOW_COUNT = 3;

    /// <summary>
    ///   Do we snap to the face normal and the grid?
    /// </summary>
    private bool isSnapping = false;

    /// <summary>
    ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
    ///   As such, this setup method must be called before the tool is used for it to have a valid state.
    /// </summary>
    public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController,
      PaletteController paletteController, Selector selector, AudioLibrary audioLibrary, WorldSpace worldSpace) {
      this.model = model;
      this.controllerMain = controllerMain;
      this.peltzerController = peltzerController;
      this.selector = selector;
      this.audioLibrary = audioLibrary;
      this.worldSpace = worldSpace;
      controllerMain.ControllerActionHandler += ControllerEventHandler;
    }

    private ExtrusionOperation.ExtrusionParams BuildExtrusionParams() {
      // Note: ExtrusionParams is a struct, so this is stack allocated and doesn't generate garbage.
      ExtrusionOperation.ExtrusionParams extrusionParams = new ExtrusionOperation.ExtrusionParams();
      // If we are snapping or block mode is on, lock extrusion to the face's normal.
      extrusionParams.lockToNormal = isSnapping || peltzerController.isBlockMode;
      extrusionParams.translationModel = peltzerController.LastPositionModel - extrusionBeginPosition;
      extrusionParams.rotationPivotModel = peltzerController.LastPositionModel;
      extrusionParams.rotationModel =
        peltzerController.LastRotationModel * Quaternion.Inverse(extrusionBeginOrientation);
      return extrusionParams;
    }

    /// <summary>
    ///   Each frame, if a mesh is currently held, update its position in world-space relative
    ///   to its original position, and the delta between the controller's position at world-start
    ///   and the controller's current position.
    /// </summary>
    private void Update() {
      if (!PeltzerController.AcquireIfNecessary(ref peltzerController) ||
          peltzerController.mode != ControllerMode.extrude)
        return;

      if (extrusions.Count == 0) {
        // Update the position of the selector if there aren't any extrusions yet so the selector can know what to
        // extrude and to render the hover highlight.
        selector.SelectAtPosition(peltzerController.LastPositionModel, Selector.FACES_ONLY);
        if (selector.hoverFace != null) {
          FaceKey hoveredFaceKey = selector.hoverFace;
          MMesh hoveredMesh = model.GetMesh(hoveredFaceKey.meshId);
          Face hoveredFace = hoveredMesh.GetFace(hoveredFaceKey.faceId);
          int materialId = hoveredFace.properties.materialId;
          if (materialId == MaterialRegistry.GEM_ID || materialId == MaterialRegistry.GLASS_ID) {
            materialId = MaterialRegistry.BLACK_ID;
          }
          PeltzerMain.Instance.highlightUtils.SetFaceStyleToExtrude(selector.hoverFace, selector.selectorPosition,
            MaterialRegistry.GetMaterialColorById(materialId));
        }
        if (selector.selectedFaces != null) {
          foreach (FaceKey faceKey in selector.selectedFaces) {
            MMesh selectedMesh = model.GetMesh(faceKey.meshId);
            Face selectedFace = selectedMesh.GetFace(faceKey.faceId);
            int materialId = selectedFace.properties.materialId;
            if (materialId == MaterialRegistry.GEM_ID || materialId == MaterialRegistry.GLASS_ID) {
              materialId = MaterialRegistry.BLACK_ID;
            }
            PeltzerMain.Instance.highlightUtils.SetFaceStyleToExtrude(faceKey, selector.selectorPosition,
              MaterialRegistry.GetMaterialColorById(materialId));
          }
        }
      }
      else {
        if (waitingToDetermineReleaseType && Time.time - triggerDownTime > PeltzerController.SINGLE_CLICK_THRESHOLD) {
          waitingToDetermineReleaseType = false;
          triggerUpToRelease = true;
        }

        ExtrusionOperation.ExtrusionParams extrusionParams = BuildExtrusionParams();
        foreach (ExtrusionOperation operation in extrusions) {
          operation.UpdateExtrudeGuide(extrusionParams);
        }
      }
    }

    private void LateUpdate() {
      foreach(ExtrusionOperation extrusion in extrusions) {
        extrusion.Render();
      }
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
      peltzerController.controllerGeometry.modifyTooltipUp.SetActive(false);
      peltzerController.controllerGeometry.modifyTooltipLeft.SetActive(false);
      peltzerController.controllerGeometry.modifyTooltipRight.SetActive(false);
      peltzerController.controllerGeometry.resizeDownTooltip.SetActive(false);
      peltzerController.controllerGeometry.resizeUpTooltip.SetActive(false);
      peltzerController.SetTouchpadHoverTexture(TouchpadHoverState.NONE);
    }

    private bool IsBeginOperationEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN
        && extrusions.Count == 0;
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
        && extrusions.Count() > 0;
    }

    private static bool IsEnlargeFaceEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && args.TouchpadLocation == TouchpadLocation.TOP
        && args.TouchpadOverlay == TouchpadOverlay.RESIZE;
    }

    private static bool IsShrinkFaceEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && args.TouchpadLocation == TouchpadLocation.BOTTOM
        && args.TouchpadOverlay == TouchpadOverlay.RESIZE;
    }

    private static bool IsStartSnapEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN;
    }

    private static bool IsEndSnapEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP;
    }

    // Touchpad Hover
    private bool IsSetUpHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.TOP;
    }

    private bool IsSetDownHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.BOTTOM;
    }

    private bool IsSetLeftHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.LEFT;
    }

    private bool IsSetRightHoverTooltipEvent(ControllerEventArgs args) {
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
    ///   Grab all hovered/selected faces for extrusion, if any.
    /// </summary>
    public void TryGrabbingMesh() {
      IEnumerable<FaceKey> selectedFaces = selector.SelectedOrHoveredFaces();
      if (selectedFaces.Count() == 0) {
        return;
      }

      // Ensure we're not multi-selecting now.
      selector.EndMultiSelection();

      // Set up the tools state for extrusion.
      peltzerController.HideTooltips();
      peltzerController.HideModifyOverlays();
      peltzerController.ChangeTouchpadOverlay(TouchpadOverlay.RESIZE);
      peltzerController.controllerGeometry.modifyTooltips.SetActive(true);
      extrusionBeginPosition = peltzerController.LastPositionModel;
      extrusionBeginOrientation = peltzerController.LastRotationModel;

      // We will make held faces transparent for the duration of the operation. We do so by directly modifying the
      // meshes in the model, such that ReMesher is correctly updated and we can correctly undo this once the
      // operation is complete.
      Dictionary<int, Dictionary<int, FaceProperties>> newFaceProperties =
        new Dictionary<int, Dictionary<int, FaceProperties>>();

      // Set up an extrusion per face.
      foreach (FaceKey faceKey in selectedFaces) {
        MMesh selectedMesh = model.GetMesh(faceKey.meshId);
        Face selectedFace = selectedMesh.GetFace(faceKey.faceId);
        ExtrusionOperation newOperation = new ExtrusionOperation(worldSpace, selectedMesh, selectedFace);
        extrusions.Add(newOperation);
        if (!newFaceProperties.ContainsKey(faceKey.meshId)) {
          newFaceProperties.Add(faceKey.meshId, new Dictionary<int, FaceProperties>());
        }
        newFaceProperties[faceKey.meshId].Add(faceKey.faceId, new FaceProperties(
           MaterialRegistry.GLASS_ID));
      }
      foreach (KeyValuePair<int, Dictionary<int, FaceProperties>> newFacePropertySet in newFaceProperties) {
        ChangeFacePropertiesCommand changeFacePropertiesCommand =
          new ChangeFacePropertiesCommand(newFacePropertySet.Key, newFacePropertySet.Value);
        temporaryHeldFaceMaterialCommands.Add(changeFacePropertiesCommand);
        changeFacePropertiesCommand.ApplyToModel(model);
      }

      // Play feedback.
      audioLibrary.PlayClip(audioLibrary.grabMeshPartSound);
      PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();

      // Deselect everything.
      selector.DeselectAll();
      return;
    }

    /// <summary>
    ///   Undoes the hack of making the selected faces transparent.
    /// </summary>
    private void UndoTemporaryHeldFaceMaterialCommands() {
      foreach (Command command in temporaryHeldFaceMaterialCommands) {
        command.GetUndoCommand(model).ApplyToModel(model);
      }
      temporaryHeldFaceMaterialCommands.Clear();
    }

    /// <summary>
    ///   Finalize extrusions and add the new faces to the mesh.
    /// </summary>
    private void ReleaseMesh() {
      peltzerController.ChangeTouchpadOverlay(TouchpadOverlay.MODIFY);
      peltzerController.ShowModifyOverlays();
      peltzerController.ShowTooltips();

      List<Command> commands = new List<Command>();
      Dictionary<int, MMesh> modifiedMeshes = new Dictionary<int, MMesh>();
      Dictionary<int, HashSet<Vertex>> newVerticesInModifiedMeshes = new Dictionary<int, HashSet<Vertex>>();
      ExtrusionOperation.ExtrusionParams extrusionParams = BuildExtrusionParams();

      foreach (ExtrusionOperation extrusion in extrusions) {
        if (modifiedMeshes.ContainsKey(extrusion.mesh.id)) {
          HashSet<Vertex> newVertices = newVerticesInModifiedMeshes[extrusion.mesh.id];
          modifiedMeshes[extrusion.mesh.id] = extrusion.DoExtrusion(modifiedMeshes[extrusion.mesh.id],
            extrusionParams, ref newVertices);
        } else {
          MMesh clonedMesh = extrusion.mesh.Clone();
          HashSet<Vertex> newVertices = new HashSet<Vertex>();
          modifiedMeshes.Add(extrusion.mesh.id, extrusion.DoExtrusion(clonedMesh, extrusionParams, ref newVertices));
          newVerticesInModifiedMeshes.Add(extrusion.mesh.id, newVertices);
        }
      }

      foreach (KeyValuePair<int, MMesh> newMeshPair in modifiedMeshes) {
        int meshId = newMeshPair.Key;
        MMesh newMesh = newMeshPair.Value;
        newMesh.RecalcBounds();
        List<Vertex> updatedVerts = new List<Vertex>(newVerticesInModifiedMeshes[meshId]);
        MeshFixer.FixMutatedMesh(model.GetMesh(newMesh.id), newMesh, new HashSet<int>(updatedVerts.Select(v => v.id)),
           /* splitNonCoplanarFaces */ true, /* mergeAdjacentCoplanarFaces*/ false);
        
        HashSet<int> updatedVertIds = new HashSet<int>();
        for (int i = 0; i < updatedVerts.Count; i++) {
          updatedVertIds.Add(updatedVerts[i].id);
        }
        
        bool isValidMesh = MeshValidator.IsValidMesh(newMesh, updatedVertIds);
        if (isValidMesh && model.CanAddMesh(newMesh)) {
          commands.Add(new ReplaceMeshCommand(meshId, newMesh));
        } else {
          // If any new mesh is invalid, abort everything.
          audioLibrary.PlayClip(audioLibrary.errorSound);
          peltzerController.TriggerHapticFeedback();
          UndoTemporaryHeldFaceMaterialCommands();
          return;
        }
      }

      // Expire the temporary held face commands, because the originally-held faces have now been removed 
      // following a successful extrusion.
      temporaryHeldFaceMaterialCommands.Clear();
      model.ApplyCommand(new CompositeCommand(commands));
      audioLibrary.PlayClip(audioLibrary.releaseMeshSound);
      PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
      PeltzerMain.Instance.extrudesCompleted++;
    }

    /// <summary>
    ///   An event handler that listens for controller input and delegates accordingly.
    /// </summary>
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (peltzerController.mode != ControllerMode.extrude)
        return;

      if (IsBeginOperationEvent(args)) {
        // If we are about to operate on selected faces, ensure the click is near those faces.
        if (selector.selectedFaces.Count > 0) {
          if (!selector.ClickIsWithinCurrentSelection(peltzerController.LastPositionModel)) {
            return;
          }
        }
        triggerUpToRelease = false;
        waitingToDetermineReleaseType = true;
        triggerDownTime = Time.time;
        TryGrabbingMesh();
      } else if (IsCompleteSingleClickEvent(args)) {
        waitingToDetermineReleaseType = false;
        triggerUpToRelease = false;
      } else if (IsReleaseEvent(args)) {
        if (isSnapping) {
          // We snapped while modifying, so we have learned a bit more about snapping.
          completedSnaps++;
        }
        ReleaseMesh();
        ClearState();
      } else if (IsEnlargeFaceEvent(args) && extrusions.Count > 0) {
        extrusions.ForEach(e => e.EnlargeExtrusionFace());
      } else if (IsShrinkFaceEvent(args) && extrusions.Count > 0) {
        extrusions.ForEach(e => e.ShrinkExtrusionFace());
      } else if (IsStartSnapEvent(args) && !peltzerController.isBlockMode) {
        isSnapping = true;
        if (completedSnaps < SNAP_KNOW_HOW_COUNT) {
          PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
        }
        PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.alignSound);
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        PeltzerMain.Instance.snappedInExtruder = true;
      } else if (IsEndSnapEvent(args) && !peltzerController.isBlockMode) {
        isSnapping = false;
        if (IsExtrudingFace()) {
          // We snapped while modifying, so we have learned a bit more about snapping.
          completedSnaps++;
        }
        PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
      } else if (IsSetUpHoverTooltipEvent(args) && !IsExtrudingFace()
        && PeltzerMain.Instance.restrictionManager.touchpadUpAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.modifyTooltipUp, TouchpadHoverState.UP);
      } else if (IsSetUpHoverTooltipEvent(args) && IsExtrudingFace()) {
        SetHoverTooltip(peltzerController.controllerGeometry.resizeUpTooltip, TouchpadHoverState.RESIZE_UP);
      } else if (IsSetDownHoverTooltipEvent(args) && IsExtrudingFace()) {
        SetHoverTooltip(peltzerController.controllerGeometry.resizeDownTooltip, TouchpadHoverState.RESIZE_DOWN);
      } else if (IsSetLeftHoverTooltipEvent(args) && !IsExtrudingFace()
        && PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.modifyTooltipLeft, TouchpadHoverState.LEFT);
      } else if (IsSetRightHoverTooltipEvent(args) && !IsExtrudingFace()
        && PeltzerMain.Instance.restrictionManager.touchpadRightAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.modifyTooltipRight, TouchpadHoverState.RIGHT);
      } else if (IsUnsetAllHoverTooltipsEvent(args)) {
        UnsetAllHoverTooltips();
      }
    }

    public bool IsExtrudingFace() {
      return extrusions.Count > 0;
    }

    public void ClearState() {
      foreach (ExtrusionOperation extrusion in extrusions) {
        extrusion.ClearExtrusionGuide();
      }
      waitingToDetermineReleaseType = false;
      extrusions.Clear();
      // Deselect everything.
      selector.DeselectAll();
    }
  }
}
