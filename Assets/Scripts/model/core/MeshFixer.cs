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
using System.Collections.ObjectModel;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{

    public class MeshFixer
    {
        /// <summary>
        ///   Modify a mesh by moving the vertices as supplied, then tries to fix it as much as is possible.
        /// </summary>
        /// <param name="originalMesh">A copy of the original mesh.</param>
        /// <param name="meshCopy">The mesh to update.</param>
        /// <param name="updatedVerts">The new vertex positions.</param>
        /// <param name="forPreview">Whether this is for a final command, or just for an in-progress preview.</param>
        /// <returns>Whether mesh geometry changed.</returns>
        public static bool MoveVerticesAndMutateMeshAndFix(
            MMesh originalMesh, MMesh meshCopy, IEnumerable<Vertex> updatedVerts, bool forPreview)
        {
            HashSet<int> updatedVertIds = new HashSet<int>();
            MMesh.GeometryOperation mutateOperation = meshCopy.StartOperation();
            foreach (Vertex vert in updatedVerts)
            {
                updatedVertIds.Add(vert.id);
                mutateOperation.ModifyVertex(vert);
            }
            mutateOperation.Commit();
            if (forPreview)
            {
                return SplitNonCoplanarFaces(originalMesh, meshCopy, updatedVertIds);
            }
            else
            {
                meshCopy.RecalcBounds();
                return FixMutatedMesh(originalMesh, meshCopy, updatedVertIds, /* splitNonCoplanarFaces */ true,
                  /* mergeAdjacentCoplanarFaces */ false);
            }
        }

        /// <summary>
        /// Fixes a mesh which has been mutated. Tries to "fix" the mesh as much as possible
        /// to reflect the user's intentions.  It will join vertices that are at the same location.  It
        /// will remove faces and edges that no longer make sense.  And it will split faces in cases
        /// where the move would cause them to be non-coplanar.
        ///
        /// The method refines the mesh through a set of steps:
        ///
        ///   1) Join vertices that are in the same place.
        ///   2) Remove zero-length segments (that may have been caused by joined vertices).
        ///   3) Remove zero-area segments (segments that are really a line).
        ///   4) Remove faces that are no longer valid (due to above).
        ///   5) Split non-coplanar faces -- as much as possible, at the vertices that moved.
        ///
        /// </summary>
        /// <param name="originalMesh">The mesh prior to any alterations.</param>
        /// <param name="alteredMesh">The altered mesh.</param>
        /// <param name="updatedVertIds">The vertices that have been updated.</param>
        /// <param name="splitNonCoplanarFaces">Whether to split faces that are non coplanar.</param>
        /// <param name="mergeAdjacentCoplanarFaces">Whether to coalesce adjacent coplanar faces.</param>
        /// <returns>Whether mesh geometry changed.</returns>
        public static bool FixMutatedMesh(MMesh originalMesh, MMesh alteredMesh, HashSet<int> updatedVertIds,
          bool splitNonCoplanarFaces, bool mergeAdjacentCoplanarFaces)
        {
            bool mutated = false;

            mutated |= JoinDuplicateVertices(alteredMesh, updatedVertIds);

            if (splitNonCoplanarFaces && !Features.allowNoncoplanarFaces)
            {
                // Face split needs to be first since it results in duplicate vertices when patching holes.
                mutated |= SplitNonCoplanarFaces(originalMesh, alteredMesh, updatedVertIds);
            }

            // The next three operations are all scoped to the set of faces that may have been changed.
            // Generate this once here to avoid duplicate work.
            HashSet<int> potentiallyChangedFaces = new HashSet<int>();
            foreach (int vertId in updatedVertIds)
            {
                potentiallyChangedFaces.UnionWith(alteredMesh.reverseTable[vertId]);
            }

            // Similarly, these methods rely on duplicate vertices being merged.
            mutated |= RemoveZeroLengthSegments(alteredMesh, potentiallyChangedFaces);
            mutated |= RemoveZeroAreaSegments(alteredMesh, potentiallyChangedFaces);
            mutated |= RemoveInvalidFacesAndHoles(alteredMesh, potentiallyChangedFaces);

            return mutated;
        }

        /// <summary>
        ///   Given a set of vertices that have been moved, check to see if they are
        ///   at the same location as another existing vertex with which they share a face.  If so, join the two
        ///   vertices by replacing all of the ids of the second one with the first.
        ///
        ///   This method can leave faces in an invalid state.  The faces should be
        ///   fixed up afterwards.
        ///
        ///   If duplicate vertices are found where BOTH are in updatedVertIds (the vertices being changed),
        ///   then they will not be joined, as that would break the universe (a lot of logic that uses this method
        ///   can't deal with us deleting vertices that are being moved). In that case, the fact that they are
        ///   duplicates will be reported via the 'unresolvedDuplicateVertices' out param.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="updatedVertIds"></param>
        /// <returns>Whether mesh geometry changed.</returns>
        public static bool JoinDuplicateVertices(MMesh mesh, HashSet<int> updatedVertIds)
        {

            bool mutated = false;

            List<int> newlyUpdatedVertexIds = new List<int>();
            List<int> deletedVertexIds = new List<int>();

            // Iterates through updatedVertIds to find duplicates. If a duplicate is found, then
            // all references to vert.id are moved to updatedVertId, and then vert.id is
            // removed from the list of the mesh's vertices.
            //
            // Merging a vert with a vertex it doesn't share a face with can potentially create bad geometry - so we only
            // check the vertex against vertices it shares a face with.
            foreach (int updatedVertId in updatedVertIds)
            {
                Vector3 vertLoc = mesh.VertexPositionInMeshCoords(updatedVertId);
                HashSet<int> vertsToCheck = new HashSet<int>();
                HashSet<int> facesForVert = mesh.reverseTable[updatedVertId];

                foreach (int faceId in facesForVert)
                {
                    Face face;
                    if (!mesh.TryGetFace(faceId, out face))
                    {
                        continue;
                    };
                    vertsToCheck.UnionWith(face.vertexIds);
                }

                foreach (int vertIndex in vertsToCheck)
                {
                    Vector3 vertexToCheckPositionMeshCoords = mesh.VertexPositionInMeshCoords(vertIndex);
                    bool areCloseEnough = (Vector3.Distance(vertLoc, vertexToCheckPositionMeshCoords) < Math3d.MERGE_DISTANCE);
                    if (!areCloseEnough)
                    {
                        continue;
                    }

                    // Only attempt to merge vertices that are not being moved by the user (merging vertices that are being
                    // actively moved would break things)
                    if (!updatedVertIds.Contains(vertIndex))
                    {
                        MMesh.GeometryOperation joinOperation = mesh.StartOperation();
                        JoinVerts(joinOperation, vertIndex, updatedVertId);
                        joinOperation.DeleteVertex(vertIndex);
                        deletedVertexIds.Add(vertIndex);
                        joinOperation.Commit();
                        mutated = true;
                    }
                }
            }

            foreach (int newlyUpdatedVertexId in newlyUpdatedVertexIds)
            {
                updatedVertIds.Add(newlyUpdatedVertexId);
            }

            return mutated;
        }

        /// <summary>
        ///   Replace all instances of a given vertex id with another.
        /// </summary>
        /// <param name="id">The vertex id to replace</param>
        /// <param name="replaceId">The vertex id to replace it with</param>
        private static void JoinVerts(MMesh.GeometryOperation operation, int id, int replaceId)
        {
            HashSet<int> facesWithVert = operation.GetMesh().reverseTable[id];
            foreach (int faceId in facesWithVert)
            {
                Face face = operation.GetCurrentFace(faceId);
                int idx = face.vertexIds.IndexOf(id);

                // Still not sure how this case comes up, but it's possible to generate it via actions like spamming
                // the extrude tool.
                if (idx != -1)
                {
                    // Replace the vertex id.  Normals should be the same.
                    Face updatedFace = new Face(
                      face.id, ReplaceAt(face.vertexIds, idx, replaceId), face.normal, face.properties);
                    operation.ModifyFace(updatedFace);
                }

            }
            operation.DeleteVertex(id);
        }

        private static ReadOnlyCollection<T> ReplaceAt<T>(ReadOnlyCollection<T> collection, int idx, T newVal)
        {
            List<T> copy = new List<T>(collection);
            copy[idx] = newVal;
            return copy.AsReadOnly();
        }

        /// <summary>
        ///   For all faces in the mesh, check to see if any segments are zero-length.  We assume any vertices that
        ///   occupy the same place have the same id.  So it boils down to replacing segments where both endpoints
        ///   are the same vertex.
        /// </summary>
        /// <returns>Whether mesh geometry changed.</returns>
        public static bool RemoveZeroLengthSegments(MMesh mesh, HashSet<int> potentiallyChangedFaces)
        {
            MMesh.GeometryOperation segmentReplaceOperation = mesh.StartOperation();
            bool mutated = false;
            foreach (int faceId in potentiallyChangedFaces)
            {
                Face face = segmentReplaceOperation.GetCurrentFace(faceId);
                bool changed;
                do
                {
                    changed = false;
                    for (int i = 0; i < face.vertexIds.Count; i++)
                    {
                        if (face.vertexIds[i] == face.vertexIds[(i + 1) % face.vertexIds.Count])
                        {
                            face = new Face(
                              face.id, RemoveAt(face.vertexIds, i), face.normal, face.properties);
                            changed = true;
                            break;
                        }
                    }

                } while (changed);
                segmentReplaceOperation.ModifyFace(face);
                mutated = true;
            }
            // As long as the face was coplanar, removing a segment won't change the normal
            segmentReplaceOperation.CommitWithoutRecalculation();
            return mutated;
        }

        /// <summary>
        ///   Remove segments that have zero-area.  In this case we look for segments that have the same vertex
        ///   with one vertex in between -- which is a line.  In this case, collapse the three segments down
        ///   into one (the one that is duplicated).
        /// </summary>
        /// <returns>Whether mesh geometry changed.</returns>
        public static bool RemoveZeroAreaSegments(MMesh mesh, HashSet<int> potentiallyChangedFaces)
        {
            MMesh.GeometryOperation zeroAreaOperation = mesh.StartOperation();
            bool mutated = false;
            foreach (int faceId in potentiallyChangedFaces)
            {
                Face face = zeroAreaOperation.GetCurrentFace(faceId);
                bool changed;
                do
                {
                    changed = false;
                    if (face.vertexIds.Count < 3)
                    {
                        // Need at least three vertices.
                        continue;
                    }

                    for (int i = 0; i < face.vertexIds.Count; i++)
                    {
                        if (face.vertexIds[i] == face.vertexIds[(i + 2) % face.vertexIds.Count])
                        {
                            face = new Face(face.id, RemoveTwoCyclicallyAfter(face.vertexIds, i),
                              face.normal, face.properties);
                            changed = true;
                            break;
                        }
                    }
                } while (changed);
                zeroAreaOperation.ModifyFace(face);
                mutated = true;
            }
            // As long as the face was already coplanar, removing a segment won't change the normal
            zeroAreaOperation.CommitWithoutRecalculation();
            return mutated;
        }

        private static ReadOnlyCollection<T> RemoveAt<T>(ReadOnlyCollection<T> collection, int idx)
        {
            List<T> copy = new List<T>(collection);
            copy.RemoveAt(idx);
            return copy.AsReadOnly();
        }

        private static ReadOnlyCollection<T> RemoveTwoCyclicallyAfter<T>(ReadOnlyCollection<T> collection, int idx)
        {
            List<T> copy = new List<T>(collection);
            int idx1 = (idx + 1) % collection.Count;
            int idx2 = (idx + 2) % collection.Count;

            // If we didn't loop around between 1 and 2, decrement idx2 to account for idx1 being removed.
            if (idx2 > idx1)
            {
                idx2--;
            }

            copy.RemoveAt(idx1);
            copy.RemoveAt(idx2);
            return copy.AsReadOnly();
        }

        /// <summary>
        ///   Remove any invalid faces.  This assumes duplicate and zero-area segments have been removed.  So it
        ///   looks for any faces with less than three vertices and removes them.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns>Whether mesh geometry changed.</returns>
        public static bool RemoveInvalidFacesAndHoles(MMesh mesh, HashSet<int> potentiallyChangedFaces)
        {
            bool mutated = false;
            MMesh.GeometryOperation removeInvalidOperation = mesh.StartOperation();
            foreach (int faceId in potentiallyChangedFaces)
            {
                Face face = removeInvalidOperation.GetCurrentFace(faceId);
                if (face.vertexIds.Count < 3)
                {
                    removeInvalidOperation.DeleteFace(face.id);
                    mutated = true;
                }
            }
            removeInvalidOperation.CommitWithoutRecalculation();
            return mutated;
        }

        /// <summary>
        ///   Split faces that are not coplanar.  Ideally, we only want to split at places where the vertices have been
        ///   moved.  And we should split into as few faces as possible.  Right now, we split at the point of any moved
        ///   vertex wich means we might insert more faces than needed (but won't update parts of faces that weren't
        ///   moved.)
        /// </summary>
        /// <param name="originalMesh">The original mesh, needed for hole patching.</param>
        /// <param name="newMesh">The new mesh, which will be mutated.</param>
        /// <param name="updatedVertIds">The set of vertices moved.</param>
        /// <returns>Whether mesh geometry changed.</returns>
        public static bool SplitNonCoplanarFaces(MMesh originalMesh, MMesh newMesh, HashSet<int> updatedVertIds)
        {
            // Generate a map of vertex ids to list of ids of faces that contain them.

            bool mutated = false;
            MMesh.GeometryOperation splitOperation = newMesh.StartOperation();
            foreach (int vertId in updatedVertIds)
            {
                foreach (int faceId in newMesh.reverseTable[vertId])
                {
                    Face face;
                    // Note: some faces may not exist in the new mesh because they were deleted as a result of merges.
                    if (splitOperation.TryGetCurrentFace(faceId, out face))
                    {
                        mutated |= MeshUtil.SplitFaceIfNeeded(splitOperation, face, vertId);
                    }
                }
            }
            splitOperation.Commit();

            return mutated;
        }
    }
}
