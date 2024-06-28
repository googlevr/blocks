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
    /// This class exists primarily to hold the static method for RenderEdges when TEMPORARY is set. It may be possible to
    /// consolidate this with the other Edge*Style classes in the future.
    /// </summary>
    public class EdgeTemporaryStyle
    {
        private static int nextId = 0;
        private static Mesh edgeRenderMesh = new Mesh();
        public class TemporaryEdge
        {
            public int id;
            public Vector3 vertex1PositionModelSpace;
            public Vector3 vertex2PositionModelSpace;

            public TemporaryEdge()
            {
                id = nextId++;
            }

            public override bool Equals(object otherObject)
            {
                if (!(otherObject is TemporaryEdge))
                    return false;

                TemporaryEdge other = (TemporaryEdge)otherObject;
                return other.id == id;
            }

            public override int GetHashCode()
            {
                return id;
            }
        }

        public static Material material;

        // Renders edge highlights.
        // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
        // edge geometry frame to frame) - 37281287
        public static void RenderEdges(Model model,
          HighlightUtils.TrackedHighlightSet<TemporaryEdge> temporaryEdgeHighlights,
          WorldSpace worldSpace)
        {
            HashSet<TemporaryEdge> keys = temporaryEdgeHighlights.getKeysForStyle((int)EdgeStyles.EDGE_SELECT);
            if (keys.Count == 0) { return; }
            edgeRenderMesh.Clear();
            int[] indices = new int[temporaryEdgeHighlights.RenderableCount() * 2];
            Vector3[] vertices = new Vector3[temporaryEdgeHighlights.RenderableCount() * 2];
            // Because Unity does not make a "arbitrary data" vertex channel available to us, we're going to abuse the UV
            // channel to pass per-vertex animation state into the shader.
            Vector2[] selectData = new Vector2[temporaryEdgeHighlights.RenderableCount() * 2];
            Vector3[] normals = new Vector3[temporaryEdgeHighlights.RenderableCount() * 2];
            //TODO(bug): setup connectivity info so that we can use correct normals from adjacent faces
            Vector3 normal = new Vector3(0f, 1f, 0f);
            int i = 0;
            float scaleFactor = InactiveRenderer.GetEdgeScaleFactor(worldSpace);
            material.SetFloat("_PointSphereRadius", scaleFactor);

            foreach (TemporaryEdge key in keys)
            {
                vertices[i] = key.vertex1PositionModelSpace;
                indices[i] = i;
                float animPct = temporaryEdgeHighlights.GetAnimPct(key);
                selectData[i] = new Vector2(animPct, 1f);
                normals[i] = normal;
                i++;
                vertices[i] = key.vertex2PositionModelSpace;
                indices[i] = i;
                // The second component of this vector isn't used yet.
                selectData[i] = new Vector2(animPct, 1f);
                normals[i] = normal;
                i++;
            }
            edgeRenderMesh.vertices = vertices;
            // These are not actually UVs - we're using the UV channel to pass per-primitive animation data so that edges
            // animate independently.
            edgeRenderMesh.uv = selectData;
            // Since we're using a line geometry shader we need to set the mesh up to supply data as lines.
            edgeRenderMesh.SetIndices(indices, MeshTopology.Lines, 0 /* submesh id */, false /* recalculate bounds */);
            if (temporaryEdgeHighlights.RenderableCount() > 0)
            {
                Graphics.DrawMesh(edgeRenderMesh, worldSpace.modelToWorld, material, 0);
            }
        }
    }
}
