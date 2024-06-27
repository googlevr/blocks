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
using System.Runtime.CompilerServices;
using com.google.apps.peltzer.client.model.util;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   Holder for calculated information about a Face.
    /// </summary>
    public struct FaceInfo
    {
        internal Bounds bounds;
        internal Plane plane;
        internal Vector3 baryCenter;
        internal List<Vector3> border;
    }

    /// <summary>
    ///   Holder for calculated information about an edge.
    /// </summary>
    public struct EdgeInfo
    {
        internal Bounds bounds;
        internal float length;
        internal Vector3 edgeStart;
        internal Vector3 edgeVector;
    }

    /// <summary>
    ///   Holder for an object along with a distance.  Makes it easy to determine distance once for
    ///   a set of candidates and then sort on that.
    /// </summary>
    public struct DistancePair<T>
    {
        public float distance;
        public T value;

        internal DistancePair(float distance, T value)
        {
            this.distance = distance;
            this.value = value;
        }
    }

    /// <summary>
    ///   Comparator for sorting DistancePairs.
    /// </summary>
    internal class DistancePairComparer<T> : IComparer<DistancePair<T>>
    {
        public int Compare(DistancePair<T> left, DistancePair<T> right)
        {
            return left.distance.CompareTo(right.distance);
        }
    }

    public class SpatialIndex
    {
        public const int MAX_INTERSECT_RESULTS = 100000;

        public CollisionSystem<int> meshes { get; private set; }
        private CollisionSystem<FaceKey> faces;
        private CollisionSystem<EdgeKey> edges;
        private CollisionSystem<VertexKey> vertices;
        private CollisionSystem<int> meshBounds;
        private Dictionary<FaceKey, FaceInfo> faceInfo;
        private Dictionary<EdgeKey, EdgeInfo> edgeInfo;

        // A reference to the model, which is the single point of truth as to whether an item exists, despite the fact
        // that this spatial index contains collections of meshes and other items.
        // The reason we wish to treat the model as a single point of truth is that changes to the model happen on the 
        // main thread, whereas the spatial index is updated on a background thread. The major worry is that something
        // is removed from the model, but returned from the spatial index to a tool. See bug for discussion.
        private Model model;

        /// <summary>
        /// Meshes that are declared to be invalid and pending removal. These meshes may still exist in the index
        /// but will be removed soon, so we behave as if they didn't exist. This is used for performance reasons,
        /// so that the main thread can immediately mark meshes for deletion while leaving the actual cleanup
        /// to the background task.
        /// </summary>
        private HashSet<int> condemnedMeshes = new HashSet<int>();
        private object condemnedMeshesLock = new object();  // lock this while accessing condemnedMeshes.
                                                            // IMPORTANT: never call a synchronized method of this class while holding condemnedMeshesLock.
                                                            // It might deadlock (because another thread might be holding the monitor lock and waiting for
                                                            // condemnedMeshesLock).

        public SpatialIndex(Model model, Bounds bounds)
        {
            this.model = model;
            Setup(bounds);
        }

        private void Setup(Bounds bounds)
        {
            meshes = new NativeSpatial<int>();
            faces = new NativeSpatial<FaceKey>();
            edges = new NativeSpatial<EdgeKey>();
            vertices = new NativeSpatial<VertexKey>();
            meshBounds = new NativeSpatial<int>();

            faceInfo = new Dictionary<FaceKey, FaceInfo>();
            edgeInfo = new Dictionary<EdgeKey, EdgeInfo>();
        }

        /// <summary>
        ///   Add a Mesh to the index.  Will index all faces and vertices. This method is not synchronized.  It should not
        ///   touch any indexes directly.  (Currently it calculates the values it will store and then stores them via a
        ///   synchronized method.)
        /// </summary>
        public void AddMesh(MMesh mesh)
        {
            // Calc all the face and edge info before we lock the index:
            Dictionary<FaceKey, FaceInfo> faceInfos = new Dictionary<FaceKey, FaceInfo>();
            Dictionary<EdgeKey, EdgeInfo> edgeInfos = new Dictionary<EdgeKey, EdgeInfo>();
            foreach (Face face in mesh.GetFaces())
            {
                faceInfos[new FaceKey(mesh.id, face.id)] = CalculateFaceInfo(mesh, face);
                for (int i = 0; i < face.vertexIds.Count; i++)
                {
                    int start = face.vertexIds[i];
                    int end = face.vertexIds[(i + 1) % face.vertexIds.Count];
                    // Edges will show up twice, since two faces always share an edge in reverse order.
                    // Don't do anything if it is already in edgeInfos.
                    EdgeKey edgeKey = new EdgeKey(mesh.id, start, end);
                    if (!edgeInfos.ContainsKey(edgeKey))
                    {
                        edgeInfos[edgeKey] = CalculateEdgeInfo(
                          mesh.VertexPositionInModelCoords(start), mesh.VertexPositionInModelCoords(end));
                    }
                }
            }
            // Lock the index and add everything:
            LoadMeshIntoIndex(mesh, faceInfos, edgeInfos);
        }

        /// <summary>
        /// Mark a mesh as condemned and pending deletion.
        /// </summary>
        public void CondemnMesh(int meshId)
        {
            lock (condemnedMeshesLock)
            {
                condemnedMeshes.Add(meshId);
            }
        }

        /// <summary>
        ///   Remove a mesh from the index.  Expects all the same face and vertex ids from when
        ///   the mesh was inserted.
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void RemoveMesh(MMesh mesh)
        {
            lock (condemnedMeshesLock)
            {
                condemnedMeshes.Remove(mesh.id);
            }
            if (meshBounds.HasItem(mesh.id))
            {
                meshBounds.Remove(mesh.id);
            }
            if (meshes.HasItem(mesh.id))
            {
                meshes.Remove(mesh.id);
                foreach (Face face in mesh.GetFaces())
                {
                    FaceKey faceKey = new FaceKey(mesh.id, face.id);
                    faces.Remove(faceKey);
                    faceInfo.Remove(faceKey);
                    for (int i = 0; i < face.vertexIds.Count; i++)
                    {
                        int start = face.vertexIds[i];
                        int end = face.vertexIds[(i + 1) % face.vertexIds.Count];
                        EdgeKey edgeKey = new EdgeKey(mesh.id, start, end);
                        if (edgeInfo.Remove(edgeKey))
                        {
                            edges.Remove(edgeKey);
                        }
                    }
                }
                foreach (int vertexId in mesh.GetVertexIds())
                {
                    vertices.Remove(new VertexKey(mesh.id, vertexId));
                }
            }
        }

        /// <summary>
        ///   Find the meshes closest to the given point, within the given radius.  The current implementation
        ///   just looks for the closest face (within that radius) and returns its associated mesh.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden. We will need to manually
        ///   remove them using MeshMath.RemoveKeysForMeshes()
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindMeshesClosestTo(Vector3 point, float radius, out List<DistancePair<int>> meshIds)
        {
            List<DistancePair<FaceKey>> faces;
            // We do not check here if a mesh exists in the model, as that check is carried out in FindFacesClosestTo.
            if (FindFacesClosestTo(point, radius, false, out faces))
            {
                meshIds = new List<DistancePair<int>>();
                lock (condemnedMeshesLock)
                {
                    foreach (DistancePair<FaceKey> pair in faces)
                    {
                        if (condemnedMeshes.Contains(pair.value.meshId)) continue;
                        DistancePair<int> pairInt = new DistancePair<int>(pair.distance, pair.value.meshId);
                        meshIds.Add(pairInt);
                    }
                }
                return true;
            }
            else
            {
                meshIds = new List<DistancePair<int>>();
                return false;
            }
        }

        /// <summary>
        ///   Finds the nearest mesh to a point (given in model-space), returning false if no mesh
        ///   is within the given radius.
        ///
        ///   This searches the Mesh Octree as opposed to using nearestFace as a proxy, and is intended as a
        ///   fallback when there are no nearby faces.
        ///   This method measures distance from the given point to the 'offset' of each mesh, which is a proxy for
        ///   the mesh center, though may not actually be the geometric center of the mesh.
        ///   This method only considers meshes if the point lies within the bounding box the mesh, a simple
        ///   check that may be confused by any geometry more complex than a rectangular prism.
        ///   On the plus side, it's cheap.
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindNearestMeshTo(Vector3 point, float radius, out int? nearestMesh,
          bool ignoreHiddenMeshes = false)
        {
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            nearestMesh = null;
            HashSet<int> meshIds;

            if (meshes.IntersectedBy(searchBounds, out meshIds))
            {
                float minDistance = float.MaxValue;

                lock (condemnedMeshesLock)
                {
                    foreach (int meshId in meshIds)
                    {
                        // Confirm the mesh actually still exists in the model.
                        if (!model.HasMesh(meshId) || condemnedMeshes.Contains(meshId))
                        {
                            continue;
                        }

                        if (ignoreHiddenMeshes && model.IsMeshHidden(meshId))
                        {
                            continue;
                        }

                        Bounds bounds = meshBounds.BoundsForItem(meshId);
                        if (!bounds.Contains(point))
                        {
                            continue;
                        }
                        float distanceToPoint = Vector3.Distance(bounds.center, point);
                        if (distanceToPoint < minDistance)
                        {
                            minDistance = distanceToPoint;
                            nearestMesh = meshId;
                        }
                    }
                }
            }

            return nearestMesh.HasValue;
        }

        /// <summary>
        ///   Finds the nearest mesh to a point (given in model-space), even if the origin point is not within the
        ///   nearby mesh, returning false if no mesh is within the given radius.
        ///
        ///   This searches the Mesh Octree as opposed to using nearestFace as a proxy, and is intended as a
        ///   fallback when there are no nearby faces.
        ///   This method measures distance from the given point to the 'offset' of each mesh, which is a proxy for
        ///   the mesh center, though may not actually be the geometric center of the mesh.
        ///   This method only considers meshes even if the point does not lie within the bounding box the mesh, a simple
        ///   check that may be confused by any geometry more complex than a rectangular prism.
        ///   On the plus side, it's cheap.
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindNearestMeshToNotIncludingPoint(Vector3 point, float radius, out int? nearestMesh,
          bool ignoreHiddenMeshes = false)
        {
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            nearestMesh = null;
            HashSet<int> meshIds;

            if (meshes.IntersectedBy(searchBounds, out meshIds))
            {
                float minDistance = float.MaxValue;

                lock (condemnedMeshesLock)
                {
                    foreach (int meshId in meshIds)
                    {
                        // Confirm the mesh actually still exists in the model.
                        if (!model.HasMesh(meshId) || condemnedMeshes.Contains(meshId))
                        {
                            continue;
                        }

                        if (ignoreHiddenMeshes && model.IsMeshHidden(meshId))
                        {
                            continue;
                        }

                        Bounds bounds = meshBounds.BoundsForItem(meshId);
                        float distanceToPoint = Vector3.Distance(bounds.center, point);
                        if (distanceToPoint < minDistance)
                        {
                            minDistance = distanceToPoint;
                            nearestMesh = meshId;
                        }
                    }
                }
            }

            return nearestMesh.HasValue;
        }

        /// <summary>
        ///   Finds the nearest meshes to a point ordered by nearness, which is defined as the distance from the point to
        ///   the center of the mesh's bounds in model-space, even if the origin point is
        ///   not within the nearby mesh, returning false if no mesh is within the given radius.
        ///
        ///   This searches the Mesh Octree as opposed to using nearestFace as a proxy, and is intended as a
        ///   fallback when there are no nearby faces.
        ///   This method measures distance from the given point to the 'offset' of each mesh, which is a proxy for
        ///   the mesh center, though may not actually be the geometric center of the mesh.
        ///   This method only considers meshes even if the point does not lie within the bounding box the mesh, a simple
        ///   check that may be confused by any geometry more complex than a rectangular prism.
        ///   On the plus side, it's cheap.
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindNearestMeshesToNotIncludingPoint(Vector3 point, float radius,
          out List<DistancePair<int>> nearestMeshes, bool ignoreHiddenMeshes = false)
        {
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            List<DistancePair<int>> results = new List<DistancePair<int>>();
            HashSet<int> meshIds;

            if (meshes.IntersectedBy(searchBounds, out meshIds))
            {
                lock (condemnedMeshesLock)
                {
                    foreach (int meshId in meshIds)
                    {
                        // Confirm the mesh actually still exists in the model.
                        if (!model.HasMesh(meshId) || condemnedMeshes.Contains(meshId))
                        {
                            continue;
                        }

                        if (ignoreHiddenMeshes && model.IsMeshHidden(meshId))
                        {
                            continue;
                        }

                        Bounds bounds = meshBounds.BoundsForItem(meshId);
                        float distance = Vector3.Distance(bounds.center, point);
                        results.Add(new DistancePair<int>(distance, meshId));
                    }
                }
            }
            if (results.Count > 0)
            {
                results.Sort(new DistancePairComparer<int>());
                nearestMeshes = results;
                return true;
            }
            else
            {
                nearestMeshes = results;
                return false;
            }
        }

        /// <summary>
        ///   Find the faces closest to a given point, within the given radius.  Uses a heuristic to determine
        ///   which face is closest.  That heuristic depends on the size of the face, the distance the point is
        ///   to the face's plane and the distance to the center of the face.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden. We will need to manually
        ///   remove them using MeshMath.RemoveKeysForMeshes()
        /// </summary>
        /// <param name="point">The point we are finding faces close to.</param>
        /// <param name="radius">The radius from the point we are finding faces in.</param>
        /// <param name="ignoreInFace">
        ///   Whether or not the point has to be inside the bounds of the face to be considered close.
        /// </param>
        /// <param name="closestFaces">The faces that were found to be the closest.</param>
        /// <returns>Whether there are any close faces.</returns>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindFacesClosestTo(Vector3 point, float radius, bool ignoreInFace,
          out List<DistancePair<FaceKey>> closestFaces, int limit = 100)
        {
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            List<DistancePair<FaceKey>> results = new List<DistancePair<FaceKey>>();
            HashSet<FaceKey> faceKeys;
            if (faces.IntersectedBy(searchBounds, out faceKeys, limit))
            {
                lock (condemnedMeshesLock)
                {
                    foreach (FaceKey faceKey in faceKeys)
                    {
                        // Confirm the mesh actually still exists in the model, and the face actually still exists in the mesh.
                        if (!model.HasMesh(faceKey.meshId) || condemnedMeshes.Contains(faceKey.meshId) ||
                          !model.GetMesh(faceKey.meshId).HasFace(faceKey.faceId))
                        {
                            continue;
                        }
                        FaceInfo info = faceInfo[faceKey];
                        float distanceToPlane = info.plane.GetDistanceToPoint(point);
                        if (Mathf.Abs(distanceToPlane) < radius)
                        {
                            // Add the face to the results if we don't care about the position being in the border of the face
                            // or if we do care about the position being within the border of the face and it is.
                            if (ignoreInFace || Math3d.IsInside(info.border, point - info.plane.normal * distanceToPlane))
                            {
                                results.Add(new DistancePair<FaceKey>(Mathf.Abs(distanceToPlane), faceKey));
                            }
                        }
                    }
                }
            }
            if (results.Count > 0)
            {
                results.Sort(new DistancePairComparer<FaceKey>());
                closestFaces = results;
                return true;
            }
            else
            {
                closestFaces = results;
                return false;
            }
        }

        /// <summary>
        ///   Find the meshes closest to a given point, within the given radius.  We do this by testing intersection
        ///   against faces, and then adding the meshIds of all faces to a hashSet.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden, so we manually remove those at
        ///   the end of the process.
        /// </summary>
        /// <param name="point">The point we are finding faces close to.</param>
        /// <param name="radius">The radius from the point we are finding faces in.</param>
        /// <param name="closeMeshes">The meshes that were found within the radius.</param>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void FindMeshesClosestTo(Vector3 point, float radius, out HashSet<int> closeMeshes)
        {
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            closeMeshes = new HashSet<int>();
            HashSet<FaceKey> faceKeys;
            if (faces.IntersectedBy(searchBounds, out faceKeys))
            {
                lock (condemnedMeshesLock)
                {
                    foreach (FaceKey faceKey in faceKeys)
                    {
                        if (closeMeshes.Contains(faceKey.meshId)) continue;
                        // Confirm the mesh actually still exists in the model, and the face actually still exists in the mesh.
                        if (!model.HasMesh(faceKey.meshId) || condemnedMeshes.Contains(faceKey.meshId) ||
                          !model.GetMesh(faceKey.meshId).HasFace(faceKey.faceId))
                        {
                            continue;
                        }

                        FaceInfo info = faceInfo[faceKey];
                        float distanceToPlane = info.plane.GetDistanceToPoint(point);
                        if (Mathf.Abs(distanceToPlane) < radius)
                        {
                            closeMeshes.Add(faceKey.meshId);
                        }
                    }
                }
            }
            closeMeshes.ExceptWith(model.GetHiddenMeshes());
        }

        /// <summary>
        ///   Find the meshes closest to a given point, within the given radius.  We do this by testing intersection
        ///   against faces, and then adding the meshIds of all faces to a hashSet.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden, so we manually remove those at
        ///   the end of the process.
        /// </summary>
        /// <param name="point">The point we are finding faces close to.</param>
        /// <param name="radius">The radius from the point we are finding faces in.</param>
        /// <param name="closeMeshes">The meshes that were found within the radius.</param>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void FindMeshesClosestToDirect(Vector3 point, float radius, ref HashSet<int> closeMeshes)
        {
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            meshBounds.IntersectedByPreallocated(searchBounds, ref closeMeshes);
            closeMeshes.ExceptWith(model.GetHiddenMeshes());
        }

        /// <summary>
        ///   The heuristic to determine which face is closest to a given point.  This is probably
        ///   something we want to tinker with.  For now it takes the rms of the distance to the plane
        ///   and to the center then adds a "fudge" to make larger faces seem farther away (so it'll
        ///   be easier to select small faces.)
        /// </summary>
        public static float AdjustedFaceDistance(float disToPlane, float disToCenter, float radius)
        {
            return Mathf.Sqrt(disToPlane * disToPlane + disToCenter * disToCenter) + radius * 0.001f;
        }

        /// <summary>
        ///   Find the edges closest to a given point, within the given radius.  Uses a heuristic to determine
        ///   which edge is closest.  That heuristic depends on the length of the edge, and the distance to the line
        ///   of the edge.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden. We will need to manually
        ///   remove them using MeshMath.RemoveKeysForMeshes()
        /// </summary>
        /// <param name="point">The point we are finding edges close to.</param>
        /// <param name="radius">The radius from the point we are finding edges in.</param>
        /// <param name="ignoreInEdge">
        ///   Whether or not the position has to be on the edge to be considered close.
        /// </param>
        /// <param name="closestEdges"> The edges that were found to be the closest.</param>
        /// <returns>Whether there are any close edges.</returns>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindEdgesClosestTo(Vector3 point, float radius, bool ignoreInEdge,
          out List<DistancePair<EdgeKey>> closestEdges)
        {
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            List<DistancePair<EdgeKey>> results = new List<DistancePair<EdgeKey>>();
            HashSet<EdgeKey> edgeKeys;
            if (edges.IntersectedBy(searchBounds, out edgeKeys))
            {
                lock (condemnedMeshesLock)
                {
                    foreach (EdgeKey edgeKey in edgeKeys)
                    {
                        // Confirm the mesh actually still exists in the model, and the verts actually still exist in the mesh.
                        if (!model.HasMesh(edgeKey.meshId) || condemnedMeshes.Contains(edgeKey.meshId))
                        {
                            continue;
                        }
                        MMesh mesh = model.GetMesh(edgeKey.meshId);
                        if (!mesh.HasVertex(edgeKey.vertexId1) || !mesh.HasVertex(edgeKey.vertexId2))
                        {
                            continue;
                        }
                        EdgeInfo info = edgeInfo[edgeKey];
                        // Project point onto line:
                        Vector3 linePointToPoint = point - info.edgeStart;
                        float t = Vector3.Dot(linePointToPoint, info.edgeVector);
                        if (ignoreInEdge || (t >= 0 && t <= info.length))
                        {
                            Vector3 onLine = info.edgeStart + info.edgeVector * t;
                            float distanceToLine = Vector3.Distance(onLine, point);
                            if (distanceToLine < radius)
                            {
                                results.Add(new DistancePair<EdgeKey>(distanceToLine, edgeKey));
                            }
                        }
                    }
                }
            }
            if (results.Count > 0)
            {
                results.Sort(new DistancePairComparer<EdgeKey>());
                closestEdges = results;
                return true;
            }
            else
            {
                closestEdges = new List<DistancePair<EdgeKey>>();
                return false;
            }
        }

        /// <summary>
        ///   The heuristic to determine which edge is closest to a given point.  Takes the distance to the
        ///   line of the edge and adds a penalty for longer edges.  This makes shorter edges a little easier
        ///   to select.
        /// </summary>
        private float AdjustedEdgeDistance(float disToLine, float length)
        {
            return disToLine + length * 0.001f;
        }

        /// <summary>
        ///   Find the vertex closest to a given point, within the given radius.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden. We will need to manually
        ///   remove them using MeshMath.RemoveKeysForMeshes()
        /// </summary>
        /// <param name="point">The point we are finding vertices close to.</param>
        /// <param name="radius">The radius from the point we are finding vertices in.</param>
        /// <param name="closestVertices">The vertices that were found to be the closest.</param>
        /// <returns>If there are any close vertices.</returns>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindVerticesClosestTo(Vector3 point, float radius, out List<DistancePair<VertexKey>> closestVertices)
        {
            List<DistancePair<VertexKey>> results = new List<DistancePair<VertexKey>>();
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            HashSet<VertexKey> verts;
            if (vertices.IntersectedBy(searchBounds, out verts))
            {
                lock (condemnedMeshesLock)
                {
                    foreach (VertexKey vert in verts)
                    {
                        // Confirm the mesh actually still exists in the model, and the vert actually still exists in the mesh.
                        if (!model.HasMesh(vert.meshId) || condemnedMeshes.Contains(vert.meshId) ||
                          !model.GetMesh(vert.meshId).HasVertex(vert.vertexId))
                        {
                            continue;
                        }
                        Bounds bounds = vertices.BoundsForItem(vert);
                        float distance = Vector3.Distance(point, bounds.center);
                        if (distance < radius)
                        {
                            results.Add(new DistancePair<VertexKey>(distance, vert));
                        }
                    }
                }
            }
            if (results.Count > 0)
            {
                results.Sort(new DistancePairComparer<VertexKey>());
                closestVertices = results;
                return true;
            }
            else
            {
                closestVertices = new List<DistancePair<VertexKey>>();
                return false;
            }
        }

        /// <summary>
        ///   Find the vertex closest to a given point, within the given radius.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden. We will need to manually
        ///   remove them using MeshMath.RemoveKeysForMeshes()
        /// </summary>
        /// <param name="point">The point we are finding vertices close to.</param>
        /// <param name="radius">The radius from the point we are finding vertices in.</param>
        /// <param name="closestVertices">The vertices that were found to be the closest.</param>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void FindVerticesClosestTo(Vector3 point, float radius, out HashSet<VertexKey> closestVertices)
        {
            closestVertices = new HashSet<VertexKey>();
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            HashSet<VertexKey> verts;

            if (vertices.IntersectedBy(searchBounds, out verts))
            {
                float radius2 = radius * radius;
                lock (condemnedMeshesLock)
                {
                    foreach (VertexKey vert in verts)
                    {
                        // Confirm the mesh actually still exists in the model, and the vert actually still exists in the mesh.
                        if (!model.HasMesh(vert.meshId) || condemnedMeshes.Contains(vert.meshId) ||
                          !model.GetMesh(vert.meshId).HasVertex(vert.vertexId))
                        {
                            continue;
                        }
                        Bounds bounds = vertices.BoundsForItem(vert);
                        Vector3 diff = point - bounds.center;
                        float dist2 = Vector3.Dot(diff, diff);
                        if (dist2 < radius2)
                        {
                            closestVertices.Add(vert);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///   Find the edges closest to a given point, within the given radius, using the closest distance between
        ///   the point and the line segment belonging to the edge.
        ///
        ///   The spatialIndex will return keys that belong to meshes that are hidden. We will need to manually
        ///   remove them using MeshMath.RemoveKeysForMeshes()
        /// </summary>
        /// <param name="point">The point we are finding edges close to.</param>
        /// <param name="radius">The radius from the point we are finding edges in.</param>
        /// <param name="closestEdges"> The edges that were found to be the closest.</param>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void FindEdgesClosestTo(Vector3 point, float radius, out HashSet<EdgeKey> closestEdges)
        {
            closestEdges = new HashSet<EdgeKey>();
            Bounds searchBounds = new Bounds(point, Vector3.one * radius * 2);
            HashSet<EdgeKey> edgeKeys;
            if (edges.IntersectedBy(searchBounds, out edgeKeys))
            {
                float radius2 = radius * radius;
                lock (condemnedMeshesLock)
                {
                    foreach (EdgeKey edgeKey in edgeKeys)
                    {
                        // Confirm the mesh actually still exists in the model, and the verts actually still exist in the mesh.
                        if (!model.HasMesh(edgeKey.meshId) || condemnedMeshes.Contains(edgeKey.meshId))
                        {
                            continue;
                        }
                        MMesh mesh = model.GetMesh(edgeKey.meshId);
                        if (!mesh.HasVertex(edgeKey.vertexId1) || !mesh.HasVertex(edgeKey.vertexId2))
                        {
                            continue;
                        }
                        EdgeInfo info = edgeInfo[edgeKey];
                        // Project point onto line:
                        Vector3 linePointToPoint = point - info.edgeStart;
                        float t = Vector3.Dot(linePointToPoint, info.edgeVector);
                        t = Mathf.Clamp(t, 0.0f, info.length);
                        Vector3 onLine = info.edgeStart + info.edgeVector * t;
                        Vector3 diff = point - onLine;
                        float dist2 = Vector3.Dot(diff, diff);
                        if (dist2 < radius2)
                        {
                            closestEdges.Add(edgeKey);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///   Finds meshes which intersect the given bounds.
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool FindIntersectingMeshes(Bounds boundingBox, out HashSet<int> meshIds)
        {
            bool success = meshBounds.IntersectedBy(boundingBox, out meshIds);
            if (success)
            {
                // Confirm the meshes actually still exist in the model.
                List<int> missingOrCondemnedMeshIds = new List<int>();
                lock (condemnedMeshesLock)
                {
                    foreach (int meshId in meshIds)
                    {
                        if (!model.HasMesh(meshId) || condemnedMeshes.Contains(meshId))
                        {
                            missingOrCondemnedMeshIds.Add(meshId);
                        }
                    }
                }
                foreach (int meshId in missingOrCondemnedMeshIds)
                {
                    meshIds.Remove(meshId);
                }
            }
            return success;
        }

        /// <summary>
        ///   Resets the entire state of the Spatial Index, using the given bounds.
        /// </summary>
        public void Reset(Bounds bounds)
        {
            Setup(bounds);
        }

        public static FaceInfo CalculateFaceInfo(MMesh mesh, Face face)
        {
            Vector3 center = Vector3.zero;
            Vector3 firstPt = mesh.VertexPositionInModelCoords(face.vertexIds[0]);
            Bounds bounds = new Bounds(firstPt, Vector3.zero);
            List<Vector3> coords = new List<Vector3>();

            foreach (int vertId in face.vertexIds)
            {
                Vector3 inModelCoords = mesh.VertexPositionInModelCoords(vertId);
                center += inModelCoords;
                bounds.Encapsulate(inModelCoords);
                coords.Add(inModelCoords);
            }
            center /= face.vertexIds.Count;

            FaceInfo faceInfo = new FaceInfo();
            faceInfo.baryCenter = center;
            faceInfo.bounds = bounds;
            faceInfo.plane = new Plane(MeshMath.CalculateNormal(coords), center);
            faceInfo.border = coords;

            return faceInfo;
        }

        private EdgeInfo CalculateEdgeInfo(Vector3 start, Vector3 end)
        {
            EdgeInfo edgeInfo = new EdgeInfo();
            Bounds bounds = new Bounds(start, Vector3.zero);
            bounds.Encapsulate(end);
            edgeInfo.bounds = bounds;
            edgeInfo.length = Vector3.Distance(start, end);
            edgeInfo.edgeStart = start;
            edgeInfo.edgeVector = (end - start).normalized;
            return edgeInfo;
        }

        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public FaceInfo GetFaceInfo(FaceKey key)
        {
            return faceInfo[key];
        }

        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool TryGetFaceInfo(FaceKey key, out FaceInfo info)
        {
            return faceInfo.TryGetValue(key, out info);
        }

        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        private void LoadMeshIntoIndex(MMesh mesh,
            Dictionary<FaceKey, FaceInfo> faceInfos, Dictionary<EdgeKey, EdgeInfo> edgeInfos)
        {
            lock (condemnedMeshesLock)
            {
                condemnedMeshes.Remove(mesh.id);
            }
            meshes.Add(mesh.id, mesh.bounds);
            meshBounds.Add(mesh.id, mesh.bounds);
            foreach (KeyValuePair<FaceKey, FaceInfo> pair in faceInfos)
            {
                faces.Add(pair.Key, pair.Value.bounds);
                faceInfo[pair.Key] = pair.Value;
            }
            foreach (KeyValuePair<EdgeKey, EdgeInfo> pair in edgeInfos)
            {
                edges.Add(pair.Key, pair.Value.bounds);
                edgeInfo[pair.Key] = pair.Value;
            }
            foreach (int vertId in mesh.GetVertexIds())
            {
                vertices.Add(new VertexKey(mesh.id, vertId),
                  new Bounds(mesh.VertexPositionInModelCoords(vertId), Vector3.zero));
            }
        }

        // Test only.
        public SpatialIndex(Bounds bounds)
        {
            Setup(bounds);
        }
    }
}
