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
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.tools {
  /// <summary>
  ///   A tool that handles the hovering and selection of meshes, faces, or vertices.
  /// </summary>
  public class Selector : MonoBehaviour, IMeshRenderOwner {
    /// <summary>
    /// Options to customize the selector's selection logic.
    /// </summary>
    public struct SelectorOptions {
      /// Whether meshes can be selected.
      public bool includeMeshes;
      /// Whether faces can be selected.
      public bool includeFaces;
      /// Whether edges can be selected.
      public bool includeEdges;
      /// Whether vertices can be selected.
      public bool includeVertices;
      /// <summary>
      /// Whether to honor grouping when selecting meshes (if true, if the user selects one mesh in
      /// a group, all the other meshes in the group will also be selected).
      /// </summary>
      public bool includeMeshGroups;

      public SelectorOptions(bool includeMeshes, bool includeFaces, bool includeEdges, bool includeVertices,
          bool includeMeshGroups) {
        this.includeMeshes = includeMeshes;
        this.includeFaces = includeFaces;
        this.includeEdges = includeEdges;
        this.includeVertices = includeVertices;
        this.includeMeshGroups = includeMeshGroups;
      }
    }

    // The types of selectable objects.
    public enum Type { VERTEX, EDGE, FACE, MESH };

    public static SelectorOptions NONE = new SelectorOptions(false, false, false, false, false);
    public static SelectorOptions ALL = new SelectorOptions(true, true, true, true, true);
    public static SelectorOptions MESHES_ONLY = new SelectorOptions(true, false, false, false, true);
    public static SelectorOptions MESHES_ONLY_IGNORE_GROUPS = new SelectorOptions(true, false, false, false, false);
    public static SelectorOptions FACES_ONLY = new SelectorOptions(false, true, false, false, false);
    public static SelectorOptions VERTICES_ONLY = new SelectorOptions(false, false, false, true, false);
    public static SelectorOptions EDGES_ONLY = new SelectorOptions(false, false, true, false, false);
    public static SelectorOptions FACES_EDGES_AND_VERTICES = new SelectorOptions(false, true, true, true, false);
    public static SelectorOptions FACES_AND_EDGES = new SelectorOptions(false, true, true, false, false);
    public static SelectorOptions NOT_MESHES = new SelectorOptions(false, true, true, true, true);
    public static SelectorOptions NOT_FACES = new SelectorOptions(true, false, true, true, true);
    public static SelectorOptions NOT_EDGES = new SelectorOptions(true, true, false, true, true);
    public static SelectorOptions NOT_VERTICES = new SelectorOptions(true, true, true, false, true);

    /// <summary>
    /// Modes for which selection should be active.
    /// </summary>
    private static readonly List<ControllerMode> selectModes = new List<ControllerMode>() {
      ControllerMode.extrude,
      ControllerMode.reshape,
      ControllerMode.move,
      ControllerMode.insertVolume,
    };

    /// <summary>
    ///   The scale of the dots for the multiselect trail.
    /// </summary>
    //private readonly Vector3 MULTISELECT_DOT_SCALE = new Vector3(0.0025f, 0.0025f, 0.0025f);
    private readonly Vector3 MULTISELECT_DOT_SCALE = new Vector3(0.0125f, 0.0125f, 0.0125f);
    /// <summary>
    ///   The scale of the indicator dot for the multiselect trail.
    /// </summary>
    private readonly Vector3 MULTISELECT_INDICATOR_SCALE = new Vector3(0.015f, 0.015f, 0.015f);
    /// <summary>
    ///   The animation time for the multiselect indicator animation.
    /// </summary>
    private const float MULTISELECT_INDICATOR_REVEAL_TIME = 0.15f;
    /// <summary>
    ///   Used to determine minimum spacing between dots in multiselect trail.
    /// </summary>
    private const float SPACING = 0.01f;

    // The size of the selection indicator - used both for display and for determining when it overlaps
    // selectable geometry.
    private const float SELECT_BALL_SIZE_WORLD = 0.005f;

    // How far away from the front of a face the edge of the selection indicator needs to be to select it.
    private const float FACE_SELECTION_DISTANCE_WORLD = 0.000f;
    // How far beneath a face the selection indicator can be and still select it.
    private const float ADDITIONAL_FACE_DEPTH_DISTANCE = 0.015f;
    // When we're only selecting faces, how far from a face we need to be to select it.
    private const float SELECTION_THRESHOLD_FACES_ONLY = 0.015f;
    private const float INCREASED_SELECTION_FACTOR = 4f;

    /// <summary>
    ///   A threshold above which a mesh is considered too far away to select.
    ///   This is in Unity units, where 1.0f = 1 meter by default.
    /// </summary>
    private const float MESH_CLOSENESS_THRESHOLD_DEFAULT = 0.1f;
    /// <summary>
    ///   A threshold above which a face is considered too far away to select for Mesh selection.
    ///   This is in Unity units, where 1.0f = 1 meter by default.
    /// </summary>
    private const float FACE_CLOSENESS_THRESHOLD_DEFAULT = 0.015f;
    /// <summary>
    ///   The threshold above which a face is considered too far away for the selector during mesh selection.
    /// </summary>
    public float faceClosenessThreshold = FACE_CLOSENESS_THRESHOLD_DEFAULT;
    /// <summary>
    ///   The position in model coords the selector should be centered on.
    /// </summary>
    public Vector3 selectorPosition;
    public ControllerMain controllerMain;
    /// <summary>
    ///   A reference to a controller for getting position.
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
    ///   The vertex currently being hovered over.
    /// </summary>
    public VertexKey hoverVertex { get; private set; }
    /// <summary>
    ///   The face currently being hovered over, and its planar highlight.
    /// </summary>
    public FaceKey hoverFace { get; private set; }
    /// <summary>
    ///   The face currently being hovered over.
    /// </summary>
    public EdgeKey hoverEdge { get; private set; }
    
    /// <summary>
    ///   The meshes currently being hovered over, and their mesh highlights.
    /// </summary>
    public HashSet<int> hoverMeshes { get; private set; }

    /// <summary>
    /// Temporary hashset - maintained as a global to avoid it getting GCed.  Instead should manually clear after use.
    /// </summary>
    private HashSet<int> tempRemovalHashset = new HashSet<int>();

    /// <summary>
    ///   The vertices selected in the most recent multi-select operation.
    /// </summary>
    public HashSet<VertexKey> selectedVertices { get; private set; }
    /// <summary>
    ///   The faces selected in the most recent multi-select operation, and planar highlights.
    /// </summary>
    public HashSet<FaceKey> selectedFaces { get; private set; }
    /// <summary>
    ///   The edges selected in the most recent multi-select operation.
    /// </summary>
    public HashSet<EdgeKey> selectedEdges { get; private set; }
    /// <summary>
    ///   The meshes selected in the most recent multi-select operation, and mesh highlights.
    /// </summary>
    public HashSet<int> selectedMeshes { get; private set; }

    private InactiveSelectionHighlighter inactiveSelectionHighlighter;

    private Bounds boundingBoxOfAllSelections;

    // Cached results of lookups from the spatial index.
    int? nearestMesh;
    List<DistancePair<FaceKey>> nearbyFaces = new List<DistancePair<FaceKey>>();
    List<DistancePair<EdgeKey>> nearbyEdges = new List<DistancePair<EdgeKey>>();
    List<DistancePair<VertexKey>> nearbyVertices = new List<DistancePair<VertexKey>>();

    // Stacks for undoing multi-select.
    private Stack<VertexKey> undoVertexMultiSelect = new Stack<VertexKey>();
    private Stack<EdgeKey> undoEdgeMultiSelect = new Stack<EdgeKey>();
    private Stack<FaceKey> undoFaceMultiSelect = new Stack<FaceKey>();
    private Stack<int> undoMeshMultiSelect = new Stack<int>();

    // Stacks for redoing multi-select.
    private Stack<VertexKey> redoVertexMultiSelect = new Stack<VertexKey>();
    private Stack<EdgeKey> redoEdgeMultiSelect = new Stack<EdgeKey>();
    private Stack<FaceKey> redoFaceMultiSelect = new Stack<FaceKey>();
    public Stack<int> redoMeshMultiSelect { get; private set; }

  /// <summary>
  /// Whether multi-select is currently enabled.
  /// </summary>
  private bool multiSelectEnabled = false;
    /// <summary>
    /// Whether multi-select is currently turned on.
    /// </summary>
    public bool isMultiSelecting { get; private set; }

    private WorldSpace worldSpace;

    /// <summary>
    ///   Parent game object to hold all "dots" of the multiselection trail.
    /// </summary>
    private GameObject multiselectTrail;
    /// <summary>
    ///   The last dot placed - reference in support of animations.
    /// </summary>
    private GameObject lastDot;
    /// <summary>
    ///   The position of the last dot placed. Used for calculating distance.
    /// </summary>
    private Vector3 lastDotPosition;
    /// <summary>
    ///   The lifetime decay value for dots in the multitrail.
    /// </summary>
    private float multiselectTrailTime = 0.4f;
    /// <summary>
    ///   The material for the dots in the multiselect trail.
    /// </summary>
    private Material multiselectDotMaterial;
    /// <summary>
    ///   HighlightUtils for managing mesh highlights.
    /// </summary>
    private HighlightUtils highlightUtils;

    /// <summary>
    ///   Indicator showing the target position of selection.
    /// </summary>
    GameObject selectIndicator;

    /// <summary>
    /// The radius in world coords of rendered points. This is pulled from the point rendering shader to ensure
    /// that selection radii are visually consistent with it.
    /// </summary>
    public float pointRadiusWorld;
    /// <summary>
    /// The radius in world coords of rendered edges. This is pulled from the edge rendering shader to ensure
    /// that selection radii are visually consistent with it.
    /// </summary>
    public float edgeRadiusWorld;

    // List of dots in multiselect trail.
    List<MSdot> DOTS = new List<MSdot>();

    // Mesh used for dots.
    Mesh DOTSmesh;

    /// <summary>
    ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
    ///   As such, this setup method must be called before the tool is used for it to have a valid state.
    /// </summary>
    public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController,
      PaletteController paletteController, WorldSpace worldSpace, SpatialIndex spatialIndex,
      HighlightUtils highlightUtils, MaterialLibrary materialLibrary) {
      this.model = model;
      this.controllerMain = controllerMain;
      this.peltzerController = peltzerController;
      this.worldSpace = worldSpace;
      this.spatialIndex = spatialIndex;
      this.highlightUtils = highlightUtils;
      this.inactiveSelectionHighlighter = new InactiveSelectionHighlighter(spatialIndex, highlightUtils, worldSpace,
        model);

      controllerMain.ControllerActionHandler += ControllerEventHandler;
      peltzerController.ModeChangedHandler += ModeChangeEventHandler;

      DOTSmesh = Resources.Load("Models/IcosphereSmall") as Mesh;

      hoverMeshes = new HashSet<int>();
      hoverFace = null;
      hoverEdge = null;
      hoverVertex = null;
      selectedMeshes = new HashSet<int>();
      selectedFaces = new HashSet<FaceKey>();
      selectedEdges = new HashSet<EdgeKey>();
      selectedVertices = new HashSet<VertexKey>();

      redoMeshMultiSelect = new Stack<int>();

      // Setup the multi-select components.
      multiselectDotMaterial = materialLibrary.selectMaterial;
      multiselectTrail = new GameObject("multiselectTrail");
      multiselectTrail.transform.position = -1f * worldSpace.WorldToModel(Vector3.zero);
      multiSelectEnabled = selectModes.Contains(peltzerController.mode);

      this.edgeRadiusWorld = materialLibrary.edgeHighlightMaterial.GetFloat("_PointSphereRadius");
      this.pointRadiusWorld = materialLibrary.pointHighlightMaterial.GetFloat("_PointSphereRadius");
    }

    void Update() {
      // Handle selector animations.
      // Multi-Selection
      if (isMultiSelecting) {
        UpdateMultiselectionTrail();
      }
    }

    public void TurnOnSelectIndicator() {
      if(selectIndicator == null) {
        selectIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        selectIndicator.name = "SelectIndicator";
        selectIndicator.GetComponent<MeshRenderer>().material = multiselectDotMaterial;
        selectIndicator.GetComponent<SphereCollider>().enabled = false;
        selectIndicator.transform.localScale = new Vector3(SELECT_BALL_SIZE_WORLD, SELECT_BALL_SIZE_WORLD, SELECT_BALL_SIZE_WORLD);
      }
      selectIndicator.transform.position = peltzerController.wandTip.transform.position;
      selectIndicator.transform.rotation = peltzerController.wandTip.transform.rotation;
    }

    public void TurnOffSelectIndicator() {
      if (selectIndicator != null) {
        GameObject.DestroyImmediate(selectIndicator);
      }
    }

    /// <summary>
    ///   Every tool should call Select from Update to try to select instead of within Selector.
    /// </summary>
    /// <param name="position">The position to select at.</param>
    public void SelectAtPosition(Vector3 position, SelectorOptions options) {
      if (!PeltzerController.AcquireIfNecessary(ref peltzerController)) {
        return;
      }

      // While the menu is being pointed at, no selection is possible. See bug for discussion.
      if (peltzerController.isPointingAtMenu) {
        return;
      }

      selectorPosition = position;

      // Start selection by finding all nearby elements if specified in SelectorOptions.
      if (options.includeVertices && selectedEdges.Count == 0 && selectedFaces.Count == 0) {
        spatialIndex.FindVerticesClosestTo(selectorPosition,
          (InactiveRenderer.GetVertScaleFactor(worldSpace) + SELECT_BALL_SIZE_WORLD) / worldSpace.scale,
          out nearbyVertices);
      } else {
        nearbyVertices.Clear();
      }


      if (options.includeEdges && selectedVertices.Count == 0 && selectedFaces.Count == 0) {
        spatialIndex.FindEdgesClosestTo(selectorPosition,
          (InactiveRenderer.GetEdgeScaleFactor(worldSpace) + SELECT_BALL_SIZE_WORLD) / worldSpace.scale,
          /*ignoreInEdge*/ false, out nearbyEdges);
      } else {
        nearbyEdges.Clear();
      }

      if (options.includeFaces && selectedEdges.Count == 0 && selectedVertices.Count == 0) {
        spatialIndex.FindFacesClosestTo(selectorPosition, ADDITIONAL_FACE_DEPTH_DISTANCE / worldSpace.scale,
          /*ignoreInFace*/ false, out nearbyFaces);
      } else {
        nearbyFaces.Clear();
      }

      // Booleans used as out values indicating successful selection.
      bool successfulSelectionVertex;
      bool successfulSelectionEdge;
      bool successfulSelectionFace;

      // If the user has already selected something, look only for other things of that type.
      if (selectedVertices.Count > 0) {
        if (nearbyVertices.Count > 0) {
          TryHighlightingAVertex(nearbyVertices[0].value, out successfulSelectionVertex);
        }
        return;
      }

      if (selectedEdges.Count > 0) {
        if (nearbyEdges.Count > 0) {
          TryHighlightingAnEdge(nearbyEdges[0].value, out successfulSelectionEdge);
        }
        return;
      }
      if (selectedFaces.Count > 0) {
        if (nearbyFaces.Count > 0) {
          TryHighlightingAFace(nearbyFaces[0].value, position, out successfulSelectionFace);
        }
        return;
      }

      // Check for overlaps between the visible previews and the selection indicator.  We use the visual radii
      // of each to drive the selection in order to ensure that the selection is consistent with what the user
      // sees and expects.
      if (nearbyVertices.Count > 0
          && nearbyVertices[0].distance * worldSpace.scale < pointRadiusWorld + SELECT_BALL_SIZE_WORLD) {
        TryHighlightingAVertex(nearbyVertices[0].value, out successfulSelectionVertex);
        if (successfulSelectionVertex) {
          // Upon vertex selection, clear all other undo stacks and all redo stacks to indicate a new multi-selection.
          ClearMultiSelectUndoState(Type.VERTEX);
          ClearMultiSelectRedoState();
        }
        return;
      }

      // Same thing for edges.
      if (nearbyEdges.Count > 0
          && nearbyEdges[0].distance * worldSpace.scale < edgeRadiusWorld + SELECT_BALL_SIZE_WORLD) {
        TryHighlightingAnEdge(nearbyEdges[0].value, out successfulSelectionEdge);
        if (successfulSelectionEdge) {
          // Upon edge selection, clear all other undo stacks and all redo stacks to indicate a new multi-selection.
          ClearMultiSelectUndoState(Type.EDGE);
          ClearMultiSelectRedoState();
        }
        return;
      }

      // Faces are different since they don't have a volumetric select-target rendered - instead we allow them
      // to be "depthy" for the purposes of selection - giving them imaginary volume beneat the surface.
      // This also allows the user to select faces by reaching a bit further into the mesh.
      // In the event that face only selection is chosen, we increase the "in front of face" selection radius.  We don't
      // do this when edges and verts are selectable as this can cause slightly unintuitive behavior where the face
      // hover is "lost" when the user hits an edge or a vert - it's better to teach users that they need to reach into
      // the face to select it, as this is the best way to ensure a face is selected in a crowded environment.
      float faceDistance = (!options.includeVertices && !options.includeEdges ?
        SELECTION_THRESHOLD_FACES_ONLY : FACE_SELECTION_DISTANCE_WORLD) + SELECT_BALL_SIZE_WORLD;
      if (nearbyFaces.Count > 0) {
        FaceInfo faceInfo = spatialIndex.GetFaceInfo(nearbyFaces[0].value);
        float distanceToPlaneModel = faceInfo.plane.GetDistanceToPoint(selectorPosition);
        float distanceToPlane = distanceToPlaneModel * worldSpace.scale;
        if ((distanceToPlane < faceDistance)
            || (distanceToPlane < 0f && -distanceToPlane < ADDITIONAL_FACE_DEPTH_DISTANCE)) {
          // Can do better if we can generate point on the edge of the poly that is closest and check distance
          // but that's significantly harder, and will mostly be in positions where we'd prefer to select an edge.
          if (Math3d.IsInside(faceInfo.border, selectorPosition - faceInfo.plane.normal * distanceToPlaneModel)) {
            TryHighlightingAFace(nearbyFaces[0].value, selectorPosition, out successfulSelectionFace);
            if (successfulSelectionFace) {
              // Upon face selection, clear all other undo stacks and all redo stacks to indicate a new multi-selection.
              ClearMultiSelectUndoState(Type.FACE);
              ClearMultiSelectRedoState();
            }
            return;
          }
        }
      }

      // We didn't select anything. Clear any previous hover highlights.
      Deselect(ALL, /*deselectSelectedHighlights*/ false, /*deselectHoveredhighlights*/ true);
    }

    /// <summary>
    /// Every tool should call Select from Update to try to select instead of within Selector.
    /// </summary>
    /// <param name="position">The position to select at.</param>
    /// <param name="forceSelection">For use in special cases when we need to force selection. Usually we
    /// don't, so we default to false.</param>
    public void SelectMeshAtPosition(Vector3 position, SelectorOptions options, bool forceSelection = false) {
      if (!PeltzerController.AcquireIfNecessary(ref peltzerController)) {
        return;
      }

      // While the menu is being pointed at, no selection is possible. See bug for discussion.
      if (peltzerController.isPointingAtMenu) {
        return;
      }

      selectorPosition = position;
      // Mesh selection is just face selection but we will highlight the mesh the face belongs to if
      // options.includeMeshes == true;

      float selectionThreshold =
        isMultiSelecting && PeltzerMain.Instance.restrictionManager.increasedMultiSelectRadiusAllowed ?
         faceClosenessThreshold * INCREASED_SELECTION_FACTOR :
         faceClosenessThreshold;

      spatialIndex.FindFacesClosestTo(selectorPosition, selectionThreshold / worldSpace.scale,
        /*ignoreInFace*/ false, out nearbyFaces);

      bool successfulSelectionMesh;

      if (nearbyFaces.Count > 0) {
        TryHighlightingAMesh(nearbyFaces[0].value.meshId, options.includeMeshGroups, forceSelection, out successfulSelectionMesh);
        if (successfulSelectionMesh) {
          // Upon mesh selection, clear all other undo stacks and all redo stacks to indicate a new multi-selection.
          ClearMultiSelectUndoState(Type.MESH);
          ClearMultiSelectRedoState();
        }
        return;
      } else if (!isMultiSelecting || forceSelection) {
        selectionThreshold = MESH_CLOSENESS_THRESHOLD_DEFAULT;

        // If we are not multiselecting, we can afford to be a bit more flexible so that the user can
        // easily grab a mesh even though they are not hovering near any of its faces (for example, grab
        // a mesh from inside). If we are multiselecting, however, we don't want this behavior because it
        // might cause large objects to get accidentally selected while the user is trying to multiselect
        // smaller objects near it (see bug).
        int? nearestMesh = null;
        if (spatialIndex.FindNearestMeshTo(selectorPosition, selectionThreshold / worldSpace.scale, out nearestMesh)) {
          // If we didn't find any nearby faces, but have a nearby mesh, we'll highlight that.
          TryHighlightingAMesh(nearestMesh.Value, options.includeMeshGroups, forceSelection, out successfulSelectionMesh);
          return;
        }
      }

      // We didn't select anything. Clear any previous hover highlights.
      Deselect(MESHES_ONLY, /*deselectSelectedHighlights*/ false, /*deselectHoveredhighlights*/ true);
    }

    public void ResetInactive() {
      inactiveSelectionHighlighter.TurnOffVertsEdges();
    }

    public void UpdateInactive(SelectorOptions options) {
      if (selectedFaces.Count == 0) {
        if (selectedVertices.Count == 0 && selectedEdges.Count == 0) {
          if (options.includeEdges) {
            if (options.includeVertices) {
              inactiveSelectionHighlighter.ShowSelectableVertsEdgesNear(selectorPosition, selectedVertices, hoverVertex,
                selectedEdges, hoverEdge);
            } else {
              inactiveSelectionHighlighter.ShowSelectableEdgesNear(selectorPosition, selectedEdges, hoverEdge);
            }
          } else if (options.includeVertices) {
            inactiveSelectionHighlighter.ShowSelectableVertsNear(selectorPosition, selectedVertices, hoverVertex);
          }
        } else if (selectedVertices.Count > 0 && options.includeVertices) {
          inactiveSelectionHighlighter.ShowSelectableVertsNear(selectorPosition, selectedVertices, hoverVertex);
        } else if (selectedEdges.Count > 0 && options.includeEdges) {
          inactiveSelectionHighlighter.ShowSelectableEdgesNear(selectorPosition, selectedEdges, hoverEdge);
        }
      } else if (selectedFaces.Count > 0) {
        ResetInactive();
      }
    }

    /// <summary>
    ///   Returns an enumeration of:
    ///   - Selected vertices, if any; or
    ///   - Hovered vertices, if any; or
    ///   - Nothing (an empty enumeration).
    /// </summary>
    public IEnumerable<VertexKey> SelectedOrHoveredVertices() {
      if (selectedVertices.Count > 0) {
        return selectedVertices;
      } else {
        // If there are no hovered items, this will return the empty enumeration, which is our intention.
        return hoverVertex == null ?
          new List<VertexKey>() : new List<VertexKey> { hoverVertex };
      }
    }

    /// <summary>
    ///   Returns an enumeration of:
    ///   - Selected edges, if any; or
    ///   - Hovered edges, if any; or
    ///   - Nothing (an empty enumeration).
    /// </summary>
    public IEnumerable<EdgeKey> SelectedOrHoveredEdges() {
      if (selectedEdges.Count > 0) {
        return selectedEdges;
      } else {
        // If there are no hovered items, this will return the empty enumeration, which is our intention.
        return hoverEdge == null ?
          new List<EdgeKey>() : new List<EdgeKey> { hoverEdge };
      }
    }

    /// <summary>
    ///   Returns an enumeration of:
    ///   - Selected faces, if any; or
    ///   - Hovered faces, if any; or
    ///   - Nothing (an empty enumeration).
    /// </summary>
    public IEnumerable<FaceKey> SelectedOrHoveredFaces() {
      if (selectedFaces.Count > 0) {
        return selectedFaces;
      } else {
        // If there are no hovered items, this will return the empty enumeration, which is our intention.
        return hoverFace == null ?
          new List<FaceKey>() : new List<FaceKey> { hoverFace };
      }
    }

    /// <summary>
    ///   Returns an enumeration of:
    ///   - Selected meshes, if any; or
    ///   - Hovered meshes, if any; or
    ///   - Nothing (an empty enumeration).
    /// </summary>
    public IEnumerable<int> SelectedOrHoveredMeshes() {
      if (selectedMeshes.Count > 0) {
        return selectedMeshes;
      } else {
        // If there are no hovered items, this will return the empty enumeration, which is our intention.
        return hoverMeshes;
      }
    }


    /// <summary>
    /// Reset state to prepare for the given controller mode.
    /// </summary>
    /// <param name="mode">The new mode.</param>
    private void ModeChangeEventHandler(ControllerMode oldMode, ControllerMode newMode) {
      if ((oldMode == ControllerMode.extrude && newMode == ControllerMode.reshape) ||
        (oldMode == ControllerMode.reshape && newMode == ControllerMode.extrude)) {
        // The user is switching between reshape/extrude: preserve any selected faces.
        Deselect(NOT_FACES, /*deselectSelectedHighlights*/ true, /*deselectHoveredhighlights*/ true);
      } else {
        // The user is switching between any other modes: remove any existing selections or highlights.
        Deselect(ALL, /*deselectSelectedHighlights*/ true, /*deselectHoveredhighlights*/ true);
      }
      multiSelectEnabled = selectModes.Contains(newMode);
    }

    /// <param name="includeGroups">
    /// If includeGroups is true, selecting one mesh will also select its group mates (meshes in the same group).
    /// </param>
    /// <param name="forceSelection">For use in special cases when we need to force selection.</param>
    /// <param name="successfulSelection">
    /// Tells us if a mesh has been successfully selected or not. An out parameter was chosen instead of changing
    /// the return type of the method to bool because the method also handles mesh hovering, whereas this bool value
    /// only deals with selection.
    /// </param>
    private void TryHighlightingAMesh(int nearestMeshId, bool includeGroups, bool forceSelection, out bool successfulSelection) {
      // Default assumption that we don't successfully select.
      successfulSelection = false;

      // Quick check for tutorial mode restrictions.
      if (PeltzerMain.Instance.restrictionManager.onlySelectableMeshIdForTutorial.HasValue
        && nearestMeshId != PeltzerMain.Instance.restrictionManager.onlySelectableMeshIdForTutorial) {
        // No mesh was selected due to tutorial restrictions.
        successfulSelection = false;
        return;
      }

      // Add the mesh to touched meshes.
      HashSet<int> touchedMeshIds = new HashSet<int> { nearestMeshId };

      if (includeGroups) {
        // Expand the list of touched meshes to include all the meshes in the their groups.
        // Note: GetMeshesAndGroupMates() will mutate touchedMeshIds in-place (for efficiency).
        model.ExpandMeshIdsToGroupMates(touchedMeshIds);
      }

      // In multi-select mode, append any newly-hovered meshes to the selected list. This is also the functionality we want 
      // when forcing click to select functionality.
      if (isMultiSelecting || forceSelection) {
        // Add only the nearest mesh id to the undo stack to avoid bugs with grouped objects.
        // When click to select is enabled, the mesh is hidden on hover, but we still want to add it to the undo stack.
        if ((!model.IsMeshHidden(nearestMeshId) || forceSelection) && !selectedMeshes.Contains(nearestMeshId)) {
          undoMeshMultiSelect.Push(nearestMeshId);
        }
        foreach (int meshId in touchedMeshIds) {
          // Or case is needed because for click to select, the mesh is highlighted and therefore hidden.
          if ((!model.IsMeshHidden(meshId) || forceSelection) && !selectedMeshes.Contains(meshId)) {
            SelectMesh(meshId);
            // We're guaranteed to have selected a mesh if the selected meshes don't contain the current mesh and it's not hidden.
            successfulSelection = true;
            peltzerController.TriggerHapticFeedback(HapticFeedback.HapticFeedbackType.FEEDBACK_1,
              /* durationSeconds */ 0.03f, /* strength */ 0.15f);
          }
        }
        return;
      }

      // If we are not multi-selecting and another mesh is already selected, we are not going to select again.
      if (selectedMeshes.Count > 0) {
        return;
      }

      // Clear any existing single-select hover highlights that aren't meshes.
      Deselect(NOT_MESHES, /*deselectSelectedHighlights*/ false, /*deselectHoveredHighlights*/ true);

      // In single-select mode, create highlights for every newlyHoveredMesh. You can only hover a single element, but
      // if we hover a single mesh in a group of meshes we need to treat them like one element.
      foreach (int meshId in touchedMeshIds) {
        if (!hoverMeshes.Contains(meshId)) {
          int canClaimMesh = model.ClaimMesh(meshId, this);
          if (canClaimMesh != -1) {
            hoverMeshes.Add(meshId);
            highlightUtils.TurnOnMesh(meshId);
            if (Features.vibrateOnHover) {
              peltzerController.TriggerHapticFeedback(HapticFeedback.HapticFeedbackType.FEEDBACK_1,
                /* durationSeconds */ 0.02f, /* strength */ 0.15f);
            }
          }
        }
      }


      // In single-select mode, destroy highlights for, and unhide, any unhovered meshes.
      foreach (int meshId in hoverMeshes) {
        if (!touchedMeshIds.Contains(meshId)) {
          highlightUtils.TurnOffMesh(meshId);
          model.RelinquishMesh(meshId, this);
          tempRemovalHashset.Add(meshId);
        }
      }
      foreach (int meshId in tempRemovalHashset) {
        hoverMeshes.Remove(meshId);
      }
      tempRemovalHashset.Clear();
    }

    /// <summary>
    /// Deselects a mesh. Handles grouped meshes as well.
    /// </summary>
    /// (meshes in the same group).</param>
    private void DeselectMesh(int meshIdToDeselect) {
      // Add the mesh to touched meshes.
      HashSet<int> meshIds = new HashSet<int> { meshIdToDeselect };

      // Expand the list of touched meshes to include all the meshes in their groups.
      // Note: GetMeshesAndGroupMates() will mutate touchedMeshIds in-place (for efficiency).
      model.ExpandMeshIdsToGroupMates(meshIds);

      foreach (int meshId in meshIds) {
        if (selectedMeshes.Contains(meshId)) {
          DeselectOneMesh(meshId);
        }
      }
    }

    /// <summary>
    /// Claim responsibility for rendering a mesh from this class.
    /// This should only be called by Model, as otherwise Model's knowledge of current ownership will be incorrect.
    /// </summary>
    public int ClaimMesh(int meshId, IMeshRenderOwner fosterRenderer) {
      if (selectedMeshes.Contains(meshId)) {
        highlightUtils.TurnOffMesh(meshId);
        selectedMeshes.Remove(meshId);
        PeltzerMain.Instance.SetSaveSelectedButtonActiveIfSelectionNotEmpty();
        return meshId;
      }
      if (hoverMeshes.Contains(meshId)) {
        highlightUtils.TurnOffMesh(meshId);
        hoverMeshes.Remove(meshId);
        return meshId;
      }
      // Didn't have it, can't relinquish ownership.
      return -1;
    }

    public void SelectMesh(int meshId) {
      MMesh mesh = model.GetMesh(meshId);

      model.ClaimMesh(meshId, this);
      highlightUtils.TurnOnMesh(meshId);
      selectedMeshes.Add(meshId);
      PeltzerMain.Instance.SetSaveSelectedButtonActiveIfSelectionNotEmpty();

      if (boundingBoxOfAllSelections == null) {
        boundingBoxOfAllSelections = mesh.bounds;
      } else {
        boundingBoxOfAllSelections.Encapsulate(mesh.bounds);
      }
      if (hoverMeshes.Contains(meshId)) {
        hoverMeshes.Remove(meshId);
      }
    }

    /// <param name="successfulSelection">Tells us if a face has been successfully selected or not. An out parameter was chosen instead of changing
    /// the return type of the method to bool because the method also handles face hovering, whereas this bool value only deals with selection.</param>
    private void TryHighlightingAFace(FaceKey faceKey, Vector3 position, out bool successfulSelection) {
      // Default assumption that we don't successfully select.
      successfulSelection = false;

      // In multi-select mode, mark all hovered faces as selected and generate their highlights.
      if (isMultiSelecting) {
        if (!selectedFaces.Contains(faceKey)) {
          SelectFace(faceKey, position);
          // We're guaranteed to have selected a face if the selected faces don't contain the current face.
          successfulSelection = true;
        }
      }

      // If anything is currently selected, regardless of multi-select state, then hovering and selection is disabled, so return.
      if (selectedFaces.Count > 0) {
        return;
      }

      // Clear any existing single-select hover highlights that aren't faces.
      Deselect(NOT_FACES, /*deselectSelectedHighlights*/ false, /*deselectHoveredHighlights*/ true);

      // In single-select mode, create highlights for the newly hovered face if it is not already hovered.
      if (hoverFace == null || hoverFace != faceKey) {
        // If there is an existing face highlight destroy it.
        if (hoverFace != null) {
          highlightUtils.TurnOff(hoverFace);
        }

        hoverFace = faceKey;
        highlightUtils.TurnOn(faceKey, position);
      }
    }

    private void SelectFace(FaceKey faceKey, Vector3 position) {
      selectedFaces.Add(faceKey);
      undoFaceMultiSelect.Push(faceKey);

      Bounds faceBounds = model.GetMesh(faceKey.meshId).CalculateFaceBoundsInModelSpace(faceKey.faceId);
      if (boundingBoxOfAllSelections == null) {
        boundingBoxOfAllSelections = faceBounds;
      } else {
        boundingBoxOfAllSelections.Encapsulate(faceBounds);
      }

      highlightUtils.TurnOn(faceKey, position);
      if (hoverFace != null && hoverFace == faceKey) {
        hoverFace = null;
      }
    }

    /// <param name="successfulSelection">Tells us if an edge has been successfully selected or not. An out parameter was chosen instead of changing
    /// the return type of the method to bool because the method also handles edge hovering, whereas this bool value only deals with selection.</param>
    private void TryHighlightingAnEdge(EdgeKey edge, out bool successfulSelection) {
      // Default assumption that we don't successfully select.
      successfulSelection = false;

      // In multi-select mode, mark all hovered edges as selected and generate their highlights.
      if (isMultiSelecting) {
        if (!selectedEdges.Contains(edge)) {
          SelectEdge(edge);
          // We're guaranteed to have selected an edge if the selected edges don't contain the current edge.
          successfulSelection = true;
        }
      }

      // If anything is currently selected, regardless of multi-select state, then hovering and selection is disabled, so return.
      if (selectedEdges.Count > 0) {
        return;
      }

      // Clear any existing single-select hover highlights that aren't edges.
      Deselect(NOT_EDGES, /*deselectSelectedHighlights*/ false, /*deselectHoveredHighlights*/ true);

      // In single-select mode, turn on highlights for the newly hovered edge if it is not already hovered.
      if (hoverEdge == null || hoverEdge != edge) {
        // If there is an existing edge highlight destroy it.
        if (hoverEdge != null) {
          highlightUtils.TurnOff(hoverEdge);
        }

        highlightUtils.TurnOn(edge);
        highlightUtils.SetEdgeStyleToSelect(edge);
        hoverEdge = edge;
      }
    }

    private void SelectEdge(EdgeKey edgeKey) {
      highlightUtils.TurnOn(edgeKey);
      highlightUtils.SetEdgeStyleToSelect(edgeKey);
      selectedEdges.Add(edgeKey);
      undoEdgeMultiSelect.Push(edgeKey);

      if (hoverEdge != null && hoverEdge == edgeKey) {
        hoverEdge = null;
      }

      // Encapsulate the vertices making up the edge in the bounding box of all selections.
      MMesh mesh = model.GetMesh(edgeKey.meshId);
      if (boundingBoxOfAllSelections == null) {
        boundingBoxOfAllSelections = new Bounds(
          mesh.VertexPositionInModelCoords(edgeKey.vertexId1), // center
          Vector3.zero); // size
      } else {
        boundingBoxOfAllSelections.Encapsulate(mesh.VertexPositionInModelCoords(edgeKey.vertexId1));
      }
      boundingBoxOfAllSelections.Encapsulate(mesh.VertexPositionInModelCoords(edgeKey.vertexId2));
    }

    /// <param name="successfulSelection">Tells us if a vertex has been successfully selected or not. An out parameter was chosen instead of changing
    /// the return type of the method to bool because the method also handles vertex hovering, whereas this bool value only deals with selection.</param>
    private void TryHighlightingAVertex(VertexKey vertex, out bool successfulSelection) {
      // Default assumption that we don't successfully select.
      successfulSelection = false;

      // In multi-select mode, mark all hovered vertices as selected and generate their highlights.
      if (isMultiSelecting) {
        if (!selectedVertices.Contains(vertex)) {
          SelectVertex(vertex);
          // We're guaranteed to have selected a vertex if the selected vertices don't contain the current vertex.
          successfulSelection = true;
        }
        return;
      }

      // If anything is currently selected, regardless of multi-select state, then hovering and selection is disabled, so return.
      if (selectedVertices.Count > 0) {
        return;
      }

      // Clear any existing single-select hover highlights that aren't vertices.
      Deselect(NOT_VERTICES, /*deselectSelectedHighlights*/ false, /*deselectHoveredHighlights*/ true);

      // In single-select mode, activate highlights for the newly hovered vertex if it is not already hovered.
      if (hoverVertex == null || hoverVertex != vertex) {
        // If there is an existing vertex highlight destroy it.
        if (hoverVertex != null) {
          highlightUtils.TurnOff(hoverVertex);
        }

        highlightUtils.TurnOn(vertex);
        highlightUtils.SetVertexStyleToSelect(vertex);
        hoverVertex = vertex;
      }
    }

    private void SelectVertex(VertexKey vertexKey) {
      selectedVertices.Add(vertexKey);
      highlightUtils.TurnOn(vertexKey);
      highlightUtils.SetVertexStyleToSelect(vertexKey);
      undoVertexMultiSelect.Push(vertexKey);

      MMesh mesh = model.GetMesh(vertexKey.meshId);
      if (boundingBoxOfAllSelections == null) {
        boundingBoxOfAllSelections = new Bounds(
          mesh.VertexPositionInModelCoords(vertexKey.vertexId), // center
          Vector3.zero); // size
      } else {
        boundingBoxOfAllSelections.Encapsulate(mesh.VertexPositionInModelCoords(vertexKey.vertexId));
      }
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'start multi-selection mode' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a select event, false otherwise.</returns>
    private bool IsStartMultiSelecting(ControllerEventArgs args) {
      // First check the controller seems in the right state.
      return args.ControllerType == ControllerType.PELTZER
        && args.Action == ButtonAction.DOWN
        && PeltzerMain.Instance.peltzerController.mode != ControllerMode.insertVolume
        && args.ButtonId == ButtonId.Trigger;
    }

    /// <summary>
    ///   Whether this matches the pattern of a 'end multi-select mode' event.
    /// </summary>
    /// <param name="args">The controller event arguments.</param>
    /// <returns>True if this is a select event, false otherwise.</returns>
    private bool IsStopMultiSelecting(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PELTZER
        && args.Action == ButtonAction.UP
        && args.ButtonId == ButtonId.Trigger;
    }

    /// <summary>
    ///   An event handler that listens for controller input and delegates accordingly.
    /// </summary>
    /// <param name="sender">The sender of the controller event.</param>
    /// <param name="args">The controller event arguments.</param>
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (!multiSelectEnabled) {
        return;
      }

      if (IsStartMultiSelecting(args) && !PeltzerMain.Instance.GetMover().IsMoving()
        && !PeltzerMain.Instance.peltzerController.isPointingAtMenu) {
        StartMultiSelection();
      } else if (isMultiSelecting && IsStopMultiSelecting(args)) {
        EndMultiSelection();
      }
    }

    /// <summary>
    ///   If the given position lies inside the bounding box of all selected items, returns true.
    ///   Else, de-selects everything that was selected and returns false.
    ///
    ///   We give some leeway by growing the bounding box by 10% in world-space. The bounding box of all selections
    ///   and the click position are both in model-space, but the growth is scaled to world-space because the user's
    ///   input accuracy is limited by world-space, not model-space.
    ///
    ///   If a user has pulled the trigger to begin an operation on selected items, it is expected (but not
    ///   enforced in code) that the responsible tool first call this method.
    ///
    ///   Will naturally return 'false' in the case that nothing is selected (because the bounding box will be empty),
    ///   which seems reasonable given that 'false' implies the tool should not continue with its operation anyway.
    /// </summary>
    public bool ClickIsWithinCurrentSelection(Vector3 clickPosition) {
      if (!Features.clickAwayToDeselect) {
        return true; // Short-circuit if the feature is disabled.
      }

      float scaleFactor = 1 + (0.1f * worldSpace.scale);
      Bounds grownBounds =
        new Bounds(boundingBoxOfAllSelections.center, boundingBoxOfAllSelections.size * scaleFactor);

      if (grownBounds.Contains(clickPosition)) {
        return true;
      }

      Deselect(ALL, /* deselectSelectedHighlights */ true, /* deselectHoveredHighlights */ true);
      return false;
    }

    public void DeselectAll() {
      Deselect(Selector.ALL, true, true);
      TurnOffSelectIndicator();
      ResetInactive();
    }

    /// <summary>
    /// Removes one mesh from list of selected meshes.
    /// </summary>
    private void DeselectOneMesh(int meshId) {
      model.RelinquishMesh(meshId, this);
      highlightUtils.TurnOffMesh(meshId);
      selectedMeshes.Remove(meshId);
      PeltzerMain.Instance.SetSaveSelectedButtonActiveIfSelectionNotEmpty();
    }

    /// <summary>
    /// Removes one face from list of selected faces.
    /// </summary>
    private void DeselectOneFace(FaceKey faceKey) {
      highlightUtils.TurnOff(faceKey);
      selectedFaces.Remove(faceKey);
    }

    /// <summary>
    /// Removes one edge from list of selected edges.
    /// </summary>
    private void DeselectOneEdge(EdgeKey edgeKey) {
      highlightUtils.TurnOff(edgeKey);
      selectedEdges.Remove(edgeKey);
    }

    /// <summary>
    /// Removes one vertex from list of selected vertices.
    /// </summary>
    private void DeselectOneVertex(VertexKey vertexKey) {
      highlightUtils.TurnOff(vertexKey);
      selectedVertices.Remove(vertexKey);
    }

    /// <summary>
    ///   Removes all items from all lists of selected items and deletes their highlighting GameObjects.
    /// </summary>
    private void Deselect(SelectorOptions options, bool deselectSelectedHighlights, bool deselectHoveredHighlights) {
      if (!PeltzerMain.Instance.restrictionManager.deselectAllowed) {
        return;
      }

      if (options.includeMeshes) {
        MeshCycler.ResetCycler();
        if (deselectSelectedHighlights) {
          foreach (int meshId in selectedMeshes) {
            model.RelinquishMesh(meshId, this);
            highlightUtils.TurnOffMesh(meshId);
          }
          selectedMeshes.Clear();
          PeltzerMain.Instance.SetSaveSelectedButtonActiveIfSelectionNotEmpty();
        }

        if (deselectHoveredHighlights) {
          foreach (int meshId in hoverMeshes) {
            model.RelinquishMesh(meshId, this);
            highlightUtils.TurnOffMesh(meshId);
          }
        }
        hoverMeshes.Clear();
      }

      if (options.includeFaces) {
        if (deselectHoveredHighlights && deselectSelectedHighlights) {
          highlightUtils.ClearFaces();
          selectedFaces.Clear();
          hoverFace = null;
        } else {
          if (deselectSelectedHighlights) {
            foreach (FaceKey key in selectedFaces) {
              highlightUtils.TurnOff(key);
            }
            selectedFaces.Clear();
          }
          if (deselectHoveredHighlights) {
            if (hoverFace != null) {
              highlightUtils.TurnOff(hoverFace);
              hoverFace = null;
            }
          }
        }
      }

      if (options.includeEdges) {
        if (deselectHoveredHighlights && deselectSelectedHighlights) {
          highlightUtils.ClearEdges();
          selectedEdges.Clear();
          hoverEdge = null;
        } else {
          if (deselectSelectedHighlights) {
            foreach (EdgeKey key in selectedEdges) {
              highlightUtils.TurnOff(key);
            }

            selectedEdges.Clear();
          }

          if (deselectHoveredHighlights) {
            if (hoverEdge != null) {
              highlightUtils.TurnOff(hoverEdge);
              hoverEdge = null;
            }
          }
        }
      }

      if (options.includeVertices) {
        if (deselectHoveredHighlights && deselectSelectedHighlights) {
          highlightUtils.ClearVertices();
          selectedVertices.Clear();
          hoverVertex = null;
        } else {
          if (deselectSelectedHighlights) {
            foreach (VertexKey vertexKey in selectedVertices) {
              highlightUtils.TurnOff(vertexKey);
            }

            selectedVertices.Clear();
          }

          if (deselectHoveredHighlights) {
            if (hoverVertex != null) {
              highlightUtils.TurnOff(hoverVertex);
              hoverVertex = null;
            }
          }
        }
      }
    }

    public bool AnythingSelected() {
      return selectedVertices.Count > 0 || selectedEdges.Count > 0
        || selectedFaces.Count > 0 || selectedMeshes.Count > 0;
    }

    /// <summary>
    /// If undo is possible, checks through undo stacks to see which one is populated, and undoes from there.
    /// </summary>
    public bool UndoMultiSelect() {
      // Can only undo if one of the undo stacks is populated.
      if (selectedVertices.Count != 0) {
        VertexKey lastVert = undoVertexMultiSelect.Pop();
        DeselectOneVertex(lastVert);
        redoVertexMultiSelect.Push(lastVert);
        return true;
      } else if (selectedEdges.Count != 0) {
        EdgeKey lastEdge = undoEdgeMultiSelect.Pop();
        DeselectOneEdge(lastEdge);
        redoEdgeMultiSelect.Push(lastEdge);
        return true;
      } else if (selectedFaces.Count != 0) {
        FaceKey lastFace = undoFaceMultiSelect.Pop();
        DeselectOneFace(lastFace);
        redoFaceMultiSelect.Push(lastFace);
        return true;
      } else if (selectedMeshes.Count != 0) {
        int lastMesh = undoMeshMultiSelect.Pop();
        DeselectMesh(lastMesh);
        redoMeshMultiSelect.Push(lastMesh);
        return true;
      }
      return false;
    }

    /// <summary>
    /// If redo is possible, checks through redo stacks to see which one is populated, and redoes from there.
    /// </summary>
    public bool RedoMultiSelect() {
      // Can only redo if one of the redo stacks is populated.
      if (redoVertexMultiSelect.Count != 0) {
        VertexKey lastVert = redoVertexMultiSelect.Pop();
        SelectVertex(lastVert);
        return true;
      } else if (redoEdgeMultiSelect.Count != 0) {
        EdgeKey lastEdge = redoEdgeMultiSelect.Pop();
        SelectEdge(lastEdge);
        return true;
      } else if (redoFaceMultiSelect.Count != 0) {
        FaceKey lastFaceKey = redoFaceMultiSelect.Pop();
        MMesh mesh = model.GetMesh(lastFaceKey.meshId);
        Face face = mesh.GetFace(lastFaceKey.faceId);
        // Calculate face center so that we can animate from center.
        Vector3 centerOfFace = MeshMath.CalculateGeometricCenter(face, mesh);
        SelectFace(lastFaceKey, centerOfFace);
        return true;
      } else if (redoMeshMultiSelect.Count != 0) {
        int lastMesh = redoMeshMultiSelect.Pop();
        bool successfulSelectionMesh;
        TryHighlightingAMesh(lastMesh, /* includeGroups = */true, /* forceSelection = */ Features.clickToSelectEnabled, out successfulSelectionMesh);
        return true;
      }
      return false;
    }

    /// <summary>
    /// Clears all local redo stacks for vertices, edges, faces, and meshes.
    /// </summary>
    public void ClearMultiSelectRedoState() {
      redoVertexMultiSelect.Clear();
      redoEdgeMultiSelect.Clear();
      redoFaceMultiSelect.Clear();
      redoMeshMultiSelect.Clear();
    }

    /// <summary>
    /// Clears all local undo stacks except the undo stack of the type passed in. Defaults to clearing
    /// all the undo stacks.
    /// </summary>
    /// <param name="type">The type whose undo stack we don't want to clear.</param>
    public void ClearMultiSelectUndoState(Type type) {
      switch (type) {
        case Type.VERTEX:
          undoEdgeMultiSelect.Clear();
          undoFaceMultiSelect.Clear();
          undoMeshMultiSelect.Clear();
          break;
        case Type.EDGE:
          undoVertexMultiSelect.Clear();
          undoFaceMultiSelect.Clear();
          undoMeshMultiSelect.Clear();
          break;
        case Type.FACE:
          undoVertexMultiSelect.Clear();
          undoEdgeMultiSelect.Clear();
          undoMeshMultiSelect.Clear();
          break;
        case Type.MESH:
          undoVertexMultiSelect.Clear();
          undoEdgeMultiSelect.Clear();
          undoFaceMultiSelect.Clear();
          break;
        default:
          undoVertexMultiSelect.Clear();
          undoEdgeMultiSelect.Clear();
          undoFaceMultiSelect.Clear();
          undoMeshMultiSelect.Clear();
          break;
      }
    }

    // Class describing each dot - mainly used for the age.
    class MSdot {
      public Transform _transform;
      public float _age;
      public Renderer _rend;
      public Vector3 rot;

      public MSdot() {
        _age = 1f;
        rot = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
      }
    }

    private void StartMultiSelection() {
      isMultiSelecting = true;
      multiselectTrail.SetActive(true);
    }

    private void UpdateMultiselectionTrail() {
      multiselectTrail.transform.position = -1f * worldSpace.WorldToModel(Vector3.zero);
      Vector3 heading = PeltzerMain.Instance.worldSpace.ModelToWorld(peltzerController.LastPositionModel) - lastDotPosition;

      if (heading.magnitude > SPACING) {
        lastDotPosition = PeltzerMain.Instance.worldSpace.ModelToWorld(peltzerController.LastPositionModel);
        MSdot _dot = new MSdot();

        GameObject g = new GameObject("dot");
        MeshFilter mf = g.AddComponent<MeshFilter>();
        mf.mesh = DOTSmesh;
        _dot._rend = g.AddComponent<MeshRenderer>();
        _dot._transform = g.transform;

        _dot._transform.localScale = Vector3.zero;
        _dot._transform.position = lastDotPosition;
        _dot._transform.localRotation = Quaternion.Euler(_dot.rot);
        _dot._rend.material = multiselectDotMaterial;
        _dot._transform.parent = multiselectTrail.transform;
        DOTS.Add(_dot);
      }

      float partA = .925f;
      float partB = .075f;
      for (int i = DOTS.Count - 1; i >= 0; i--) {
        DOTS[i]._age = Mathf.Clamp01(DOTS[i]._age - Time.deltaTime*1.5f);
        if (DOTS[i]._age > partA) {
          DOTS[i]._transform.localScale = MULTISELECT_DOT_SCALE * (1 - (DOTS[i]._age - partA) / partB);
        } else {
          DOTS[i]._transform.localScale = MULTISELECT_DOT_SCALE * DOTS[i]._age / partA;
        }
       // DOTS[i]._transform.Rotate(DOTS[i].rot);
        DOTS[i]._rend.material.color = Color.HSVToRGB(DOTS[i]._age, 1f - DOTS[i]._age, 1) * new Color(1, 1, 1, .75f * DOTS[i]._age);
        if (DOTS[i]._age < .01f) {
          Destroy(DOTS[i]._transform.gameObject);
          DOTS.RemoveAt(i);
        }
      }
    }

    public void EndMultiSelection() {
      isMultiSelecting = false;

      for (int i = DOTS.Count - 1; i >= 0; i--) {
        Destroy(DOTS[i]._transform.gameObject);
      }
      DOTS.Clear();

      if (selectedMeshes.Count > 1 ) {
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelect");
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelectMeshes");
      } else if (selectedFaces.Count > 1) {
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelect");
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelectFaces");
      } else if (selectedVertices.Count > 1) {
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelect");
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelectVertices");
      } else if (selectedEdges.Count > 1) {
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelect");
        PeltzerMain.Instance.Analytics.SuccessfulOperation("multiSelectEdges");
      }

      multiselectTrail.SetActive(false);
    }

    public void ClearState() {
      Deselect(Selector.ALL, /*deselectSelectedHighlights*/ true, /*deselectHoveredHighlights*/ true);
    }

    // Test Method
    public void SetSelectModeActiveForTest(bool selectModeActive) {
      this.isMultiSelecting = selectModeActive;
    }

    // Test Method
    public void AddSelectedFaceForTest(FaceKey faceKey) {
      selectedFaces.Add(faceKey);
    }

    // Test Method
    public void AddSelectedMeshForTest(int meshId) {
      selectedMeshes.Add(meshId);
    }
  }
}
