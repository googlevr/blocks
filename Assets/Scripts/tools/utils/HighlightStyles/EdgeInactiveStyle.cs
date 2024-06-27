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
    /// This class exists primarily to hold the static method for RenderEdges when INACTIVE is set. It may be possible to
    /// consolidate this with the other Edge*Style classes in the future.
    /// </summary>
    public class EdgeInactiveStyle
    {

        public static Material material;
        public static Vector3 selectPositionModel;
        private static Mesh edgeRenderMesh = new Mesh();


        // Renders edge highlights.
        // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
        // edge geometry frame to frame) - 37281287
        public static void RenderEdges(Model model,
          HighlightUtils.TrackedHighlightSet<EdgeKey> edgeHighlights,
          WorldSpace worldSpace)
        {
            HashSet<EdgeKey> keys = edgeHighlights.getKeysForStyle((int)EdgeStyles.EDGE_INACTIVE);
            if (keys.Count == 0) { return; }
            edgeRenderMesh.Clear();
            int[] indices = new int[edgeHighlights.RenderableCount() * 2];
            Vector3[] vertices = new Vector3[edgeHighlights.RenderableCount() * 2];
            // Because Unity does not make a "arbitrary data" vertex channel available to us, we're going to abuse the UV
            // channel to pass per-vertex animation state into the shader.
            Vector2[] selectData = new Vector2[edgeHighlights.RenderableCount() * 2];
            Vector3[] normals = new Vector3[edgeHighlights.RenderableCount() * 2];
            //TODO(bug): setup connectivity info so that we can use correct normals from adjacent faces
            Vector3 normal = new Vector3(0f, 1f, 0f);
            int i = 0;

            float radius2 = InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS *
                            InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS;
            foreach (EdgeKey key in keys)
            {
                if (!model.HasMesh(key.meshId)) { continue; }
                MMesh mesh = model.GetMesh(key.meshId);
                if (!mesh.HasVertex(key.vertexId1) || !mesh.HasVertex(key.vertexId2)) continue;
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId1);
                Vector3 diff = vertices[i] - selectPositionModel;
                float dist2 = Vector3.Dot(diff, diff);
                float alpha = Mathf.Clamp((radius2 - dist2) / radius2, 0f, 1f);

                indices[i] = i;
                float animPct = edgeHighlights.GetAnimPct(key);
                selectData[i] = new Vector2(animPct, alpha);
                normals[i] = normal;
                i++;
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId2);
                diff = vertices[i] - selectPositionModel;
                dist2 = Vector3.Dot(diff, diff);
                alpha = Mathf.Clamp((radius2 - dist2) / radius2, 0f, 1f);
                indices[i] = i;
                selectData[i] = new Vector2(animPct, alpha);
                normals[i] = normal;
                i++;
            }
            edgeRenderMesh.vertices = vertices;
            // These are not actually UVs - we're using the UV channel to pass per-primitive animation data so that edges
            // animate independently.
            edgeRenderMesh.uv = selectData;
            // Since we're using a line geometry shader we need to set the mesh up to supply data as lines.
            edgeRenderMesh.SetIndices(indices, MeshTopology.Lines, 0 /* submesh id */, false /* recalculate bounds */);
            if (edgeHighlights.RenderableCount() > 0)
            {
                Graphics.DrawMesh(edgeRenderMesh, worldSpace.modelToWorld, material, 0);
            }
        }
    }
}
