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

using com.google.apps.peltzer.client.analytics;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.app;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   A tool for inserting freeform 'strokes'.
  /// </summary>
  public class Freeform : MonoBehaviour {
    /// <summary>
    /// The threshold for reversing the direction of the stroke at start.
    /// Set to 90 degrees because its a perfect right angle, this must be equal to or greater than 90.
    /// </summary>
    private const float REVERSE_FACE_ANGLE_THRESHOLD = 90f;
    /// <summary>
    /// The angle from the normal of a spine that we consider to be "backwards". 
    /// </summary>
    private const float BACKWARDS_ANGLE_THRESHOLD = 90f;
    /// <summary>
    /// The number of spine.lengths backwards a user must move before we think they are intentionally trying to go
    /// backwards.
    /// </summary>
    private const float BACKWARDS_DISTANCE_THRESHOLD = 0.9f;
    /// <summary>
    /// The distance a user has to move to enforce a pending checkpoint.
    /// </summary>
    private const float FORCE_CHECKPOINT_CHANGE_THRESHOLD = 0.04f;
    /// <summary>
    /// The number of spine.lengths we remove from the distance the user moved. This makes it so that the user
    /// has to move further before we consider adding a new spine; making stroke creation more controlled.
    /// </summary>
    private const float CONTROLLER_CHANGE_THRESHOLD = 1f;
    /// <summary>
    /// The number of seconds the user scales an object continuously before we start increasing the rate of the
    /// scaling process.
    /// </summary>
    private const float FAST_SCALE_THRESHOLD = 1f;
    /// <summary>
    ///   If user is scaling for a what we consider a long time, we will increase the scaling rate by this amount.
    /// </summary>
    private const int LONG_TERM_SCALE_FACTOR = 2;
    /// <summary>
    /// The default scale of the stroke to be inserted.
    /// </summary>
    private const float DEFAULT_SCALE_INDEX = 7;
    /// <summary>
    /// The max scale of the stroke to be inserted.
    /// </summary>
    private const int MAX_SCALE_INDEX = 20;
    /// <summary>
    /// The min scale of the stroke to be inserted.
    /// </summary>
    private const int MIN_SCALE_INDEX = 1;
    /// <summary>
    /// The maximum number of vertices a face of a stroke can have.
    /// </summary>
    private const int MAX_VERTEX_COUNT = 10;
    /// <summary>
    /// The minimum number of vertices a face of a stroke can have.
    /// </summary>
    private const int MIN_VERTEX_COUNT = 3;
    /// <summary>
    /// The maximum number of checkpoints a stroke can have before we segment it.
    /// </summary>
    private const int MAX_CHECKPOINT_COUNT = 15;
    /// <summary>
    /// Handle floating point errors.
    /// </summary>
    private float EPSILON = 0.1f;

    /// <summary>
    /// The distance between the controller and the *edge* (not the center) of the stroke hint.
    /// The position of the stroke is adjusted so that approximately this much of a gap, in WORLD SPACE,
    /// is kept between the controller and the edge of the stroke. This is expressed in world space
    /// because we want this to be independent of zoom level, for UX purposes (otherwise zooming in
    /// would make the stroke seem further away, which is confusing).
    ///
    /// Once the user starts to draw, though, this is converted to a fixed offset in model space that is
    /// retained during the entirety of the drawing operation, regardless of zoom level.
    /// </summary>
    private const float DISTANCE_TO_PREVIEW_EDGE_WORLD_SPACE = 0.01f;

    private PeltzerController peltzerController;
    private Model model;
    private AudioLibrary audioLibrary;
    private WorldSpace worldSpace;

    private ScaleType scaleType = ScaleType.NONE;

    /// <summary>
    /// The current number of vertices of the stroke face.
    /// </summary>
    private int vertexCount = 4;
    /// <summary>
    /// The scale of the shape to be inserted.
    /// </summary>
    private float insertScaleIndex = DEFAULT_SCALE_INDEX;
    /// <summary>
    /// The scale of the current front-face. Stored separately so that when a stroke is complete, we can revert to the
    /// prior starting scale.
    /// </summary>
    private float currentScaleIndex;

    /// <summary>
    /// Whether an insertion is in progress.
    /// </summary>
    private bool insertionInProgress;
    /// <summary>
    /// Whether we are snapping.
    /// </summary>
    private bool isSnapping;
    /// <summary>
    /// Whether the user is manually checkpointing.
    /// </summary>
    private bool isManualCheckpointing;
    /// <summary>
    // The benchmark we set to determine when the user has been scaling for a long time.
    /// </summary>
    private float longTermScaleStartTime = float.MaxValue;
    /// <summary>
    /// All the MMesh segments of the current stroke excluding the currentVolume.
    /// </summary>
    private List<int> strokeVolumeSegments;
    /// <summary>
    /// The current MMesh for the stroke being inserted.
    /// </summary>
    private MMesh currentVolume;
    /// <summary>
    /// The preview of the stroke being inserted.
    /// </summary>
    private GameObject currentHighlight;
    /// <summary>
    /// The face being moved to form the stroke.
    /// </summary>
    private Face currentFrontFace;
    /// <summary>
    /// The face not being moved to form the stroke.
    /// </summary>
    private Face currentBackFace;
    /// <summary>
    /// The location of each vertex at the time the last checkpoint was added.
    /// </summary>
    private Dictionary<int, Vector3> originalVertexLocations;
    /// <summary>
    /// The structure that defines the stroke and all possible positions for the next stroke segment using the last
    /// spine in strokeSpine.
    /// </summary>
    private List<Spine> strokeSpine = new List<Spine>();
    /// <summary>
    /// The offset from the controller position.
    /// </summary>
    private Vector3 freeformModelSpaceOffsetWhileDrawing;
    /// <summary>
    /// The normal of the front face at start. This is used to determine if we should reverse the front face.
    /// </summary>
    private Vector3 frontFaceNormalAtStart;
    /// <summary>
    /// The position of the controller at the start of a stroke.
    /// </summary>
    private Vector3 controllerPositionAtStart;
    /// <summary>
    /// The distance that a user has to move for us to lock in which face is going to be the front face and start making
    /// the stroke.
    /// </summary>
    private float chooseFaceDistance;
    /// <summary>
    /// The axis that defines the plane that the freeform is being made in when snapping.
    /// </summary>
    private Vector3 definingAxis;
    /// <summary>
    /// Allows the Spine logic to tell the Freeform to checkpoint. This happens when a user moves back on themself and
    /// we want to autogenerate a portion of the spine.
    /// </summary>
    private bool waitingToForceCheckpoint;
    /// <summary>
    /// Where the controller was when the Spine logic told the freeform it should be checkpointing. This will be used to
    /// determine if the controller has moved enough to enforce the checkpoint.
    /// </summary>
    private Vector3 controllerPositionAtPromptToCheckpoint;
    /// <summary>
    /// The number of checkpoints in the currentVolume. When the current number of checkpoints hits the maximum number
    /// we will segment the stroke.
    /// </summary>
    private int numCheckpointsInCurrentVolume;
    /// <summary>
    /// Used to determine if we should show the snap tooltip or not. Don't show the tooltip if the user already
    /// showed enough knowledge of how to snap.
    /// </summary>
    private int completedSnaps = 0;
    private const int SNAP_KNOW_HOW_COUNT = 3;
    /// <summary>
    /// Because snaps during strokes are triggered in rapid succession when the trigger is held down, count
    /// one completed snap per stroke only.
    /// </summary>
    private bool recordedSnapThisStroke = false;

    // Detection for trigger down & straight back up, vs trigger down and hold -- either of which 
    // begins a stroke.
    private bool triggerUpToEnd;
    private bool waitingToDetermineReleaseType;
    private float triggerDownTime;

    // Controller UI elements.
    private GameObject strokeOverlay_CENTER;
    private Vector3 strokeOriginAtLastCheckpoint;

    /// <summary>
    ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
    ///   As such, this setup method must be called before the tool is used for it to have a valid state.
    /// </summary>
    public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController,
      AudioLibrary audioLibrary, WorldSpace worldSpace) {
      // Nothing interesting to see here...
      this.model = model;
      this.peltzerController = peltzerController;
      this.audioLibrary = audioLibrary;
      this.worldSpace = worldSpace;
      controllerMain.ControllerActionHandler += ControllerEventHandler;
      peltzerController.MaterialChangedHandler += MaterialChangeHandler;
      peltzerController.ModeChangedHandler += ModeChangedHandler;
      CreateNewVolumeAndHighlight();
      currentHighlight.SetActive(peltzerController.mode == ControllerMode.insertStroke);
    }

    /// <summary>
    ///   Creates a new MMesh and a highlight (GameObject) for a new stroke.
    /// </summary>
    private void CreateNewVolumeAndHighlight() {
      int id = model.GenerateMeshId();
      currentVolume = new MMesh(id, Vector3.zero, Quaternion.identity, new Dictionary<int, Vertex>(vertexCount * 2), 
        new Dictionary<int, Face>(2 + vertexCount * 2));
      MMesh.GeometryOperation meshConstructionOperation = currentVolume.StartOperation();
      
      originalVertexLocations = new Dictionary<int, Vector3>(vertexCount * 2);
      
      // If we had something prior, we'll delete it when we're done.
      GameObject previousHighlight = null;
      if (currentHighlight != null) {
        previousHighlight = currentHighlight;
      }

      // The vertices and faces of the MMesh we're creating. 
      // Go in a circle and add the currently-selected number of vertices for the front and back faces.
      float scale = (GridUtils.GRID_SIZE / 2f) * insertScaleIndex;

      // Determine the width so that it is approximately a spine length.
      float width = GridUtils.GRID_SIZE / 2.0f;
      List<int> frontVertIds = new List<int>(vertexCount);
      List<int> backVertIds = new List<int>(vertexCount);
      for (int i = 0; i < vertexCount; i++) {
        float theta = (Mathf.PI / 2f) + i * (2 * Mathf.PI / vertexCount);
        // We add a ring of vertices for the 'back face'.
        Vertex backVert = meshConstructionOperation.AddVertexMeshSpace(
          new Vector3(Mathf.Cos(theta) * scale, 0f, Mathf.Sin(theta) * scale));
        backVertIds.Add(backVert.id);
              // Remember the original vertex locations, so we can correctly compute deltas.
        originalVertexLocations[backVert.id] = backVert.loc;

        // And a ring of vertices for the 'front face'
        Vertex frontVert = meshConstructionOperation.AddVertexMeshSpace(
          new Vector3(Mathf.Cos(theta) * scale, width, Mathf.Sin(theta) * scale));
        frontVertIds.Add(frontVert.id);
        originalVertexLocations[frontVert.id] = frontVert.loc;
      }

      // Create the 'side faces' with clockwise ordering. Each side face has 4 verts.
      for (int i = 0; i < vertexCount; i++) {
        List<int> vertexIds = new List<int>() {
          frontVertIds[i],
          frontVertIds[(i + 1) % frontVertIds.Count],
          backVertIds[(i + 1) % backVertIds.Count],
          backVertIds[i] };
        
        
        // Note: We don't need to calculate the normals here, they will be recalculated once we are done manipulating
        // these faces in the stroke.
        meshConstructionOperation.AddFace(vertexIds, new FaceProperties(peltzerController.currentMaterial));
      }

      // Create the front and back faces.
      frontVertIds.Reverse();
      currentFrontFace = meshConstructionOperation.AddFace(frontVertIds, new FaceProperties(peltzerController.currentMaterial));
      currentBackFace = meshConstructionOperation.AddFace(backVertIds, new FaceProperties(peltzerController.currentMaterial));
      
      meshConstructionOperation.Commit();
      currentVolume.RecalcBounds();
      
      // We generate the highlight directly and don't go via the cache as this isn't a permanent mesh for which 
      // we want a cached highlight.
      currentHighlight = MeshHelper.GameObjectFromMMesh(worldSpace, currentVolume);

      // Force an update to get the position and rotation right, so we don't see a flash.
      UpdatePreviewPositionAndRotation();

      // Destroy the previous preview, if any.
      if (previousHighlight != null) {
        GameObject.Destroy(previousHighlight);
      }
    }

    /// <summary>
    ///   Creates a new MMesh and a highlight (GameObject) for a new segment of the stroke from the current front face.
    /// </summary>
    private void CreateNewVolumeAndHighlightSegment() {
      int id = model.GenerateMeshId();
      // If we had something prior, we'll delete it when we're done.
      GameObject previousHighlight = null;
      if (currentHighlight != null) {
        previousHighlight = currentHighlight;
      }
      
      // Create the mesh and its highlight.
      MMesh newVolume =
        new MMesh(id, currentVolume.offset, currentVolume.rotation, 
          new Dictionary<int, Vertex>(vertexCount * 2), new Dictionary<int, Face>(2 + vertexCount * 2));

      MMesh.GeometryOperation segmentOperation = newVolume.StartOperation();
      
      originalVertexLocations = new Dictionary<int, Vector3>(vertexCount * 2);
      
      List<int> frontVertIds = new List<int>(vertexCount);
      List<int> backVertIds = new List<int>(vertexCount);
  

      // Determine the width so that it is approximately a spine length.
      for (int i = 0; i < vertexCount; i++) {
        // We add a ring of vertices for the 'back face'.
        Vertex backVert = segmentOperation.AddVertexMeshSpace(
          currentVolume.VertexPositionInMeshCoords(currentFrontFace.vertexIds[(vertexCount - 1) - i]));
        backVertIds.Add(backVert.id);
        // Remember the original vertex locations, so we can correctly compute deltas.
        originalVertexLocations[backVert.id] = backVert.loc;
        
        // And a ring of vertices for the 'front face'
        Vertex frontVert = segmentOperation.AddVertexMeshSpace(
          currentVolume.VertexPositionInMeshCoords(currentFrontFace.vertexIds[(vertexCount - 1) - i]));
        frontVertIds.Add(frontVert.id);
        originalVertexLocations[frontVert.id] = frontVert.loc;
      }

      // Create the 'side faces' with clockwise ordering. Each side face has 4 verts.
      for (int i = 0; i < vertexCount; i++) {
        List<int> vertexIds = new List<int>() {
          frontVertIds[i],
          frontVertIds[(i + 1) % frontVertIds.Count],
          backVertIds[(i + 1) % backVertIds.Count],
          backVertIds[i] };
        
        
        // Note: We don't need to calculate the normals here, they will be recalculated once we are done manipulating
        // these faces in the stroke.
        segmentOperation.AddFace(vertexIds, new FaceProperties(peltzerController.currentMaterial));
      }
      
      // Create the front and back faces.
      frontVertIds.Reverse();
      currentFrontFace = segmentOperation.AddFace(frontVertIds, new FaceProperties(peltzerController.currentMaterial));
      currentBackFace = segmentOperation.AddFace(backVertIds, new FaceProperties(peltzerController.currentMaterial));
      
      segmentOperation.Commit();
      newVolume.RecalcBounds();

      currentVolume = newVolume;
      
      // We generate the highlight directly and don't go via the cache as this isn't a permanent mesh for which 
      // we want a cached highlight.
      currentHighlight = MeshHelper.GameObjectFromMMesh(worldSpace, currentVolume);

      // Destroy the previous preview, if any.
      if (previousHighlight != null) {
        GameObject.Destroy(previousHighlight);
      }
    }

    private void Update() {
      // Nothing to see here...
      if (!PeltzerController.AcquireIfNecessary(ref peltzerController) ||
        peltzerController.mode != ControllerMode.insertStroke) {
        return;
      }

      // If the Freeform was told to force checkpoint by the Spine logic see if the user has moved far enough yet to
      // do this.
      if (waitingToForceCheckpoint && Vector3.Distance(controllerPositionAtPromptToCheckpoint,
        worldSpace.ModelToWorld(peltzerController.LastPositionModel)) > FORCE_CHECKPOINT_CHANGE_THRESHOLD) {
        AddCheckpoint();
        AddSpine();
      }

      UpdatePreviewPositionAndRotation();
    }

    /// <summary>
    /// Updates the freeform tool by either: positioning and rotating the preview if the user is not inserting a
    /// stroke yet, or adding spines to the stroke to fill the space between the last spine and the controller.
    /// </summary>
    private void UpdatePreviewPositionAndRotation() {
      if (waitingToDetermineReleaseType) {
        // If a stroke is in progress, and the trigger has been down for longer than WAIT_THRESHOLD, then this is
        // a hold-trigger-and-drag operation which can be completed by raising the trigger.
        if (Time.time - triggerDownTime > PeltzerController.SINGLE_CLICK_THRESHOLD) {
          waitingToDetermineReleaseType = false;
          triggerUpToEnd = true;
        }
      }

      // Determine the offset from the controller depending on mode.
      if (!insertionInProgress) {
        currentHighlight.SetActive(!PeltzerMain.Instance.peltzerController.isPointingAtMenu);

        MeshWithMaterialRenderer renderer = currentHighlight.GetComponent<MeshWithMaterialRenderer>();
        
        // The user isn't in the middle of making a freeform stroke, 
        // place/rotate the preview based on the current controller position/rotation.
        renderer.SetPositionModelSpace(peltzerController.LastPositionModel + GetFreeformPreviewOffSetModelSpace());

        // If we are snapping we want to help the user create an arch by orientating the preview to their eyes and their
        // body's natural rotation. This means their arm movement will be aligned with the preview and they will create
        // better arches naturally.
        Quaternion focalRotation = Quaternion.Euler(0, Camera.main.transform.rotation.eulerAngles.y, 0);

        Quaternion rotation = isSnapping ?
          GridUtils.SnapToNearest(peltzerController.LastRotationWorld, Quaternion.identity, 90f) :
          peltzerController.LastRotationWorld;

        // Level out the rotation so that an edge always points down. We only have to do this if the polygonal face has
        // an even number of vertices. The odd number faces do this naturally. We want to do this since a user will
        // naturally create an arch with a downward swipe.
        if (vertexCount % 2 == 0) {
          rotation = rotation * Quaternion.Euler(0f, 180f / vertexCount, 0f);
        }

        renderer.SetOrientationModelSpace(worldSpace.WorldOrientationToModel(rotation), /* smooth */ false);
      } else if (strokeSpine.Count() == 0) {
        // We have yet to determine which is going to be the "front face".
        Vector3 controllerChange = peltzerController.LastPositionModel - controllerPositionAtStart;

        // If the user has moved a certain distance since start we'll decide.
        if (controllerChange.magnitude > chooseFaceDistance) {
          // We'll make the back face the front face if the user is moving in the opposite direction than the front
          // face normal.
          if (Vector3.Angle(frontFaceNormalAtStart, controllerChange) > REVERSE_FACE_ANGLE_THRESHOLD) {
            Face faceTemp = currentFrontFace;
            currentFrontFace = currentBackFace;
            currentBackFace = faceTemp;
          }

          StartSpine();
        }
      } else {
        Vector3 controllerPosition = (peltzerController.LastPositionModel + freeformModelSpaceOffsetWhileDrawing);

        // If we are creating a snapped freeform we only want to consider controller movement in the plane the freeform
        // is being built on.
        controllerPosition = definingAxis == Vector3.zero || !isSnapping ?
          controllerPosition :
          Math3d.ProjectPointOnPlane(definingAxis, strokeSpine.Last().origin, controllerPosition);

        // Find the vector from the origin of the last spine to the controller position.
        Vector3 controllerChange = controllerPosition - strokeSpine.Last().origin;

        // Find the distance we want to fill with spines. This is the distance from the origin to the controller
        // minus a threshold. The threshold makes the user have to move more before creating a spine giving stroke
        // creation more stability.
        float distanceToFill = controllerChange.magnitude - CONTROLLER_CHANGE_THRESHOLD * strokeSpine.Last().length;

        // Find how much space there is between the controller and the stroke being made.
        // To generate the stroke we will fill this distance with as many spines as possible.
        float estimatedSpinesToAdd = Mathf.Floor(distanceToFill / strokeSpine.Last().length);

        // We only auto-generate spines if the user isn't checkpointing. If they are then they are responsible for
        // generating their own spines with physically added checkpoints.
        if (!isManualCheckpointing) {
          // Prevent the user from moving back on the stroke accidentally. A stroke will turn around to start building
          // itself backwards but this should only happen if the user moves far enough away.
          if (Vector3.Angle(strokeSpine.Last().normal, controllerChange) > BACKWARDS_ANGLE_THRESHOLD
            && distanceToFill < strokeSpine.Last().length * BACKWARDS_DISTANCE_THRESHOLD) {
            return;
          }


          
          // While there is still space between the controller and the stroke that we can fill with a spine, find the
          // right spine. We pre-compute the number of spines and use a for loop instead of a while loop that checks on
          // every checkpoint if there is still room for a new spine. This prevents the code from getting stuck in a
          // while loop on update crashing the app. This can happen if the generated geometry causes the stroke to loop
          // around the controller position.
          for (int i = 0; i < estimatedSpinesToAdd; i++) {
            // Now that we've updated the front face to be in the correct position we see if we should checkpoint or
            // "extend" the last spine. We don't actually lengthen the spine but it appears that way because we don't
            // checkpoint in between the current spine and the previous spine.
            UpdateSpine();

            if (!SpineIsAligned()) {
              AddCheckpoint();

              // If this checkpoint causes us to insert an invalid segment the stroke will stop drawing and we
              // need to break out of this loop.
              if (strokeSpine.Count == 0) {
                break;
              }
            } else {
              UpdateOriginalPositions();
            }

            // If we are about to update the front face position for the first time collapse the preview width.
            // We don't do this until the stroke updates the front face position the first time so that the user can't
            // create a stroke with no width by quickly releasing the trigger.
            if (strokeSpine.Count == 1) {
              // Move the verts of the front face so that they are flush with the back face. We will let the algorithm build
              // the volume up for us. The width we saw before was just for the preview.
              Vector3 delta = MeshMath.CalculateGeometricCenter(currentBackFace, currentVolume)
                - MeshMath.CalculateGeometricCenter(currentFrontFace, currentVolume);
              MMesh.GeometryOperation adjustOperation = currentVolume.StartOperation();
              foreach (int vertexId in currentFrontFace.vertexIds) {
                // Apply the positional delta in model space.
                Vector3 newModelPosition = currentVolume.MeshCoordsToModelCoords(originalVertexLocations[vertexId]) + delta;
                adjustOperation.ModifyVertexModelSpace(vertexId, newModelPosition);
              }
              adjustOperation.Commit();
              UpdateOriginalPositions();
            }

            UpdateFrontFacePosition();
            AddSpine();

            // Now that a new vertebra has selected added check the remaining distance from the new spine to the
            // controller. If it happens to be less then a spine length break out. We include this extra check in case
            // the estimated number of spines to add was wrong.
            distanceToFill = Vector3.Distance(controllerPosition, strokeSpine.Last().origin)
              - (CONTROLLER_CHANGE_THRESHOLD * strokeSpine.Last().length);
            if (distanceToFill < strokeSpine.Last().length) {
              break;
            }
          }
          
          UpdateOriginalPositions();
        } else {
          UpdateSpine();
          UpdateFrontFacePosition();
        }

        // Update the mesh.
        currentVolume.RecalcBounds();
        MMesh.AttachMeshToGameObject(
          worldSpace, currentHighlight, currentVolume, /* updateOnly */ estimatedSpinesToAdd == 0);
      }
    }

    /// <summary>
    /// Updates the Spine by finding the nearestVertebra and selecting that to make up the current segement of the
    /// spine.
    /// </summary>
    private void UpdateSpine() {
      Vertebra previousVertebra =
       strokeSpine.Count() > 1 ? strokeSpine[strokeSpine.Count() - 2].CurrentVertebra() : null;

      // Allows the Spine logic to tell the Freeform to checkpoint. This happens when a user moves back on themself and
      // we want to autogenerate a portion of the spine.
      bool shouldForceCheckpoint;

      Vector3 controllerUpVector;
      if (Config.Instance.VrHardware == VrHardware.Vive) {
        controllerUpVector = worldSpace.WorldVectorToModel(peltzerController.transform.up);
      } else {
        controllerUpVector = worldSpace.WorldVectorToModel(peltzerController.wandTip.transform.up);
      }

      // Find which vertebra of the active spine is closest to the controller's position. This will be used as the
      // origin for the next spine and as the center of the next checkpointed face.
      Vertebra nearestVertebra = strokeSpine.Last().NearestVertebra(
        peltzerController.LastPositionModel + freeformModelSpaceOffsetWhileDrawing, isSnapping, isManualCheckpointing,
        previousVertebra, controllerUpVector, out shouldForceCheckpoint);

      strokeSpine.Last().SelectVertebra(nearestVertebra);

      // We've chosen a vertebra that should be checkpointed before progressing. If we are generating an auto-stroke
      // that will be handled later in the the update loop, but if we are not we need to force this condition. We do
      // this by marking that the stroke should be checkpointing and then waiting until the user has moved far enough
      // to stabilize the stroke. If we are already waitingToForceCheckpointing don't do anything.
      if (!waitingToForceCheckpoint && shouldForceCheckpoint && isManualCheckpointing) {
        waitingToForceCheckpoint = true;
        controllerPositionAtPromptToCheckpoint = worldSpace.ModelToWorld(peltzerController.LastPositionModel);
      } else if (!shouldForceCheckpoint && waitingToForceCheckpoint) {
        // If we the Spine logic is telling us we don't have to checkpoint anymore cancel any pending waits to
        // checkpoint.
        waitingToForceCheckpoint = false;
      }
    }

    /// <summary>
    /// Moves the front face to its current position.
    /// </summary>
    private void UpdateFrontFacePosition() {
      Vertebra nearestVertebra = strokeSpine.Last().CurrentVertebra();

      // Find the rotational delta being applied to the front face when its at the nearestVertebra.
      Quaternion faceRotDelta = Quaternion.FromToRotation(strokeSpine.Last().normal, nearestVertebra.normal);
      // Find the positional delta being applied to the vertices of the front face.
      Vector3 delta = nearestVertebra.position - strokeSpine.Last().origin;
      MMesh.GeometryOperation updateFrontOperation = currentVolume.StartOperation();
      foreach (int vertexId in currentFrontFace.vertexIds) {
        // Apply the rotational delta in model space.
        Vector3 rotatedModelPosition = Math3d.RotatePointAroundPivot(
          currentVolume.MeshCoordsToModelCoords(originalVertexLocations[vertexId]), strokeSpine.Last().origin, faceRotDelta);
        // Apply the positional delta in model space.
        Vector3 newModelPosition = rotatedModelPosition + delta;
        updateFrontOperation.ModifyVertexModelSpace(vertexId, newModelPosition);
      }
      updateFrontOperation.Commit();
    }

    /// <summary>
    /// Checks to see if the current spine and previous spine in the strokeSpine are aligned. If they are we won't add
    /// a checkpoint.
    /// </summary>
    /// <returns>True if the current spine and previous spine are aligned.</returns>
    private bool SpineIsAligned() {
      if (strokeSpine.Count() < 2) {
        return true;
      }

      Vertebra currentVertebra = strokeSpine.Last().CurrentVertebra();
      Vertebra previousVertebra = strokeSpine[strokeSpine.Count() - 2].CurrentVertebra();

      // If a vertebra hasn't been selected for the active spine yet, return.
      if (currentVertebra == null || previousVertebra == null) {
        return false;
      }

      // Check if the direction and normals of the previous and current vertebra are the same.
      // This indicates that the two spine segements are aligned and we shouldn't place a checkpoint.
      return Math3d.CompareVectors(currentVertebra.direction, previousVertebra.direction, 0.001f)
        && Math3d.CompareVectors(currentVertebra.normal, previousVertebra.normal, 0.001f);
    }

    /// <summary>
    /// Adds the first spine.
    /// </summary>
    private void StartSpine() {
      List<Vector3> vertices = new List<Vector3>();
      foreach (int id in currentBackFace.vertexIds) {
        vertices.Add(currentVolume.VertexPositionInModelCoords(id));
      }

      vertices.Reverse();
      definingAxis = Vector3.zero;

      strokeSpine.Add(new Spine(vertices, definingAxis));
      strokeOriginAtLastCheckpoint = strokeSpine.Last().origin;
    }

    /// <summary>
    /// Finds the offset from the controller, in model space, where the preview should be.
    /// This is the offset that should be added to the controller's position in model space to obtain
    /// the position where the center of the stroke preview should be.
    /// </summary>
    /// <returns>The offset.</returns>
    private Vector3 GetFreeformPreviewOffSetModelSpace() {
      // Size of the volume currently being inserted. Note that the scale is already computed into this because
      // when we generate the volume, we bake the scale into its geometry.
      float size = currentVolume.bounds.size.z;
      // Compute the distance from the controller to the edge of the preview. This is given as a constant in
      // world space, so here we just convert it to model space.
      float distanceToEdgeModelSpace = DISTANCE_TO_PREVIEW_EDGE_WORLD_SPACE / worldSpace.scale;
      // Now that we know how big the volume is and the distance to the edge, it's easy to find the distance
      // to the center.
      float distanceToCenterModelSpace = distanceToEdgeModelSpace + size * 0.5f;
      // Now we have a distance from the controller that we want to convert into a model space offset.
      // To do that, we just multiply it by Vector3.forward to get it as a vector, and then transform it
      // by the controller's current orientation to get it to point the right way.
      return peltzerController.LastRotationModel * Vector3.forward * distanceToCenterModelSpace;
    }

    /// <summary>
    ///   Begins a new stroke, setting up some default variables.
    /// </summary>
    private void StartStroke() {
      audioLibrary.PlayClip(audioLibrary.genericSelectSound);
      PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
      insertionInProgress = true;
      MeshWithMaterialRenderer renderer = currentHighlight.GetComponent<MeshWithMaterialRenderer>();
      currentVolume.offset = renderer.GetPositionInModelSpace();
      currentVolume.rotation = renderer.GetOrientationInModelSpace();
      currentScaleIndex = insertScaleIndex;
      numCheckpointsInCurrentVolume = 0;
      strokeVolumeSegments = new List<int>();

      // Find the normal of the current "front face". We'll use it to detect if the user moves in the opposite direction
      // and we should change the front face to be the back face.
      List<Vector3> vertices = new List<Vector3>();
      foreach (int id in currentFrontFace.vertexIds) {
        vertices.Add(currentVolume.MeshCoordsToModelCoords(originalVertexLocations[id]));
      }
      chooseFaceDistance = Spine.FindSpineLength(MeshMath.FindHeightOfARegularPolygonalFace(vertices)) * 1.1f;
      frontFaceNormalAtStart = MeshMath.CalculateNormal(vertices);
      controllerPositionAtStart = peltzerController.LastPositionModel;

      // Instantiate the strokeSpine which will hold all the spines that make the stroke.
      strokeSpine = new List<Spine>();
      freeformModelSpaceOffsetWhileDrawing = GetFreeformPreviewOffSetModelSpace();
    }

    /// <summary>
    ///   Finishes a stroke, and inserts it into the model.
    /// </summary>
    private void EndStroke() {
      if (strokeVolumeSegments.Count > 0) {
        model.ApplyCommand(SetMeshGroupsCommand.CreateGroupMeshesCommand(model, strokeVolumeSegments));
        audioLibrary.PlayClip(audioLibrary.genericReleaseSound);
      }
      ClearState();
    }

    /// <summary>
    ///   Finishes a stroke segment, and inserts it into the model.
    /// </summary>
    private void EndStrokeSegment() {

      // Ensure nothing (such as redo, or tool switch) has caused an id clash since this mesh was created.
      if (model.HasMesh(currentVolume.id)) {
        currentVolume.ChangeId(model.GenerateMeshId());
      }

      MeshFixer.FixMutatedMesh(currentVolume, currentVolume,
        new HashSet<int>(currentVolume.GetVertexIds()),
        /* splitNonCoplanarFaces */ false, /* mergeAdjacentCoplanarFaces*/ true);

      if (!model.CanAddMesh(currentVolume)) {
        PeltzerMain.Instance.Analytics.FailedOperation("insertStroke");
        audioLibrary.PlayClip(audioLibrary.errorSound);
        peltzerController.TriggerHapticFeedback();

        EndStroke();
      } else {
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        model.ApplyCommand(new AddMeshCommand(currentVolume));

        strokeVolumeSegments.Add(currentVolume.id);
        CreateNewVolumeAndHighlightSegment();
        PeltzerMain.Instance.Analytics.SuccessfulOperation("insertStroke");
      }
    }

    /// <summary>
    ///   Scales the front-face of an in-progress stroke: achieved by recalculating the X and Y points from first
    ///   principles given the new current scale, rather than trying to move them by some scale factor.
    /// </summary>
    private void ScaleFrontFace(bool scaleUp) {
      // Nothing too big, nothing too small.
      if ((scaleUp && currentScaleIndex >= MAX_SCALE_INDEX) || (!scaleUp && currentScaleIndex == MIN_SCALE_INDEX)) {
        return;
      }

      if (scaleUp) {
        currentScaleIndex += IsLongTermScale() ? LONG_TERM_SCALE_FACTOR : 1;
        audioLibrary.PlayClip(audioLibrary.incrementSound);
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
      } else {
        currentScaleIndex -= IsLongTermScale() ? LONG_TERM_SCALE_FACTOR : 1;
        audioLibrary.PlayClip(audioLibrary.decrementSound);
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
      }
      // We find the new vertex positions by drawing a vector from the center of the currentFace to a vertex, then spin
      // it around the normal of the face stopping at each required angle and adding the vertex.
      float radius = (GridUtils.GRID_SIZE / 2) * currentScaleIndex;
      float angle = 360f / currentFrontFace.vertexIds.Count;

      List<Vector3> meshSpaceFaceVertices = new List<Vector3>();
      for (int i = 0; i < currentFrontFace.vertexIds.Count; i++) {
        meshSpaceFaceVertices.Add(originalVertexLocations[currentFrontFace.vertexIds[i]]);
      }

      Vector3 center = MeshMath.CalculateGeometricCenter(meshSpaceFaceVertices);
      Vector3 normal = MeshMath.CalculateNormal(meshSpaceFaceVertices);
      // This is the vector we will spin around the normal.
      Vector3 radialArm = radius * (meshSpaceFaceVertices[0] - center).normalized;

      MMesh.GeometryOperation scaleFrontfaceOperation = currentVolume.StartOperation();
      
      for (int i = 0; i * angle < (360f - EPSILON); i++) {
        Vector3 meshSpaceLocation = center + Quaternion.AngleAxis(i * angle, normal) * radialArm;
        int vertexId = currentFrontFace.vertexIds[i];
        scaleFrontfaceOperation.ModifyVertex(new Vertex(vertexId, meshSpaceLocation));
      }
      scaleFrontfaceOperation.Commit();
      UpdateOriginalPositions();
    }

    // Adding a checkpoint means adding a new front face; removing the prior front face (but keeping its verts); 
    // and adding a whole new set of sides between the prior and new front face.
    private void AddCheckpoint() {
      // We will segment the stroke when the numCheckpointsInCurrentVolume exceeds the max but only once the
      // definingAxis has been choosen if we are snapping otherwise when we reorient the stroke only the most recent
      // segment will rotate.
      if (definingAxis != Vector3.zero || !isSnapping) {
        numCheckpointsInCurrentVolume++;
      }

      // If adding this checkpoint is going to go over the max allowed number end the seg
      if (numCheckpointsInCurrentVolume > MAX_CHECKPOINT_COUNT) {
        // Create a new currentVolume segement.
        EndStrokeSegment();
        numCheckpointsInCurrentVolume = 0;
        return;
      }

      waitingToForceCheckpoint = false;
      strokeOriginAtLastCheckpoint = strokeSpine.Last().origin;

      List<Vertex> newVertices = new List<Vertex>(vertexCount);
      List<int> newVertexIds = new List<int>(vertexCount);

      MMesh.GeometryOperation checkpointOperation = currentVolume.StartOperation();
      
      // Add the new front face vertices, keeping track of the old front face vertices.
      for (int i = 0; i <= vertexCount; i++) {
        if (i != vertexCount) {
          // Get the current vertexId.
          int vertexId = currentFrontFace.vertexIds[i];

          Vertex newVertex = checkpointOperation.AddVertexMeshSpace(
            checkpointOperation.GetCurrentVertexPositionMeshSpace(vertexId));
          
          // Add a new vertex ahead of it.
          newVertexIds.Add(newVertex.id);

          newVertices.Add(newVertex);
          originalVertexLocations[newVertex.id] = newVertex.loc;
        }

        if (i == 0) {
          continue;
        }

        // Create the new 'side faces' with clockwise ordering. Each side face has 4 verts.
        // The first face 'loops around'.
        List<int> newFaceVertexIds = new List<int>() {
            currentFrontFace.vertexIds[i-1],
            currentFrontFace.vertexIds[i % vertexCount],
            newVertices[i % vertexCount].id,
            newVertices[i-1].id
          };

        checkpointOperation.AddFace(newFaceVertexIds, new FaceProperties(peltzerController.currentMaterial));
      }

      // Create a new front face.
      currentFrontFace = checkpointOperation.AddFace(newVertexIds, currentFrontFace.properties);
      checkpointOperation.Commit();
    }

    /// <summary>
    /// Adds a spine to the strokeSpine based on the current front face.
    /// </summary>
    private void AddSpine() {
      // If we haven't defined the definingAxis see if we can. We need to have determined two unique vectors that we
      // want to be in the plane we are creating the stroke in. Then we can get the normal of that plane by taking the
      // cross product of the two vectors.
      if (strokeSpine.Count() > 0 && definingAxis == Vector3.zero && isSnapping && !isManualCheckpointing) {
        Vertebra lastSelectedVertebra = strokeSpine.Last().CurrentVertebra();

        if (completedSnaps < SNAP_KNOW_HOW_COUNT && !recordedSnapThisStroke) {
          // The user successfully added a snap during this stroke. Record it as a completed snap.
          completedSnaps++;
          recordedSnapThisStroke = true;
        }

        if (lastSelectedVertebra != null &&
          !Math3d.CompareVectors(lastSelectedVertebra.direction.normalized, strokeSpine.Last().normal, 0.001f)) {

          // Rotate the mesh so that the defining axis lines up with the users hand.
          Vector3 vertebraProjectedPosition = Math3d.ProjectPointOnPlane(
            strokeSpine.Last().normal,
            strokeSpine.Last().origin,
            lastSelectedVertebra.position);

          Vector3 controllerProjectedPosition = Math3d.ProjectPointOnPlane(
            strokeSpine.Last().normal,
            strokeSpine.Last().origin,
            (peltzerController.LastPositionModel + freeformModelSpaceOffsetWhileDrawing));

          Vector3 vertebraProjectedDirection =
            ((vertebraProjectedPosition - strokeSpine.Last().origin) * 1000f).normalized;
          Vector3 controllerProjectedDirection =
            ((controllerProjectedPosition - strokeSpine.Last().origin) * 1000f).normalized;

          Quaternion rotationalDelta = Quaternion.FromToRotation(vertebraProjectedDirection, controllerProjectedDirection);
          currentVolume.rotation = rotationalDelta * currentVolume.rotation;

          definingAxis = Vector3.Cross(controllerProjectedDirection, strokeSpine.Last().normal);
        }
      }

      List<Vector3> vertices = new List<Vector3>();
      foreach (int id in currentFrontFace.vertexIds) {
        vertices.Add(currentVolume.VertexPositionInModelCoords(id));
      }

      strokeSpine.Add(new Spine(vertices, definingAxis));
    }

    /// <summary>
    /// Updates the originalVertexLocations to be their current position so that we can simulate a checkpoint without
    /// actually adding more vertices to the mesh.
    /// </summary>
    private void UpdateOriginalPositions() {
      foreach (int vertexId in currentFrontFace.vertexIds) {
        originalVertexLocations[vertexId] = currentVolume.VertexPositionInMeshCoords(vertexId);
      }
    }

    /// <summary>
    /// Changes the scale of the preview if the change is within the min and max scale index.
    /// </summary>
    /// <param name="increase">Whether we should increase the scale.</param>
    private void ChangeScale(bool increase) {
      int change = increase ? 1 : -1;
      if (IsLongTermScale()) {
          change *= LONG_TERM_SCALE_FACTOR;
      }

      if (insertScaleIndex + change >= MIN_SCALE_INDEX && insertScaleIndex + change <= MAX_SCALE_INDEX) {
        insertScaleIndex += change;
        CreateNewVolumeAndHighlight();
        if (change > 0) {
          audioLibrary.PlayClip(audioLibrary.incrementSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        } else {
          audioLibrary.PlayClip(audioLibrary.decrementSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        }
      }
    }

    /// <summary>
    /// Changes the number of vertices on the preview if the change is within the min and max vertex count.
    /// </summary>
    /// <param name="increase"></param>
    private void ChangeVertexCount(bool increase) {
      int change = increase ? 1 : -1;

      if (vertexCount + change >= MIN_VERTEX_COUNT && vertexCount + change <= MAX_VERTEX_COUNT) {
        vertexCount += change;
        CreateNewVolumeAndHighlight();
        if (change > 0) {
          audioLibrary.PlayClip(audioLibrary.swipeRightSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        } else {
          audioLibrary.PlayClip(audioLibrary.swipeLeftSound);
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        }
      } else {
        audioLibrary.PlayClip(audioLibrary.shapeMenuEndSound);
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
      }
    }

    /// <summary>
    /// Set the current checkpointing mode and toggle all the UI elements.
    /// </summary>
    /// <param name="shouldBeManuallyCheckpointing">Whether we should be manually checkpointing.</param>
    private void SetCheckpointingMode(bool shouldBeManuallyCheckpointing) {
      // TODO: Toggle UI elements.
      isManualCheckpointing = shouldBeManuallyCheckpointing;
    }

    /// <summary>
    ///   An event handler that listens for controller input and delegates accordingly.
    /// </summary>
    /// <param name="sender">The sender of the controller event.</param>
    /// <param name="args">The controller event arguments.</param>
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (peltzerController.mode != ControllerMode.insertStroke)
        return;

      if (IsBeginOperationEvent(args)) {
        triggerUpToEnd = false;
        waitingToDetermineReleaseType = true;
        triggerDownTime = Time.time;
        StartStroke();
        peltzerController.ChangeTouchpadOverlay(TouchpadOverlay.FREEFORM);
      } else if (IsCompleteSingleClickEvent(args)) {
        waitingToDetermineReleaseType = false;
        triggerUpToEnd = false;
        SetCheckpointingMode(/*isManualCheckpointing*/ true);
        peltzerController.ChangeTouchpadOverlay(TouchpadOverlay.FREEFORM);
      } else if (IsFinishStrokeEvent(args)) {
        EndStrokeSegment();
        EndStroke();
        SetCheckpointingMode(/*isManualCheckpointing*/ false);
        peltzerController.ChangeTouchpadOverlay(TouchpadOverlay.FREEFORM);
        recordedSnapThisStroke = false;
      } else if (IsScaleEvent(args)) {
        if (scaleType == ScaleType.NONE) {
           longTermScaleStartTime = Time.time + FAST_SCALE_THRESHOLD;
        }
        scaleType = (args.TouchpadLocation == TouchpadLocation.TOP) ? ScaleType.SCALE_UP : ScaleType.SCALE_DOWN;
        if (!insertionInProgress) {
          ChangeScale(args.TouchpadLocation == TouchpadLocation.TOP);
        } else {
          ScaleFrontFace(args.TouchpadLocation == TouchpadLocation.TOP);
        }
      } else if (IsChangeStrokeVertexCountEvent(args)) {
        ChangeVertexCount(args.TouchpadLocation == TouchpadLocation.RIGHT);
      } else if (IsInsertStrokeCheckpointEvent(args)) {
        if (isManualCheckpointing) {
          AddCheckpoint();
          AddSpine();
        }
      } else if (IsStopScalingEvent(args)) {
        StopScaling();
      } else if (IsSetUpHoverTooltipEvent(args)
        && PeltzerMain.Instance.restrictionManager.touchpadUpAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.freeformTooltipUp, TouchpadHoverState.UP);
      } else if (IsSetDownHoverTooltipEvent(args)
        && PeltzerMain.Instance.restrictionManager.touchpadDownAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.freeformTooltipDown, TouchpadHoverState.DOWN);
      } else if (IsSetLeftHoverTooltipEvent(args) && !insertionInProgress
        && PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.freeformTooltipLeft, TouchpadHoverState.LEFT);
      } else if (IsSetRightHoverTooltipEvent(args) && !insertionInProgress
        && PeltzerMain.Instance.restrictionManager.touchpadRightAllowed) {
        SetHoverTooltip(peltzerController.controllerGeometry.freeformTooltipRight, TouchpadHoverState.RIGHT);
      } else if (IsSetCenterHoverTooltipEvent(args) && insertionInProgress) {
        SetHoverTooltip(peltzerController.controllerGeometry.freeformTooltipCenter, TouchpadHoverState.NONE);
      } else if (IsUnsetAllHoverTooltipsEvent(args)) {
        UnsetAllHoverTooltips();
      } else if (IsStartSnapEvent(args)) {
        isSnapping = true;
        PeltzerMain.Instance.Analytics.SuccessfulOperation("usedSnapping");
        PeltzerMain.Instance.Analytics.SuccessfulOperation("usedSnappingFreeform");
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.alignSound);
        if (completedSnaps < SNAP_KNOW_HOW_COUNT) {
          PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
        }
      } else if (IsEndSnapEvent(args)) {
        // Allow the user to create a stroke that has snapped and unsnapping segments.
        if (strokeSpine.Count > 0) {
          AddCheckpoint();
          AddSpine();
        }
        isSnapping = false;
        PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
        // The definingAxis is only used for snapping.
        definingAxis = Vector3.zero;
      }
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'scale' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a scale event, false otherwise.</returns>
    private static bool IsScaleEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && (args.TouchpadLocation == TouchpadLocation.BOTTOM || args.TouchpadLocation == TouchpadLocation.TOP)
        && !PeltzerMain.Instance.Zoomer.Zooming;
    }

    /// <summary>
    ///   Whether this matches the pattern of the end of a 'scale' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a scale event, false otherwise.</returns>
    private bool IsStopScalingEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.UP
        && scaleType != ScaleType.NONE
        && !PeltzerMain.Instance.Zoomer.Zooming;
    }

    /// <summary>
    ///   Stop scaling.
    /// </summary>
    private void StopScaling() {
      scaleType = ScaleType.NONE;
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
    ///   Whether this matches the pattern of an event which should change the number of stroke polygon vertices.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a 'change stroke vertex count' event, false otherwise.</returns>
    private bool IsChangeStrokeVertexCountEvent(ControllerEventArgs args) {
      return !insertionInProgress
      && args.ControllerType == ControllerType.PELTZER
      && args.ButtonId == ButtonId.Touchpad
      && args.Action == ButtonAction.DOWN
      && (args.TouchpadLocation == TouchpadLocation.RIGHT || args.TouchpadLocation == TouchpadLocation.LEFT)
      && !PeltzerMain.Instance.Zoomer.Zooming;
    }

    /// <summary>
    ///   Whether this matches the pattern of an event which should begin the creation of a stroke.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a 'start stroke' event, false otherwise.</returns>
    private bool IsBeginOperationEvent(ControllerEventArgs args) {
      return !insertionInProgress
      && args.ControllerType == ControllerType.PELTZER
      && args.ButtonId == ButtonId.Trigger
      && args.Action == ButtonAction.DOWN;
    }

    private bool IsCompleteSingleClickEvent(ControllerEventArgs args) {
      return waitingToDetermineReleaseType
          && args.ControllerType == ControllerType.PELTZER
          && args.ButtonId == ButtonId.Trigger
          && args.Action == ButtonAction.UP;
    }

    private bool IsFinishStrokeEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger
        && ((args.Action == ButtonAction.UP && triggerUpToEnd)
        || (args.Action == ButtonAction.DOWN && !triggerUpToEnd))
        && insertionInProgress;
    }

    /// <summary>
    ///   Whether this matches the pattern of an event which should insert a checkpoint into the current stroke.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is an 'insert stroke checkpoint' event, false otherwise.</returns>
    private bool IsInsertStrokeCheckpointEvent(ControllerEventArgs args) {
      // If the controller is a Rift, use the secondary button to signal a checkpoint; otherwise use the touchpad.
      if (Config.Instance.VrHardware == VrHardware.Rift) {
        return insertionInProgress
        && args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.SecondaryButton
        && args.Action == ButtonAction.UP
        && !PeltzerMain.Instance.Zoomer.Zooming;
      }
      return insertionInProgress
      && args.ControllerType == ControllerType.PELTZER
      && args.ButtonId == ButtonId.Touchpad
      && args.Action == ButtonAction.UP
      && (args.TouchpadLocation == TouchpadLocation.CENTER || args.TouchpadLocation == TouchpadLocation.LEFT
      || args.TouchpadLocation == TouchpadLocation.RIGHT)
      && !PeltzerMain.Instance.Zoomer.Zooming;
    }

    // Touchpad Hover Tests.
    private static bool IsSetUpHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.TOP;
    }

    /// <summary>
    ///   Override logic for 'undo' for in-progress strokes.
    /// </summary>
    /// <returns>True if anything changed as a result of this operation.</returns>
    public bool Undo() {
      if (!insertionInProgress) {
        return false;
      }
      SetCheckpointingMode(/*shouldBeManuallyCheckpointing*/ false);
      ClearState();
      return true;
    }

    public void ClearState() {
      insertionInProgress = false;
      waitingToForceCheckpoint = false;
      strokeSpine = new List<Spine>();
      // TODO: Do we want to do this when you change tools?
      // SetCheckpointingMode(/*shouldBeManuallyCheckpointing*/ false);
      CreateNewVolumeAndHighlight();
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

    private static bool IsSetCenterHoverTooltipEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.CENTER;
    }

    private static bool IsUnsetAllHoverTooltipsEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.NONE;
    }

    private static bool IsToggleCheckpointingModeEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.ApplicationMenu
        && args.Action == ButtonAction.DOWN;
    }

    public bool IsStroking() {
      return insertionInProgress;
    }

    public bool IsManualStroking() {
      return isManualCheckpointing;
    }

    /// <summary>
    ///   Whether this matches a start snapping event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if the palette trigger is down.</returns>
    private static bool IsStartSnapEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.DOWN;
    }

    /// <summary>
    ///   Whether this matches an end snapping event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if the palette trigger is up.</returns>
    private static bool IsEndSnapEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Trigger
        && args.Action == ButtonAction.UP;
    }

    private void ModeChangedHandler(ControllerMode oldMode, ControllerMode newMode) {
      if (currentHighlight != null) {
        currentHighlight.SetActive(newMode == ControllerMode.insertStroke);
      }

      // TODO: If in progress, do something to cancel it.
      if (oldMode == ControllerMode.insertStroke) {
        UnsetAllHoverTooltips();
      }
    }

    private void MaterialChangeHandler(int newMaterialId) {
      MMesh.GeometryOperation matChangeOp = currentVolume.StartOperation();
      foreach (Face face in currentVolume.GetFaces()) {
        matChangeOp.ModifyFace(face.id, face.vertexIds, new FaceProperties(newMaterialId));
      }
      // Material change only - don't recalc normals.
      matChangeOp.CommitWithoutRecalculation();
      MMesh.AttachMeshToGameObject(worldSpace, currentHighlight, currentVolume, /* updateOnly */ true,
        MaterialRegistry.GetMaterialAndColorById(newMaterialId));
    }

    /// <summary>
    ///   Unset all of the touchpad hover text tooltips.
    /// </summary>
    private void UnsetAllHoverTooltips() {
      peltzerController.controllerGeometry.freeformTooltipUp.SetActive(false);
      peltzerController.controllerGeometry.freeformTooltipDown.SetActive(false);
      peltzerController.controllerGeometry.freeformTooltipLeft.SetActive(false);
      peltzerController.controllerGeometry.freeformTooltipRight.SetActive(false);
      peltzerController.controllerGeometry.freeformTooltipCenter.SetActive(false);
      peltzerController.SetTouchpadHoverTexture(TouchpadHoverState.NONE);
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
  }
}
