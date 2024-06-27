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

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    /// This component is responsible for managing the relationship between Poly and the rendered version of the
    /// universal grid.  Currently it sets up the grid, adjusts its scale based on zoom level, and kicks off the render.
    /// </summary>
    public class GridHighlightComponent : MonoBehaviour
    {
        // Number of verts to draw in each row of the grid
        private int numVertsInRow = 7;
        // Scale of the grid - a value of 4 will have every visible unity represent 4 grid units for instance.
        private int gridScale = 4;
        private Material gridMaterial;
        private WorldSpace worldSpace;
        private PeltzerController peltzerController;
        private GridHighlighter gridHighlight;

        private float origWorldSpaceSpacing;
        public void Setup(MaterialLibrary materialLibrary, WorldSpace worldSpace, PeltzerController peltzerController)
        {
            this.gridMaterial = new Material(materialLibrary.gridMaterial);
            this.worldSpace = worldSpace;
            this.peltzerController = peltzerController;
            this.gridHighlight = new GridHighlighter();
            this.gridHighlight.InitGrid(numVertsInRow, gridScale);
            this.origWorldSpaceSpacing = GridUtils.GRID_SIZE * gridScale;
        }

        /// <summary>
        /// Renders the grid if we're in block mode.
        /// </summary>
        public void LateUpdate()
        {
            if (peltzerController.isBlockMode)
            {
                // Resize the grid if our current zoom has caused it to get either too coarse or too fine grained.
                // TODO(bug): We'll need to tweak these based on feedback.

                if (worldSpace.scale * GridUtils.GRID_SIZE * gridScale < 0.75f * origWorldSpaceSpacing)
                {
                    gridScale = gridScale * 2;
                }
                else if (worldSpace.scale * GridUtils.GRID_SIZE * gridScale > 1.5f * origWorldSpaceSpacing)
                {
                    gridScale = Mathf.Max(1, gridScale / 2);
                }
                Vector3 curPos = peltzerController.LastPositionModel;
                Vector3 curPosWorld = worldSpace.ModelToWorld(curPos);
                // Set the shader uniforms for center of grid and fade radius
                gridMaterial.SetVector("_GridCenterWorld", new Vector4(curPosWorld.x, curPosWorld.y, curPosWorld.z));
                float worldSpaceRadius = 0.66f * worldSpace.scale
                  * Mathf.Floor(numVertsInRow / 2) * GridUtils.GRID_SIZE * gridScale;
                gridMaterial.SetFloat("_GridRenderRadius", worldSpaceRadius);
                // Have the grid render itself
                gridHighlight.Render(curPos, worldSpace.modelToWorld, gridMaterial, gridScale);
            }
        }
    }
}