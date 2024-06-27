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
using UnityEngine;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.import;
using System.Linq;
using System.Text;
using com.google.apps.peltzer.client.model.export;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   The Poly model. This is the top-level class that represents the whole "scene" that the user is currently
    ///   editing. A Poly model is essentially a collection of MMeshes.
    ///
    ///   All meshes in the model are represented in Model Space coordinates. When displaying, these are converted to
    ///   world space (the Unity coordinate system) via the WorldSpace class, which allows the user to pan/rotate/zoom
    ///   the model.
    ///
    ///   User-driven mutations of the model are represented through Command objects, which represent individual
    ///   operations that change the model (adding meshes, deleting meshes, etc).
    ///
    ///   Note that, as a general principle, each MMesh should be immutable once added to the Model. Modifying an MMesh
    ///   is represented by replacing the original mesh with a new MMesh with the same ID.
    /// </summary>
    public class Model : IMeshRenderOwner, IMeshRenderOwnerOwner
    {

        // Maximum size of the undo stack - when we hit this size we discard the older half of the stack.
        private static int undoStackMaxSize = 80;

        // Model change events.
        public event Action<MMesh> OnMeshAdded;
        public event Action<MMesh, bool, bool, bool> OnMeshChanged;
        public event Action<MMesh> OnMeshDeleted;
        public event Action<Command> OnUndo;
        public event Action<Command> OnRedo;
        public event Action<Command> OnCommandApplied;

        /// <summary>
        /// Delegate that approves or rejects a proposed command before it gets applied to the model.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <returns>True if the command should be accepted and applied, false if it should be rejected.</returns>
        public delegate bool CommandValidator(Command command);

        /// <summary>
        /// Command validators. If present, they have the prerrogative to approve or reject each Command before
        /// it gets applied to the model. This is used, for example, for the tutorial where we want to carefully
        /// validate each thing the user does.
        /// </summary>
        public event CommandValidator OnValidateCommand;

        private Bounds bounds;
        private readonly Dictionary<int, MMesh> meshById =
          new Dictionary<int, MMesh>();
        private Dictionary<int, IMeshRenderOwner> renderOwners = new Dictionary<int, IMeshRenderOwner>();
        // Tracks previous render owner for any mesh currently owned by Model.  Primarily for debugging.
        private Dictionary<int, IMeshRenderOwner> previousRenderOwners = new Dictionary<int, IMeshRenderOwner>();

        private Command currentCommand;
        private readonly List<Command> allCommands = new List<Command>();

        private readonly Stack<Command> undoStack = new Stack<Command>();
        private readonly Stack<Command> redoStack = new Stack<Command>();
        private readonly ReMesher remesher = new ReMesher();
        private readonly HashSet<int> hiddenMeshes = new HashSet<int>();
        private readonly System.Random random = new System.Random();

        public MeshRepresentationCache meshRepresentationCache { private set; get; }

        /// <summary>
        /// Maps group ID to the list of meshes that belong to that group.
        /// The "null group" (GROUP_NONE) is not included in this list.
        /// </summary>
        private readonly Dictionary<int, List<MMesh>> groupById = new Dictionary<int, List<MMesh>>();

        public Dictionary<int, List<MMesh>> GetAllGroups()
        {
            return groupById;
        }

        /// <summary>
        ///   Meshes which are scheduled for deletion and should not be shown anywhere, regardless of what their
        ///   'preview' says. This is necessary as the 'delete' tool will batch its commands, rather than deleting
        ///   as meshes are touched, but we wish for users to believe the meshes are deleted immediately.
        /// </summary>
        private HashSet<int> meshesMarkedForDeletion = new HashSet<int>();

        // Whether we are currently allowing changes to the model.  This is a bit of a hack to allow us to do
        // read-only operations on the background thread.
        // NOTE: This variable should only be changed on the main thread.
        public bool writeable { get; set; }

        // We batch all undo commands within a certain short timeframe.
        private const float BATCH_FREQUENCY_SECONDS = 0.5f;
        private float undoBatchStartTime;
        private bool lastCommandWasNewBatch = false;

        /// <summary>
        ///   Create a default, empty Model, with given bounds.
        /// </summary>
        /// <param name="bounds">The bounds of the model's space.</param>
        public Model(Bounds bounds)
        {
            this.bounds = bounds;

            // Start as writeable.
            writeable = true;

            meshRepresentationCache = PeltzerMain.Instance.gameObject.AddComponent<MeshRepresentationCache>();
            meshRepresentationCache.Setup(this, PeltzerMain.Instance.worldSpace);
        }

        /// <summary>
        /// Clears the model of all its meshes and resets bounds to default value
        /// </summary>
        public void Clear(WorldSpace worldspace)
        {
            bounds = worldspace.bounds;
            remesher.Clear();
            meshById.Clear();
            undoStack.Clear();
            redoStack.Clear();
            undoBatchStartTime = 0.0f;
            hiddenMeshes.Clear();
            meshRepresentationCache.Clear();
        }

        /// <summary>
        ///   Render the model to the scene.
        /// </summary>
        public void Render()
        {
            remesher.Render(this);
        }

        public ReMesher GetReMesher()
        {
            return remesher;
        }

        /// <summary>
        ///   Deals with the batching of commands: all commands performed within BATCH_FREQUENCY_SECONDS of a command
        ///   will be batched together into a single command for 'undo' purposes.
        ///   This helps with tools that can create tens of commands per second, and also when a user is just spamming
        ///   the trigger button in any mode.
        /// </summary>
        /// <param name="command">
        /// A command which will be added to allCommands and the undo stack (potentially batched with
        /// other recent commands)
        /// </param>
        private void AddAndMaybeBatchCommands(Command command)
        {
            LimitUndoStack();

            Command undoCommand = command.GetUndoCommand(this);

            // Check if we're still in a batch.
            if (Time.time - undoBatchStartTime <= BATCH_FREQUENCY_SECONDS)
            {
                if (lastCommandWasNewBatch)
                {
                    // Convert the last command in allCommands to a composite command, and add the new command
                    // to it.
                    List<Command> compositeCommandList = new List<Command>();
                    compositeCommandList.Add(currentCommand);
                    compositeCommandList.Add(command);
                    CompositeCommand bundledCommand = new CompositeCommand(compositeCommandList);
                    currentCommand = bundledCommand;

                    // Convert the top undo command in the undoStack to a composite command, and add the new
                    // undo command to it.
                    Command lastUndoCommand = undoStack.Pop();
                    List<Command> undoCommandList = new List<Command>();
                    // Need to reverse the order for undo.
                    undoCommandList.Insert(0, lastUndoCommand);
                    undoCommandList.Insert(0, undoCommand);
                    CompositeCommand bundledUndoCommand = new CompositeCommand(undoCommandList);
                    undoStack.Push(bundledUndoCommand);

                    lastCommandWasNewBatch = false;
                }
                else
                {
                    // Replace the active compositeCommand with a version that includes the new command.
                    CompositeCommand baseCommand = (CompositeCommand)currentCommand;
                    List<Command> forwardCommandList = baseCommand.GetCommands();
                    forwardCommandList.Add(command);
                    currentCommand = new CompositeCommand(forwardCommandList);

                    // Replace the active undo compositeCommand with a version that includes the new undo command.
                    List<Command> undoCommandList = ((CompositeCommand)undoStack.Pop()).GetCommands();
                    undoCommandList.Insert(0, undoCommand);
                    CompositeCommand bundledUndoCommand = new CompositeCommand(undoCommandList);
                    undoStack.Push(bundledUndoCommand);
                }
            }
            else
            {
                lastCommandWasNewBatch = true;
                undoBatchStartTime = Time.time;
                undoStack.Push(undoCommand);
                currentCommand = command;
            }
        }

        // Checks if the undo stack is about to exceed the maximum size, and discard the older half if it is.
        private void LimitUndoStack()
        {
            if (undoStack.Count > undoStackMaxSize - 1)
            {
                List<Command> reducedCommandList = new List<Command>(undoStackMaxSize / 2);
                for (int i = 0; i < undoStackMaxSize / 2; i++)
                {
                    reducedCommandList.Add(undoStack.Pop());
                }

                undoStack.Clear();
                for (int i = reducedCommandList.Count - 1; i >= 0; i--)
                {
                    undoStack.Push(reducedCommandList[i]);
                }
            }
        }

        // Sets the maximum size of the undo stack.
        public static void SetMaxUndoStackSize(int maxSize)
        {
            undoStackMaxSize = maxSize;
        }

        /// <summary>
        ///   Apply a Command to the model.
        /// </summary>
        /// <param name="command"></param>
        public void ApplyCommand(Command command)
        {
            AssertOrThrow.True(writeable, "Model is not writable.");

            if (OnValidateCommand != null)
            {
                // Check with all registered command validators. It only takes one of them to veto the command.
                foreach (Delegate del in OnValidateCommand.GetInvocationList())
                {
                    CommandValidator validator = (CommandValidator)del;
                    if (!validator.Invoke(command))
                    {
                        // Validator rejected the command.
                        return;
                    }
                }
            }

            redoStack.Clear();  // Once we apply a command, clear all redos
            AddAndMaybeBatchCommands(command);
            command.ApplyToModel(this);
            if (OnCommandApplied != null)
            {
                OnCommandApplied(command);
            }
        }

        /// <summary>
        ///   Whether an 'undo' can be applied at this time.
        /// </summary>
        /// <returns></returns>
        public bool CanUndo()
        {
            return undoStack.Count > 0;
        }

        /// <summary>
        ///   Undo the last command applied to the model -- if there is one.
        /// </summary>
        public bool Undo()
        {
            AssertOrThrow.True(writeable, "Model is not writable.");

            if (undoStack.Count > 0)
            {
                // Abort all undo batching.
                undoBatchStartTime = 0.0f;

                Command command = undoStack.Pop();
                redoStack.Push(command.GetUndoCommand(this));
                command.ApplyToModel(this);
                currentCommand = command;

                if (OnUndo != null)
                {
                    OnUndo(command);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        ///   Redo the previously undone command, if possible.
        /// </summary>
        public bool Redo()
        {
            AssertOrThrow.True(writeable, "Model is not writable.");

            if (redoStack.Count > 0)
            {
                // Abort all undo batching.
                undoBatchStartTime = 0.0f;

                Command command = redoStack.Pop();
                undoStack.Push(command.GetUndoCommand(this));
                command.ApplyToModel(this);
                currentCommand = command;

                if (OnRedo != null)
                {
                    OnRedo(command);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        ///   Whether a given mesh can be added to the model, specifically whether it would fit within the model's bounds.
        /// </summary>
        /// <param name="mesh">The Mesh.</param>
        /// <returns>True if the mesh can be added.</returns>
        public bool CanAddMesh(MMesh mesh)
        {
            return Math3d.ContainsBounds(bounds, mesh.bounds);
        }

        /// <summary>
        ///   Whether a given mesh can be moved by specific dimensions and remain within the model's bounds.
        /// </summary>
        /// <param name="mesh">The Mesh.</param>
        /// <param name="positionDelta">The positional move delta.</param>
        /// <param name="rotDelta">The rotational move delta.</param>
        /// <returns>True if the mesh can be moved as specified.</returns>
        public bool CanMoveMesh(MMesh mesh, Vector3 positionDelta, Quaternion rotDelta)
        {
            Bounds newBounds = mesh.CalculateBounds(positionDelta, rotDelta);
            return Math3d.ContainsBounds(bounds, newBounds);
        }

        /// <summary>
        ///   Attempts to add a mesh to the model.
        /// </summary>
        /// <param name="mesh">The Mesh.</param>
        /// <param name="applyAddMeshEffect">Whether to apply a shiny effect to the mesh addition.</param>
        /// <returns>True if the mesh was added to the model.</returns>
        /// <exception cref="System.Exception">Thrown when a mesh with the same id is already in the model.</exception>
        public bool AddMesh(MMesh mesh, bool applyAddMeshEffect = false)
        {
            AssertOrThrow.True(writeable, "Model is not writable.");
            AssertOrThrow.False(meshById.ContainsKey(mesh.id), "Mesh already exists.");

            if (!CanAddMesh(mesh))
                return false;

            meshById[mesh.id] = mesh;
            if (applyAddMeshEffect)
            {
                UXEffectManager.GetEffectManager().StartEffect(new MeshInsertEffect(mesh, this));
            }
            else
            {
                remesher.AddMesh(mesh);
            }

            if (mesh.groupId != MMesh.GROUP_NONE)
            {
                // Assign mesh to its group. This will update our index which says which meshes belong
                // to which group.
                SetMeshGroup(mesh.id, mesh.groupId);
            }

            if (OnMeshAdded != null)
            {
                OnMeshAdded(mesh);
            }
            return true;
        }

        /// <summary>
        ///   Attempts to add a mesh to the model, given the contents of an obj file and an mtl file.
        ///   This will position the imported mesh in model space such that the imported geometry sits in the direction
        ///   that the viewer is currently viewing and at a given minimum distance.
        /// </summary>
        /// <param name="objFileContents">The contents of a .obj file.</param>
        /// <param name="mtlFileContents">The contents of a .mtl file.</param>
        /// <param name="viewerPosInModelSpace">The position of the viewer, in model space.</returns>
        /// <param name="viewerPosInModelSpace">The direction the viewer is facing, in model space.</returns>
        /// <param name="minDistanceFromViewer">The minimum distance from the viewer at which the imported geometry
        /// should be placed.</returns>
        /// <returns>True if the mesh was added to the model.</returns>
        public bool AddMeshFromObjAndMtl(string objFileContents, string mtlFileContents, Vector3 viewerPosInModelSpace,
            Vector3 viewerDirInModelSpace, float minDistanceFromViewer)
        {
            AssertOrThrow.True(writeable, "Model is not writable.");

            MMesh mesh;
            if (!ObjImporter.MMeshFromObjFile(objFileContents, mtlFileContents, GenerateMeshId(), out mesh) ||
                !CanAddMesh(mesh))
            {
                return false;
            }

            // We will now transform the mesh such that it's in front of the user but at the specified minimum distance.

            // Get the original bounding box.
            Bounds bounds = mesh.bounds;

            // Rotate the mesh to match the direction of the viewer.
            MMesh.MoveMMesh(mesh, Vector3.zero, Quaternion.FromToRotation(Vector3.forward, viewerDirInModelSpace));

            // Now figure out where the center of the imported geometry should be. We can figure this out by starting
            // at the viewer position and then moving along the viewing direction. The distance we should move is
            // the minimum distance plus half the depth of the bounding box, to guarantee that the nearest part of
            // the geometry will be at least minDistanceFromViewer away from the viewer.
            float distToCenterOfGeometry = minDistanceFromViewer + mesh.bounds.extents.z * 0.5f;
            Vector3 correctCenter = viewerPosInModelSpace + distToCenterOfGeometry * viewerDirInModelSpace.normalized;

            // Now transform the mesh to place the center of its bounding box in the right place.
            Vector3 offset = correctCenter - mesh.bounds.center;
            MMesh.MoveMMesh(mesh, offset, Quaternion.identity);

            // Finally, add the mesh to the model.
            AddMesh(mesh);
            return true;
        }

        /// <summary>
        ///   Remove a given mesh from the model. The mesh must be in the model.
        /// </summary>
        /// <exception cref="System.Exception">
        ///   Thrown when a mesh with the given id is not in the model, or if the model is not writeable.
        /// </exception>
        public void DeleteMesh(int meshId)
        {
            AssertOrThrow.True(writeable, "Model is not writable.");
            AssertOrThrow.True(meshById.ContainsKey(meshId), "Mesh not found.");

            // Remove mesh from the set of all meshes, and from its group, before sending the call to the spatial
            // index. This will allow the spatial index to check the state of the model before returning any items
            // that may be queued for spatial index removal.
            MMesh mesh = meshById[meshId];
            RemoveMeshFromGroup(mesh);
            meshById.Remove(meshId);

            // If the mesh was marked for deletion (not all are), then remove it from that set.
            bool wasMarkedForDeletion = meshesMarkedForDeletion.Remove(meshId);

            // Remove the mesh from either hiddenMeshes, or the remesher.
            hiddenMeshes.Remove(meshId);
            remesher.RemoveMesh(meshId);

            PeltzerMain.Instance.GetSelector().ResetInactive();

            // Queue the mesh for deletion from the spatial index, and trigger any other code registered to listen for
            // mesh deletion.
            if (OnMeshDeleted != null)
            {
                OnMeshDeleted(mesh);
            }
        }

        /// <summary>
        /// Set the face properties for all faces of a mesh.
        /// </summary>
        /// <param name="meshId">The mesh ID of the mesh to modify.</param>
        /// <param name="newPropertiesForAllFaces">The properties to set on all faces.</param>
        public void ChangeAllFaceProperties(int meshId, FaceProperties newPropertiesForAllFaces)
        {
            ChangeFaceProperties(meshId, newPropertiesForAllFaces, null);
        }

        /// <summary>
        /// Set face properties for each face of a mesh.
        /// </summary>
        /// <param name="meshId">The mesh ID of the mesh to modify.</param>
        /// <param name="propertiesByFaceId">The properties to set on each face.</param>
        public void ChangeFaceProperties(int meshId, Dictionary<int, FaceProperties> propertiesByFaceId)
        {
            ChangeFaceProperties(meshId, null, propertiesByFaceId);
        }

        /// <summary>
        /// Changes the face properties for the indicated mesh.
        /// </summary>
        /// <param name="meshId">The ID of the mesh whose face properties are to be changed.</param>
        /// <param name="propertiesForAllFaces">If not null, the new FaceProperties to apply to all mesh faces.</param>
        /// <param name="propertiesByFaceId">If propertiesForAllFaces is null, this is a dictionary indicating
        /// which FaceProperties to apply to each face.</param>
        private void ChangeFaceProperties(int meshId, FaceProperties? propertiesForAllFaces,
            Dictionary<int, FaceProperties> propertiesByFaceId)
        {
            MMesh mesh = GetMesh(meshId);
            if (propertiesForAllFaces != null)
            {
                foreach (int faceId in mesh.GetFaceIds())
                {
                    mesh.GetFace(faceId).SetProperties(propertiesForAllFaces.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<int, FaceProperties> pair in propertiesByFaceId)
                {
                    mesh.GetFace(pair.Key).SetProperties(pair.Value);
                }
            }
            MeshUpdated(meshId, /* materialsChanged */ true, /* geometryChanged */ false);
        }

        /// <summary>
        ///   Notify the model that a mesh was moved (or changed in any way that
        ///   affects its bounds).
        /// </summary>
        /// <param name="meshId">The mesh's id.</param>
        /// <param name="materialsChanged">If true, the mesh's materials changed.</param>
        /// <param name="geometryChanged">If true, the mesh's offset, rotation, faces or verts changed.</param>
        /// <param name="vertsOrFacesChanged">If true, the mesh's faces or verts changed.</param>
        public void MeshUpdated(int meshId, bool materialsChanged, bool geometryChanged, bool vertsOrFacesChanged = true)
        {
            AssertOrThrow.True(writeable, "Model is not writable.");
            MMesh mesh;
            bool hasMesh = meshById.TryGetValue(meshId, out mesh);
            AssertOrThrow.True(hasMesh, "Mesh not found.");

            PeltzerMain.Instance.GetSelector().ResetInactive();

            if (OnMeshChanged != null)
            {
                OnMeshChanged(mesh, materialsChanged, geometryChanged, vertsOrFacesChanged);
            }
            // Update the renderer.
            remesher.RemoveMesh(meshId);
            if (!hiddenMeshes.Contains(meshId))
            {
                remesher.AddMesh(mesh);
            }
        }

        /// <summary>
        ///   If not already hidden, temporarily hide the mesh while rendering since it is
        ///   being actively edited.  Other tools are responsible for drawing the mesh
        ///   during this time.
        /// </summary>
        /// <param name="meshId">The mesh id.</param>
        private void HideMesh(int meshId)
        {
            remesher.RemoveMesh(meshId);
            hiddenMeshes.Add(meshId);
        }

        /// <summary>
        ///   If the given mesh is hidden, unhides it.
        /// </summary>
        /// <param name="meshId">The mesh id.</param>
        private void UnhideMesh(int meshId)
        {
            if (!hiddenMeshes.Contains(meshId))
                return;

            hiddenMeshes.Remove(meshId);
            remesher.AddMesh(meshById[meshId]);
        }

        /// <summary>
        ///   Marks a mesh for deletion, removing it from the ReMesher or destroying its preview.
        ///   A mesh marked for deletion will never be shown.
        /// </summary>
        public void MarkMeshForDeletion(int meshId)
        {
            remesher.RemoveMesh(meshId);

            if (renderOwners.ContainsKey(meshId))
            {
                renderOwners[meshId].ClaimMesh(meshId, this);
                renderOwners.Remove(meshId);
            }

            meshesMarkedForDeletion.Add(meshId);
        }

        /// <summary>
        ///   Unmarks a mesh for deletion, restoring it to the ReMesher.
        /// </summary>
        public void UnmarkMeshForDeletion(int meshId)
        {
            meshesMarkedForDeletion.Remove(meshId);
            UnhideMesh(meshId);
            MMesh mesh = meshById[meshId];
        }

        /// <summary>
        /// Allows another owner to claim a mesh if and only if it is not currently owned.
        /// </summary>
        /// <param name="meshId">The id of the mesh being claimed.</param>
        /// <returns></returns>
        public int ClaimMeshIfUnowned(int meshId, IMeshRenderOwner fosterRenderer)
        {
            if (renderOwners.ContainsKey(meshId))
            {
                return -1;
            }
            else
            {
                return ClaimMesh(meshId, fosterRenderer);
            }
        }

        /// <summary>
        /// Claim responsibility for rendering a mesh from this class.
        /// </summary>
        /// <param name="meshId">The id of the mesh being claimed</param>
        /// <returns>The id of the mesh that was claimed, or -1 for failure.</returns>
        public int ClaimMesh(int meshId, IMeshRenderOwner fosterRenderer)
        {
            //Debug.Log("Model claim mesh called " + fosterRenderer.GetType());
            IMeshRenderOwner renderOwner;
            if (renderOwners.TryGetValue(meshId, out renderOwner))
            {
                //Debug.Log("Prev owner was " + renderOwners[meshId].GetType());
                //Debug.Log(fosterRenderer + " claiming " + meshId + " from " + renderOwners[meshId]);
                if (meshesMarkedForDeletion.Contains(meshId))
                {
                    throw new Exception("Mesh marked for deletion has an owner when it ought not to be able to.");
                }

                // An error, but shouldn't be a fatal one.
                if (renderOwner == fosterRenderer) return meshId;

                int claimedMeshId = renderOwner.ClaimMesh(meshId, fosterRenderer);
                if (claimedMeshId == -1)
                {
                    throw new Exception("Mesh owner [" + renderOwner + "] failed to allow mesh to be claimed");
                }
                else
                {
                    renderOwners[meshId] = fosterRenderer;
                    return meshId;
                }
            }
            else
            {
                // Handle currently unowned mesh.
                //Debug.Log(fosterRenderer + " claiming " + meshId + " from Model");
                if (meshesMarkedForDeletion.Contains(meshId))
                {
                    return -1;
                }

                //Debug.Log("Model hiding mesh: " + meshId);
                HideMesh(meshId);
                renderOwners[meshId] = fosterRenderer;
                previousRenderOwners.Remove(meshId);
                return meshId;
            }
        }

        /// <summary>
        /// Gives responsibility for rendering a mesh to this class.  Generally, this should only be done to Model - the
        /// general dynamic being that tool classes attempt to claim ownership from the current owner whenever they need a
        /// preview, and then bequeath it back to Model when they are done (provided a competing claim hasn't arisen) - if
        /// ownership is needed sooner, Model will call Claim on the previous owner.
        /// </summary>
        /// <param name="meshId">The id of the mesh being bequeathed</param>
        /// <returns>The id of the mesh that is being bequeathed, or -1 for failure.</returns>
        public void RelinquishMesh(int meshId, IMeshRenderOwner fosterRenderer)
        {
            IMeshRenderOwner renderOwner;
            if (renderOwners.TryGetValue(meshId, out renderOwner) && renderOwner == fosterRenderer)
            {
                UnhideMesh(meshId);
                renderOwners.Remove(meshId);
                previousRenderOwners[meshId] = fosterRenderer;
            }
            else
            {
                if (renderOwners.ContainsKey(meshId))
                {
                    throw new Exception("Incorrect owner attempted to relinquish mesh. Current owner " + renderOwners[meshId]
                    + " erroneous owner: " + fosterRenderer);
                }
                else
                {
                    if (!previousRenderOwners.ContainsKey(meshId))
                    {
                        throw new Exception("Attempt to relinquish Model owned mesh with no previous owner by " + fosterRenderer);
                    }
                    else
                    {
                        throw new Exception("Attempt to relinquish Model owned mesh. Previous owner " + previousRenderOwners[meshId]
                                            + " erroneous owner: " + fosterRenderer);
                    }
                }
            }
        }

        public void AddToRemesher(int meshId)
        {
            if (!meshById.ContainsKey(meshId))
            {
                return;
            }

            MMesh mesh = meshById[meshId];
            remesher.AddMesh(mesh);
        }

        /// <summary>
        ///   Get all meshes in the model.
        /// </summary>
        /// <returns>A collection of all meshes in the model.</returns>
        public ICollection<MMesh> GetAllMeshes()
        {
            return meshById.Values;
        }

        /// <summary>
        /// Get corresponding meshes for given ids.
        /// </summary>
        /// <returns>A collection of meshes in the model matching the given ids.</returns>
        public List<MMesh> GetMatchingMeshes(HashSet<int> ids)
        {
            List<MMesh> list = new List<MMesh>(ids.Count);
            foreach (int id in ids)
            {
                MMesh mesh;
                if (meshById.TryGetValue(id, out mesh))
                {
                    list.Add(mesh);
                }
            }
            return list;
        }

        /// <summary>
        ///   Returns the number of meshes in this model (including those that are 'hidden' or outside of ReMesher).
        /// </summary>
        public int GetNumberOfMeshes()
        {
            return meshById.Count;
        }

        /// <summary>
        ///   Get a mesh by id.
        /// </summary>
        /// <param name="meshId"></param>
        /// <returns>The mesh.</returns>
        /// <exception cref="System.Exception">Thrown when a mesh with
        ///   the given id is not in the model.</exception>
        public MMesh GetMesh(int meshId)
        {
            MMesh mesh;
            bool hasMesh = meshById.TryGetValue(meshId, out mesh);
            AssertOrThrow.True(hasMesh, "Mesh not found.");
            return mesh;
        }

        /// <summary>
        ///   Gets meshes by id.
        /// </summary>
        /// <param name="meshIds">The IDs of the meshes to get.</param>
        /// <returns>The requested meshes.</returns>
        /// <exception cref="System.Exception">Thrown when a mesh with
        ///   one or more of the given ids is not in the model.</exception>
        public List<MMesh> GetMeshes(IEnumerable<int> meshIds)
        {
            return new List<MMesh>(meshIds.Select(id => GetMesh(id)));
        }

        /// <summary>
        ///  Check if a mesh of the given id exists.
        /// </summary>
        /// <param name="meshId">Mesh id.</param>
        /// <returns>True if a mesh of that id is in the model.</returns>
        public bool HasMesh(int meshId)
        {
            return meshById.ContainsKey(meshId);
        }

        /// <summary>
        /// Returns the list of meshes that have at least one face with the given material ID.
        /// </summary>
        /// <param name="materialId">The material ID to search for.</param>
        /// <returns>The list of meshes where at least one of the faces has that material ID.</returns>
        public List<MMesh> GetMeshesByMaterialId(int materialId)
        {
            List<MMesh> result = new List<MMesh>();
            foreach (MMesh mesh in meshById.Values)
            {
                foreach (Face face in mesh.GetFaces())
                {
                    if (face.properties.materialId == materialId)
                    {
                        result.Add(mesh);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the (only) mesh in the scene that has the given material.
        /// </summary>
        /// <param name="materialId">The material ID to search for.</param>
        /// <returns>The only mesh with the given material. Throws an exception if there's
        /// more than one, or if there are none.</returns>
        public MMesh GetOnlyMeshWithMaterialId(int materialId)
        {
            List<MMesh> result = GetMeshesByMaterialId(materialId);
            AssertOrThrow.True(result.Count == 1,
                "Expected exactly one mesh with material ID " + materialId + ", there are " + result.Count);
            return result[0];
        }

        /// <summary>
        ///  Check if a group of the given id exists.
        /// </summary>
        /// <param name="groupId">Group id.</param>
        /// <returns>True if a group of that id is in the model.</returns>
        public bool HasGroup(int groupId)
        {
            return groupById.ContainsKey(groupId);
        }

        /// <summary>
        ///  Fetches the meshes in a given group.
        ///  Returns true and populates the given list with the meshes in a given group,
        ///  or returns false and leaves the list in its previous state.
        /// </summary>
        public bool GetMeshesInGroup(int groupId, out List<MMesh> meshes)
        {
            return groupById.TryGetValue(groupId, out meshes);
        }

        /// <summary>
        ///  Returns the number of meshes in a given group, or 0 if the group does not exist.
        /// </summary>
        public int GetNumMeshesInGroup(int groupId)
        {
            List<MMesh> meshesInGroup;
            if (groupById.TryGetValue(groupId, out meshesInGroup))
            {
                return meshesInGroup.Count;
            }
            return 0;
        }

        /// <summary>
        ///  Generates a new mesh ID that does not refer to any existing mesh.
        /// </summary>
        /// <returns>An integer id.</returns>
        public int GenerateMeshId(List<int> badIds = null)
        {
            int meshId;
            do
            {
                meshId = random.Next();
            } while (HasMesh(meshId) || (badIds != null && badIds.Contains(meshId)));
            return meshId;
        }

        /// <summary>
        ///  Generates a new group ID that does not refer to any existing group.
        /// </summary>
        /// <returns>An integer id.</returns>
        public int GenerateGroupId()
        {
            int groupId;
            do
            {
                groupId = random.Next();
                // Note that we can't use MMesh.GROUP_NONE as a group ID because it's a special value
                // that means "no group".
            } while (groupId == MMesh.GROUP_NONE || HasGroup(groupId));
            return groupId;
        }

        /// <summary>
        /// Takes a set of mesh IDs and expands it to also include all mesh IDs of
        /// the meshes that are part of the same groups as the original ones.
        /// So if meshes { 100, 101, 102, 103 } are a group and { 200, 201, 202 } are another group,
        /// then ExpandMeshIdsToGroupMates({ 102, 201 }) would result in
        /// { 100, 101, 102, 103, 200, 201, 202 }, which includes the original meshes and
        /// all the other meshes in the same groups.</summary>
        /// <param name="meshes">The IDs of the meshes whose groups are to be retrieved.
        /// This input will be mutated to include the extra mesh IDs. The resulting
        /// set will not contain duplicates..</param>
        public void ExpandMeshIdsToGroupMates(HashSet<int> meshIdsInOut)
        {
            // For each mesh in the input set, check which group it belongs to; if it belongs
            // to a group, then add the other members of the group to the list.
            HashSet<int> originalMeshIds = new HashSet<int>(meshIdsInOut);
            foreach (int meshId in originalMeshIds)
            {
                MMesh mesh = GetMesh(meshId);
                if (mesh.groupId != MMesh.GROUP_NONE)
                {
                    // Mesh is in a group. Add all the members of the group.
                    List<MMesh> peers = groupById[mesh.groupId];
                    foreach (MMesh peer in peers)
                    {
                        meshIdsInOut.Add(peer.id);
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether or not all the passed meshes belong to a single group.
        /// </summary>
        /// <param name="meshes">The meshes to test.</param>
        /// <returns>True if all meshes belong to a single group (or if the list is empty),
        /// false otherwise. GROUP_NONE is not considered a "group" so if any meshes are in
        /// GROUP_NONE, this method will return false.</returns>
        public bool AreMeshesInSameGroup(IEnumerable<int> meshes)
        {
            Model model = PeltzerMain.Instance.model;
            // Note: there are more readable ways to write this code using Count() and Any() and such
            // niceties, but since this method is in the critical path, this is written so that the
            // iterator only has to be traversed once.
            IEnumerator<int> enumerator = meshes.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                // List is empty.
                return true;
            }
            // We test by comparing all group IDs to the first one. They must all match.
            while (!model.HasMesh(enumerator.Current))
            {
                if (!enumerator.MoveNext())
                {
                    // Only invalid mesh IDs were passed.
                    return true;
                }
            }
            int expectedGroupId = model.GetMesh(enumerator.Current).groupId;
            if (expectedGroupId == MMesh.GROUP_NONE)
            {
                // GROUP_NONE is not a serious group.
                return false;
            }
            while (enumerator.MoveNext())
            {
                if (model.HasMesh(enumerator.Current))
                {
                    if (expectedGroupId != model.GetMesh(enumerator.Current).groupId)
                    {
                        return false;
                    }
                }
            }
            // All meshes are in the same group, and that group is not GROUP_NONE.
            return true;
        }

        /// <summary>
        /// (Re)assigns the given mesh to the given group ID.
        /// </summary>
        /// <param name="meshId">The ID of the mesh to reassign.</param>
        /// <param name="newGroupId">The new group on which to put the mesh. If this is
        /// MMesh.GROUP_NONE, the mesh will be removed from the group.</param>
        public void SetMeshGroup(int meshId, int newGroupId)
        {
            MMesh mesh = GetMesh(meshId);
            // First, remove it from its previous group, if any.
            RemoveMeshFromGroup(mesh);
            // Now assign the mesh to the new group.
            mesh.groupId = newGroupId;
            if (newGroupId != MMesh.GROUP_NONE)
            {
                // Add it to the dictionary.
                if (!groupById.ContainsKey(newGroupId))
                {
                    groupById[newGroupId] = new List<MMesh>();
                }
                groupById[newGroupId].Add(mesh);
            }
        }

        /// <summary>
        /// Removes the mesh from its group, if it's in a group. Does nothing if the mesh
        /// is not in a group. This also cleans up the group from the index if the group
        /// becomes empty as a result of the removal.
        /// </summary>
        /// <param name="mesh">The mesh to remove from its group.</param>
        private void RemoveMeshFromGroup(MMesh mesh)
        {
            if (mesh.groupId != MMesh.GROUP_NONE && groupById.ContainsKey(mesh.groupId))
            {
                groupById[mesh.groupId].Remove(mesh);
                // If group is now empty, clean it up.
                if (groupById[mesh.groupId].Count == 0)
                {
                    groupById.Remove(mesh.groupId);
                }
            }
            mesh.groupId = MMesh.GROUP_NONE;
        }

        /// <summary>
        /// Computes and returns the complete bounds of the all of the model's meshes.
        /// </summary>
        /// <returns>The bounding box that encapsulates all meshes of the model.</returns>
        public Bounds FindBoundsOfAllMeshes()
        {
            if (meshById.Count > 0)
            {
                // Must be intialized as an actual bounds to be included in the encapsulation, or else will
                // also encapsulate Bounds.zero.
                Bounds allBounds = meshById.First().Value.bounds;
                foreach (KeyValuePair<int, MMesh> pair in meshById)
                {
                    allBounds.Encapsulate(pair.Value.bounds);
                }
                return allBounds;
            }
            else
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }
        }

        public bool IsMeshHidden(int meshId)
        {
            return hiddenMeshes.Contains(meshId);
        }

        public bool MeshIsMarkedForDeletion(int meshId)
        {
            return meshesMarkedForDeletion.Contains(meshId);
        }

        // For serialization
        public List<Command> GetAllCommands()
        {
            return allCommands;
        }

        // For serialization.
        public Stack<Command> GetUndoStack()
        {
            return undoStack;
        }

        // For serialization.
        public Stack<Command> GetRedoStack()
        {
            return redoStack;
        }

        // For test or tutorial only.
        public void HideMeshForTestOrTutorial(int meshId)
        {
            HideMesh(meshId);
        }

        // For test or tutorial only.
        public void UnhideMeshForTestOrTutorial(int meshId)
        {
            UnhideMesh(meshId);
        }

        public HashSet<int> GetHiddenMeshes()
        {
            return hiddenMeshes;
        }

        // Meshinfos should not be modified outside of remesher.
        // This exists to enable exporting coalesced meshes.
        public HashSet<ReMesher.MeshInfo> GetAllRemesherMeshInfos()
        {
            return remesher.GetAllMeshInfos();
        }

        /// <summary>
        /// Overloaded. Returns a set with the union of all remix IDs being used by meshes in the model.
        /// </summary>
        public HashSet<string> GetAllRemixIds()
        {
            return GetAllRemixIds(meshById.Values);
        }

        /// <summary>
        /// Overloaded. Returns a set with the union of all remix IDs being used by the given meshes.
        /// </summary>
        public HashSet<string> GetAllRemixIds(ICollection<MMesh> meshes)
        {
            HashSet<string> allRemixIds = new HashSet<string>();
            foreach (MMesh mesh in meshes)
            {
                if (mesh.remixIds != null)
                {
                    allRemixIds.UnionWith(mesh.remixIds);
                }
            }
            return allRemixIds;
        }

        public String DebugConsoleDump()
        {
            StringBuilder builder = new StringBuilder();
            HashSet<ReMesher.MeshInfo> meshInfos = remesher.GetAllMeshInfos();
            builder.Append("Remesher contents:\n");
            foreach (ReMesher.MeshInfo info in meshInfos)
            {
                builder.Append("mat id: " + info.materialAndColor.matId + " contains [\n");
                foreach (int meshId in info.GetMeshIds())
                {
                    builder.Append("  " + meshId + "\n");
                }
                builder.Append("]\n");
            }
            builder.Append("Hidden meshes: [\n");
            foreach (int meshId in hiddenMeshes)
            {
                builder.Append("    " + meshId + "\n");
            }
            builder.Append("]\n");
            builder.Append("Owned meshes: [\n");
            foreach (int meshId in this.renderOwners.Keys)
            {
                builder.Append("    " + meshId + ":" + renderOwners[meshId].ToString() + "\n");
            }
            builder.Append("]\n");
            builder.Append("Previous Render Owners: [\n");
            foreach (int meshId in previousRenderOwners.Keys)
            {
                builder.Append("    " + meshId + ":" + previousRenderOwners[meshId].ToString() + "\n");
            }
            builder.Append("]\n");
            MeshWithMaterialRenderer[] renderers = GameObject.FindObjectsOfType<MeshWithMaterialRenderer>();
            builder.Append("Mesh with material renderers: [\n");
            foreach (MeshWithMaterialRenderer mwmr in renderers)
            {
                builder.Append("  MeshWithMaterialRenderer: " + mwmr.gameObject.name + "\n");
                foreach (MeshWithMaterial mesh in mwmr.meshes)
                {
                    builder.Append("    meshmat: " + mesh.materialAndColor.matId + "\n");
                    builder.Append("    mesh tricount: " + mesh.mesh.triangles.Length + "\n");
                }
            }
            builder.Append("]\n");
            return builder.ToString();
        }
    }
}
