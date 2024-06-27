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
using System.Linq;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.model.render;
using System;

namespace com.google.apps.peltzer.client.tools
{
    /// <summary>
    /// Tool for subdividing faces. User sees a line across a face, and is able to subdivide the face
    /// into two faces based on the line.
    /// </summary>
    public class Subdivider : MonoBehaviour
    {

        /// <summary>
        /// The current mode of operation for the tool.
        /// </summary>
        public enum Mode
        {
            // Standard subdivide tool with a single subdivision on the selected face.
            SINGLE_FACE_SUBDIVIDE,
            // Experimental tool that uses a plane to create subdivisions on all intersecting faces.
            // Gated by Features.planeSubdivideEnabled
            PLANE_SUBDIVIDE,
            // Experimental tool that loops around the geometry creating subdivisions.
            // Gated by Features.loopSubdivideEnabled and Features.allowNoncoplanarFaces.
            LOOP_SUBDIVIDE
        };

        /// <summary>
        /// If the subdivision vertex is within this squared distance from an existing vertex on the face or a
        /// previously created vertex in this subdivide operation, the existing vertex will be used instead of
        /// creating a new one.
        /// </summary>
        private const float VERTEX_REUSE_SQUARED_DISTANCE_THRESHOLD = 0.00000025f;

        // Over how many seconds the highlight shown after a subdivision animates in.
        private const float EDGE_HIGHLIGHT_ANIMATION_IN_TIME = 0.5f;

        // After how long the highlight shown after a subdivision expires.
        private const float EDGE_HIGHLIGHT_DURATION = 0.5f;

        // Amount of time after a press and hold operation is complete.
        private const float PRESS_AND_HOLD_DELAY = 0.5f;

        /// <summary>
        /// Struct that maintains the two endpoints of a subdivide as well as the vertex index
        /// that would occur before the endpoint if going around the face vertices in order.
        /// It also holds information about the edges in which those endpoints occur.
        /// </summary>
        private class SubdividePoints
        {
            public Vector3 point1 { get; set; }
            public int point1Index { get; set; }
            public EdgeKey edge1 { get; set; }
            public Vector3 point2 { get; set; }
            public int point2Index { get; set; }
            public EdgeKey edge2 { get; set; }
        }

        /// <summary>
        /// A class that will hold state for a single subdivision.
        /// </summary>
        private class Subdivision
        {
            public Vector3 startPoint { get; set; }
            public Vector3 sliceDirection { get; set; }
            public Face face { get; set; }
            public MMesh mesh { get; set; }
        }

        public ControllerMain controllerMain;
        /// <summary>
        ///   A reference to a controller capable of issuing subdivide commands.
        /// </summary>
        private PeltzerController peltzerController;
        /// <summary>
        ///   A reference to the overall model being built.
        /// </summary>
        private Model model;
        /// <summary>
        /// Face, edge, and vertex selector for detecting which face is hovered.
        /// </summary>
        private Selector selector;
        /// <summary>
        /// Library for playing sounds.
        /// </summary>
        private AudioLibrary audioLibrary;

        /// <summary>
        /// Holds details about the active subdivisions to be performed.
        /// </summary>
        private List<Subdivision> activeSubdivisions = new List<Subdivision>();

        /// <summary>
        /// The current mode the tool is in, such as loop subdivide or single face subdivide.
        /// </summary>
        private Mode currentMode = Mode.SINGLE_FACE_SUBDIVIDE;

        /// <summary>
        /// If true, the trigger can be held to transition to a different tool. Currently only
        /// used when loop subdivide is enabled.
        /// </summary>
        private bool pressAndHoldEnabled
        {
            get { return Features.loopSubdivideEnabled; }
        }

        /// <summary>
        /// Keeps track of the user holding the trigger button. Needed to transition the tool
        /// into other modes such as loop subdivide.
        /// </summary>
        private bool isTriggerBeingHeld;

        /// <summary>
        /// The time the user started holding the trigger button, only set if isTriggerBeingHeld
        /// is true.
        /// </summary>
        private float triggerHoldStartTime;

        /// <summary>
        /// Whether we are snapping.
        /// </summary>
        private bool isSnapping;

        private WorldSpace worldSpace;

        /// <summary>
        /// The list of meshes that are currently selected.
        /// </summary>
        private List<MMesh> selectedMeshes = new List<MMesh>();

        /// <summary>
        /// A GameObject which is used to indicate something to the user, like a gizmo. Currently used by the planeSubdivider
        /// to show a plane representing the cut.
        /// </summary>
        GameObject guidanceMesh;

        /// <summary>
        /// Keeps track of the faces that each edge in the mesh is connecting. This is needed to find 
        /// the next face when performing loop subdivide operations.
        /// </summary>
        private Dictionary<EdgeKey, List<int>> edgeKeysToFaceIds = new Dictionary<EdgeKey, List<int>>();

        /// <summary>
        ///   The edge hints currently being shown.
        /// </summary>
        private List<EdgeTemporaryStyle.TemporaryEdge> currentTemporaryEdges;

        /// <summary>
        ///   After a subdivision, we briefly flash an edge highlight to show success. This queue manages turning off the
        ///   highlights once they've flashed.
        /// </summary>
        Queue<KeyValuePair<float, EdgeKey>> highlightsToTurnOff = new Queue<KeyValuePair<float, EdgeKey>>();

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
          PaletteController paletteController, Selector selector, AudioLibrary audioLibrary, WorldSpace worldSpace)
        {
            this.model = model;
            this.controllerMain = controllerMain;
            this.peltzerController = peltzerController;
            this.selector = selector;
            this.audioLibrary = audioLibrary;
            this.worldSpace = worldSpace;

            controllerMain.ControllerActionHandler += ControllerEventHandler;
            peltzerController.ModeChangedHandler += ControllerModeChangedHandler;

            currentTemporaryEdges = new List<EdgeTemporaryStyle.TemporaryEdge>();
            currentMode = Mode.SINGLE_FACE_SUBDIVIDE;
        }

        void Update()
        {
            // We need to clean up any 'thrown away' objects when their time comes (even if the user has changed mode).
            while (highlightsToTurnOff.Count > 0 && highlightsToTurnOff.Peek().Key < Time.time)
            {
                KeyValuePair<float, EdgeKey> highlightToTurnOff = highlightsToTurnOff.Dequeue();
                PeltzerMain.Instance.highlightUtils.TurnOff(highlightToTurnOff.Value);
            }

            if (!PeltzerController.AcquireIfNecessary(ref peltzerController) || peltzerController.mode != ControllerMode.subdivideFace)
            {
                return;
            }

            bool isNewMode = false;
            if (pressAndHoldEnabled
              && isTriggerBeingHeld
              && Time.time > triggerHoldStartTime + PRESS_AND_HOLD_DELAY)
            {
                currentMode = Mode.LOOP_SUBDIVIDE;
                audioLibrary.PlayClip(audioLibrary.genericSelectSound);
                peltzerController.TriggerHapticFeedback();
                isTriggerBeingHeld = false;
                isNewMode = true;
            }

            // Update the position of the selector.
            if (currentMode == Mode.PLANE_SUBDIVIDE)
            {
                selector.SelectMeshAtPosition(peltzerController.LastPositionModel, Selector.MESHES_ONLY);
            }
            else
            {
                selector.SelectAtPosition(peltzerController.LastPositionModel, Selector.FACES_ONLY);
            }
            selector.UpdateInactive(Selector.EDGES_ONLY);

            // Clean up all temp edges.
            foreach (EdgeTemporaryStyle.TemporaryEdge tempEdge in currentTemporaryEdges)
            {
                PeltzerMain.Instance.highlightUtils.TurnOff(tempEdge);
            }
            currentTemporaryEdges.Clear();

            UpdateHighlights(isNewMode);
        }

        private void ControllerModeChangedHandler(ControllerMode oldMode, ControllerMode newMode)
        {
            if (oldMode == ControllerMode.subdivideFace)
            {
                ClearState();
                UnsetAllHoverTooltips();
            }
        }

        public bool IsSubdividing()
        {
            return activeSubdivisions.Count > 0;
        }

        private void UpdateHighlights(bool isNewMode)
        {
            activeSubdivisions.Clear();

            switch (currentMode)
            {
                case Mode.LOOP_SUBDIVIDE:
                    UpdateHighlightsForLoopSubdivide(isNewMode);
                    break;
                case Mode.PLANE_SUBDIVIDE:
                    UpdateHighlightsForPlaneSubdivide();
                    break;
                case Mode.SINGLE_FACE_SUBDIVIDE:
                    UpdateHighlightsForSingleFaceSubdivide();
                    break;
                default:
                    break;
            }
        }

        private void CleanUpAfterMeshSelectionEnds()
        {
            selectedMeshes.Clear();
            Destroy(guidanceMesh);
            guidanceMesh = null;
            PeltzerMain.Instance.highlightUtils.ClearTemporaryEdges();
        }

        /// <summary>
        /// Update highlights for a loop subdivide operation.
        /// </summary>
        private void UpdateHighlightsForLoopSubdivide(bool isNewMode)
        {
            if (selector.hoverFace == null)
            {
                CleanUpAfterMeshSelectionEnds();
                return;
            }

            List<MMesh> hoverMeshes = new List<MMesh>();
            hoverMeshes.Add(model.GetMesh(selector.hoverFace.meshId));

            // Check if the list of selected meshes has changed, ignoring ordering.
            bool selectionChanged = (hoverMeshes.Count != selectedMeshes.Count) || hoverMeshes.Except(selectedMeshes).Any();
            if (selectionChanged || isNewMode)
            {
                selectedMeshes = hoverMeshes;
                // Update our cache of edge keys to faces.
                edgeKeysToFaceIds.Clear();
                foreach (MMesh mesh in selectedMeshes)
                {
                    foreach (var keyValue in MeshUtil.ComputeEdgeKeysToFaceIdsMap(mesh))
                    {
                        edgeKeysToFaceIds[keyValue.Key] = keyValue.Value;
                    }
                }
            }

            Subdivision initialFaceSubdivision = GetInitialFaceSubdivision();

            // A percentage representing how far along each edge the points of the subdivision will occur.
            float loopSubdivideEdgeCutPercentage;

            // The edge from which this loop subdivision operation will begin.
            EdgeKey initialEdge;
            ComputeLoopSubdivideParameters(
              initialFaceSubdivision,
              out loopSubdivideEdgeCutPercentage,
              out initialEdge);

            // If we are snapping or in block mode divide each face right through the middle.
            if (isSnapping || peltzerController.isBlockMode)
            {
                loopSubdivideEdgeCutPercentage = 0.5f;
            }

            // Stop the operation if the initial face is not a quad.
            // TODO(bug): Look into making this work with triangles in some cases, like a the top of a primitive
            // sphere.
            if (initialFaceSubdivision.face.vertexIds.Count != 4)
            {
                return;
            }

            // We keep track of the most recent edge the subdivision has passed through.
            EdgeKey currentSubdivisionExitEdge;
            AdjustSubdivisionForEdgeCutPercentage(
                initialFaceSubdivision,
                loopSubdivideEdgeCutPercentage,
                initialEdge,
                out currentSubdivisionExitEdge);

            // Once we come back to an face that has already been part of a subdivision, the operation has completed.
            HashSet<int> visitedFaceIds = new HashSet<int>() { initialFaceSubdivision.face.id };
            Subdivision currentSubdivision = initialFaceSubdivision;

            SubdividePoints subdividePoints = null;

            bool nextSubdivisionFound = currentSubdivision != null && currentSubdivisionExitEdge != null;
            while (nextSubdivisionFound)
            {
                // We don't snap subdivisions in loop subdivide operations. Snapping is instead handled by locking the
                // edge cut percentage to 50% instead of computing it.
                subdividePoints = HandleSingleSubdivision(currentSubdivision, shouldSnapSubdivision: false);

                // Attempt to find the next subdivision.
                Subdivision nextSubdivision;
                EdgeKey nextSubdivisionExitEdge;
                nextSubdivisionFound = FindNextSubdivisionForLoopSubdivide(currentSubdivision,
                  currentSubdivisionExitEdge,
                  loopSubdivideEdgeCutPercentage,
                  subdividePoints,
                  edgeKeysToFaceIds,
                  ref visitedFaceIds,
                  out nextSubdivision,
                  out nextSubdivisionExitEdge);

                currentSubdivision = nextSubdivision;
                currentSubdivisionExitEdge = nextSubdivisionExitEdge;
            }
        }

        /// <summary>
        /// Update highlights for a plane slice subdivision.
        /// </summary>
        private void UpdateHighlightsForPlaneSubdivide()
        {
            if (selector.hoverFace == null && selector.hoverMeshes.Count == 0)
            {
                CleanUpAfterMeshSelectionEnds();
                return;
            }

            selectedMeshes.Clear();
            foreach (int hoverMeshId in selector.hoverMeshes)
            {
                selectedMeshes.Add(model.GetMesh(hoverMeshId));
            }

            Vector3 planeNormal = (peltzerController.LastRotationModel * Vector3.up).normalized;
            Vector3 planeOffset = peltzerController.LastPositionModel;

            // TODO(bug): Use Graphics.DrawMesh instead.
            Plane plane = new Plane(planeNormal, planeOffset);
            if (guidanceMesh == null)
            {
                guidanceMesh = CreatePlaneGuidanceMesh();
            }

            guidanceMesh.transform.position = worldSpace.ModelToWorld(planeOffset);
            guidanceMesh.transform.rotation = worldSpace.ModelOrientationToWorld(peltzerController.LastRotationModel);

            // Go through each face in each mesh and find intersection points with the plane.
            foreach (MMesh mesh in selectedMeshes)
            {
                foreach (Face face in mesh.GetFaces())
                {

                    List<Vector3> intersectionPoints = new List<Vector3>();
                    List<Vector3> vertexPositions = new List<Vector3>();

                    for (int i = 0; i < face.vertexIds.Count; i++)
                    {
                        vertexPositions.Add(mesh.VertexPositionInModelCoords(mesh.GetVertex(face.vertexIds[i]).id));
                    }

                    FindFacePlaneIntersection(plane, vertexPositions, out intersectionPoints);
                    if (intersectionPoints.Count >= 2)
                    {
                        Subdivision subdivision = new Subdivision();
                        subdivision.face = face;
                        subdivision.mesh = mesh;
                        subdivision.startPoint = intersectionPoints[0];
                        subdivision.sliceDirection = intersectionPoints[1] - intersectionPoints[0];
                        HandleSingleSubdivision(subdivision, false);
                    }
                }
            }
        }

        /// <summary>
        /// Update highlights for a single face subdivision (i.e. plane subdivider and loop subdivider are disabled).
        /// </summary>
        private void UpdateHighlightsForSingleFaceSubdivide()
        {
            if (selector.hoverFace == null)
            {
                CleanUpAfterMeshSelectionEnds();
                return;
            }

            selectedMeshes.Clear();
            selectedMeshes.Add(model.GetMesh(selector.hoverFace.meshId));

            Subdivision singleSubdivision = GetInitialFaceSubdivision();
            if (singleSubdivision != null)
            {
                HandleSingleSubdivision(singleSubdivision,
                  isSnapping || peltzerController.isBlockMode /* shouldSnapSubdivision */);
            }
        }

        /// <summary>
        /// Creates a GameObject to guide the user when using the plane subdivide feature.
        /// </summary>
        private GameObject CreatePlaneGuidanceMesh()
        {
            GameObject container = new GameObject();
            GameObject plane1 = GameObject.CreatePrimitive(PrimitiveType.Plane);
            GameObject plane2 = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane2.transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
            plane1.transform.parent = container.transform;
            plane2.transform.parent = container.transform;
            container.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
            return container;
        }

        /// <summary>
        /// Creates a temp edge to highlight the current subdivision and handles snapping.
        /// </summary>
        /// <param name="subdivision">The subdivision to consider.</param>
        /// <param name="shouldSnapSubdivision">
        ///   Whether the tool should attempt to snap the subdivision to the interesting points of the face it subdivides
        ///   (vertices and edge bisectors).
        /// </param>
        /// <returns>The SubdivisionPoints representing that new edge that the subdivision will create.</returns>
        private SubdividePoints HandleSingleSubdivision(Subdivision subdivision, bool shouldSnapSubdivision)
        {
            activeSubdivisions.Add(subdivision);

            // Finding the points that the subdivision line interest with the edges of the face.
            SubdividePoints edgeIntersectionPoints;
            GetSubdividePoints(subdivision.face, subdivision.mesh, subdivision.startPoint,
              subdivision.sliceDirection.normalized, out edgeIntersectionPoints);

            if (shouldSnapSubdivision)
            {
                SnapSubdivision(subdivision, edgeIntersectionPoints);
                // We need to recompute this because we just modified the subdivision.
                GetSubdividePoints(subdivision.face, subdivision.mesh, subdivision.startPoint,
                  subdivision.sliceDirection.normalized, out edgeIntersectionPoints);
            }

            // Create the temp edge and add it to the active list.
            EdgeTemporaryStyle.TemporaryEdge tempEdge = new EdgeTemporaryStyle.TemporaryEdge();
            tempEdge.vertex1PositionModelSpace = edgeIntersectionPoints.point1;
            tempEdge.vertex2PositionModelSpace = edgeIntersectionPoints.point2;
            PeltzerMain.Instance.highlightUtils.TurnOn(tempEdge);
            PeltzerMain.Instance.highlightUtils.SetTemporaryEdgeStyleToSelect(tempEdge);
            currentTemporaryEdges.Add(tempEdge);

            return edgeIntersectionPoints;
        }

        private Subdivision GetInitialFaceSubdivision()
        {
            FaceKey initialFaceKey = selector.hoverFace;
            MMesh mesh = model.GetMesh(selector.hoverFace.meshId);
            Face initialFace = mesh.GetFace(initialFaceKey.faceId);

            Subdivision initialFaceSubdivision = new Subdivision();
            initialFaceSubdivision.mesh = mesh;
            initialFaceSubdivision.face = initialFace;

            // Project the position of the controller onto the plane of the current face.
            List<Vector3> vertices = new List<Vector3>(initialFace.vertexIds.Count);
            for (int i = 0; i < initialFace.vertexIds.Count; i++)
            {
                int id = initialFace.vertexIds[i];
                vertices.Add(mesh.VertexPositionInModelCoords(id));
            }
            Vector3 closestPointOnFace = Math3d.ProjectPointOnPlane(
                MeshMath.CalculateNormal(vertices),
                mesh.VertexPositionInModelCoords(initialFace.vertexIds[0]),
                peltzerController.LastPositionModel);
            initialFaceSubdivision.startPoint = closestPointOnFace;

            // Find the direction the subdivide will slice the initial face.
            // We use Vector3.right because the resting position should be a horizontal line over a face in front of the
            // camera.
            initialFaceSubdivision.sliceDirection = peltzerController.LastRotationModel * Vector3.right;

            return initialFaceSubdivision;
        }

        /// <summary>
        /// Given a face with 4 verts and an edge, find the opposite edge.
        /// This can be expanded in the future to support faces with more than 4 verts.
        /// </summary>
        private EdgeKey FindOppositeEdge(Face face, EdgeKey input)
        {
            if (face.vertexIds.Count != 4)
            {
                Debug.LogError("Unable to find opposite edge on a non quad face: " + face.id);
            }
            int id1 = -1, id2 = -1;
            bool id1Set = false;
            foreach (int vertexId in face.vertexIds)
            {
                if (input.ContainsVertex(vertexId))
                {
                    continue;
                }
                if (!id1Set)
                {
                    id1 = vertexId;
                    id1Set = true;
                }
                else
                {
                    id2 = vertexId;
                    break;
                }
            }
            return new EdgeKey(input.meshId, id1, id2);
        }

        /// <summary>
        /// Given a the current subdivision, some connectivity information and a list of visited faces, attempts to find
        /// the next subdivision to be performed in a loop subdivision operation.
        /// </summary>
        /// <param name="currentSubdivision">The last subdivision of the chain so far.</param>
        /// <param name="currentSubdivisionExitEdge">The exit edge of currentSubdivision.</param>
        /// <param name="loopSubdivideEdgeCutPercentage">The percentage to cut the face at.</param>
        /// <param name="currentSubdividePoints">The SubdividePoints associated with currentSubdivision.</param>
        /// <param name="edgeKeysToFaceIds">The dict containing the mapping from edges to face IDs for the mesh.</param>
        /// <param name="visitedFaceIds">
        ///   A collection of faces to exclude because they have been visited already by previous subdivisions in the chain.
        /// </param>
        /// <param name="nextSubdivision">The next subdivision in the chain, or null if this method can't find one.</param>
        /// <param name="nextSubdivisionExitEdge">
        ///   The exit edge for nextSubdivision, or null if this method can't find one.
        /// </param>
        /// <returns> Whether a suitable subdivision was found to continue the chain. </returns>
        private bool FindNextSubdivisionForLoopSubdivide(Subdivision currentSubdivision,
          EdgeKey currentSubdivisionExitEdge,
          float loopSubdivideEdgeCutPercentage,
          SubdividePoints currentSubdividePoints,
          Dictionary<EdgeKey, List<int>> edgeKeysToFaceIds,
          ref HashSet<int> visitedFaceIds,
          out Subdivision nextSubdivision,
          out EdgeKey nextSubdivisionExitEdge)
        {
            List<int> faceIds;

            // Attempt to figure out the next face to be subdivided.
            // We find the 2 faces connected to the exit edge of the current subdivision and
            // find a face that we haven't visited (i.e. the next one). We may have already
            // visited both faces in which case the operation is complete (we looped around).
            // Then we find the start point of the new subdivision, which is the exit point of
            // the current one. Finally, we adjust the new subdivision according to the
            // loopSubdivideEdgeCutPercentage, get its exit edge and return.
            edgeKeysToFaceIds.TryGetValue(currentSubdivisionExitEdge, out faceIds);
            foreach (int faceId in faceIds)
            {
                if (currentSubdivision.face.id != faceId && !visitedFaceIds.Contains(faceId))
                {
                    visitedFaceIds.Add(faceId);

                    Vector3 startPoint = currentSubdivisionExitEdge == currentSubdividePoints.edge1
                      ? currentSubdividePoints.point1
                      : currentSubdividePoints.point2;

                    nextSubdivision = new Subdivision();
                    nextSubdivision.mesh = currentSubdivision.mesh;
                    nextSubdivision.face = currentSubdivision.mesh.GetFace(faceId);
                    nextSubdivision.startPoint = startPoint;

                    // Stop the operation if we hit anything that is not a quad.
                    // TODO(bug): Look into making this work with triangles in some cases, like a the top of a primitive
                    // sphere.
                    if (nextSubdivision.face.vertexIds.Count != 4)
                    {
                        continue;
                    }

                    AdjustSubdivisionForEdgeCutPercentage(
                      nextSubdivision,
                      loopSubdivideEdgeCutPercentage,
                      currentSubdivisionExitEdge /* originEdge */,
                      out nextSubdivisionExitEdge);

                    // All done, we found the next subdivision and exit edge.
                    return true;
                }
            }

            // Could not find the next subdivision.
            nextSubdivision = null;
            nextSubdivisionExitEdge = null;
            return false;
        }

        /// <summary>
        ///   Modifies the provided subdivision to cut between the edge that is closest to the subdivision's
        ///   startPoint and the opposite edge in the face (assumming a quad).
        ///
        ///   This works by taking the originEdge, and starting the subdivision along it according to
        ///   edgeCutPercentage, then ending the subdivision on (1 - edgeCutPercentage) on the exit
        ///   edge and returning it to the caller so that it can be passed as the origin edge for
        ///   the next subdivision in the chain. In other words, the exitEdge of a subdivision
        ///   becomes the originEdge of the next one in the chain.
        /// </summary>
        /// <param name="subdivision"> The subdivision to be modified. </param>
        /// <param name="edgeCutPercentage">
        ///   A value in (0,1) to indicate the point in which the cut should occur along the edge.
        /// </param>
        /// <param name="originEdge">The origin edge of the current subdivision.</param>
        /// <param name="exitEdge">
        ///   The computed exit edge of the current subdivision to be passed to the next one.
        /// </param>
        private void AdjustSubdivisionForEdgeCutPercentage(Subdivision subdivision,
          float edgeCutPercentage,
          EdgeKey originEdge,
          out EdgeKey exitEdge)
        {

            MMesh mesh = subdivision.mesh;
            exitEdge = FindOppositeEdge(subdivision.face, originEdge);

            // The orientation of these points within an edge is not a concern here because
            // GetFaceVertexIndicesForEdge() will get us the indices as they appear in clockwise
            // manner along the face. This means that the vertices indices will be flipped for 
            // a face in which they appear on the exitEdge, and the next subdivision on the 
            // chain, in which the same edge will be the origin edge instead.

            // Points on the origin edge.
            int[] originEdgeIndices = GetFaceVertexIndicesForEdge(subdivision.face, originEdge);
            Vector3 v1 = mesh.VertexPositionInModelCoords(subdivision.face.vertexIds[originEdgeIndices[0]]);
            Vector3 v2 = mesh.VertexPositionInModelCoords(subdivision.face.vertexIds[originEdgeIndices[1]]);

            // Points on the exit edge.
            int[] exitEdgeIndices = GetFaceVertexIndicesForEdge(subdivision.face, exitEdge);
            Vector3 op1 = mesh.VertexPositionInModelCoords(subdivision.face.vertexIds[exitEdgeIndices[0]]);
            Vector3 op2 = mesh.VertexPositionInModelCoords(subdivision.face.vertexIds[exitEdgeIndices[1]]);

            var originEdgeCutPercentage = edgeCutPercentage;
            var exitEdgeCutPercentage = 1 - edgeCutPercentage;

            subdivision.startPoint = v1 + originEdgeCutPercentage * (v2 - v1);
            Vector3 target = op1 + exitEdgeCutPercentage * (op2 - op1);
            subdivision.sliceDirection = target - subdivision.startPoint;
        }

        /// <summary>
        /// Given a subdivision, finds the closest edge to the cursor and the edgeCutPercentage along that edge.
        /// </summary>
        /// <param name="subdivision">The subdivision used to compute the edgeCutPercentage.</param>
        /// <param name="edgeCutPercentage">
        ///   A value in (0,1) along origin edge at which the subdivision should be adjusted.
        /// </param>
        /// <param name="originEdge">
        ///   The origin edge of the subdivision (i.e. The edge closest to the subdivision's startPoint).
        /// </param>
        private void ComputeLoopSubdivideParameters(Subdivision subdivision,
          out float edgeCutPercentage,
          out EdgeKey originEdge)
        {

            SubdividePoints subdividePoints;
            MMesh mesh = subdivision.mesh;
            GetSubdividePoints(subdivision.face,
              subdivision.mesh,
              subdivision.startPoint,
              subdivision.sliceDirection.normalized,
              out subdividePoints);

            float d1 = Vector3.Distance(subdivision.startPoint, subdividePoints.point1);
            float d2 = Vector3.Distance(subdivision.startPoint, subdividePoints.point2);

            Vector3 closestPoint;

            // Choose the subdivide point closest to the startPoint.
            if (d1 < d2)
            {
                closestPoint = subdividePoints.point1;
                originEdge = subdividePoints.edge1;
            }
            else
            {
                closestPoint = subdividePoints.point2;
                originEdge = subdividePoints.edge2;
            }

            // Points on the origin edge.
            int[] originEdgeIndices = GetFaceVertexIndicesForEdge(subdivision.face, originEdge);
            Vector3 v1 = mesh.VertexPositionInModelCoords(subdivision.face.vertexIds[originEdgeIndices[0]]);
            Vector3 v2 = mesh.VertexPositionInModelCoords(subdivision.face.vertexIds[originEdgeIndices[1]]);

            edgeCutPercentage = (closestPoint - v1).magnitude / (v2 - v1).magnitude;
        }

        /// <summary>
        /// Given a face and an edge key, returns an array with 2 elements, containing the indices of
        /// the verts in the edge in clockwise order as they appear on the face.
        /// </summary>
        private int[] GetFaceVertexIndicesForEdge(Face face, EdgeKey edge)
        {
            int lastIndex = face.vertexIds.Count - 1;
            if (edge.ContainsVertex(face.vertexIds[0]) && edge.ContainsVertex(face.vertexIds[lastIndex]))
            {
                return new int[] { lastIndex, 0 };
            }
            for (int i = 0; i < lastIndex; i++)
            {
                if (edge.ContainsVertex(face.vertexIds[i]) && edge.ContainsVertex(face.vertexIds[i + 1]))
                {
                    return new int[] { i, i + 1 };
                }
            }
            Debug.LogError("Edge not in face: (" + edge.vertexId1 + ", " + edge.vertexId2 + "), in face " + face.id);
            return null;
        }

        private List<Vector3> FindInterestingFacePoints(List<Vector3> coplanarVertices)
        {
            // Add all the vertices to facePoints.
            List<Vector3> facePoints = new List<Vector3>(coplanarVertices);

            // Add all the edge segment bisectors to facePoints.
            facePoints.AddRange(MeshMath.CalculateEdgeBisectors(coplanarVertices));

            // Remove all colinear vertices from the face.
            List<Vector3> corners = MeshMath.FindCornerVertices(coplanarVertices);

            // Find the edgeBisectors for a full edge.
            foreach (Vector3 edgeBisector in MeshMath.CalculateEdgeBisectors(corners))
            {
                if (!facePoints.Contains(edgeBisector))
                    facePoints.Add(edgeBisector);
            }

            // Find the center of the face.
            facePoints.Add(MeshMath.CalculateGeometricCenter(corners));

            return facePoints;
        }

        /// <summary>
        ///   Makes only the supplied tooltip visible and ensures the others are off.
        /// </summary>
        /// <param name="tooltip">The tooltip text to activate.</param>
        /// <param name="state">The hover state.</param>
        private void SetHoverTooltip(GameObject tooltip, TouchpadHoverState state)
        {
            if (!tooltip.activeSelf)
            {
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
        private void UnsetAllHoverTooltips()
        {
            peltzerController.controllerGeometry.modifyTooltipUp.SetActive(false);
            peltzerController.controllerGeometry.modifyTooltipLeft.SetActive(false);
            peltzerController.controllerGeometry.modifyTooltipRight.SetActive(false);
            peltzerController.controllerGeometry.resizeUpTooltip.SetActive(false);
            peltzerController.controllerGeometry.resizeDownTooltip.SetActive(false);
            peltzerController.SetTouchpadHoverTexture(TouchpadHoverState.NONE);
        }

        public void ResetOverlays()
        {
            peltzerController.ShowModifyOverlays();
            peltzerController.ShowTooltips();
        }

        private void FinishSubdivide()
        {
            SubdivideFaces(activeSubdivisions);
            ResetOverlays();
            ClearState();
        }

        /// <summary>
        ///   Resets the subdivide tool to its default state.
        /// </summary>
        public void ClearState()
        {
            PeltzerMain.Instance.highlightUtils.ClearTemporaryEdges();
            selectedMeshes.Clear();
            Destroy(guidanceMesh);
            guidanceMesh = null;
            activeSubdivisions.Clear();

            if (Features.planeSubdivideEnabled)
            {
                currentMode = Mode.PLANE_SUBDIVIDE;
            }
            else
            {
                currentMode = Mode.SINGLE_FACE_SUBDIVIDE;
            }
        }

        private bool IsStartPressAndHoldEvent(ControllerEventArgs args)
        {
            return pressAndHoldEnabled
              && args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.DOWN;
        }

        private bool IsFinishSubdivideEvent(ControllerEventArgs args)
        {
            // Normally, we want the operation to complete as soon as the trigger
            // is down, but when press and hold is enabled we want to wait until
            // the trigger is released.
            ButtonAction buttonActionToCheck = pressAndHoldEnabled ?
              ButtonAction.UP : ButtonAction.DOWN;

            return args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger
              && args.Action == buttonActionToCheck
              && IsSubdividing();
        }

        /// <summary>
        ///   Whether this matches a start snapping event.
        /// </summary>
        /// <param name="args">The controller event arguments.</param>
        /// <returns>True if the trigger is down.</returns>
        private static bool IsStartSnapEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.DOWN;
        }

        /// <summary>
        ///   Whether this matches an end snapping event.
        /// </summary>
        /// <param name="args">The controller event arguments.</param>
        /// <returns>True if the trigger is up.</returns>
        private static bool IsEndSnapEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.UP;
        }

        // Touchpad Hover
        private bool IsSetUpHoverTooltipEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && !IsSubdividing()
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.TOP;
        }

        private bool IsSetDownHoverTooltipEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && !IsSubdividing()
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.BOTTOM;
        }

        private bool IsSetLeftHoverTooltipEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && !IsSubdividing()
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.LEFT;
        }

        private bool IsSetRightHoverTooltipEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && !IsSubdividing()
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.TOUCHPAD && args.TouchpadLocation == TouchpadLocation.RIGHT;
        }

        private static bool IsUnsetAllHoverTooltipsEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.NONE;
        }

        /// <summary>
        ///   Subdivides the mesh according to the provided list of subdivisions.
        /// </summary>
        private void SubdivideFaces(List<Subdivision> subdivisions)
        {
            if (subdivisions.Count == 0)
            {
                return;
            }

            // Keys are mesh IDs, values are the actual meshes.
            Dictionary<int, MMesh> modifiedMeshes = new Dictionary<int, MMesh>();

            // Keys are mesh IDs, values are collections of IDs of created verts.
            // This is needed to reuse verts created over multiple GeometryOperations without
            // having to iterate over every vertex in the mesh.
            Dictionary<int, HashSet<int>> reusableVertsDict = new Dictionary<int, HashSet<int>>();

            foreach (Subdivision subdivision in subdivisions)
            {

                // If we haven't cloned the mesh associated with this subdivision, clone it,
                // otherwise reuse the previously cloned mesh.
                MMesh modifiedMesh;
                if (!modifiedMeshes.TryGetValue(subdivision.mesh.id, out modifiedMesh))
                {
                    modifiedMesh = subdivision.mesh.Clone();
                    modifiedMeshes[subdivision.mesh.id] = modifiedMesh;
                }

                // Get the set of reusable vertices for this mesh, or create it.
                HashSet<int> reusableVertsForCurrentMesh;
                if (!reusableVertsDict.TryGetValue(subdivision.mesh.id, out reusableVertsForCurrentMesh))
                {
                    reusableVertsForCurrentMesh = new HashSet<int>();
                    reusableVertsDict[subdivision.mesh.id] = reusableVertsForCurrentMesh;
                }

                MMesh.GeometryOperation subdivideOperation = modifiedMesh.StartOperation();

                Vector3 start = subdivision.startPoint;
                Vector3 sliceDirection = subdivision.sliceDirection;

                // Check this is a valid subdivision.
                SubdividePoints points;
                if (!GetSubdividePoints(subdivision.face, subdivision.mesh, start, sliceDirection.normalized, out points))
                {
                    audioLibrary.PlayClip(audioLibrary.errorSound);
                    peltzerController.TriggerHapticFeedback();
                    return;
                }

                Face face = subdivision.face;
                subdivideOperation.DeleteFace(face.id);
                selector.DeselectAll();

                // To make this a little bit simpler, order our points to coincide with vertex order on face.
                if (points.point1Index > points.point2Index)
                {
                    int tmp = points.point1Index;
                    points.point1Index = points.point2Index;
                    points.point2Index = tmp;

                    Vector3 pointTmp = points.point1;
                    points.point1 = points.point2;
                    points.point2 = pointTmp;
                }

                int startVertIdSeg1 = face.vertexIds[points.point1Index];
                int endVertIdSeg1 = face.vertexIds[(points.point1Index + 1) % face.vertexIds.Count];
                int startVertIdSeg2 = face.vertexIds[points.point2Index];
                int endVertIdSeg2 = face.vertexIds[(points.point2Index + 1) % face.vertexIds.Count];

                // Find the nearest existing vertices within VERTEX_REUSE_DISTANCE_THRESHOLD. If we find one, we
                // use the existing vertex instead of creating a new one. If we don't, we create new vertices.
                Vertex newVertex1 = null;
                Vertex newVertex2 = null;
                float nearestSquaredDistanceToNewVertex1 = VERTEX_REUSE_SQUARED_DISTANCE_THRESHOLD;
                float nearestSquaredDistanceToNewVertex2 = VERTEX_REUSE_SQUARED_DISTANCE_THRESHOLD;

                // The vertices we need to check (to reuse them) is the union of previously created verts
                // (in previous subdivisions) and the vertices in the face of the current subdivision.
                HashSet<int> vertsToCheck = new HashSet<int>();
                vertsToCheck.UnionWith(face.vertexIds);
                vertsToCheck.UnionWith(reusableVertsForCurrentMesh);

                foreach (int vertexId in vertsToCheck)
                {
                    Vertex vertex = modifiedMesh.GetVertex(vertexId);
                    Vector3 vertexPos = modifiedMesh.VertexPositionInModelCoords(vertexId);
                    float squaredDistanceistanceToNewVertex1 = Vector3.SqrMagnitude(vertexPos - points.point1);
                    if (squaredDistanceistanceToNewVertex1 < nearestSquaredDistanceToNewVertex1)
                    {
                        newVertex1 = vertex;
                        nearestSquaredDistanceToNewVertex1 = squaredDistanceistanceToNewVertex1;
                    }
                    float squaredDistanceToNewVertex2 = Vector3.SqrMagnitude(vertexPos - points.point2);
                    if (squaredDistanceToNewVertex2 < nearestSquaredDistanceToNewVertex2)
                    {
                        newVertex2 = vertex;
                        nearestSquaredDistanceToNewVertex2 = squaredDistanceToNewVertex2;
                    }
                }

                bool vertex1AlreadyExisted = true;
                bool vertex2AlreadyExisted = true;
                if (newVertex1 == null)
                {
                    newVertex1 = subdivideOperation.AddVertexModelSpace(points.point1);
                    vertex1AlreadyExisted = false;
                    reusableVertsForCurrentMesh.Add(newVertex1.id);
                }
                if (newVertex2 == null)
                {
                    newVertex2 = subdivideOperation.AddVertexModelSpace(points.point2);
                    vertex2AlreadyExisted = false;
                    reusableVertsForCurrentMesh.Add(newVertex2.id);
                }

                // If the two subdivide points are an edge on the subdivide face, don't subdivide.
                if (vertex1AlreadyExisted && vertex2AlreadyExisted && VerticesAreEdgeOnFace(newVertex1, newVertex2, face))
                {
                    audioLibrary.PlayClip(audioLibrary.errorSound);
                    peltzerController.TriggerHapticFeedback();
                    return;
                }

                // Create our new faces. Basically, wind around the face until we hit our "cut" points. When we hit
                // one, we make a face.
                List<int> newFaceIndices = new List<int>();
                for (int i = 0; i < face.vertexIds.Count; i++)
                {
                    if (face.vertexIds[i] != newVertex1.id && face.vertexIds[i] != newVertex2.id)
                    {
                        newFaceIndices.Add(face.vertexIds[i]);
                    }
                    if (i == points.point1Index)
                    {
                        newFaceIndices.Add(newVertex1.id);
                        newFaceIndices.Add(newVertex2.id);
                        for (int k = points.point2Index + 1; k < face.vertexIds.Count; k++)
                        {
                            if (face.vertexIds[k] != newVertex1.id && face.vertexIds[k] != newVertex2.id)
                            {
                                newFaceIndices.Add(face.vertexIds[k]);
                            }
                        }
                        Face newFace = subdivideOperation.AddFace(new List<int>(newFaceIndices), face.properties);
                        // done with first face, start drawing our second face
                        newFaceIndices.Clear();
                        newFaceIndices.Add(newVertex1.id);
                    }
                    else if (i == points.point2Index)
                    {
                        newFaceIndices.Add(newVertex2.id);
                        Face newFace = subdivideOperation.AddFace(new List<int>(newFaceIndices), face.properties);
                        break;
                    }
                }

                // Split the segments of other faces where needed.
                foreach (Face oldFace in new List<Face>(modifiedMesh.GetFaces()))
                {
                    if (oldFace.id == subdivision.face.id)
                    {
                        continue;
                    }
                    Face fixedFace = oldFace;
                    // Try to replace the first segment.
                    if (!vertex1AlreadyExisted)
                    {
                        List<int> indices = MaybeInsertVert(fixedFace, startVertIdSeg1, endVertIdSeg1, newVertex1.id);
                        if (indices.Count > 0)
                        {
                            subdivideOperation.ModifyFace(fixedFace.id, indices, fixedFace.properties);
                        }
                    }
                    // Try to replace the second segment.
                    if (!vertex2AlreadyExisted)
                    {
                        List<int> indices = MaybeInsertVert(fixedFace, startVertIdSeg2, endVertIdSeg2, newVertex2.id);
                        if (indices.Count > 0)
                        {
                            subdivideOperation.ModifyFace(fixedFace.id, indices, fixedFace.properties);
                        }
                    }
                }

                subdivideOperation.Commit();

                EdgeKey edgeKey = new EdgeKey(modifiedMesh.id, newVertex1.id, newVertex2.id);
                PeltzerMain.Instance.highlightUtils.TurnOn(edgeKey, EDGE_HIGHLIGHT_ANIMATION_IN_TIME);

                highlightsToTurnOff.Enqueue(new KeyValuePair<float, EdgeKey>(Time.time + EDGE_HIGHLIGHT_DURATION, edgeKey));
                PeltzerMain.Instance.subdividesCompleted++;
            }

            // Apply commands to create our new meshes, or error if we find an invalid operation.
            bool errorOccurred = false;
            foreach (var modifiedMesh in modifiedMeshes.Values)
            {
                if (model.CanAddMesh(modifiedMesh))
                {
                    model.ApplyCommand(new ReplaceMeshCommand(modifiedMesh.id, modifiedMesh));
                }
                else
                {
                    errorOccurred = true;
                    break;
                }
            }

            if (!errorOccurred)
            {
                audioLibrary.PlayClip(audioLibrary.subdivideSound);
                peltzerController.TriggerHapticFeedback();
            }
            else
            {
                audioLibrary.PlayClip(audioLibrary.errorSound);
                peltzerController.TriggerHapticFeedback();
            }
        }

        /// <summary>
        /// Check if two vertices are an edge of a face.
        /// </summary>
        /// <returns>True if the two vertices are an edge of the face.</returns>
        private static bool VerticesAreEdgeOnFace(Vertex v1, Vertex v2, Face face)
        {
            if (face.vertexIds.Count < 3)
            {
                return false;
            }
            int prevId = face.vertexIds[face.vertexIds.Count - 1];
            foreach (int vertId in face.vertexIds)
            {
                if ((prevId == v1.id && vertId == v2.id) || (prevId == v2.id && vertId == v1.id))
                {
                    return true;
                }
                prevId = vertId;
            }
            return false;
        }

        /// <summary>
        ///   Given start and end vertex ids of a segment, insert a vertex in that segment, if found.
        /// </summary>
        /// <param name="face">The face to update.</param>
        /// <param name="startId">Vertex id of the segment start.</param>
        /// <param name="endId">Vertex id of the segment end.</param>
        /// <param name="newVertId">Vertex id to insert.</param>
        /// <returns>
        ///    The updated list of vertices for the new face if the segment was found, or empty list if no
        ///    segment was found.
        /// </returns>
        // Public for testing.
        public static List<int> MaybeInsertVert(Face face, int startId, int endId, int newVertId)
        {
            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                // Look for the segment in either order.
                if ((face.vertexIds[i] == startId && face.vertexIds[(i + 1) % face.vertexIds.Count] == endId)
                  || (face.vertexIds[i] == endId && face.vertexIds[(i + 1) % face.vertexIds.Count] == startId))
                {
                    List<int> newVertIds = new List<int>();
                    // Add the verts that came before the new point.
                    for (int j = 0; j <= i; j++)
                    {
                        newVertIds.Add(face.vertexIds[j]);
                    }
                    newVertIds.Add(newVertId);

                    // Copy the verts after the segment.
                    for (int j = i + 1; j < face.vertexIds.Count; j++)
                    {
                        newVertIds.Add(face.vertexIds[j]);
                    }
                    // Return the vertices of the new face.
                    return newVertIds;
                }
            }
            // Segment wasn't found. No need to update the face.
            return new List<int>();
        }

        /// <summary>
        ///   An event handler that listens for controller input and delegates accordingly.
        /// </summary>
        private void ControllerEventHandler(object sender, ControllerEventArgs args)
        {
            if (peltzerController.mode != ControllerMode.subdivideFace)
                return;

            if (IsStartPressAndHoldEvent(args))
            {
                triggerHoldStartTime = Time.time;
                isTriggerBeingHeld = true;
            }
            else if (IsFinishSubdivideEvent(args))
            {
                if (isSnapping)
                {
                    // We snapped while modifying, so we have learned a bit more about snapping.
                    completedSnaps++;
                }
                FinishSubdivide();
                triggerHoldStartTime = 0;
                isTriggerBeingHeld = false;
            }
            else if (IsStartSnapEvent(args) && !peltzerController.isBlockMode)
            {
                PeltzerMain.Instance.snappedInSubdivider = true;
                isSnapping = true;
                if (completedSnaps < SNAP_KNOW_HOW_COUNT)
                {
                    PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
                }
                PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.alignSound);
                PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
            }
            else if (IsEndSnapEvent(args) && !peltzerController.isBlockMode)
            {
                isSnapping = false;
                PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
            }
            else if (IsSetUpHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadUpAllowed)
            {
                SetHoverTooltip(peltzerController.controllerGeometry.modifyTooltipUp, TouchpadHoverState.UP);
            }
            else if (IsSetLeftHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed)
            {
                SetHoverTooltip(peltzerController.controllerGeometry.modifyTooltipLeft, TouchpadHoverState.LEFT);
            }
            else if (IsSetRightHoverTooltipEvent(args) && PeltzerMain.Instance.restrictionManager.touchpadRightAllowed)
            {
                SetHoverTooltip(peltzerController.controllerGeometry.modifyTooltipRight, TouchpadHoverState.RIGHT);
            }
            else if (IsUnsetAllHoverTooltipsEvent(args))
            {
                UnsetAllHoverTooltips();
            }
        }

        /// <summary>
        /// Gets intersection points with face boundary.
        /// </summary>
        /// <param name="face">Face to get points for.</param>
        /// <param name="mesh">Mesh to get points for.</param>
        /// <param name="subdividePoints">2 points of intersection with face boundary.</param>
        /// <returns>True if the intersection points were found.</returns>
        private bool GetSubdividePoints(
            Face face, MMesh mesh, Vector3 pointOfIntersection, Vector3 sliceDirection, out SubdividePoints subdividePoints)
        {

            List<Vertex> faceVertices = new List<Vertex>(face.vertexIds.Count);
            foreach (int id in face.vertexIds)
            {
                faceVertices.Add(new Vertex(id, mesh.VertexPositionInModelCoords(id)));
            }

            Vector3 faceNormal = mesh.rotation * face.normal;
            subdividePoints = new SubdividePoints();

            Vector3 normalToSliceLine = Vector3.Cross(sliceDirection, faceNormal).normalized;
            float slicePointDot = Vector3.Dot(pointOfIntersection, normalToSliceLine);

            bool faceEdgeIntersection1Found = false;
            bool faceEdgeIntersection2Found = false;
            Vertex prev = faceVertices[faceVertices.Count - 1];
            for (int i = 0; i < faceVertices.Count; i++)
            {
                Vertex curr = faceVertices[i];
                Vector3 edge = curr.loc - prev.loc;
                float d1 = Vector3.Dot(prev.loc, normalToSliceLine);
                float d2 = Vector3.Dot(curr.loc, normalToSliceLine);

                // The two following checks in the if and else are determining if curr and prev are above and below our
                // slice line, which means our slice point is somewhere along the edge.
                if (d1 <= slicePointDot && slicePointDot < d2)
                {
                    // We can use the ratio of dot-products to determine how far along the edge the cut point is.
                    float ratio = (slicePointDot - d1) / (d2 - d1);
                    if (faceEdgeIntersection1Found)
                    {
                        faceEdgeIntersection2Found = true;
                        subdividePoints.point2 = prev.loc + edge * ratio;
                        subdividePoints.point2Index = i == 0 ? faceVertices.Count - 1 : i - 1;
                        subdividePoints.edge2 = new EdgeKey(mesh.id, prev.id, curr.id);
                        break;
                    }
                    else
                    {
                        faceEdgeIntersection1Found = true;
                        subdividePoints.point1 = prev.loc + edge * ratio;
                        subdividePoints.point1Index = i == 0 ? faceVertices.Count - 1 : i - 1;
                        subdividePoints.edge1 = new EdgeKey(mesh.id, prev.id, curr.id);
                    }
                }
                else if (d2 <= slicePointDot && slicePointDot < d1)
                {
                    float ratio = (slicePointDot - d2) / (d1 - d2);
                    if (faceEdgeIntersection1Found)
                    {
                        faceEdgeIntersection2Found = true;
                        subdividePoints.point2 = curr.loc - edge * ratio;
                        subdividePoints.point2Index = i == 0 ? faceVertices.Count - 1 : i - 1;
                        subdividePoints.edge2 = new EdgeKey(mesh.id, prev.id, curr.id);
                        break;
                    }
                    else
                    {
                        faceEdgeIntersection1Found = true;
                        subdividePoints.point1 = curr.loc - edge * ratio;
                        subdividePoints.point1Index = i == 0 ? faceVertices.Count - 1 : i - 1;
                        subdividePoints.edge1 = new EdgeKey(mesh.id, prev.id, curr.id);
                    }
                }
                prev = curr;
            }
            return faceEdgeIntersection2Found;
        }

        /// <summary>
        /// Takes the points that the subdivide line interesects with the edges and snap these individually
        /// to the nearest point of interest.
        /// </summary>
        private void SnapSubdivision(Subdivision subdivision, SubdividePoints edgeIntersectionPoints)
        {
            Face face = subdivision.face;
            List<Vector3> currentFaceVertices = new List<Vector3>(face.vertexIds.Count);
            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                currentFaceVertices.Add(subdivision.mesh.VertexPositionInModelCoords(face.vertexIds[i]));
            }
            List<Vector3> interestingFacePoints = FindInterestingFacePoints(currentFaceVertices);

            Vector3 firstPoint = Math3d.NearestPoint(edgeIntersectionPoints.point1, interestingFacePoints);
            Vector3 secondPoint = Math3d.NearestPoint(edgeIntersectionPoints.point2, interestingFacePoints);

            // Redefine the snapped subdivide line.
            subdivision.startPoint = firstPoint;
            subdivision.sliceDirection = secondPoint - firstPoint;
        }

        /// <summary>
        /// Given a plane and a line segment defined by 2 vectors, finds the intersection point.
        /// </summary>
        bool GetSegmentPlaneIntersection(Plane plane, Vector3 p1, Vector3 p2, out Vector3 intersectionPoint)
        {
            float d1 = plane.GetDistanceToPoint(p1);
            float d2 = plane.GetDistanceToPoint(p2);

            // Both points are on the same side of the plane, so no intersection.
            if (d1 * d2 > 0)
            {
                intersectionPoint = Vector3.zero;
                return false;
            }

            // t is the normalized distance from p1 to p2 where the intersection happens.
            float t = d1 / (d1 - d2);
            intersectionPoint = p1 + t * (p2 - p1);
            return true;
        }

        /// <summary>
        /// Given a plane and a face defined by an order list of vertices, finds out the points
        /// where the plane intersects the face edges.
        /// </summary>
        void FindFacePlaneIntersection(Plane plane,
          List<Vector3> vertices, out List<Vector3> outSegTips)
        {
            Vector3 intersectionPoint;
            outSegTips = new List<Vector3>();

            Vector3 prev = vertices[vertices.Count - 1];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 curr = vertices[i];
                if (GetSegmentPlaneIntersection(plane, curr, prev, out intersectionPoint))
                {
                    outSegTips.Add(intersectionPoint);
                }
                prev = curr;
            }
        }
    }
}
