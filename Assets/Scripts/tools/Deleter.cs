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
using com.google.apps.peltzer.client.model.core;
using UnityEngine;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.tools
{
    /// <summary>
    ///   Tool which handles deletion of meshes.
    /// </summary>
    public class Deleter : MonoBehaviour
    {
        public ControllerMain controllerMain;
        private PeltzerController peltzerController;
        private Model model;
        private Selector selector;
        private AudioLibrary audioLibrary;

        /// <summary>
        /// Whether we are currently deleting all hovered objects.
        /// </summary>
        public bool isDeleting { get; private set; }
        /// <summary>
        /// The set of meshes to delete when this deletion command finishes.
        /// </summary>
        private HashSet<int> meshIdsToDelete = new HashSet<int>();
        /// <summary>
        /// When we last made a noise and buzzed because of a deletion.
        /// </summary>
        private float timeLastDeletionFeedbackPlayed;
        /// <summary>
        /// Leave some time between playing deletion feedback.
        /// </summary>
        private const float INTERVAL_BETWEEN_DELETION_FEEDBACKS = 0.5f;
        /// <summary>
        /// Whether we have shown the snap tooltip for this tool yet. (Show only once because there are no direct
        /// snapping behaviors for Painter and Deleter).
        /// </summary>
        private bool snapTooltipShown = false;

        /// <summary>
        ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
        ///   As such, this setup method must be called before the tool is used for it to have a valid state.
        /// </summary>
        public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController,
          Selector selector, AudioLibrary audioLibrary)
        {
            this.model = model;
            this.controllerMain = controllerMain;
            this.peltzerController = peltzerController;
            this.selector = selector;
            this.audioLibrary = audioLibrary;
            controllerMain.ControllerActionHandler += ControllerEventHandler;
        }

        /// <summary>
        /// If we are in delete mode, try and delete all hovered meshes.
        /// </summary>
        public void Update()
        {
            if (!PeltzerController.AcquireIfNecessary(ref peltzerController) ||
                !(peltzerController.mode == ControllerMode.delete || peltzerController.mode == ControllerMode.deletePart))
            {
                return;
            }

            if (peltzerController.mode == ControllerMode.deletePart)
            {
                selector.UpdateInactive(Selector.FACES_EDGES_AND_VERTICES);
                selector.SelectAtPosition(peltzerController.LastPositionModel, Selector.FACES_EDGES_AND_VERTICES);
            }
            else
            {
                // Update the position of the selector even if we aren't deleting yet so the selector can detect which meshes to
                // delete. If we aren't deleting yet we want to hide meshes and show their highlights.
                selector.SelectMeshAtPosition(peltzerController.LastPositionModel, Selector.MESHES_ONLY);

                foreach (int meshId in selector.hoverMeshes)
                {
                    PeltzerMain.Instance.highlightUtils.SetMeshStyleToDelete(meshId);
                }

                if (!isDeleting || selector.hoverMeshes.Count == 0)
                {
                    return;
                }

                // Stop rendering each hovered mesh, and mark it for deletion.
                int[] hoveredKeys = new int[selector.hoverMeshes.Count];
                selector.hoverMeshes.CopyTo(hoveredKeys, 0);
                foreach (int meshId in hoveredKeys)
                {
                    if (meshIdsToDelete.Add(meshId))
                    {
                        model.MarkMeshForDeletion(meshId);
                        PeltzerMain.Instance.highlightUtils.TurnOffMesh(meshId);
                        if (Time.time - timeLastDeletionFeedbackPlayed > INTERVAL_BETWEEN_DELETION_FEEDBACKS)
                        {
                            timeLastDeletionFeedbackPlayed = Time.time;
                            audioLibrary.PlayClip(audioLibrary.deleteSound);
                            peltzerController.TriggerHapticFeedback();
                        }
                    }
                }
            }
        }

        /// <summary>
        ///   Whether this matches the pattern of a 'start deleting' event.
        /// </summary>
        /// <param name="args">The controller event arguments.</param>
        /// <returns>True if this is a start deleting event, false otherwise.</returns>
        private bool IsStartDeletingEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.DOWN;
        }

        /// <summary>
        ///   Whether this matches the pattern of a 'stop deleting' event.
        /// </summary>
        /// <param name="args">The controller event arguments.</param>
        /// <returns>True if this is a stop deleting event, false otherwise.</returns>
        private bool IsFinishDeletingEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.UP;
        }

        /// <summary>
        ///   An event handler that listens for controller input and delegates accordingly.
        /// </summary>
        /// <param name="sender">The sender of the controller event.</param>
        /// <param name="args">The controller event arguments.</param>
        private void ControllerEventHandler(object sender, ControllerEventArgs args)
        {
            if (peltzerController.mode == ControllerMode.delete)
            {
                if (IsStartDeletingEvent(args))
                {
                    StartDeleting();
                }
                else if (IsFinishDeletingEvent(args))
                {
                    FinishDeleting();
                }
                else if (IsSetSnapTriggerTooltipEvent(args) && !snapTooltipShown)
                {
                    // Show tool tip about the snap trigger.
                    PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
                    snapTooltipShown = true;
                }
            }
            else if (peltzerController.mode == ControllerMode.deletePart)
            {
                if (IsStartDeletingEvent(args))
                {
                    DeleteAPart();
                }
            }
        }

        private void DeleteAPart()
        {
            if (selector.hoverFace != null)
            {
                DeleteFace(selector.hoverFace);
            }
            else if (selector.hoverVertex != null)
            {
                DeleteVertex(selector.hoverVertex);
            }
            else if (selector.hoverEdge != null)
            {
                DeleteEdge(selector.hoverEdge);
            }
        }

        private void DeleteVertex(VertexKey vertexKey)
        {
            MMesh mesh = model.GetMesh(vertexKey.meshId).Clone();
            MMesh.GeometryOperation deleteVertOp = mesh.StartOperation();

            // Keep track of the starting vert of each face's list of retained verts.  We use this to determine which order
            // to join the lists in.
            Dictionary<int, int> startVertToFace = new Dictionary<int, int>();
            Dictionary<int, List<int>> faceToRetainedVerts = new Dictionary<int, List<int>>();

            int nextFaceId = -1;
            foreach (int faceId in mesh.reverseTable[vertexKey.vertexId])
            {
                if (nextFaceId == -1)
                {
                    nextFaceId = faceId;
                }
                Face f = mesh.GetFace(faceId);
                for (int i = 0; i < f.vertexIds.Count; i++)
                {
                    if (f.vertexIds[i] == vertexKey.vertexId)
                    {
                        List<int> retainedVerts = new List<int>();
                        int startIndex = (i + 1) % f.vertexIds.Count;
                        startVertToFace[f.vertexIds[startIndex]] = faceId;
                        retainedVerts.Add(f.vertexIds[startIndex]);
                        while (f.vertexIds[(startIndex + 1) % f.vertexIds.Count] != vertexKey.vertexId)
                        {
                            startIndex = (startIndex + 1) % f.vertexIds.Count;
                            retainedVerts.Add(f.vertexIds[startIndex]);
                        }
                        faceToRetainedVerts[faceId] = retainedVerts;
                    }
                }
            }

            List<int> newFaceVertexIds = new List<int>();
            HashSet<int> faces = new HashSet<int>(mesh.reverseTable[vertexKey.vertexId]);

            while (faces.Count > 0)
            {
                List<int> retainedVerts = faceToRetainedVerts[nextFaceId];
                newFaceVertexIds.AddRange(retainedVerts);
                faces.Remove(nextFaceId);
                deleteVertOp.DeleteFace(nextFaceId);
                nextFaceId = startVertToFace[retainedVerts[retainedVerts.Count - 1]];
            }

            deleteVertOp.DeleteVertex(vertexKey.vertexId);
            deleteVertOp.AddFace(newFaceVertexIds, mesh.GetFace(nextFaceId).properties);
            deleteVertOp.Commit();

            model.ApplyCommand(new ReplaceMeshCommand(mesh.id, mesh));
        }

        private int FindLastEdgeVertexInFace(EdgeKey edge, Face face)
        {
            int face1EdgeKey1Index = -1;
            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                if (face.vertexIds[i] == edge.vertexId1)
                {
                    if (face.vertexIds[(i + 1) % face.vertexIds.Count] == edge.vertexId2)
                    {
                        return (i + 1) % face.vertexIds.Count;
                    }
                    else
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private void DeleteEdge(EdgeKey edgeKey)
        {
            // To delete an edge we will:
            // 1) Find the two faces that are incident to the edge (we can guarantee there will be exactly two*)
            // 1a) Unless the edge is intruding within a single face.  In which case we deal with the special case by removing
            //    the intruding edge.
            // 2) In each of the two faces, find where the pair of vertices specified by the edgekey is within its vertex
            //    ids. Store the index of the second one of these to occur.
            // 3) Create a new vertex list for the new face we need to replace the old ones with.
            // 4) For each adjacent face add the vertexid at the index previously stored (which is one of the ones in the
            //    edgekey) and then from that index continue adding vertex ids until we hit another vert in the edgekey.  That
            //    vertex is NOT added.
            // 5) There's now a vertex list for the new face, so we add this along with the face properties from one of the 
            //    two adjacent faces (choosing arbitrarily).

            MMesh mesh = model.GetMesh(edgeKey.meshId).Clone();

            // Step 1: Find the two faces incident to the edge.
            Face face1 = null;
            Face face2 = null;
            FindIncidentFaces(edgeKey, out face1, out face2);
            if (face1 != null && face2 == null)
            {
                //Special case - deleting an internal edge to the face. Delete the vertex that doesn't border other verts in the
                //face - this will be shown in the vert list by a repeated vert in the face ABCDCE.  To fix this, we delete the
                //DC portion of the sequence resulting in ABCE.
                //We do this by iterating through our vert list looking for a vert that is in the edgekey and has edgekey verts
                //on either side - in our example the D.  Then we construct a new face starting with the next element, but cut
                //it off two vertices earlier - so CEAB/
                MMesh.GeometryOperation deleteInternalEdgeOperation = mesh.StartOperation();
                int vertCount = face1.vertexIds.Count;
                // (x - 1) % count doesn't work, but (x + count - 1) % count is mathematically equivalent
                int modulusMinusOne = vertCount - 1;
                int startVert = -1;
                // Find the point 
                for (int i = 0; i < vertCount; i++)
                {
                    if (edgeKey.ContainsVertex(face1.vertexIds[i]))
                    {
                        if (edgeKey.ContainsVertex(face1.vertexIds[(i + 1) % vertCount])
                          && edgeKey.ContainsVertex(face1.vertexIds[(i + modulusMinusOne) % vertCount]))
                        {
                            startVert = (i + 1) % vertCount;
                        }
                    }
                }
                List<int> replacementFaceVertIds = new List<int>();
                for (int i = 0; i < vertCount - 2; i++)
                {
                    replacementFaceVertIds.Add(face1.vertexIds[(startVert + i) % vertCount]);
                }
                deleteInternalEdgeOperation.DeleteFace(face1.id);
                deleteInternalEdgeOperation.AddFace(replacementFaceVertIds, face1.properties);
                deleteInternalEdgeOperation.Commit();

                MMesh.GeometryOperation cleanupOp = mesh.StartOperation();
                if (mesh.reverseTable[edgeKey.vertexId1].Count == 0)
                {
                    cleanupOp.DeleteVertex(edgeKey.vertexId1);
                }
                if (mesh.reverseTable[edgeKey.vertexId2].Count == 0)
                {
                    cleanupOp.DeleteVertex(edgeKey.vertexId2);
                }
                cleanupOp.Commit();
                if (MeshValidator.IsValidMesh(mesh, new HashSet<int>(replacementFaceVertIds)))
                {
                    model.ApplyCommand(new ReplaceMeshCommand(mesh.id, mesh));
                }
                return;
            }

            MMesh.GeometryOperation edgeDeletionOperation = mesh.StartOperation();
            edgeDeletionOperation.DeleteFace(face1.id);
            edgeDeletionOperation.DeleteFace(face2.id);

            int face1EdgeKey1Index = FindLastEdgeVertexInFace(edgeKey, face1);
            if (face1EdgeKey1Index == -1) return;
            int face2EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face2);
            if (face2EdgeKeyIndex == -1) return;

            List<int> vertexIds = new List<int>();
            vertexIds.Add(face1.vertexIds[face1EdgeKey1Index]);
            while (!edgeKey.ContainsVertex(face1.vertexIds[(face1EdgeKey1Index + 1) % face1.vertexIds.Count]))
            {
                face1EdgeKey1Index = (face1EdgeKey1Index + 1) % face1.vertexIds.Count;
                vertexIds.Add(face1.vertexIds[face1EdgeKey1Index]);
            }
            vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            while (!edgeKey.ContainsVertex(face2.vertexIds[(face2EdgeKeyIndex + 1) % face2.vertexIds.Count]))
            {
                face2EdgeKeyIndex = (face2EdgeKeyIndex + 1) % face2.vertexIds.Count;
                vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            }

            edgeDeletionOperation.AddFace(vertexIds, face1.properties);
            edgeDeletionOperation.Commit();
            if (MeshValidator.IsValidMesh(mesh, new HashSet<int>(vertexIds)))
            {
                model.ApplyCommand(new ReplaceMeshCommand(mesh.id, mesh));
            }
            return;
        }

        // Finds the two faces incident to a given edge.
        private void FindIncidentFaces(EdgeKey edgeKey, out Face face1, out Face face2)
        {
            MMesh mesh = model.GetMesh(edgeKey.meshId);
            face1 = null;
            face2 = null;
            foreach (Face face in mesh.GetFaces())
            {
                if (face.vertexIds.Contains(edgeKey.vertexId1) && face.vertexIds.Contains(edgeKey.vertexId2))
                { // Could optimise this to be one pass
                    if (face1 == null)
                    {
                        face1 = face;
                    }
                    else
                    {
                        face2 = face;
                        return;
                    }
                }
            }
        }

        private void DeleteFace(FaceKey faceKey)
        {
            MMesh mesh = model.GetMesh(faceKey.meshId).Clone();
            MMesh.GeometryOperation deleteFaceOp = mesh.StartOperation();
            Face faceToDelete = mesh.GetFace(faceKey.faceId);

            Vector3 avgLogMeshSpace = Vector3.zero;
            foreach (int vertexId in faceToDelete.vertexIds)
            {
                avgLogMeshSpace += mesh.VertexPositionInMeshCoords(vertexId);
            }
            avgLogMeshSpace /= faceToDelete.vertexIds.Count;

            Vertex mergedVert = deleteFaceOp.AddVertexMeshSpace(avgLogMeshSpace);

            List<int> facesToDelete = new List<int>();
            facesToDelete.Add(faceToDelete.id);
            List<Face> facesToAdd = new List<Face>();

            foreach (Face f in mesh.GetFaces())
            {
                bool found = false;
                foreach (int vertexId in faceToDelete.vertexIds)
                {
                    if (f.vertexIds.Contains(vertexId))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) continue;


                facesToDelete.Add(f.id);

                List<int> verts = new List<int>();
                List<Vector3> vertLocs = new List<Vector3>();

                bool added = false;
                foreach (int vertexId in f.vertexIds)
                {
                    bool found2 = false;
                    foreach (int vertexId2 in faceToDelete.vertexIds)
                    {
                        if (vertexId == vertexId2)
                        {
                            found2 = true;
                            break;
                        }
                    }

                    if (!found2)
                    {
                        verts.Add(vertexId);
                        vertLocs.Add(deleteFaceOp.GetCurrentVertexPositionMeshSpace(vertexId));
                    }
                    else
                    {
                        // Open question: Is this what we want to do if multiple verts from the deleted face are in another face? Can we ensure they would always have been in order and this is safe?
                        // What about <3-gons generated.
                        if (added)
                        {
                            continue;
                        }

                        added = true;
                        verts.Add(mergedVert.id);
                        vertLocs.Add(mergedVert.loc);
                    }
                }
                deleteFaceOp.AddFace(verts, f.properties);
            }

            foreach (int vertexId in faceToDelete.vertexIds)
            {
                deleteFaceOp.DeleteVertex(vertexId);
            }

            foreach (int f in facesToDelete)
            {
                deleteFaceOp.DeleteFace(f);
            }

            deleteFaceOp.Commit();

            model.ApplyCommand(new ReplaceMeshCommand(mesh.id, mesh));
        }

        private void StartDeleting()
        {
            isDeleting = true;
        }

        private void FinishDeleting()
        {
            isDeleting = false;

            selector.DeselectAll();

            List<Command> deleteCommands = new List<Command>();
            foreach (int meshId in meshIdsToDelete)
            {
                deleteCommands.Add(new DeleteMeshCommand(meshId));
            }

            if (deleteCommands.Count > 0)
            {
                Command compositeCommand = new CompositeCommand(deleteCommands);
                model.ApplyCommand(compositeCommand);

            }

            meshIdsToDelete.Clear();
        }

        private static bool IsSetSnapTriggerTooltipEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.LIGHT_DOWN;
        }

        /// <summary>
        ///   Cancel any deletions that have been performed in the current operation.
        /// </summary>
        public bool CancelDeletionsSoFar()
        {
            bool anythingToDo = meshIdsToDelete.Count > 0;
            foreach (int meshId in meshIdsToDelete)
            {
                model.UnmarkMeshForDeletion(meshId);
            }
            meshIdsToDelete.Clear();
            return anythingToDo;
        }

        // Test method.
        public void TriggerUpdateForTest()
        {
            Update();
        }

        // This function returns a point which is a projection from a point to a plane.
        public static Vector3 ProjectPointOnPlane(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
        {
            // First calculate the distance from the point to the plane:
            float distance = Vector3.Dot(planeNormal.normalized, (point - planePoint));

            // Reverse the sign of the distance.
            distance *= -1;

            // Get a translation vector.
            Vector3 translationVector = planeNormal.normalized * distance;

            // Translate the point to form a projection
            return point + translationVector;
        }
    }
}