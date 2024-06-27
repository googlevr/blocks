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

using com.google.apps.peltzer.client.alignment;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils {
  /// <summary>
  ///   A collection of meshes that are tracking the position of the controller, for use in multiple tools who
  ///   can end up with a mesh as the 'toolhead'.
  /// </summary>
  public class HeldMeshes : MonoBehaviour {
    /// <summary>
    ///   A single held mesh.
    /// </summary>
    public class HeldMesh {
      // A clone of the MMesh being held.
      public MMesh Mesh { private set; get; }
      // A Unity GameObject preview of the MMesh being held.
      public GameObject Preview { private set; get; }

      // The original locations of the vertices of the held mesh (so we don't have to use deltas when filling).
      public Dictionary<int, Vector3> originalVertexLocations;
      // The original size of the mesh, useful when filling.
      public Vector3 originalSize;

      // The original offset of the MMesh.
      public Vector3 originalOffset;
      // The original rotation of the MMesh.
      public Quaternion originalRotation = Quaternion.identity;
      // The offset between the tip of the controller and the center of the preview.
      public Vector3 grabOffset;
      // The offset between the controller rotation and the rotation of the held mesh.
      public Quaternion rotationOffsetSelf = Quaternion.identity;

      // Whether the mesh is a primitive, and should therefore do things like display size when dragging.
      public bool isPrimitive;

      // The extents of the bounds around the mesh when dropped into the scene at the start of InsertType.FILL.
      internal Vector3 fillStartExtents;
      // The position of the transform for the mesh when dropped into the scene at the start of InsertType.FILL.
      internal Vector3 fillStartPosition;
      // The rotation of the primitive when inserted into the scene at the start of InsertType.FILL.
      internal Quaternion fillStartRotation;

      /// <summary>
      /// A mesh that is tracking the position of the controller in some way.
      /// </summary>
      /// <param name="originalMesh"></param>
      /// <param name="grabPoint"></param>
      /// <param name="rotationOffsetSelf"></param>
      /// <param name="worldSpace"></param>
      /// <param name="meshComponentsCache"></param>
      /// <param name="sizes">An optional size override, for when we have a better idea of the mesh's size than that
      /// provided by its AABB.</param>
      public HeldMesh(MMesh originalMesh, Vector3 grabPoint, Quaternion rotationOffsetSelf, WorldSpace worldSpace,
          MeshRepresentationCache meshComponentsCache, Dictionary<int, Vector3> sizes = null) {
        originalVertexLocations = new Dictionary<int, Vector3>(originalMesh.vertexCount);
        foreach (Vertex v in originalMesh.GetVertices()) {
          originalVertexLocations.Add(v.id, v.loc);
        }

        Mesh = originalMesh;
        if (sizes != null) {
          originalSize = sizes[originalMesh.id];
          isPrimitive = true;
        }
        else {
          originalSize = originalMesh.bounds.size;
          isPrimitive = false;
        }
        grabOffset = Mesh.offset - grabPoint;
        this.rotationOffsetSelf = rotationOffsetSelf;
        originalOffset = originalMesh.offset;
        originalRotation = originalMesh.rotation;
        if (meshComponentsCache != null) {
          Preview = meshComponentsCache.GeneratePreview(originalMesh);
        } else {
          Preview = MeshHelper.GameObjectFromMMesh(worldSpace, originalMesh);
        }
        Mesh.offset = Vector3.zero;
        Mesh.rotation = Quaternion.identity;
      }

      internal void SetUsePreviewMaterial(bool usePreview) {
        MeshWithMaterialRenderer renderer = Preview.GetComponent<MeshWithMaterialRenderer>();
        if (renderer) {
          renderer.UsePreviewShader(usePreview);
        }
      }

      internal void SetOriginalPositionsForFilling() {
        fillStartPosition = Preview.GetComponent<MeshWithMaterialRenderer>().GetPositionInModelSpace();
        fillStartRotation = Preview.GetComponent<MeshWithMaterialRenderer>().GetOrientationInModelSpace();
        fillStartExtents = Mesh.bounds.extents;
      }
    }

    // Length of time to do smoothing necessary for transitioning between modes.
    private const float TRANSITION_DURATION = 0.1f;

    // The mode that we're operating in. (ie, are we inserting, filling, block move, snap move, or free move.)
    private enum HoldMode { INSERT, FILL, BLOCK, SNAP, FREE };

    /// <summary>
    /// Defines how mesh placement is calculated.
    /// </summary>
    public enum PlacementMode {
      // The controller's position and orientation directly defines the placement/rotation of the held meshes.
      // This is normally used when inserting new meshes, as they don't have an "original pose".
      ABSOLUTE,
      // The meshes are given in their original position/rotation. The meshes are translated and rotated by an offset
      // defined by how much the controller moved/rotated since the operation started. This is normally used for
      // moving existing meshes.
      OFFSET
    };

    // If the held meshes are 'hidden' the preview will be inactive and they won't update in position.
    private bool isHidden;

    // Meshes.
    internal List<HeldMesh> heldMeshes;
    // Snapping.
    private bool isSnapping;
    private SnapGrid snapGrid;
    // A reference to the vertices that make up the preview face.
    private List<Vector3> coplanarPreviewFaceVerticesAtOrigin;
    // The face on the previewMesh being used for snapping.
    private Face previewFace;
    // The preview mesh for the game object the held mesh is being snapped to.
    private GameObject snapTargetPreview;
    private SnapSpace snapSpace;
    private SnapDetector snapDetector;

    // Filling.
    // Time limit to detect insertion of a simple shape before we start to insert a fill.
    public static readonly float INSERT_TIME_LIMIT = 0.3f;
    private float insertStartTime;
    // First a user begins inserting...
    public bool IsInserting;
    // ...then if they keep holding the trigger, they're filling.
    public bool IsFilling;
    // The corner of the fill volume that is locked in world space.
    private Vector3 lockedCorner;
    // The quadrant that the diagonal, created by the controllers start (centered at the origin) and current
    // position, is in.
    private Vector3 currentQuadrant;
    // The position of the controller at the start of insertion.
    private Vector3 controllerStartPosition;
    // The most-recent scale factor used when fill-scaling these HeldMeshes, or null. This is grid-aligned.
    Vector3? lastFillingScaleFactor;

    // Controls sticky snapping for rotation. This causes the snapped rotation to only change when the user
    // rotates the controller significantly away from the snapped rotation, to avoid constant orientation changes
    // at the edge of two snap regions.
    private StickyRotationSnapper stickyRotationSnapper;

    // Tools.
    private PeltzerController peltzerController;
    private WorldSpace worldSpace;

    // Moving.
    // Keep track of where the mesh 'started' so we can track movement, avoiding floating-point error from
    // continually adding deltas.
    public Vector3 centroidStartPositionModel;
    // The position of the controller at creation, in MODEL space.
    public Vector3 controllerStartPositionModel;
    // The rotation of the controller at creation, in MODEL space.
    private Quaternion controllerStartRotationModel;
    // The extents of the imaginary bounding box around all held meshes.
    public Vector3 extentsOfHeldMeshes;

    // The slerp operation for the parent transform when operating in block mode.
    private Slerpee blockSlerpee = null;

    // The time of the last hold mode transition - needed for determining when to stop slerping when transitioning
    // out of snapping mode.
    private float lastTransitionTime = 0f;

    // The most recent hold mode.
    private HoldMode lastMode = HoldMode.FREE;

    private FaceSnapEffect currentFaceSnapEffect;

    private bool setupDone;

    private GameObject paletteRuler;
    private GameObject ruler;
    // X on front
    private GameObject rulerXFrontBack;
    private TextMeshPro XFrontText;
    // Y on front
    private GameObject rulerYFrontBack;
    private TextMeshPro YFrontText;
    // Z on top
    private GameObject rulerZTopBottom;
    private TextMeshPro ZTopText;
    // X on top
    private GameObject rulerXTopBottom;
    private TextMeshPro XTopText;
    // Y on side
    private GameObject rulerYSide;
    private TextMeshPro YSideText;
    // Z on side
    private GameObject rulerZSide;
    private TextMeshPro ZSideText;

    private TextMeshPro paletteRulerText;
    
    // A set of cubes which are used to delineate what the ruler is measuring (useful for primitives other than cube.
    private GameObject pXpYpZ;
    private GameObject pXpYnZ;
    private GameObject pXnYpZ;
    private GameObject pXnYnZ;
    private GameObject nXpYpZ;
    private GameObject nXpYnZ;
    private GameObject nXnYpZ;
    private GameObject nXnYnZ;
     

    // Note this method is copied below with slightly different behavior.
    public void Setup(IEnumerable<MMesh> meshes, Vector3 controllerPositionAtCreation, 
        Quaternion controllerRotationAtCreation, PeltzerController peltzerController, WorldSpace worldSpace,
        MeshRepresentationCache meshComponentsCache, PlacementMode placementMode = PlacementMode.OFFSET,
        Dictionary<MMesh, HeldMesh> oldPreviews = null, bool renderAsPreviewMeshes = false,
        Dictionary<int, Vector3> sizes = null) {
      this.controllerStartPositionModel = (placementMode == PlacementMode.ABSOLUTE) ?
        Vector3.zero : controllerPositionAtCreation;
      this.controllerStartRotationModel = (placementMode == PlacementMode.ABSOLUTE) ?
        Quaternion.identity : controllerRotationAtCreation;
      this.peltzerController = peltzerController;
      this.worldSpace = worldSpace;
      heldMeshes = new List<HeldMesh>(meshes.Count());
      stickyRotationSnapper = new StickyRotationSnapper(controllerStartRotationModel);

      centroidStartPositionModel = Math3d.FindCentroid(meshes.Select(m => m.offset));
      Bounds overallBounds = new Bounds();
      overallBounds.center = centroidStartPositionModel;

      foreach (MMesh mesh in meshes) {
        overallBounds.Encapsulate(mesh.bounds);
        Quaternion rotationOffsetSelf = Math3d.Normalize((placementMode == PlacementMode.ABSOLUTE) ? Quaternion.identity :
          (Quaternion.Inverse(controllerRotationAtCreation) * mesh.rotation));
        HeldMesh oldHeldMesh;
        if (oldPreviews != null && oldPreviews.TryGetValue(mesh, out oldHeldMesh)) {
          rotationOffsetSelf = oldHeldMesh.rotationOffsetSelf;
        }
        if (sizes != null) {
          heldMeshes.Add(new HeldMesh(mesh.Clone(), this.controllerStartPositionModel,
            rotationOffsetSelf,
            worldSpace, meshComponentsCache, sizes));
        }
        else {
          heldMeshes.Add(new HeldMesh(mesh.Clone(), this.controllerStartPositionModel,
            rotationOffsetSelf,
            worldSpace, meshComponentsCache));
        }
      }
      if (renderAsPreviewMeshes) {
        for (int i = 0; i < heldMeshes.Count; i++) {
          heldMeshes[i].SetUsePreviewMaterial(true);
        }
      }

      extentsOfHeldMeshes = overallBounds.extents;

      ruler = ObjectFinder.ObjectById("ID_Ruler3");
      rulerXFrontBack = ObjectFinder.ObjectById("ID_XText");
      rulerYFrontBack = ObjectFinder.ObjectById("ID_YText");
      rulerZTopBottom = ObjectFinder.ObjectById("ID_ZText");
      rulerXTopBottom = ObjectFinder.ObjectById("ID_XText2");
      rulerYSide = ObjectFinder.ObjectById("ID_YText2");
      rulerZSide = ObjectFinder.ObjectById("ID_ZText2");
      paletteRuler = ObjectFinder.ObjectById("ID_PaletteRuler");
      pXpYpZ = ObjectFinder.ObjectById("ID_pXpYpZ");
      pXpYnZ = ObjectFinder.ObjectById("ID_pXpYnZ");
      pXnYpZ = ObjectFinder.ObjectById("ID_pXnYpZ");
      pXnYnZ = ObjectFinder.ObjectById("ID_pXnYnZ");
      nXpYpZ = ObjectFinder.ObjectById("ID_nXpYpZ");
      nXpYnZ = ObjectFinder.ObjectById("ID_nXpYnZ");
      nXnYpZ = ObjectFinder.ObjectById("ID_nXnYpZ");
      nXnYnZ = ObjectFinder.ObjectById("ID_nXnYnZ");

      paletteRulerText = paletteRuler.GetComponent<TextMeshPro>();
      XFrontText = rulerXFrontBack.GetComponent<TextMeshPro>();
      XTopText = rulerXTopBottom.GetComponent<TextMeshPro>();
      YFrontText = rulerYFrontBack.GetComponent<TextMeshPro>();
      YSideText = rulerYSide.GetComponent<TextMeshPro>();
      ZTopText = rulerZTopBottom.GetComponent<TextMeshPro>();
      ZSideText = rulerZSide.GetComponent<TextMeshPro>();

      if (snapDetector == null) {
        snapDetector = new SnapDetector();
      }
      // Force an update to get everything in the right position.
      UpdatePositions();
      setupDone = true;
    }

    // As above, but avoids cloning the mesh, and will not use the preview cache. 
    // Duplicated to avoid any performance overhead from e.g. cloning meshes into a temporary collection.
    public void SetupWithNoCloneOrCache(IEnumerable<MMesh> meshes, Vector3 controllerPositionAtCreation,
        PeltzerController peltzerController, WorldSpace worldSpace,
        Dictionary<MMesh, HeldMesh> oldPreviews = null) {
      this.controllerStartPositionModel = controllerPositionAtCreation;
      this.controllerStartRotationModel = peltzerController.LastRotationModel;
      this.peltzerController = peltzerController;
      this.worldSpace = worldSpace;
      heldMeshes = new List<HeldMesh>();
      stickyRotationSnapper = new StickyRotationSnapper(controllerStartRotationModel);

      centroidStartPositionModel = Math3d.FindCentroid(meshes.Select(m => m.offset));
      Bounds overallBounds = new Bounds();
      overallBounds.center = centroidStartPositionModel;

      foreach (MMesh mesh in meshes) {
        overallBounds.Encapsulate(mesh.bounds);
        Quaternion rotationOffsetCentroid = Quaternion.Inverse(peltzerController.LastRotationModel);
        Quaternion rotationOffsetSelf = Quaternion.Inverse(peltzerController.LastRotationModel) * mesh.rotation;
        HeldMesh oldHeldMesh;
        if (oldPreviews != null && oldPreviews.TryGetValue(mesh, out oldHeldMesh)) {
          rotationOffsetSelf = oldHeldMesh.rotationOffsetSelf;
        }

        heldMeshes.Add(new HeldMesh(mesh, peltzerController.LastPositionModel, rotationOffsetSelf,
          worldSpace, /* cache */ null));
      }

      extentsOfHeldMeshes = overallBounds.extents;

      // Force an update to get everything in the right position.
      UpdatePositions();
    }

    /// <summary>
    /// Detects what would snap at this given moment for the heldMeshes.
    /// </summary>
    public void DetectSnap() {
      // TODO (bug): Snap multiple meshes.
      if (heldMeshes.Count > 1 || isSnapping) return;
      MMesh mesh = heldMeshes[0].Mesh;
      GameObject preview = heldMeshes[0].Preview;
      MeshWithMaterialRenderer renderMesh = preview.transform.GetComponent<MeshWithMaterialRenderer>();
      Vector3 previewMeshOffset = renderMesh.GetPositionInModelSpace();
      Quaternion previewMeshRotation = renderMesh.GetOrientationInModelSpace();

      snapDetector.DetectSnap(mesh, previewMeshOffset, previewMeshRotation);
    }

    // Hide the rope guides.
    public void HideSnapGuides() {
      if (Features.useContinuousSnapDetection) {
        if (snapDetector != null) {
          snapDetector.HideGuides();
        }

        if (snapSpace != null) {
          snapSpace.StopSnap();
        }
      }
    }

    /// <summary>
    ///   To the user there is only one way to engage snapping. However, there are actually multiple snap modes.
    ///   Currently there is UNIVERSAL and MESH snapping. MESH snapping takes priority over UNIVERSAL snapping.
    ///   If there is a mesh in the selector we will use this mesh as a reference to the new grid we should be
    ///   snapping to. If there is no mesh nearby we will snap to the universal grid.
    /// </summary>
    public void StartSnapping(Model model, SpatialIndex spatialIndex) {
      if (isSnapping || !PeltzerMain.Instance.restrictionManager.snappingAllowed)
        return;

      // We won't even try and snap multiple selected meshes, behaviour is ill-defined and our code below won't
      // support it, bug
      // We can snap multiple meshes with the snapGrid implementation.
      if (heldMeshes.Count > 1)
        return;

      isSnapping = true;
      PeltzerMain.Instance.Analytics.SuccessfulOperation("usedSnapping");
      PeltzerMain.Instance.Analytics.SuccessfulOperation("usedSnappingHeldMeshes");

      if (Features.useContinuousSnapDetection) {
        MMesh mesh = heldMeshes[0].Mesh;
        GameObject preview = heldMeshes[0].Preview;
        MeshWithMaterialRenderer renderMesh = preview.transform.GetComponent<MeshWithMaterialRenderer>();
        Vector3 previewMeshOffset = renderMesh.GetPositionInModelSpace();
        Quaternion previewMeshRotation = renderMesh.GetOrientationInModelSpace();

        snapSpace = snapDetector.ExecuteSnap(mesh, previewMeshOffset, previewMeshRotation);
        PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.alignSound);
        PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
      } else {
        MMesh mesh = heldMeshes[0].Mesh;
        GameObject preview = heldMeshes[0].Preview;
        MeshWithMaterialRenderer renderMesh = preview.transform.GetComponent<MeshWithMaterialRenderer>();
        Vector3 previewMeshOffset = renderMesh.GetPositionInModelSpace();
        Quaternion previewMeshRotation = renderMesh.GetOrientationInModelSpace();
        Quaternion finalVolumePreviewMeshRotation = Quaternion.identity;
        int snapTargetId;
        snapGrid = new SnapGrid(
          mesh,
          previewMeshOffset,
          previewMeshRotation,
          model,
          spatialIndex,
          worldSpace,
          peltzerController.mode == ControllerMode.subtract,
          out finalVolumePreviewMeshRotation,
          out previewFace,
          out coplanarPreviewFaceVerticesAtOrigin,
          out snapTargetId);
        if (snapGrid.snapType == SnapGrid.SnapType.FACE) {
          if (snapTargetId != -1) {
            currentFaceSnapEffect = new FaceSnapEffect(snapTargetId);
            UXEffectManager.GetEffectManager().StartEffect(currentFaceSnapEffect);
          }
        }
        renderMesh.SetOrientationModelSpace(finalVolumePreviewMeshRotation, /* smooth */ true);
        if (snapGrid.snapType == SnapGrid.SnapType.UNIVERSAL) {
          PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.alignSound);
        } else {
          PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.snapSound);
        }
      }
    }

    /// <summary>
    ///   Exit snapping mode and reset the grid, snapType and the selector.
    /// </summary>
    public void StopSnapping() {
      if (currentFaceSnapEffect != null) {
        currentFaceSnapEffect.Finish();
        currentFaceSnapEffect = null;
      }
      if (!isSnapping)
        return;

      // Reset snapping.
      isSnapping = false;
      if (Features.useContinuousSnapDetection) {
        HideSnapGuides();
        snapSpace = null;
      } else {
        snapGrid.snapType = SnapGrid.SnapType.NONE;
      }
    }

    public void StartInserting(Vector3 controllerStartPosition) {
      HideSnapGuides();
      this.controllerStartPosition = controllerStartPosition;
      insertStartTime = Time.time;
      IsInserting = true;
    }

    public void FinishInserting() {
      IsInserting = false;
      IsFilling = false;
      lastFillingScaleFactor = null;
      snapSpace = null;
    }

    /// <summary>
    /// Returns a dictionary that associates held meshes and their previews.
    /// </summary>
    /// <returns>A dictionary where the keys are the held meshes and the corresponding value is
    /// the held mesh's preview object.</returns>
    public Dictionary<MMesh, GameObject> GetHeldMeshesAndPreviews() {
      Dictionary<MMesh, GameObject> result = new Dictionary<MMesh, GameObject>();
      foreach (HeldMesh heldMesh in heldMeshes) {
        result[heldMesh.Mesh] = heldMesh.Preview;
      }
      return result;
    }

    /// <summary>
    ///   Sets all the variables needed for filling a volume.
    /// </summary>
    private void StartFillingVolume() {
      // If volume filling is not allowed at this point in time, return.
      if (!PeltzerMain.Instance.restrictionManager.volumeFillingAllowed) {
        return;
      }

      IsFilling = true;
      IsInserting = false;

      currentQuadrant = Vector3.zero;
      foreach (HeldMesh heldMesh in heldMeshes) {
        heldMesh.SetOriginalPositionsForFilling();
      }
    }

    /// <summary>
    ///   Update the position of the mesh whilst in 'block mode'.
    ///   In block mode, we snap the bounding box of the entire selection.
    /// </summary>
    private void UpdatePositionBlockMode() {
      Vector3 grabOffset = centroidStartPositionModel - controllerStartPositionModel;
      Vector3 controllerDelta = peltzerController.LastPositionModel - controllerStartPositionModel;
      Vector3 newCentroid = centroidStartPositionModel + controllerDelta;
      newCentroid = Math3d.RotatePointAroundPivot(newCentroid, peltzerController.LastPositionModel + grabOffset,
        peltzerController.LastRotationModel);
      Vector3 centroidDelta = newCentroid - centroidStartPositionModel;

      // Compute how much the controller has rotated in MODEL space. Note that we don't have to add any special
      // behavior to handle world rotation, because rotating the world is EQUIVALENT to rotating the controller
      // in the opposite way in model space. So, to us, there's only rotation in model space.
      Quaternion unsnappedRotation =
          peltzerController.LastRotationModel * Quaternion.Inverse(controllerStartRotationModel);
      Quaternion baseSnappedRotation = stickyRotationSnapper.UpdateRotation(unsnappedRotation);

      // Initialize slerp for parent transform if it hasn't already been initialized.
      if (blockSlerpee == null) {
        blockSlerpee = new Slerpee(unsnappedRotation);
      }

      // If we're transitioning into block mode, don't slerp the parent transform and instead slerp the child
      // transforms to smooth into block mode.  Once that completes, slerp the parent transform and not the child
      // transforms to ensure that grouped meshes rotate correctly with each other.
      Quaternion parentSlerpedSnappedRotation;
      bool inTransitionToBlockMode = TRANSITION_DURATION >= (Time.time - lastTransitionTime);
      if (inTransitionToBlockMode) {
        // Still transitioning in, instantly update parent.  Individual meshes will slerp during this period.
        parentSlerpedSnappedRotation = blockSlerpee.UpdateOrientationInstantly(baseSnappedRotation);
      } else {
        // Done transitioning, start slerping parent, individual meshes will update immediately based on
        // parent slerp.
        parentSlerpedSnappedRotation = blockSlerpee.StartOrUpdateSlerp(baseSnappedRotation);
      }

      Bounds tempBounds = new Bounds();
      foreach (HeldMesh heldMesh in heldMeshes) {

        // There's still a small misalignment that is causing groups of meshes to not rotate in rigid formation.
        // As it's only visually noticeable when slerping is slowed down 10x, fixing this isn't urgent, but be aware
        // that there's currently a small amount of error here. The final snapped positions are correct.
        Vector3 smoothedPosition = Math3d.RotatePointAroundPivot(heldMesh.originalOffset + centroidDelta,
          peltzerController.LastPositionModel, parentSlerpedSnappedRotation);
        Vector3 snappedPosition = Math3d.RotatePointAroundPivot(heldMesh.originalOffset + centroidDelta,
          peltzerController.LastPositionModel, baseSnappedRotation);

        MeshWithMaterialRenderer renderMesh = heldMesh.Preview.GetComponent<MeshWithMaterialRenderer>();

        // Linear position smoothing makes groups of objects rotate incorrectly, but is needed to transition in and
        // out of snapping.  Since we don't snap groups, only smooth when we're holding a single mesh.
        if (heldMeshes.Count == 1) {
          // Note: we don't use smoothing when doing Setup (before setupDone == true) because in ABSOLUTE
          // positioning mode, the first update (driven by Setup()) will move the meshes from the origin to the
          // correct position.
          renderMesh.SetPositionModelSpace(snappedPosition, /* smooth */ setupDone);
        } else {
          renderMesh.SetPositionWithDisplayOverrideModelSpace(snappedPosition, smoothedPosition);
        }
        // Only smooth orientations while we're transitioning into block mode - otherwise we want to use the parent
        // transform smoothing. Doing normalization because in testing floating point drift caused issues here.
        // Also, don't smooth if we are in the process of doing setup (setupDone == false), as we don't want
        // the mesh to animate from the identity rotation to its initial rotation.
        renderMesh.SetOrientationWithDisplayOverrideModelSpace(
          Math3d.Normalize(baseSnappedRotation * heldMesh.originalRotation),
          Math3d.Normalize(parentSlerpedSnappedRotation * heldMesh.originalRotation),
          /* smooth */ setupDone && inTransitionToBlockMode);

        // Encapsulate the mesh bounds.
        Bounds meshBounds = heldMesh.Mesh.bounds;
        // The mesh offset may be wrong, so we explicitly set the bounds center here.
        // First, compute the difference between the preview's position and the mesh's position.
        Vector3 delta = renderMesh.positionModelSpace - heldMesh.Mesh.offset;
        // Apply that delta to the bounding box center to obtain the current bounding box.
        // Remember that bounding boxes are represented in model space, not in mesh space.
        meshBounds.center = heldMesh.Mesh.bounds.center + delta;
        if (tempBounds.extents == Vector3.zero) {
          tempBounds = meshBounds;
        } else {
          tempBounds.Encapsulate(meshBounds);
        }
      }

      // Find how far the bounding box needs to be moved to be snapped to the grid.
      Vector3 snappedDelta = GridUtils.SnapToGrid(tempBounds.center, tempBounds) - tempBounds.center;

      // Apply the snappedDelta to each mesh.
      foreach (HeldMesh heldMesh in heldMeshes) {
        MeshWithMaterialRenderer renderer = heldMesh.Preview.GetComponent<MeshWithMaterialRenderer>();
        renderer.SetPositionModelSpace(
          renderer.GetPositionInModelSpace() + snappedDelta,
          /* smooth */ true);
      }
    }

    /// <summary>
    ///   Update the position of the mesh whilst in 'snapping' mode.
    /// </summary>
    private void UpdatePositionSnapping() {
      foreach (HeldMesh heldMesh in heldMeshes) {
        MeshWithMaterialRenderer renderMesh =
          heldMesh.Preview.transform.GetComponent<MeshWithMaterialRenderer>();

        Vector3 previewOffset = peltzerController.LastPositionModel + heldMesh.grabOffset;
        previewOffset = Math3d.RotatePointAroundPivot(previewOffset, peltzerController.LastPositionModel,
        peltzerController.LastRotationModel * Quaternion.Inverse(controllerStartRotationModel));
        Quaternion previewRotation = renderMesh.GetOrientationInModelSpace();
        Vector3 positionToSnap = Vector3.zero;
        int previewFaceId = 0;

        Quaternion rotationDeltaModel =
        peltzerController.LastRotationModel * Quaternion.Inverse(controllerStartRotationModel);

        SnapTransform snappedTransform = null;

        if (Features.useContinuousSnapDetection) {
          Vector3 newPositionModel = Math3d.RotatePointAroundPivot(
          peltzerController.LastPositionModel + heldMesh.grabOffset,
          peltzerController.LastPositionModel, rotationDeltaModel);

          Quaternion newRotationModel = peltzerController.LastRotationModel * heldMesh.rotationOffsetSelf;
          snappedTransform = snapSpace.Snap(previewOffset, newRotationModel);
          snapDetector.UpdateHints(snapSpace, heldMesh.Mesh, previewOffset, previewRotation);
          renderMesh.SetPositionModelSpace(snappedTransform.position, /* smooth */ true);
          renderMesh.SetOrientationModelSpace(snappedTransform.rotation, /* smooth */ true);
        } else {
          if (snapGrid.snapType == SnapGrid.SnapType.VERTEX
              || snapGrid.snapType == SnapGrid.SnapType.FACE) {
            List<Vector3> vertices = new List<Vector3>(coplanarPreviewFaceVerticesAtOrigin.Count);
            foreach (Vector3 vertex in coplanarPreviewFaceVerticesAtOrigin) {
              vertices.Add((previewRotation * vertex) + previewOffset);
            }
            positionToSnap = MeshMath.CalculateGeometricCenter(vertices);
            previewFaceId = previewFace.id;
          }
          SnapInfo snapInfo = snapGrid
            .snapToGrid(
            positionToSnap,
            previewFaceId,
            previewOffset,
            previewRotation,
            heldMesh.Mesh);
          snappedTransform = snapInfo.transform;

          if (snapGrid.snapType == SnapGrid.SnapType.FACE && currentFaceSnapEffect != null) {
            currentFaceSnapEffect.UpdateSnapEffect(snapInfo);
          }

          renderMesh.SetPositionModelSpace(snappedTransform.position, /* smooth */ true);
          renderMesh.SetOrientationModelSpace(snappedTransform.rotation, /* smooth */ true);
        }
      }
    }

    /// <summary>
    ///   Update the position of the mesh whilst in neither 'grid mode' nor 'snapping' mode.
    /// </summary>
    private void UpdatePositionFreely() {
      bool inTransitionToFreeMode = TRANSITION_DURATION >= (Time.time - lastTransitionTime);

      // Calculate how the controller has rotated in model space since the start of the operation.
      Quaternion rotationDeltaModel =
        peltzerController.LastRotationModel * Quaternion.Inverse(controllerStartRotationModel);

      foreach (HeldMesh heldMesh in heldMeshes) {
        // Figure out the new position for this mesh based on how the controller has moved/rotated.
        Vector3 newPositionModel = Math3d.RotatePointAroundPivot(
          peltzerController.LastPositionModel + heldMesh.grabOffset,
          peltzerController.LastPositionModel, rotationDeltaModel);

        MeshWithMaterialRenderer meshRenderer = 
          heldMesh.Preview.transform.GetComponent<MeshWithMaterialRenderer>();
        meshRenderer.SetPositionModelSpace(newPositionModel, /* smooth */ false);
        meshRenderer.SetOrientationModelSpace(peltzerController.LastRotationModel * heldMesh.rotationOffsetSelf,
          /* smooth */ inTransitionToFreeMode);
      }
    }

    public void UpdatePositions() {
      // No need to update positions when pointing at the menu.
      if (PeltzerMain.Instance.peltzerController.isPointingAtMenu) {
        return;
      }

      if (IsInserting) {
        if (lastMode != HoldMode.INSERT) {
          HandleModeTransition(HoldMode.INSERT);
          lastMode = HoldMode.INSERT;
        }

        // Wait to see if we're filling, and also freeze the previews.
        // TODO (bug): Determine if time, or distance from start click is the correct way to determine when to
        // switch between insert types.
        if (Time.time - insertStartTime > INSERT_TIME_LIMIT) {
          StartFillingVolume();
        }
      } else if (IsFilling) {
        if (lastMode != HoldMode.FILL) {
          HandleModeTransition(HoldMode.FILL);
          lastMode = HoldMode.FILL;
        }
        ScalePreviewsToFill();
      } else if (peltzerController.isBlockMode && !isSnapping) {
        if (lastMode != HoldMode.BLOCK) {
          HandleModeTransition(HoldMode.BLOCK);
          lastMode = HoldMode.BLOCK;
        }
        UpdatePositionBlockMode();
      } else if (isSnapping) {
        if (lastMode != HoldMode.SNAP) {
          HandleModeTransition(HoldMode.SNAP);
          lastMode = HoldMode.SNAP;
        }
        UpdatePositionSnapping();
      } else {
        if (lastMode != HoldMode.FREE) {
          HandleModeTransition(HoldMode.FREE);
          lastMode = HoldMode.FREE;
        }
        UpdatePositionFreely();
      }
    }

    /// <summary>
    ///   Does any required bookkeeping when transitioning between modes.
    /// </summary>
    private void HandleModeTransition(HoldMode curMode) {
      lastTransitionTime = Time.time;
      if (lastMode == HoldMode.BLOCK) {
        blockSlerpee = null;
      }
    }

    private static int spacer = 0;

    /// <summary>
    /// Adjust the scale of the preview based on the distance between controllerStartPosition to
    /// the controller's current position.
    /// </summary>
    private void ScalePreviewsToFill() {
      Vector3 currentDelta;
      if (peltzerController.isBlockMode && isSnapping) {
        // Fill in grid units and produce "correct"  x, y, and z ratios, meaning ratios that produce a
        // uniformly scaled primitive when being used in volume insertion; for example, click and
        // dragging a cube will only produce a perfect cube.
        currentDelta = GetGridFillDiagonal(/*uniformScale*/ true);
      } else if (peltzerController.isBlockMode) {
        // Fill in grid units.
        currentDelta = GetGridFillDiagonal(/*uniformScale*/ false);
      } else if (isSnapping) {
        // Fill smoothly and produce "correct"  x, y, and z ratios, meaning ratios that produce a
        // uniformly scaled primitive when being used in volume insertion; for example, click and
        // dragging a cube will only produce a perfect cube.
        currentDelta = GetSmoothFillDiagonal(/*uniformScale*/ true);
      } else {
        // Just fill smoothly.
        currentDelta = GetSmoothFillDiagonal(/*uniformScale*/ false);
      }
      Vector3 newQuadrant = GetQuadrant(peltzerController.LastPositionModel - controllerStartPosition);

      // If the quadrant has changed reset the locked corner.
      bool quadrantChanged = newQuadrant != currentQuadrant;
      if (quadrantChanged) {
        currentQuadrant = newQuadrant;
        lockedCorner = FindLockedCorner();
      }

      foreach (HeldMesh heldMesh in heldMeshes) {
        // Move the vertices.
        MMesh.GeometryOperation vertScaleOperation = heldMesh.Mesh.StartOperation();
        Vector3 newOffset = Vector3.zero;
        Dictionary<int, Vector3> newVertexLocations = new Dictionary<int, Vector3>();

        Vector3 scaleFactor = Vector3.one +
          new Vector3(currentDelta.x / heldMesh.originalSize.x,
            currentDelta.y / heldMesh.originalSize.y,
            currentDelta.z / heldMesh.originalSize.z);

        // The only way to get from a scale factor of 1.0 to a scale factor of >1.1 is to first have passed a 
        // scale factor of 1.2, or to already be at 1.1. Essentially, you must drag to the 20% increase mark
        // before you can hit the 10% increase mark. This hysteresis helps users drag along a dominant axis without
        // accidentally adding noise on a minor axis. See bug for more details.
        if (scaleFactor.x < 1.2f && (lastFillingScaleFactor == null || lastFillingScaleFactor.Value.x < 1.1f)) {
          scaleFactor.x = 1f;
        }
        if (scaleFactor.y < 1.2f && (lastFillingScaleFactor == null || lastFillingScaleFactor.Value.y < 1.1f)) {
          scaleFactor.y = 1f;
        }
        if (scaleFactor.z < 1.2f && (lastFillingScaleFactor == null || lastFillingScaleFactor.Value.z < 1.1f)) {
          scaleFactor.z = 1f;
        }

        lastFillingScaleFactor = scaleFactor;

        foreach (KeyValuePair<int, Vector3> pair in heldMesh.originalVertexLocations) {
          int id = pair.Key;
          Vector3 originalLoc = pair.Value;
          Vector3 scaledVertexLoc = Vector3.Scale(originalLoc, scaleFactor);

          vertScaleOperation.ModifyVertexMeshSpace(id, scaledVertexLoc);
          newOffset += scaledVertexLoc;
          
          }
        
        newOffset /= heldMesh.originalVertexLocations.Count;

        // Move the offset to the center, and move the vertices in the opposite direction to keep them in the right place.
        Dictionary<int, Vertex> newVertices = new Dictionary<int, Vertex>();
        Vector3 offsetDelta = newOffset - heldMesh.Mesh.offset;
        foreach (KeyValuePair<int, Vector3> pair in heldMesh.originalVertexLocations) {
          vertScaleOperation.ModifyVertexMeshSpace(pair.Key, 
            vertScaleOperation.GetCurrentVertexPositionMeshSpace(pair.Key) - offsetDelta);
        }

        vertScaleOperation.Commit();
        // And update the offset and the vertices.
        heldMesh.Mesh.offset = newOffset;
        heldMesh.Mesh.RecalcBounds();
        
        // And regenerate the previews.
        MMesh.AttachMeshToGameObject(worldSpace, heldMesh.Preview, heldMesh.Mesh, /* updateOnly */ true);

        // Then re-position them to be locked to one corner.
        MeshWithMaterialRenderer renderMesh = heldMesh.Preview.GetComponent<MeshWithMaterialRenderer>();
        Vector3 updatedPosition =
          ResetVolumePosition(heldMesh.fillStartPosition, heldMesh.fillStartExtents, heldMesh.Mesh.bounds.extents);

        renderMesh.SetPositionModelSpace(updatedPosition, /* smooth */ false);
        renderMesh.SetOrientationModelSpace(heldMesh.fillStartRotation, /* smooth */ false);

        bool useRuler = heldMesh.isPrimitive && Features.showVolumeInserterRuler;
        if (useRuler) DisplayRuler(heldMesh, scaleFactor);
      }
    }

    // Displays a ruler for drag-sizing a primitive. Most of this is calculations for properly aligning text depending
    // on where the user drags.
    private void DisplayRuler(HeldMesh heldMesh, Vector3 scaleFactor) {
      ruler.SetActive(true);
      paletteRuler.SetActive(true);

      // Dimensions and half dimensions of the mesh we're measuring, using its oriented bounding box rather than AABB.
      float dimX = heldMesh.originalSize.x;
      float dimY = heldMesh.originalSize.y;
      float dimZ = heldMesh.originalSize.z;
      float hDimX = dimX / 2f;
      float hDimY = dimY / 2f;
      float hDimZ = dimZ / 2f;

      // Mesh space coords of the extents of the oriented bounding box - used for text positioning.
      float leftX, rightX;
      float bottomY, topY;
      float frontZ, backZ;
      
      // model space x coord immediately to the left of the primitive
      leftX = currentQuadrant.x == 1
        ? -hDimX
        : -dimX * scaleFactor.x + hDimX;
      
      // model space x coord for the right side on the primitive
      rightX = currentQuadrant.x != 1
        ? hDimX
        : dimX * scaleFactor.x - hDimX;
      
      // model space y coord immediately on top of the primitive
      bottomY = currentQuadrant.y == 1
        ? -hDimY
        : -dimY * scaleFactor.y + hDimY;
      
      // model space y coord immediately on top of the primitive
      topY = currentQuadrant.y != 1
        ? hDimY
        : dimY * scaleFactor.y - hDimY;

      // model space z coord to the front of the model
      frontZ = currentQuadrant.z == 1
        ? -hDimZ
        : -dimZ * scaleFactor.z + hDimZ;       
 
      // model space z coord to the front of the model
      backZ = currentQuadrant.z != 1
        ? hDimZ
        : dimZ * scaleFactor.z - hDimZ;
      
      XFrontText.text = (Mathf.Round(heldMesh.originalSize.x * scaleFactor.x * 100) / 100).ToString("0.00") + "m";
      XTopText.text = XFrontText.text;
      float XTwidth2 = ruler.transform.localScale.x * XFrontText.textBounds.size.x / 1f;
      float XTheight2 = ruler.transform.localScale.x * XFrontText.textBounds.size.y / 2f;
      
      YFrontText.text = (Mathf.Round(heldMesh.originalSize.y * scaleFactor.y * 100) / 100).ToString("0.00") + "m";
      YSideText.text = YFrontText.text;
      float YTwidth2 = ruler.transform.localScale.x * YFrontText.textBounds.size.x / 1f;
      float YTheight2 = ruler.transform.localScale.x * YFrontText.textBounds.size.y / 2f;
      
      ZTopText.text = (Mathf.Round(heldMesh.originalSize.z * scaleFactor.z * 100) / 100).ToString("0.00") + "m";
      ZSideText.text = ZTopText.text;
      float ZTwidth2 = ruler.transform.localScale.x * ZTopText.textBounds.size.x / 1f;
      float ZTheight2 = ruler.transform.localScale.x * ZTopText.textBounds.size.y / 2f;
      
      Vector3 XFrontPos, XTopPos, YFrontPos, YSidePos, ZTopPos, ZSidePos;
      Quaternion XFrontRot, XTopRot, YFrontRot, YSideRot, ZTopRot, ZSideRot;
      
      // Win Z-Fights
      float epsilon = 0.001f;
      
            
      // This section figures out which side of the box the user is most facing, and ensures that we display 
      // measurements on it even if controller position would indicate otherwise.
      
      // Using vector from headset to BB center - raw view direction is more unpredictable
      Vector3 center = heldMesh.fillStartPosition + heldMesh.fillStartRotation *
        new Vector3((leftX + rightX) * 0.5f, (topY + bottomY) * 0.5f, (frontZ + backZ) * 0.5f);
      Vector3 modelSpaceCameraForward = (center - worldSpace.WorldToModel(Camera.main.transform.position)).normalized;
      //Determine which side of the bounding box the user is most facing
      Vector3 bbUp, bbForward, bbRight;
      bbUp = heldMesh.fillStartRotation * Vector3.up;
      bbForward = heldMesh.fillStartRotation * Vector3.forward;
      bbRight = heldMesh.fillStartRotation * Vector3.right;
      
      // Order
      // 0 Right
      // 1 Left
      // 2 Top
      // 3 Bottom
      // 4 Back
      // 5 Front
      float[] dots = new float[6];

      dots[0] = Vector3.Dot(bbRight.normalized, modelSpaceCameraForward);
      dots[1] = Vector3.Dot(-bbRight.normalized, modelSpaceCameraForward);
      dots[2] = Vector3.Dot(bbUp.normalized, modelSpaceCameraForward);
      dots[3] = Vector3.Dot(-bbUp.normalized, modelSpaceCameraForward);
      dots[4] = Vector3.Dot(bbForward.normalized, modelSpaceCameraForward);
      dots[5] = Vector3.Dot(-bbForward.normalized, modelSpaceCameraForward);
      //Pick the most negative dot product - that face is most facing the view direction
      int mostFacingFace = 0;

      for (int i = 1; i <= 5; i++) {
        if (dots[i] < dots[mostFacingFace]) {
          mostFacingFace = i;
        }
      }
      
      // Handle left/right alignment
      if ((currentQuadrant.x > 0 || mostFacingFace == 0) && mostFacingFace != 1) {
        // Align right
        ZSideRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          * Quaternion.AngleAxis(-90f, Vector3.up));
        YSideRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          *  Quaternion.AngleAxis(90f, Vector3.right) * Quaternion.AngleAxis(-90f, Vector3.up));

        YSidePos.x = rightX + epsilon;
        ZSidePos.x = rightX + epsilon;
        XFrontPos.x = rightX - XTwidth2;
        YFrontPos.x = rightX - YTheight2;
        XTopPos.x = rightX - XTwidth2;
        ZTopPos.x = rightX - ZTheight2;
      } else {
        // Align left
        ZSideRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          * Quaternion.AngleAxis(90f, Vector3.up));
        YSideRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          * Quaternion.AngleAxis(90f, Vector3.right) * Quaternion.AngleAxis(90f, Vector3.up) );

        XFrontPos.x = leftX + XTwidth2;
        YFrontPos.x = leftX + YTheight2;
        YSidePos.x = leftX - epsilon;
        ZSidePos.x = leftX - epsilon;
        XTopPos.x = leftX + XTwidth2;
        ZTopPos.x = leftX + ZTheight2;
      }
      
      // Handle top/bottom alignment
      if ((currentQuadrant.y > 0 || mostFacingFace == 2) && mostFacingFace != 3) {
        // Align top
        XTopRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          * Quaternion.AngleAxis(90f, Vector3.right));
        ZTopRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          *  Quaternion.AngleAxis(90f, Vector3.up) * Quaternion.AngleAxis(90f, Vector3.right));
      
        YSidePos.y = topY - YTwidth2;
        ZSidePos.y = topY - ZTheight2;
        XFrontPos.y = topY - XTheight2;
        YFrontPos.y = topY - YTwidth2;
        XTopPos.y = topY + epsilon;
        ZTopPos.y = topY + epsilon;
      } else {
        // Align bottom
        XTopRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          * Quaternion.AngleAxis(-90f, Vector3.right));
        ZTopRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          *  Quaternion.AngleAxis(90f, Vector3.up) * Quaternion.AngleAxis(-90f, Vector3.right));
      
        YSidePos.y = bottomY + YTwidth2;
        ZSidePos.y = bottomY + ZTheight2;
        XFrontPos.y = bottomY + XTheight2;
        YFrontPos.y = bottomY + YTwidth2;
        XTopPos.y = bottomY - epsilon;
        ZTopPos.y = bottomY - epsilon;      
      }
      
      // Handle front/back alignment
      if ((currentQuadrant.z > 0 || mostFacingFace == 4) && mostFacingFace != 5) {
        // Align back
        XFrontRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation 
          * Quaternion.AngleAxis(180f, Vector3.up));
        YFrontRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation
          * Quaternion.AngleAxis(180f, Vector3.up) * Quaternion.AngleAxis(90f, Vector3.forward));

        YSidePos.z = backZ - YTheight2;
        ZSidePos.z = backZ - ZTwidth2;
        XFrontPos.z = backZ + epsilon;
        YFrontPos.z = backZ + epsilon;
        XTopPos.z = backZ - XTheight2;
        ZTopPos.z = backZ - ZTwidth2;
      } else {
        // Align front
        XFrontRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation);
        YFrontRot = worldSpace.ModelOrientationToWorld(heldMesh.fillStartRotation *
          Quaternion.AngleAxis(90f, Vector3.forward));

        YSidePos.z = frontZ + YTheight2;
        ZSidePos.z = frontZ + ZTwidth2;
        XFrontPos.z = frontZ - epsilon;
        YFrontPos.z = frontZ - epsilon;
        XTopPos.z = frontZ + XTheight2;
        ZTopPos.z = frontZ + ZTwidth2;
      }

      
      // Position the cubes that frame the dimensions we are measuring - this essentially frames an oriented bounding
      // box for our primitives (even though their real bounding box is AABB).
      SetBoundingBoxCubeXForm(pXpYpZ, heldMesh, new Vector3(rightX, topY, backZ));
      SetBoundingBoxCubeXForm(pXpYnZ, heldMesh, new Vector3(rightX, topY, frontZ));
      SetBoundingBoxCubeXForm(pXnYpZ, heldMesh, new Vector3(rightX, bottomY, backZ));
      SetBoundingBoxCubeXForm(pXnYnZ, heldMesh, new Vector3(rightX, bottomY, frontZ));
      SetBoundingBoxCubeXForm(nXpYpZ, heldMesh, new Vector3(leftX, topY, backZ));
      SetBoundingBoxCubeXForm(nXpYnZ, heldMesh, new Vector3(leftX, topY, frontZ));
      SetBoundingBoxCubeXForm(nXnYpZ, heldMesh, new Vector3(leftX, bottomY, backZ)); 
      SetBoundingBoxCubeXForm(nXnYnZ, heldMesh, new Vector3(leftX, bottomY, frontZ));
      
      // Sets the text on the paletteRuler which appears above the palette
      paletteRuler.GetComponent<TextMeshPro>().text = XFrontText.text + " x " + YFrontText.text + " x " + ZTopText.text;

      // Setting the transforms for all of the other text elements
      SetRulerXForm(rulerYSide, heldMesh, YSideRot, YSidePos);
      SetRulerXForm(rulerZSide, heldMesh, ZSideRot, ZSidePos);
      SetRulerXForm(rulerXFrontBack, heldMesh, XFrontRot, XFrontPos);
      SetRulerXForm(rulerYFrontBack, heldMesh, YFrontRot, YFrontPos);
      SetRulerXForm(rulerXTopBottom, heldMesh, XTopRot, XTopPos);
      SetRulerXForm(rulerZTopBottom, heldMesh, ZTopRot, ZTopPos);
    }

    private void SetBoundingBoxCubeXForm(GameObject bbCube, HeldMesh heldMesh, Vector3 positionMesh) {
      bbCube.transform.rotation = heldMesh.fillStartRotation;
      bbCube.transform.position = worldSpace.ModelToWorld(heldMesh.fillStartPosition
        + heldMesh.fillStartRotation * positionMesh);
    }
    
    private void SetRulerXForm(GameObject ruler, HeldMesh heldMesh, Quaternion orientationWorld, Vector3 positionMesh) {
      ruler.transform.rotation = orientationWorld;
      ruler.transform.position =
        worldSpace.ModelToWorld(heldMesh.fillStartPosition + heldMesh.fillStartRotation * positionMesh);
    }

    /// <summary>
    /// Corrects the diagonal of a rectangular prism such that it produces a primitive volume with "correct"
    /// x, y, and z ratios, meaning ratios that produce a uniformly scaled primitive when being used in volume insertion;
    /// for example, click and dragging a cube will only produce a perfect cube.
    /// All primitives except the torus fit correctly in a 1x1x1 rectangular prism; the torus fits in
    /// a 4x1x4 prism, and the diagonal must be scaled accordingly.
    /// </summary>
    private Vector3 CorrectRatio(Vector3 diagonal) {
      float maxExtent = Mathf.Max(diagonal.x, diagonal.y, diagonal.z);
      bool isTorus = (Primitives.Shape)peltzerController.shapesMenu.CurrentItemId == Primitives.Shape.TORUS;
      return new Vector3(maxExtent, isTorus ? maxExtent * 0.25f : maxExtent, maxExtent);
    }
    
    /// <summary>
    ///   Finds the diagonal for the rectangular prism that the volumeMesh should fill that fills in grid units.
    /// </summary>
    /// <param name="uniformScale">Whether the diagonal should be corrected before being returned so that
    /// it produces a volume with uniform x, y, and z ratios.</param>
    /// <returns>
    ///   The diagonal for the rectangular prism that is snapped to the grid and not smaller than the simple mesh.
    /// </returns>
    private Vector3 GetGridFillDiagonal(bool uniformScale) {
      Vector3 diagonal = peltzerController.LastPositionModel - controllerStartPosition;

      // We can't work out the 'rotation' of a group of meshes yet, so we pick one at random.
      diagonal = Quaternion.Inverse(GetFirstHeldMesh().fillStartRotation) * diagonal;

      // Find the controller delta in grid units, ensuring that the rectangular prism is not smaller than
      // the bounds of the original insertion.
      // We remove the original size of the bounding box: a user must move as far as the bounding box on
      // any given axis to begin filling.
      diagonal.x = Mathf.Max(0, GridUtils.SnapToGrid(Mathf.Abs(diagonal.x) - extentsOfHeldMeshes.x));
      diagonal.y = Mathf.Max(0, GridUtils.SnapToGrid(Mathf.Abs(diagonal.y) - extentsOfHeldMeshes.y));
      diagonal.z = Mathf.Max(0, GridUtils.SnapToGrid(Mathf.Abs(diagonal.z) - extentsOfHeldMeshes.z));

      return uniformScale ? CorrectRatio(diagonal) : diagonal;
    }
    
    /// <summary>
    ///   Finds the diagonal for the rectangular prism that the volumeMesh should fill that ensures a smooth
    ///   fill effect.
    /// </summary>
    /// <param name="uniformScale">Whether the diagonal should be corrected before being returned so that
    /// it produces a volume with uniform x, y, and z ratios.</param>
    /// <returns>
    ///   The diagonal for the rectangular prism that is NOT NECESSARILY snapped to the grid
    ///   and not smaller than the simple mesh.
    /// </returns>
    private Vector3 GetSmoothFillDiagonal(bool uniformScale) {
      Vector3 diagonal = peltzerController.LastPositionModel - controllerStartPosition;

      // We can't work out the 'rotation' of a group of meshes yet, so we pick one at random.
      diagonal = Quaternion.Inverse(GetFirstHeldMesh().fillStartRotation) * diagonal;

      // We remove the original size of the bounding box: a user must move as far as the bounding box on
      // any given axis to begin filling.
      diagonal.x = Mathf.Max(0, Mathf.Abs(diagonal.x) - extentsOfHeldMeshes.x);
      diagonal.y = Mathf.Max(0, Mathf.Abs(diagonal.y) - extentsOfHeldMeshes.y);
      diagonal.z = Mathf.Max(0, Mathf.Abs(diagonal.z) - extentsOfHeldMeshes.z);

      return uniformScale ? CorrectRatio(diagonal) : diagonal;
    }

    /// <summary>
    ///   Determines what quadrant a vector is pointing towards.
    /// </summary>
    /// <param name="diagonal">The vector to check.</param>
    /// <returns>A Vector3 representing the quadrant.</returns>
    private Vector3 GetQuadrant(Vector3 diagonal) {
      // We can't work out the 'rotation' of a group of meshes yet, so we pick one at random.
      diagonal = Quaternion.Inverse(GetFirstHeldMesh().fillStartRotation) * diagonal;
      Vector3 quadrant = new Vector3();

      quadrant.x = (diagonal.x >= 0) ? 1 : -1;
      quadrant.y = (diagonal.y >= 0) ? 1 : -1;
      quadrant.z = (diagonal.z >= 0) ? 1 : -1;

      return quadrant;
    }

    /// <summary>
    ///   Moves the volumeMesh so that its position in world space, after scaling, is locked at the lockedCorner.
    /// </summary>
    /// <param name="previousPosition">The position of the volumeMesh before scaling.</param>
    /// <param name="previousExtents">The extents of the bounding box around the volumeMesh before scaling.</param>
    /// <param name="finalExtents">The extents of the bounding box around the volumeMesh after scaling.</param>
    private Vector3 ResetVolumePosition(Vector3 previousPosition, Vector3 previousExtents, Vector3 finalExtents) {
      Vector3 delta = finalExtents - previousExtents;
      Vector3 identityPosition = new Vector3();

      identityPosition.x = (lockedCorner.x == 1) ? previousPosition.x - delta.x : previousPosition.x + delta.x;
      identityPosition.y = (lockedCorner.y == 1) ? previousPosition.y - delta.y : previousPosition.y + delta.y;
      identityPosition.z = (lockedCorner.z == 1) ? previousPosition.z - delta.z : previousPosition.z + delta.z;

      Vector3 shift = identityPosition - previousPosition;
      // We can't work out the 'rotation' of a group of meshes yet, so we pick one at random.
      Vector3 rotatedShift = GetFirstHeldMesh().fillStartRotation * shift;

      Vector3 rotatedPosition = previousPosition + (rotatedShift.normalized
        * Vector3.Distance(previousPosition, identityPosition));

      return rotatedPosition;
    }

    /// <summary>
    ///   Hides all held meshes from view by deactivating their previews.
    /// </summary>
    public void Hide() {
      if (!isHidden) {
        isHidden = true;
        foreach (HeldMesh heldMesh in heldMeshes) {
          heldMesh.Preview.SetActive(false);
        }
      }
    }

    /// <summary>
    ///   Unhides all held meshes. Note that this does not set the position or rotation of the held meshes,
    ///   which may not have been updated whilst the meshes were hidden.
    /// </summary>
    public void Unhide() {
      if (isHidden) {
        isHidden = false;
        foreach (HeldMesh heldMesh in heldMeshes) {
          heldMesh.Preview.SetActive(true);
        }
      }
    }

    /// <summary>
    ///   Finds the lockedCorner based on the quadrant the diagonal of the rectangular prism is pointing towards.
    /// </summary>
    /// <returns>The inverse of the quadrant which represents the lockedCorner.</returns>
    private Vector3 FindLockedCorner() {
      return currentQuadrant * -1;
    }

    public HeldMesh GetFirstHeldMesh() {
      return heldMeshes[0];
    }

    public IEnumerable<MMesh> GetMeshes() {
      return heldMeshes.Select(h => h.Mesh);
    }

    public IEnumerable<int> GetMeshIds() {
      return heldMeshes.Select(h => h.Mesh.id);
    }

    public void DestroyPreviews() {
      ruler.SetActive(false);
      paletteRuler.SetActive(false);
      foreach (HeldMesh heldMesh in heldMeshes) {
        DestroyImmediate(heldMesh.Preview);
      }
      if (currentFaceSnapEffect != null) {
        currentFaceSnapEffect.Finish();
        currentFaceSnapEffect = null;
      }
    }
  }
}
