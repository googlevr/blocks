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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   A tool responsible for reshaping meshes in the scene. Implemented as MonoBehaviour
  ///   so we can have an Update() loop.
  ///
  ///   A reshape consists of moving vertices, edges or faces of a mesh. Reshape operations are always done in such
  ///   a way that the resulting mesh remains valid. If the user attempts to reshape the mesh in an invalid way, we
  ///   retain the last valid state of the mesh, but show the outline of the invalid mesh to the user to help them
  ///   understand that they are being geometrically unreasonable and help guide them back to mathematical sanity.
  /// </summary>
  public class Reshaper : MonoBehaviour, IMeshRenderOwner {
    /// <summary>
    ///   The factor by which meshes should be scaled.
    /// </summary>
    private const float SCALE_FACTOR = 1.5f;
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
    ///   The spatial index to the model.
    /// </summary>
    private SpatialIndex spatialIndex;
    /// <summary>
    /// Selector for detecting which item is hovered or selected.
    /// </summary>
    private Selector selector;
    /// <summary>
    /// Library for playing sounds.
    /// </summary>
    private AudioLibrary audioLibrary;
    /// <summary>
    ///   A cache of Mesh representations.
    /// </summary>
    private MeshRepresentationCache meshRepresentationCache;
    /// <summary>
    ///   The controller position in model space when the vertices or mesh(es) was (were) grabbed.
    /// </summary>
    private Vector3 moveStartPosition;
    /// <summary>
    /// The controller orientation in model space when the vertices or mesh(es) was (were) grabbed.
    /// </summary>
    private Quaternion reshapeBeginOrientation;
    /// <summary>
    ///   The previews of the meshes that the reshaper tool has grabbed and is reshaping, and
    ///   the Unity GameObjects used to render them to the scene.
    /// </summary>
    private Dictionary<MMesh, GameObject> grabbedMeshesAndPreviews = new Dictionary<MMesh, GameObject>();
    /// <summary>
    ///   Meshes that the reshaper tool has grabbed and is reshaping, with all of their static faces removed,
    ///   and GameObject previews of such.
    /// </summary>
    private Dictionary<MMesh, GameObject> badMeshesAndPreviews = new Dictionary<MMesh, GameObject>();
    /// <summary>
    /// Maintained so we can send some signals to analytics for how many faces people are moving.
    /// </summary>
    private List<FaceKey> grabbedFaces = new List<FaceKey>();
    /// <summary>
    /// Maintained so we can send some signals to analytics for how many edges people are moving.
    /// </summary>
    private List<EdgeKey> grabbedEdges = new List<EdgeKey>();
    /// <summary>
    ///   All unique vertices that are parts of selected edges.
    /// </summary>
    private HashSet<VertexKey> allVertices = new HashSet<VertexKey>();
    /// <summary>
    /// Stores a relation from a vertex back to a face, so we can determine the normal of the face.
    /// </summary>
    private Dictionary<VertexKey, FaceKey> vertexToFace = new Dictionary<VertexKey, FaceKey>();

    private WorldSpace worldSpace;

    private bool isReshaping = false;
    private bool startedReshapingThisFrame = false;

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
    /// These meshes are the result of mutating the original meshes in the most naive way possible to reflect
    /// what the user did (just moving the vertices to where the user said they wanted them).
    /// As a result, these will often be invalid, have non-coplanar faces and many other such embarassing
    /// defects.
    ///
    /// We use these to represent what the user is trying to do, but this won't ultimately be the final result of the
    /// operation. Instead, these naively mutated meshes are fed to the BackgroundMeshValidator, which will
    /// do the hard work (in a separate thread) to figure out how to convert our naive mutation into a valid mesh
    /// with all the necessary triangulations, vertex deduping, and other niceties of civilized geometries.
    ///
    /// We may BRIEFLY use these meshes for display while BackgroundMeshValidator hasn't given us our first
    /// "known good state". But as soon as we have a "known valid" state, that's that we use for display
    /// instead of this.
    /// </summary>
    private Dictionary<int, MMesh> naivelyMutatedMeshes = new Dictionary<int, MMesh>();

    private Dictionary<int, Dictionary<int, Vertex>> movesByMesh = new Dictionary<int, Dictionary<int, Vertex>>();

    // Background validator. During a reshape operation, it evaluates the validity of our meshes in the background.
    private BackgroundMeshValidator backgroundValidator;

    // State of current operation for real-time validation.
    private bool errorFeedbackGivenForCurrentOperation = false;

    /// <summary>
    ///   If we are snapping face moves to normals.
    /// </summary>
    private bool isSnapping = false;

    /// <summary>
    ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
    ///   As such, this setup method must be called before the tool is used for it to have a valid state.
    /// </summary>
    public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController,
      PaletteController paletteController, Selector selector, AudioLibrary audioLibrary, WorldSpace worldSpace,
      SpatialIndex spatialIndex, MeshRepresentationCache meshRepresentationCache) {
      this.model = model;
      this.controllerMain = controllerMain;
      this.peltzerController = peltzerController;
      this.selector = selector;
      this.audioLibrary = audioLibrary;
      this.worldSpace = worldSpace;
      this.spatialIndex = spatialIndex;
      this.meshRepresentationCache = meshRepresentationCache;
      controllerMain.ControllerActionHandler += ControllerEventHandler;
      peltzerController.ModeChangedHandler += ModeChangeEventHandler;

      backgroundValidator = new BackgroundMeshValidator(model);
      selector.TurnOnSelectIndicator();
    }

    internal struct FaceAndVertexCount {
      internal int faceCount;
      internal int vertexCount;

      internal FaceAndVertexCount(int faceCount, int vertexCount) {
        this.faceCount = faceCount;
        this.vertexCount = vertexCount;
      }
    }
    Dictionary<int, FaceAndVertexCount> meshIdToLastKnownFaceAndVertexCounts = new Dictionary<int, FaceAndVertexCount>();

    /// <summary>
    ///   Each frame, if a mesh is currently held, update its position in world-space relative
    ///   to its original position, and the delta between the controller's position at world-start
    ///   and the controller's current position.
    /// </summary>
    private void Update() {
      if (!PeltzerController.AcquireIfNecessary(ref peltzerController) || peltzerController.mode != ControllerMode.reshape) {
        return;
      }

      if (isReshaping) {
        // Wait one frame before performing the first update cycle for an in-progress reshape operation.
        // This helps avoid frame drops, given that a lot of work happens in order begin a reshape operation.
        if (startedReshapingThisFrame) {
          startedReshapingThisFrame = false;
          return;
        }

        if (waitingToDetermineReleaseType && Time.time - triggerDownTime > PeltzerController.SINGLE_CLICK_THRESHOLD) {
          waitingToDetermineReleaseType = false;
          triggerUpToRelease = true;
        }

        // Use the position of the controller to update the "naive" version of the meshes, which reflect what the
        // user is trying to do.
        UpdateNaivelyMutatedMeshes();

        // We feed the naive meshes to the background validator. The validator will try to clean it up and produce
        // a valid mesh, making it available asynchronously to us later.
        backgroundValidator.UpdateMeshes(naivelyMutatedMeshes, allVertices);

        // Note that the background validator is asynchronous, so the validity state that it reports is
        // not an immediate answer to the meshes we just provided with UpdateMeshes(). Instead, the validity
        // state is likely to reflect the state of the meshes a few frames in the past.
        BackgroundMeshValidator.Validity validity = backgroundValidator.ValidityState;

        if (validity == BackgroundMeshValidator.Validity.INVALID) {
          if (!errorFeedbackGivenForCurrentOperation) {
            // The current operation is invalid, play error...
            audioLibrary.PlayClip(audioLibrary.errorSound);
            peltzerController.TriggerHapticFeedback(
              HapticFeedback.HapticFeedbackType.FEEDBACK_2, /* durationSeconds */ 0.25f, /* strength */ 0.3f);
            errorFeedbackGivenForCurrentOperation = true;
          }

          // Show an error outline of the mesh in its current, invalid state.
          foreach (KeyValuePair<MMesh, GameObject> pair in badMeshesAndPreviews) {
            // Update the bad mesh vertices to match the naive mesh vertices. This is a neat trick: the visuals of
            // the error overlay don't need to respect the newly-split faces, so all we're effectively doing is
            // updating the positions of the already-known faces that are affected by this reshape operation.
            MMesh badMesh = pair.Key;
            GameObject preview = pair.Value;
            MMesh.GeometryOperation operation = badMesh.StartOperation();
            operation.ModifyVertices(naivelyMutatedMeshes[badMesh.id].GetVertices());
            operation.CommitWithoutRecalculation();

            // Update the preview of the bad mesh and set it to show.
            preview.SetActive(true);
            MMesh.AttachMeshToGameObject(
              worldSpace, preview, badMesh, /* updateOnly */ true, MaterialRegistry.GetReshaperErrorMaterial());
          }
        } else if (validity == BackgroundMeshValidator.Validity.VALID) {
          // The current move is valid, remove the error outlines for each mesh.
          foreach (GameObject badMeshPreview in badMeshesAndPreviews.Values) {
            badMeshPreview.SetActive(false);
          }
          errorFeedbackGivenForCurrentOperation = false;
        } else {
          // If the validity state is not VALID or INVALID, it means the validator hasn't decided yet.
          // In that case, we don't do anything.
        }

        // Update the preview of the mesh. For each mesh, if we have last good state for it (by courtesy of the
        // background validator), then we use it for display. If we don't, we use our naive meshes.
        foreach (KeyValuePair<MMesh, GameObject> pair in grabbedMeshesAndPreviews) {
          MMesh snappyPreview;
          if (validity != BackgroundMeshValidator.Validity.NOT_YET_KNOWN) {
            // Display the "last known good" state.
            Dictionary<int, MMesh> lastGoodState = backgroundValidator.GetLastValidState();
            MMesh lastGoodStateForMesh;
            lastGoodState.TryGetValue(pair.Key.id, out lastGoodStateForMesh);
            snappyPreview = lastGoodStateForMesh != null ? lastGoodStateForMesh : naivelyMutatedMeshes[pair.Key.id];
          } else {
            // Display the naive preview, as that's the best thing we have for now.
            // Hopefully BackgroundMeshValidator will catch up in a few frames and we'll have something
            // better to show.
            snappyPreview = naivelyMutatedMeshes[pair.Key.id];
          }

          // We wish to avoid re-triangulating the entire mesh to generate a preview as often as possible, which is
          // controlled by the updateOnly flag below.
          // Our current heuristic, which we expect to work in the vast majority of cases, is to check whether the face
          // and vertex count has not changed as a result of the most-recent mesh fixing operation. If they have
          // remained constant then it's likely we haven't merged any verts, split any faces, or un-done either of
          // those things (or anything else that can affect the geometry beyond vertex location).
          FaceAndVertexCount lastKnownFaceAndVertexCount = meshIdToLastKnownFaceAndVertexCounts[pair.Key.id];
          FaceAndVertexCount currentFaceAndVertexCount =
            new FaceAndVertexCount(snappyPreview.faceCount, snappyPreview.vertexCount);
          bool updateOnly = lastKnownFaceAndVertexCount.faceCount == currentFaceAndVertexCount.faceCount
            && lastKnownFaceAndVertexCount.vertexCount == currentFaceAndVertexCount.vertexCount;

          MMesh.AttachMeshToGameObject(worldSpace, pair.Value, snappyPreview, updateOnly);
          meshIdToLastKnownFaceAndVertexCounts[pair.Key.id] = currentFaceAndVertexCount;
        }
      } else {
        // Update the position of the selector.
        if (selector == null) {
          selector.TurnOnSelectIndicator();
        }
        selector.SelectAtPosition(peltzerController.LastPositionModel, Selector.FACES_EDGES_AND_VERTICES);
        selector.UpdateInactive(Selector.FACES_EDGES_AND_VERTICES);
        // Play the selection animation for newly-hovered or -selected faces.
        if (selector.hoverFace != null) {
          PeltzerMain.Instance.highlightUtils.SetFaceStyleToSelect(selector.hoverFace, selector.selectorPosition);
        }
        if (selector.selectedFaces != null) {
          foreach (FaceKey faceKey in selector.selectedFaces) {
            PeltzerMain.Instance.highlightUtils.SetFaceStyleToSelect(faceKey, selector.selectorPosition);
          }
        }
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
        if (currentOverlay == TouchpadOverlay.RESET_ZOOM) {
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
      peltzerController.controllerGeometry.modifyTooltipUp.SetActive(false);
      peltzerController.controllerGeometry.modifyTooltipLeft.SetActive(false);
      peltzerController.controllerGeometry.modifyTooltipRight.SetActive(false);
      peltzerController.controllerGeometry.resizeUpTooltip.SetActive(false);
      peltzerController.controllerGeometry.resizeDownTooltip.SetActive(false);
      peltzerController.SetTouchpadHoverTexture(TouchpadHoverState.NONE);
    }

    private bool IsBeginOperationEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN
        && !isReshaping;
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
        && isReshaping;
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
        && !isReshaping
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.TOP;
    }

    private bool IsSetDownHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && !isReshaping
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.BOTTOM;
    }

    private bool IsSetLeftHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && !isReshaping
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.LEFT;
    }

    private bool IsSetRightHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && !isReshaping
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.RIGHT;
    }

    private static bool IsUnsetAllHoverTooltipsEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.NONE;
    }

    /// <summary>
    ///   Generates a preview of a mesh with faces that the Reshaper will not modify removed.
    /// </summary>
    /// <returns></returns>
    private void GenerateBadMeshesAndPreviews() {
      badMeshesAndPreviews.Clear();

      // Find the faces that are not being modified, per mesh.
      Dictionary<int, HashSet<int>> movesByMesh = new Dictionary<int, HashSet<int>>();
      foreach (VertexKey vertexKey in allVertices) {
        HashSet<int> set;
        if (!movesByMesh.TryGetValue(vertexKey.meshId, out set)) {
          movesByMesh[vertexKey.meshId] = new HashSet<int>();
          set = movesByMesh[vertexKey.meshId];
        }
        set.Add(vertexKey.vertexId);
      }

      // Remove static faces and create a preview.
      foreach (MMesh mesh in grabbedMeshesAndPreviews.Keys) {
        // We need to clone the original mesh as we will be modifying it.
        MMesh badMesh = mesh.Clone();
        HashSet<int> badMeshVerts = movesByMesh[badMesh.id];
        MMesh.GeometryOperation operation = badMesh.StartOperation();
        foreach (int faceId in mesh.GetFaceIds()) {
          // For each face, we check to see if any of its vertices are not in the 'allVertices' being reshaped.
          // If so, that face will never move and as such we will never need an 'error' preview for it, so we
          // remove it from the 'badMesh'.
          // We don't *need* to remove the vertices, so we don't bother here.
          bool shouldRemoveFace = true;
          Face face = badMesh.GetFace(faceId);
          foreach (int vertId in face.vertexIds) {
            if (badMeshVerts.Contains(vertId)) {
              shouldRemoveFace = false;
              break;
            }
          }

          if (shouldRemoveFace) {
            operation.DeleteFace(faceId);
          }
        }
        // Face deletion doesn't change normals, so don't recalculate them.
        operation.CommitWithoutRecalculation();

        // Add the preview to the collection of previews for this operation.
        GameObject badMeshPreview = MeshHelper.GameObjectFromMMesh(worldSpace, badMesh,
          MaterialRegistry.GetReshaperErrorMaterial());
        badMeshesAndPreviews.Add(badMesh, badMeshPreview);
        badMeshPreview.SetActive(false);
      }
    }

    public void StartMove() {
      moveStartPosition = peltzerController.LastPositionModel;
      reshapeBeginOrientation = peltzerController.LastRotationModel;
      peltzerController.HideTooltips();
      peltzerController.HideModifyOverlays();

      // Create a Unity GameObject to render the meshes whilst they are being moved.
      //
      // Also add the vertices of each face to the collection of all selected vertices.
      // Maintain a reference from vertex back to face, so we can tell which normal
      // to move along if we are snapping.

      // Add all faces selected.
      IEnumerable<FaceKey> selectedFaces = selector.SelectedOrHoveredFaces();
      foreach (FaceKey faceKey in selectedFaces) {
        grabbedFaces.Add(faceKey);
        MMesh mesh = model.GetMesh(faceKey.meshId);
        Face face = mesh.GetFace(faceKey.faceId);
        if (!grabbedMeshesAndPreviews.ContainsKey(mesh)) {
          GameObject preview = meshRepresentationCache.GeneratePreview(mesh);
          grabbedMeshesAndPreviews.Add(mesh, preview);
          meshIdToLastKnownFaceAndVertexCounts.Add(mesh.id,
            new FaceAndVertexCount(mesh.faceCount, mesh.vertexCount));
          model.ClaimMesh(mesh.id, this);
        }
        foreach (int vertId in face.vertexIds) {
          VertexKey newVertexKey = new VertexKey(faceKey.meshId, vertId);
          allVertices.Add(newVertexKey);
          if (!vertexToFace.ContainsKey(newVertexKey)) {
            vertexToFace.Add(newVertexKey, faceKey);
          }
        }
      }

      // Add all edges selected.
      IEnumerable<EdgeKey> selectedEdges = selector.SelectedOrHoveredEdges();
      foreach (EdgeKey edgeKey in selectedEdges) {
        grabbedEdges.Add(edgeKey);
        MMesh mesh = model.GetMesh(edgeKey.meshId);
        if (!grabbedMeshesAndPreviews.ContainsKey(mesh)) {
          GameObject preview = meshRepresentationCache.GeneratePreview(mesh);
          grabbedMeshesAndPreviews.Add(mesh, preview);
          meshIdToLastKnownFaceAndVertexCounts.Add(mesh.id,
            new FaceAndVertexCount(mesh.faceCount, mesh.vertexCount));
          model.ClaimMesh(mesh.id, this);
        }

        VertexKey newVertexKey1 = new VertexKey(edgeKey.meshId, edgeKey.vertexId1);
        VertexKey newVertexKey2 = new VertexKey(edgeKey.meshId, edgeKey.vertexId2);
        allVertices.Add(newVertexKey1);
        allVertices.Add(newVertexKey2);
      }

      // Add all vertices selected.
      IEnumerable<VertexKey> selectedVertices = selector.SelectedOrHoveredVertices();
      foreach (VertexKey vertexKey in selectedVertices) {
        MMesh mesh = model.GetMesh(vertexKey.meshId);
        if (!grabbedMeshesAndPreviews.ContainsKey(mesh)) {
          GameObject preview = meshRepresentationCache.GeneratePreview(mesh);
          grabbedMeshesAndPreviews.Add(mesh, preview);
          meshIdToLastKnownFaceAndVertexCounts.Add(mesh.id,
            new FaceAndVertexCount(mesh.faceCount, mesh.vertexCount));
          model.ClaimMesh(mesh.id, this);
        }

        // Add each selected vertex that isn't included in a face or segment.
        if (!allVertices.Contains(vertexKey)) {
          allVertices.Add(vertexKey);
        }
      }

      // Generate the previews of 'bad meshes' -- the outline of meshes that will show in case of an error.
      GenerateBadMeshesAndPreviews();

      // Did we actually select something?
      isReshaping = allVertices.Count > 0;
      if (!isReshaping) {
        return;
      }

      // Ensure we're not multi-selecting now.
      selector.EndMultiSelection();

      // De-select everything, Reshaper will now manage state of what is being moved and the corresponding
      // preview GameObjects.
      selector.DeselectAll();

      // Set up the validation work.
      backgroundValidator.StartValidating();

      startedReshapingThisFrame = true;

      // Play some feedback.
      audioLibrary.PlayClip(audioLibrary.grabMeshPartSound);
      PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
    }

    public bool IsReshaping() {
      return isReshaping;
    }

    public bool IsReshapingFaces() {
      return isReshaping && grabbedFaces.Count > 0;
    }

    private void CompleteMove() {
      AssertOrThrow.True(isReshaping, "CompleteMove() called without isReshaping == true.");

      // First, finish any bg work.
      backgroundValidator.StopValidating();
      isReshaping = false;

      peltzerController.ShowTooltips();
      peltzerController.ShowModifyOverlays();

      bool moveErrors = false;

      // Unhide and check we have some valid state for each mesh.
      Dictionary<int, MMesh> lastGoodState = backgroundValidator.GetLastValidState();
      foreach (int meshId in naivelyMutatedMeshes.Keys) {
        if (!lastGoodState.ContainsKey(meshId)) {
          // We ended up with a mesh with no valid state from this move, abort.
          lastGoodState.Clear();
          moveErrors = true;
          break;
        }
      }

      List<Command> commands = new List<Command>();
      if (lastGoodState.Count > 0 && !moveErrors) {
        // Update each mesh.
        foreach (int meshId in naivelyMutatedMeshes.Keys) {
          // If any mesh is invalid, don't bother checking the rest.
          MMesh updatedMesh = lastGoodState[meshId];
          updatedMesh.RecalcBounds();
          if (model.CanAddMesh(updatedMesh)) {
            commands.Add(new ReplaceMeshCommand(meshId, updatedMesh));
          } else {
            moveErrors = true;
            break;
          }
        }
      }

      if (moveErrors) {
        audioLibrary.PlayClip(audioLibrary.errorSound);
        peltzerController.TriggerHapticFeedback();
      } else {
        audioLibrary.PlayClip(audioLibrary.releaseMeshSound);
        model.ApplyCommand(new CompositeCommand(commands));
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        if (grabbedFaces.Count > 0) {
          PeltzerMain.Instance.faceReshapesCompleted++;
        }
      }

      ClearState();
    }

    /// <summary>
    /// Claim responsibility for rendering a mesh from this class.
    /// This should only be called by Model, as otherwise Model's knowledge of current ownership will be incorrect.
    /// </summary>
    public int ClaimMesh(int meshId, IMeshRenderOwner fosterRenderer) {
      MMesh mesh = model.GetMesh(meshId);
      if (grabbedMeshesAndPreviews.ContainsKey(mesh)) {
        GameObject go = grabbedMeshesAndPreviews[model.GetMesh(meshId)];
        DestroyImmediate(go);
        grabbedMeshesAndPreviews.Remove(mesh);
        return meshId;
      }
      // Didn't have it, can't relinquish ownership.
      return -1;
    }

    public void ClearState() {
      ClearPreviewsAndRestoreHiddenMeshesAndSelector();
      grabbedFaces.Clear();
      grabbedEdges.Clear();
    }

    public void ClearPreviewsAndRestoreHiddenMeshesAndSelector() {
      // Clear cached copy of preview mesh.
      naivelyMutatedMeshes.Clear();

      movesByMesh.Clear();

      // Show the meshes again.
      foreach (MMesh mesh in grabbedMeshesAndPreviews.Keys) {
        model.RelinquishMesh(mesh.id, this);
        DestroyImmediate(grabbedMeshesAndPreviews[mesh]);
      }

      // Destroy all of the badmesh previews.
      foreach (GameObject gameObject in badMeshesAndPreviews.Values) {
        DestroyImmediate(gameObject);
      }

      allVertices.Clear();
      vertexToFace.Clear();
      grabbedMeshesAndPreviews.Clear();
      meshIdToLastKnownFaceAndVertexCounts.Clear();
      badMeshesAndPreviews.Clear();
      selector.DeselectAll();
    }

    /// <summary>
    ///   Updates our naive meshes, moving the vertices to reflect the the controller's movement.
    ///   This means making a copy of the mesh from the model and then applying the delta to each mesh separately.
    /// </summary>
    private void UpdateNaivelyMutatedMeshes() {
      Vector3 delta = peltzerController.LastPositionModel - moveStartPosition;

      foreach (VertexKey vertexKey in allVertices) {
        MMesh mesh;
        if (!naivelyMutatedMeshes.TryGetValue(vertexKey.meshId, out mesh)) {
          mesh = model.GetMesh(vertexKey.meshId).Clone();
          naivelyMutatedMeshes[vertexKey.meshId] = mesh;
          movesByMesh[vertexKey.meshId] = new Dictionary<int, Vertex>();
        }
        Vector3 oldLocInModelCoords = model.GetMesh(vertexKey.meshId).VertexPositionInModelCoords(vertexKey.vertexId);
        Vector3 newLocInMeshSpace;
        if (isSnapping || peltzerController.isBlockMode) {
          FaceKey faceKey;
          if (vertexToFace.TryGetValue(vertexKey, out faceKey) && mesh.HasFace(faceKey.faceId)) {
            // This vertex is part of an entire face that we are moving. In this case we don't want individual
            // vertices to snap to the grid or other vertices, as that would deform the face. Instead, we want the face
            // motion to snap to an ad-hoc grid defined by its normal.
            newLocInMeshSpace = mesh.ModelCoordsToMeshCoords(
              oldLocInModelCoords + Vector3.Project(GridUtils.SnapToGrid(delta),
              mesh.rotation * mesh.GetFace(faceKey.faceId).normal));
          } else {
            // This is an individual vertex that we're moving (not part of a selected face). So snap it to
            // other vertices and the grid in MODEL space.
            List<VertexKey> nearbyVertices = null;
            if (allVertices.Count == 1) {
              List<DistancePair<VertexKey>> nearbyVertexPairs;
              spatialIndex.FindVerticesClosestTo(
                oldLocInModelCoords + delta,
                GridUtils.GRID_SIZE,
                out nearbyVertexPairs);

              // Parse the DistancePairs returned from the SpatialIndex, we will only need the Keys.
              nearbyVertices = nearbyVertexPairs.Select(pair => pair.value).ToList();
            }

            if (nearbyVertices != null && nearbyVertices.Count > 0) {
              // Found a vertex that we should be snapping to.
              newLocInMeshSpace = mesh.ModelCoordsToMeshCoords(
                MeshMath.FindClosestVertex(nearbyVertices, oldLocInModelCoords + delta, model));
            } else {
              // We have more than one vertex held, or the vertex we're holding was not snapped to another vertex.
              // So, we move it by a snapped delta.
              Vector3 newLocModelSpace = oldLocInModelCoords + GridUtils.SnapToGrid(delta);
              // Vertex positions are expressed in MESH space, so compute the corresponding mesh space position.
              newLocInMeshSpace = mesh.ModelCoordsToMeshCoords(newLocModelSpace);
            }
          }
        } else {
          // If grid mode is not on and we are not snapping, allow for rotation of the selected face(s).

          // The point about which the face should be rotated, after the translation.
          Vector3 rotationPivotModel = peltzerController.LastPositionModel;
          // The model-space delta between the controller's current rotation and its rotation when the
          // operation began.
          Quaternion rotDelta = peltzerController.LastRotationModel * Quaternion.Inverse(reshapeBeginOrientation);

          // Move and rotate each vert by the positional and rotational delta.
          MoveAndRotateVertexFreely(vertexKey, delta, rotationPivotModel, rotDelta, out newLocInMeshSpace);
        }
        movesByMesh[vertexKey.meshId][vertexKey.vertexId] = (new Vertex(vertexKey.vertexId, newLocInMeshSpace));
      }

      // Update the vertex positions in naivelyMutatedMeshes. It's a "naive" operation because we just move
      // vertices around without caring whether the movement is valid or not. It's not our job to care about
      // this. Proper triangulation and cleanup will be done by the BackgroundMeshValidator.
      List<int> meshIds = new List<int>(naivelyMutatedMeshes.Keys);
      foreach (int meshId in meshIds) {
        MMesh mesh = naivelyMutatedMeshes[meshId];
        MMesh.GeometryOperation mutateOperation = mesh.StartOperation();
        mutateOperation.ModifyVertices(movesByMesh[mesh.id]);
        mutateOperation.Commit();
      }
    }
    
    /// <summary>
    /// Moves and rotates a given vertex by the given model-space position and rotation deltas.
    /// </summary>
    private void MoveAndRotateVertexFreely(VertexKey vertexKey, Vector3 delta, Vector3 rotationPivotModel,
      Quaternion rotationDelta, out Vector3 newLocationMeshSpace) {
      MMesh mesh = model.GetMesh(vertexKey.meshId);
      Vector3 oldLocationModelSpace = mesh.VertexPositionInModelCoords(vertexKey.vertexId);
      Vector3 newLocationModelSpace = oldLocationModelSpace + delta;

      // Rotate the point about the requested pivot.
      newLocationModelSpace = Math3d.RotatePointAroundPivot(newLocationModelSpace, rotationPivotModel,
        rotationDelta);

      newLocationMeshSpace = mesh.ModelCoordsToMeshCoords(newLocationModelSpace);
    }

    /// <summary>
    ///   Begin reshaping, if we have anything to reshape.
    /// </summary>
    private void MaybeStartOperation() {
      if (selector.SelectedOrHoveredFaces().Count() > 0
        || selector.SelectedOrHoveredEdges().Count() > 0
        || selector.SelectedOrHoveredVertices().Count() > 0) {
        StartMove();
      }
    }

    /// <summary>
    ///   An event handler that listens for controller input and delegates accordingly.
    /// </summary>
    /// <param name="sender">The sender of the controller event.</param>
    /// <param name="args">The controller event arguments.</param>
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (peltzerController.mode != ControllerMode.reshape)
        return;

      if (IsBeginOperationEvent(args)) {
        // If we are about to operate on selected items, ensure the click is near those items.
        if (selector.selectedEdges.Count > 0 || selector.selectedFaces.Count > 0 || selector.selectedVertices.Count > 0) {
          if (!selector.ClickIsWithinCurrentSelection(peltzerController.LastPositionModel)) {
            return;
          }
        }
        triggerUpToRelease = false;
        waitingToDetermineReleaseType = true;
        triggerDownTime = Time.time;
        MaybeStartOperation();
      } else if (IsCompleteSingleClickEvent(args)) {
        waitingToDetermineReleaseType = false;
        triggerUpToRelease = false;
      } else if (IsReleaseEvent(args)) {
        if (isSnapping) {
          // We snapped while modifying, so we have learned a bit more about snapping.
          completedSnaps++;
        }
        CompleteMove();
      } else if (IsStartSnapEvent(args) && !peltzerController.isBlockMode) {
        // can only snap when grabbing one face, otherwise we'd have to resize faces.
        if (IsReshapingFaces()) {
          PeltzerMain.Instance.snappedWhenReshapingFaces = true;
        }
        if (completedSnaps < SNAP_KNOW_HOW_COUNT) {
          PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
        }
        isSnapping = true;
        PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.alignSound);
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
      } else if (IsEndSnapEvent(args) && !peltzerController.isBlockMode) {
        isSnapping = false;
        if (isReshaping) {
          // We snapped while modifying, so we have learned a bit more about snapping.
          completedSnaps++;
        }
        PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
      } else if (IsSetUpHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadUpAllowed) {
        SetHoverTooltip(
          peltzerController.controllerGeometry.modifyTooltipUp, TouchpadHoverState.UP, args.TouchpadOverlay);
      } else if (
        IsSetLeftHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed) {
        SetHoverTooltip(
          peltzerController.controllerGeometry.modifyTooltipLeft, TouchpadHoverState.LEFT, args.TouchpadOverlay);
      } else if (IsSetRightHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadRightAllowed) {
        SetHoverTooltip(
          peltzerController.controllerGeometry.modifyTooltipRight, TouchpadHoverState.RIGHT, args.TouchpadOverlay);
      } else if (IsUnsetAllHoverTooltipsEvent(args)) {
        UnsetAllHoverTooltips();
      }
    }

    private void ModeChangeEventHandler(ControllerMode oldMode, ControllerMode newMode) {
      if (oldMode != ControllerMode.reshape) return;

      if (isReshaping) {
        CompleteMove();
      }

      selector.TurnOffSelectIndicator();
      selector.ResetInactive();
      UnsetAllHoverTooltips();
    }
  }
}
