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
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    /// This class exists primarily to hold the static method for RenderVertices when INACTIVE is set. It may be possible to
    /// consolidate this with the other Vertex*Style classes in the future.
    /// </summary>
    public class VertexInactiveStyle
    {

        public static Material material;
        public static Vector3 selectPositionModel;
        private static Mesh vertexRenderMesh = new Mesh();
        // Renders vertex highlights.
        // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
        // vertex geometry frame to frame) - 37281287
        public static void RenderVertices(Model model,
          HighlightUtils.TrackedHighlightSet<VertexKey> vertexHighlights,
          WorldSpace worldSpace)
        {
            // Renders vertex highlights.
            HashSet<VertexKey> keys = vertexHighlights.getKeysForStyle((int)VertexStyles.VERTEX_INACTIVE);
            if (keys.Count == 0) { return; }
            vertexRenderMesh.Clear();
            int[] indices = new int[vertexHighlights.RenderableCount()];
            Vector3[] vertices = new Vector3[vertexHighlights.RenderableCount()];
            // Because Unity does not make a "arbitrary data" vertex channel available to us, we're going to abuse the UV
            // channel to pass per-vertex animation state into the shader.
            Vector2[] selectData = new Vector2[vertexHighlights.RenderableCount()];
            float radius2 = InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS *
                            InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS;
            int i = 0;
            foreach (VertexKey key in keys)
            {
                if (!model.HasMesh(key.meshId)) { continue; }
                MMesh mesh = model.GetMesh(key.meshId);
                if (!mesh.HasVertex(key.vertexId))
                {
                    continue;
                }
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId);
                Vector3 diff = vertices[i] - selectPositionModel;
                float dist2 = Vector3.Dot(diff, diff);
                float alpha = Mathf.Clamp((radius2 - dist2) / radius2, 0f, 1f);
                indices[i] = i;
                float animPct = vertexHighlights.GetAnimPct(key);

                selectData[i] = new Vector2(animPct, alpha);
                i++;
            }
            vertexRenderMesh.vertices = vertices;
            // These are not actually UVs - we're using the UV channel to pass per-primitive animation data so that edges
            // animate independently.
            vertexRenderMesh.uv = selectData;
            // Since we're using a point geometry shader we need to set the mesh up to supply data as points.
            vertexRenderMesh.SetIndices(indices, MeshTopology.Points, 0 /* submesh id */, false /* recalculate bounds */);

            Graphics.DrawMesh(vertexRenderMesh, worldSpace.modelToWorld, material, 0);
        }
    }
}
