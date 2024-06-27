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
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    /// This class handles rendering of inactive meshes, caching as much as it can frame to frame.
    /// </summary>
    public class InactiveRenderer
    {
        private static float SCALE_THRESH = 1f;
        private const int MAX_INDEX_COUNT = 64000;

        private class StaticCachedRenderMesh
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public List<int> indices;
            public int numElements;
            public bool dirty;
            public Mesh mesh;

            public StaticCachedRenderMesh()
            {
                mesh = new Mesh();
                vertices = new Vector3[MAX_INDEX_COUNT];
                normals = new Vector3[MAX_INDEX_COUNT];
                indices = new List<int>(MAX_INDEX_COUNT);
                numElements = 0;
                dirty = false;
                vertices[0] = new Vector3(999999, 999999, 999999);
                vertices[1] = new Vector3(-999999, -999999, -999999);
                mesh.vertices = vertices;
                // Make sure this never gets culled
                mesh.RecalculateBounds();
            }

            public void Clear()
            {
                Array.Clear(vertices, 0, vertices.Length);
                Array.Clear(normals, 0, normals.Length);
                indices.Clear();
                numElements = 0;
                dirty = false;
            }
        }


        private Model model;
        private WorldSpace worldSpace;
        public Material inactiveEdgeMaterial;
        public Material inactivePointMaterial;
        private Vector3 selectPositionWorld;

        /// <summary>
        /// Sets whether the inactive renderer should render inactive vertices.
        /// </summary>
        public bool showPoints { get; set; }

        /// <summary>
        /// Determines whether the inactive renderer should render inactive edges.
        /// </summary>
        public bool showEdges { get; set; }

        private static float baseVertexScale;
        private static float baseEdgeScale;

        public InactiveRenderer(Model model, WorldSpace worldSpace, MaterialLibrary materialLibrary)
        {
            this.model = model;
            this.worldSpace = worldSpace;
            availableEdgeMeshes = new List<StaticCachedRenderMesh>();
            edgeMeshes = new List<StaticCachedRenderMesh>();
            meshesInEdgeMeshes = new HashSet<int>();
            availablePointMeshes = new List<StaticCachedRenderMesh>();
            pointMeshes = new List<StaticCachedRenderMesh>();
            meshesInPointMeshes = new HashSet<int>();
            inactiveEdgeMaterial = new Material(materialLibrary.edgeInactiveMaterial);
            inactivePointMaterial = new Material(materialLibrary.pointInactiveMaterial);
            baseVertexScale = inactivePointMaterial.GetFloat("_PointSphereRadius");
            baseEdgeScale = inactiveEdgeMaterial.GetFloat("_PointSphereRadius");
        }

        private List<StaticCachedRenderMesh> availableEdgeMeshes;
        private List<StaticCachedRenderMesh> edgeMeshes;
        private HashSet<int> meshesInEdgeMeshes;

        private List<StaticCachedRenderMesh> availablePointMeshes;
        private List<StaticCachedRenderMesh> pointMeshes;
        private HashSet<int> meshesInPointMeshes;

        /// <summary>
        /// Returns the scale factor used for rendering inactive vertices - used by selector to make sure selection radii
        /// match with what the user sees.
        /// </summary>
        public static float GetVertScaleFactor(WorldSpace worldSpace)
        {
            return (Mathf.Min(worldSpace.scale, SCALE_THRESH) / SCALE_THRESH) * baseVertexScale;
        }

        /// <summary>
        /// Returns the scale factor used for rendering inactive edges - used by selector to make sure selection radii
        /// match with what the user sees.
        /// </summary>
        public static float GetEdgeScaleFactor(WorldSpace worldSpace)
        {
            return (Mathf.Min(worldSpace.scale, SCALE_THRESH) / SCALE_THRESH) * baseEdgeScale;
        }

        private List<EdgeKey> edges = new List<EdgeKey>();
        private HashSet<EdgeKey> edgeSet = new HashSet<EdgeKey>();

        /// <summary>
        /// Turns on edge wireframes for supplied meshes. (Will use cached data if a mesh has been passed to this method
        /// since the most recent clear.)
        /// </summary>
        public void TurnOnEdgeWireframe(IEnumerable<int> meshIds)
        {
            edges.Clear();
            edgeSet.Clear();
            foreach (int meshId in meshIds)
            {
                if (meshesInEdgeMeshes.Contains(meshId)) continue;

                if (!model.HasMesh(meshId)) continue;

                MMesh polyMesh = model.GetMesh(meshId);

                foreach (Face curFace in polyMesh.GetFaces())
                {
                    for (int i = 0; i < curFace.vertexIds.Count; i++)
                    {
                        edgeSet.Add(new EdgeKey(meshId, curFace.vertexIds[i], curFace.vertexIds[(i + 1) % curFace.vertexIds.Count]));
                    }
                }
                meshesInEdgeMeshes.Add(meshId);
            }
            edges = edgeSet.ToList();
            int curEdge = 0;
            while (curEdge < edges.Count)
            {
                StaticCachedRenderMesh curMesh = GetCurEdgeMesh();
                int curMeshStartingIndex = curMesh.numElements;
                int edgesSpaceStillNeeded = (edges.Count - curEdge) * 2;
                int edgeSpaceLeftInCurMesh = MAX_INDEX_COUNT - curMesh.numElements;
                int edgesToPutInCurMesh = Mathf.Min(edgesSpaceStillNeeded, edgeSpaceLeftInCurMesh);
                int curMeshIndex = curMeshStartingIndex;
                curMesh.dirty = curMesh.dirty || edgesToPutInCurMesh > 0;
                for (int i = curEdge; i < edgesToPutInCurMesh / 2 + curEdge; i++)
                {
                    int index = curMeshIndex + 2 * (i - curEdge);
                    if (!model.HasMesh(edges[i].meshId)) continue;

                    MMesh polyMesh = model.GetMesh(edges[i].meshId);

                    curMesh.vertices[index] =
                      polyMesh.VertexPositionInModelCoords(edges[i].vertexId1);
                    curMesh.vertices[index + 1] =
                      polyMesh.VertexPositionInModelCoords(edges[i].vertexId2);
                    curMesh.normals[index] = new Vector3(0f, 1f, 0f);
                    curMesh.normals[index + 1] = new Vector3(0f, 1f, 0f);

                    curMesh.indices.Add(index);
                    curMesh.indices.Add(index + 1);
                }
                curEdge += edgesToPutInCurMesh / 2;
                curMesh.numElements += edgesToPutInCurMesh;
                curMesh.mesh.vertices = curMesh.vertices;
                curMesh.mesh.normals = curMesh.normals;
                curMesh.mesh.SetIndices(curMesh.indices.ToArray(), MeshTopology.Lines, 0, false /* recalculateBounds */);
            }
        }

        /// <summary>
        /// Turns on vertex wireframes for supplied meshes. (Will use cached data if a mesh has been passed to this method
        /// since the most recent clear.)
        /// </summary>
        public void TurnOnPointWireframe(IEnumerable<int> meshIds)
        {
            List<VertexKey> vertexKeys = new List<VertexKey>();
            foreach (int meshId in meshIds)
            {
                if (meshesInPointMeshes.Contains(meshId)) continue;

                if (!model.HasMesh(meshId)) continue;

                MMesh polyMesh = model.GetMesh(meshId);

                foreach (int vertId in polyMesh.GetVertexIds())
                {
                    vertexKeys.Add(new VertexKey(meshId, vertId));
                }
                meshesInPointMeshes.Add(meshId);
            }

            int curVert = 0;
            while (curVert < vertexKeys.Count)
            {
                StaticCachedRenderMesh curMesh = GetCurPointMesh();
                int curMeshStartingIndex = curMesh.numElements;
                int vertexSpaceStillNeeded = (vertexKeys.Count - curVert);
                int vertSpaceLeftInCurMesh = MAX_INDEX_COUNT - curMesh.numElements;
                int numVertsToPutInCurMesh = Mathf.Min(vertexSpaceStillNeeded, vertSpaceLeftInCurMesh);
                int curMeshIndex = curMeshStartingIndex;
                curMesh.dirty = curMesh.dirty || numVertsToPutInCurMesh > 0;
                for (int i = curVert; i < numVertsToPutInCurMesh + curVert; i++)
                {
                    int index = curMeshIndex + i - curVert;

                    if (!model.HasMesh(vertexKeys[i].meshId)) continue;

                    MMesh polyMesh = model.GetMesh(vertexKeys[i].meshId);

                    curMesh.vertices[index] =
                      polyMesh.VertexPositionInModelCoords(vertexKeys[i].vertexId);
                    curMesh.indices.Add(index);

                }
                curVert += numVertsToPutInCurMesh;
                curMesh.numElements += numVertsToPutInCurMesh;
                curMesh.mesh.vertices = curMesh.vertices;
                curMesh.mesh.normals = curMesh.normals;
                curMesh.mesh.SetIndices(curMesh.indices.ToArray(), MeshTopology.Points, 0, false /* recalculateBounds */);
            }
        }

        /// <summary>
        /// Sets the select position to use for rendering inactive elements - this is used to fade out the selection.
        /// </summary>
        /// <param name="selectPositionModel"></param>
        public void SetSelectPosition(Vector3 selectPositionModel)
        {
            selectPositionWorld = worldSpace.ModelToWorld(selectPositionModel);
        }

        private StaticCachedRenderMesh GetCurEdgeMesh()
        {
            if (edgeMeshes.Count == 0)
            {
                if (availableEdgeMeshes.Count > 0)
                {
                    int lastAvailableMeshIndex = availableEdgeMeshes.Count - 1;
                    StaticCachedRenderMesh mesh = availableEdgeMeshes[lastAvailableMeshIndex];
                    edgeMeshes.Add(mesh);
                    availableEdgeMeshes.RemoveAt(lastAvailableMeshIndex);
                    return mesh;
                }
                StaticCachedRenderMesh newMesh = new StaticCachedRenderMesh();
                edgeMeshes.Add(newMesh);
                return newMesh;
            }

            StaticCachedRenderMesh lastMesh = edgeMeshes[edgeMeshes.Count - 1];
            if (lastMesh.numElements >= MAX_INDEX_COUNT)
            {
                StaticCachedRenderMesh newMesh = new StaticCachedRenderMesh();
                edgeMeshes.Add(newMesh);
                return newMesh;
            }
            return lastMesh;
        }

        private StaticCachedRenderMesh GetCurPointMesh()
        {
            if (pointMeshes.Count == 0)
            {
                if (availablePointMeshes.Count > 0)
                {
                    int lastAvailableMeshIndex = availablePointMeshes.Count - 1;
                    StaticCachedRenderMesh mesh = availablePointMeshes[lastAvailableMeshIndex];
                    pointMeshes.Add(mesh);
                    availablePointMeshes.RemoveAt(lastAvailableMeshIndex);
                    return mesh;
                }
                StaticCachedRenderMesh newMesh = new StaticCachedRenderMesh();
                pointMeshes.Add(newMesh);
                return newMesh;
            }

            StaticCachedRenderMesh lastMesh = pointMeshes[pointMeshes.Count - 1];
            if (lastMesh.numElements >= MAX_INDEX_COUNT)
            {
                StaticCachedRenderMesh newMesh = new StaticCachedRenderMesh();
                pointMeshes.Add(newMesh);
                return newMesh;
            }
            return lastMesh;
        }

        /// <summary>
        /// Clears all vertices and edges out of the inactive renderer.
        /// </summary>
        public void Clear()
        {
            availableEdgeMeshes.AddRange(edgeMeshes);
            edgeMeshes.Clear();
            foreach (StaticCachedRenderMesh mesh in availableEdgeMeshes)
            {
                mesh.Clear();
            }
            meshesInEdgeMeshes.Clear();

            availablePointMeshes.AddRange(pointMeshes);
            pointMeshes.Clear();
            foreach (StaticCachedRenderMesh mesh in availablePointMeshes)
            {
                mesh.Clear();
            }
            meshesInPointMeshes.Clear();
            // If the user has changed the flag, we handle it here so that next time they use the tool it's updated.
            // It's a bit janky, but since this is handling a console command rather than real UX it's okay - if we go
            // with the new radius it will be set from the start and this just goes away.
            InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS = Features.expandedWireframeRadius
              ? InactiveSelectionHighlighter.NEW_INACTIVE_HIGHLIGHT_RADIUS
              : InactiveSelectionHighlighter.OLD_INACTIVE_HIGHLIGHT_RADIUS;
        }

        /// <summary>
        /// Renders the inactive edges.
        /// </summary>
        public void RenderEdges()
        {
            if (showEdges && edgeMeshes.Count > 0)
            {
                float scaleFactor = GetEdgeScaleFactor(worldSpace);
                inactiveEdgeMaterial.SetFloat("_PointSphereRadius", scaleFactor);
                inactiveEdgeMaterial.SetFloat("_VertexSphereRadius", scaleFactor);
                inactiveEdgeMaterial.SetVector("_SelectPositionWorld", selectPositionWorld);
                inactiveEdgeMaterial.SetFloat("_SelectRadius", InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS);
                for (int i = 0; i < edgeMeshes.Count; i++)
                {
                    Graphics.DrawMesh(edgeMeshes[i].mesh, worldSpace.modelToWorld, inactiveEdgeMaterial,
                      MeshWithMaterialRenderer.DEFAULT_LAYER);
                }
            }
        }

        /// <summary>
        /// Renders the inactive vertices.
        /// </summary>
        public void RenderPoints()
        {
            if (showPoints && pointMeshes.Count > 0)
            {
                float scaleFactor = GetVertScaleFactor(worldSpace);
                inactivePointMaterial.SetFloat("_PointSphereRadius", scaleFactor);
                inactivePointMaterial.SetVector("_SelectPositionWorld", selectPositionWorld);
                inactivePointMaterial.SetFloat("_SelectRadius", InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS);
                for (int i = 0; i < pointMeshes.Count; i++)
                {
                    Graphics.DrawMesh(pointMeshes[i].mesh, worldSpace.modelToWorld, inactivePointMaterial,
                      MeshWithMaterialRenderer.DEFAULT_LAYER);
                }
            }
        }

    }

}
