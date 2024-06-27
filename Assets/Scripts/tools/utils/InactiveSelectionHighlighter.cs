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
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils
{
    public class InactiveSelectionHighlighter
    {
        public static float INACTIVE_HIGHLIGHT_RADIUS = .5f;
        public static float OLD_INACTIVE_HIGHLIGHT_RADIUS = .5f;
        public static float NEW_INACTIVE_HIGHLIGHT_RADIUS = 10f;
        public static readonly int MAX_VERTS_IN_WIREFRAME = 500;
        private SpatialIndex spatialIndex;
        private HighlightUtils highlightUtils;
        private WorldSpace worldSpace;
        private Model model;

        private HashSet<VertexKey> currentSelectableVerts;
        private HashSet<EdgeKey> currentSelectableEdges;
        private HashSet<int> meshesInRange;

        private HashSet<VertexKey> knownSelectedOrHighlightedVerts;
        private HashSet<EdgeKey> knownSelectedOrHighlightedEdges;

        private MMesh closestMesh;
        private float closestDistance;

        public InactiveSelectionHighlighter(SpatialIndex spatialIndex, HighlightUtils highlightUtils,
          WorldSpace worldSpace, Model model)
        {
            this.spatialIndex = spatialIndex;
            this.highlightUtils = highlightUtils;
            this.worldSpace = worldSpace;
            this.model = model;

            currentSelectableVerts = new HashSet<VertexKey>();
            currentSelectableEdges = new HashSet<EdgeKey>();
            knownSelectedOrHighlightedEdges = new HashSet<EdgeKey>();
            knownSelectedOrHighlightedVerts = new HashSet<VertexKey>();
            meshesInRange = new HashSet<int>();
        }

        /// <summary>
        /// Turns on inactive vertex and edge rendering near the current position.
        /// </summary>
        /// <param name="selectPositionModel">The current active selection position.</param>
        /// <param name="currentlySelectedVerts">Currently selected vertices from Selector</param>
        /// <param name="currentlyHoveredVert">Currently hovered vertex from Selector</param>
        /// <param name="currentlySelectedEdges">Currently selected edges from Selector</param>
        /// <param name="currentlyHoveredEdge">Currently hovered edge from Selector</param>
        public void ShowSelectableVertsEdgesNear(Vector3 selectPositionModel,
          HashSet<VertexKey> currentlySelectedVerts,
          VertexKey currentlyHoveredVert,
          HashSet<EdgeKey> currentlySelectedEdges,
          EdgeKey currentlyHoveredEdge)
        {
            GetMeshesNear(selectPositionModel, currentlySelectedEdges.Count == 0, currentlySelectedVerts.Count == 0,
              currentlySelectedVerts, currentlySelectedEdges, currentlyHoveredVert, currentlyHoveredEdge);
        }

        /// <summary>
        /// Turns off all inactive mesh element rendering.
        /// </summary>
        public void TurnOffVertsEdges()
        {
            TurnOffInactiveEdges();
            TurnOffInactiveVerts();
            highlightUtils.inactiveRenderer.Clear();
            meshesInRange.Clear();
        }

        /// <summary>
        /// Turns off inactive vertex rendering.
        /// </summary>
        private void TurnOffInactiveVerts()
        {
            foreach (VertexKey key in currentSelectableVerts)
            {
                highlightUtils.TurnOff(key);
            }
        }

        /// <summary>
        /// Turns off inactive edge rendering.
        /// </summary>
        private void TurnOffInactiveEdges()
        {
            foreach (EdgeKey key in currentSelectableEdges)
            {
                highlightUtils.TurnOff(key);
            }
        }

        /// <summary>
        /// Turns on inactive verts.
        /// </summary>
        public void ShowSelectableVertsNear(Vector3 selectPositionModel,
          HashSet<VertexKey> currentlySelectedVerts,
          VertexKey currentlyHoveredVert)
        {
            TurnOffInactiveEdges();
            GetMeshesNear(selectPositionModel, true, false, currentlySelectedVerts, null, currentlyHoveredVert, null);

        }

        /// This should be treated as a local variable for GetMeshesNear - it's being declared outside of the method in
        /// order to preallocate the hash set.
        private HashSet<int> nearbyMeshes = new HashSet<int>();
        private void GetMeshesNear(Vector3 selectPositionModel, bool showVerts, bool showEdges,
          HashSet<VertexKey> selectedVerts, HashSet<EdgeKey> selectedEdges, VertexKey hoveredVert, EdgeKey hoveredEdge)
        {
            nearbyMeshes.Clear();
            spatialIndex.FindMeshesClosestToDirect(selectPositionModel, INACTIVE_HIGHLIGHT_RADIUS, ref nearbyMeshes);
            SelectFromMeshes(nearbyMeshes, showVerts, showEdges, selectedVerts, selectedEdges, hoveredVert, hoveredEdge);
            highlightUtils.inactiveRenderer.SetSelectPosition(selectPositionModel);
        }

        /// This should be treated as a local variable for SelectFromMeshes - it's being declared outside of the method in
        /// order to preallocate the hash set.
        private HashSet<int> newlyInactiveMeshes = new HashSet<int>();
        /// <summary>
        /// Given a set of meshids which should render wireframes, manage the rendering of the wireframes.
        /// </summary>
        private void SelectFromMeshes(HashSet<int> selectableMeshIds, bool showVerts, bool showEdges,
            HashSet<VertexKey> selectedVerts, HashSet<EdgeKey> selectedEdges, VertexKey hoveredVert, EdgeKey hoveredEdge)
        {
            newlyInactiveMeshes.Clear();
            newlyInactiveMeshes.UnionWith(meshesInRange);
            newlyInactiveMeshes.ExceptWith(selectableMeshIds);
            highlightUtils.inactiveRenderer.TurnOnEdgeWireframe(newlyInactiveMeshes);

            // And turn them off
            // Remove the inactive ones from the set we'll render
            meshesInRange.ExceptWith(newlyInactiveMeshes);
            // Remove ones that are actually selected
            selectableMeshIds.ExceptWith(meshesInRange);
            highlightUtils.inactiveRenderer.showEdges = showEdges;
            highlightUtils.inactiveRenderer.showPoints = showVerts;
            if (showEdges)
            {
                highlightUtils.inactiveRenderer.TurnOnEdgeWireframe(selectableMeshIds);
            }
            if (showVerts)
            {
                highlightUtils.inactiveRenderer.TurnOnPointWireframe(selectableMeshIds);
            }
            // And add any new ones we found
            meshesInRange.UnionWith(selectableMeshIds);
        }

        /// This should be treated as a local variable for ShowSelectableVertsNearInternal - it's being declared outside of
        /// the method in order to preallocate the hash set.
        private static HashSet<VertexKey> newlyInactiveVerts = new HashSet<VertexKey>();
        /// <summary>
        /// Renders inactive vertices in a radius around the selection point.
        /// </summary>
        private void ShowSelectableVertsNearInternal(HashSet<VertexKey> currentlySelectedVerts,
          HashSet<VertexKey> selectableVerts,
          VertexKey currentlyHoveredVert)
        {
            // Subtract that set from our old set to find which have transitioned into inactive
            newlyInactiveVerts.Clear();
            newlyInactiveVerts.UnionWith(currentSelectableVerts);
            newlyInactiveVerts.ExceptWith(selectableVerts);
            // Remove the inactive ones from the set we'll render
            currentSelectableVerts.ExceptWith(newlyInactiveVerts);
            // And add any new ones we found
            currentSelectableVerts.UnionWith(selectableVerts);
            // Remove ones that are actually selected
            currentSelectableVerts.ExceptWith(currentlySelectedVerts);
            // And if one is hovered, remove that too.
            if (currentlyHoveredVert != null)
            {
                currentSelectableVerts.Remove(currentlyHoveredVert);
            }
            // Now turn 'em all on.
            foreach (VertexKey key in currentSelectableVerts)
            {
                highlightUtils.TurnOn(key);
                highlightUtils.SetVertexStyleToInactive(key);
            }
        }

        /// <summary>
        /// Turns on inactive edges.
        /// </summary>
        public void ShowSelectableEdgesNear(Vector3 selectPositionModel,
          HashSet<EdgeKey> currentlySelectedEdges,
          EdgeKey currentlyHoveredEdge)
        {
            TurnOffInactiveVerts();
            GetMeshesNear(selectPositionModel, false, true, null, currentlySelectedEdges, null, currentlyHoveredEdge);
        }
    }
}