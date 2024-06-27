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

#undef MMESH_PARANOID_INTEGRITY_CHECK
using System.Collections.Generic;
using UnityEngine;

using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.main;
using System;
using System.Linq;
using System.Runtime.Serialization;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.serialization;

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  ///   An MMesh represents a mesh in the model. Named 'MMesh' to avoid ambiguity with Unity meshes. An MMesh is
  ///   what you normally think of as a mesh: a piece of geometry made of vertices, edges and faces.
  ///
  ///   Although not strictly enforced everywhere, a general principle is that once an MMesh is in the model,
  ///   it is immutable. Mutations of an MMesh actually consist of replacing it with a new MMesh that's the result
  ///   of the mutation. The fact that cloning an MMesh is relatively cheap makes that model viable.
  ///
  ///   The coordinates of the components of a mesh (vertices, normals, etc) are represented in what is called
  ///   Mesh Space, which is the private frame of reference of the mesh itself (not shared with other meshes
  ///   in the model). The offset and rotation properties of the mesh indicate how to convert from Mesh Space to
  ///   Model Space. For example, if a mesh has a vertex with coordinates (1, 2, 3) in Mesh Space, and the offset
  ///   of the mesh is (1000, 2000, 3000), then the position of that vertex in Model Space would be (1001, 2002, 3003).
  ///
  ///   An MMesh can be attached to (and detached from) a GameObject to make it render on the screen. In particular,
  ///   it is rendered by the MeshWithMaterialRenderer behavior, which takes the mesh, transforms into World Space
  ///   and renders it.
  /// 
  ///   Partial class is to allow GeometryOperation to be an inner class but have its own file.
  /// </summary>
  public partial class MMesh {
    /// <summary>
    /// For generating unique ids.
    /// </summary>
    private static readonly System.Random rand = new System.Random();

    // Max sizes. TODO(bug): Tune these and enforce them, temp value for now.
    public static readonly int MAX_FACES = 20000;

    /// <summary>
    /// Special group ID value meaning "no group".
    /// </summary>
    public const int GROUP_NONE = 0;

    /// <summary>
    /// ID of this MMesh in the model. See ChangeId() for notes on setting this.
    /// </summary>
    private int _id;

    /// <summary>
    /// Offset (position) of this MMesh in model space (for transforming coordinates from Mesh Space to Model Space).
    /// </summary>
    public Vector3 _offset;

    /// <summary>
    /// Rotation of this MMesh in model space (for transforming coordinates from Mesh Space to Model Space).
    /// </summary>
    public Quaternion _rotation = Quaternion.identity;

    private Dictionary<int, Vertex> verticesById;
    private Dictionary<int, Face> facesById;
    
    public int vertexCount { get { return verticesById.Count; }}
    public int faceCount { get { return facesById.Count; }}

    /// <summary>
    /// Reverse table which provides vertex id to face id lookup - each vertex maps to the set of faces in which it is
    /// used.
    /// Until all mesh modification is fully converted to use GeometryOperation - http://bug the only safe way to
    /// use this data is by calling RecalcReverseTable
    /// </summary>
    public Dictionary<int, HashSet<int>> reverseTable;
    
    /// <summary>
    /// Bounds of this mesh in model coordinates. NOT in mesh coordinates.
    /// </summary>
    public Bounds bounds { get; private set; }

    /// <summary>
    /// ID of the group to which this mesh belongs, or GROUP_NONE if this mesh is ungrouped.
    /// Meshes with the same groupId belong to the same group and stay together during
    /// selection/move/etc.
    /// </summary>
    public int groupId { get; set; }

    /// <summary>
    /// The IDs of the original assets that originated this mesh (for remixing attribution).
    /// When a set of meshes is imported from Zandria, this ID is set on each of the meshes.
    /// To cut down on garbage generation, null is used instead of the empty set if there
    /// is no attribution.
    /// </summary>
    public HashSet<string> remixIds { get; set; }

    // Read-only getters.
    public int id { get { return _id; } }

    private Vector3 _offsetJitter;

    public MMesh(int id, Vector3 offset, Quaternion rotation,
      Dictionary<int, Vertex> verticesById, Dictionary<int, Face> facesById,
      int groupId = GROUP_NONE, HashSet<string> remixIds = null) {
      _id = id;
      this._offset = offset;
      System.Random rand = new System.Random();
      
      this._offsetJitter = new Vector3((float)(rand.NextDouble() - 0.5) / 5000f, 
        (float)(rand.NextDouble() - 0.5) / 5000f, 
        (float)(rand.NextDouble() - 0.5) / 5000f);
      this._rotation = rotation;
      this.verticesById = verticesById;
      this.facesById = facesById;
      this.groupId = groupId;
      this.remixIds = remixIds;
      this.reverseTable = new Dictionary<int, HashSet<int>>();
      RecalcBounds();
      RecalcReverseTable();
    }

    public MMesh(int id, Vector3 offset, Quaternion rotation,
      Dictionary<int, Vertex> verticesById, Dictionary<int, Face> facesById,
      Bounds bounds, Dictionary<int, HashSet<int>> reverseTable, int groupId = GROUP_NONE,
      HashSet<string> remixIds = null) {
      _id = id;
      System.Random rand = new System.Random();
      
      this._offsetJitter = new Vector3((float)(rand.NextDouble() - 0.5) / 5000f, 
        (float)(rand.NextDouble() - 0.5) / 5000f, 
        (float)(rand.NextDouble() - 0.5) / 5000f);
      this._offset = offset;
      this._rotation = rotation;
      this.verticesById = verticesById;
      this.facesById = facesById;
      this.bounds = bounds;
      this.reverseTable = reverseTable;
      this.groupId = groupId;
      this.remixIds = remixIds;
    }

    /// <summary>
    ///   Deep copy of Mesh.
    /// </summary>
    /// <returns>The copy.</returns>
    public MMesh Clone() {
      Dictionary<int, Face> facesCloned = new Dictionary<int, Face>(facesById.Count);
      foreach (KeyValuePair<int, Face> pair in facesById) {
        facesCloned.Add(pair.Key, pair.Value.Clone());
      }

      Dictionary<int, HashSet<int>> reverseTableCloned = new Dictionary<int, HashSet<int>>(reverseTable.Count);

      foreach (KeyValuePair<int, HashSet<int>> pair in reverseTable) {
        reverseTableCloned[pair.Key] = new HashSet<int>(pair.Value);
      }

      Dictionary<int, Vertex> verticesCloned = new Dictionary<int, Vertex>(verticesById);
      HashSet<string> remixIdsCloned = remixIds == null ? null : new HashSet<string>(remixIds);

      return new MMesh(id,
        _offset,
        _rotation,
        verticesCloned,
        facesCloned,
        bounds,
        reverseTableCloned,
        groupId,
        remixIdsCloned);
    }

    /// <summary>
    ///   Deep copy of Mesh while updating the id.
    ///   This preserves the remixId.
    /// </summary>
    /// <returns>The copy.</returns>
    public MMesh CloneWithNewId(int newId) {
      return CloneWithNewIdAndGroup(newId, groupId);
    }

    /// <summary>
    ///   Deep copy of Mesh while updating the id and group ID.
    ///   This preserves the remixId.
    /// </summary>
    /// <returns>The copy.</returns>
    public MMesh CloneWithNewIdAndGroup(int newId, int newGroupId) {
      Dictionary<int, Face> facesCloned = new Dictionary<int, Face>(facesById.Count);
      foreach (KeyValuePair<int, Face> pair in facesById) {
        facesCloned.Add(pair.Key, pair.Value.Clone());
      }

      return new MMesh(newId,
        _offset,
        _rotation,
        new Dictionary<int, Vertex>(verticesById),
        facesCloned,
        bounds,
        new Dictionary<int, HashSet<int>>(reverseTable),
        newGroupId,
        remixIds == null ? null : new HashSet<string>(remixIds));
    }

    /// <summary>
    ///   Changes the ID of this mesh. USE WITH GREAT CARE.
    ///   Tools, the model, the undo/redo stacks and who knows what else might be holding a reference to the
    ///   previous ID, these will all become corrupt and throw exceptions if you change this without updating
    ///   them.
    ///   This should probably only be used where a mesh has not yet been added to the model, and we're just
    ///   updating the mesh's ID because it would collide with an existing ID.
    /// </summary>
    public void ChangeId(int newId) {
      _id = newId;
    }

    /// <summary>
    ///   Changes the Group ID of this mesh. Much safer than ChangeId above, but still read the commentary for
    ///   that method and take care when using this method.
    /// </summary>
    public void ChangeGroupId(int newGroupId) {
      groupId = newGroupId;
    }

    /// <summary>
    /// Changes the remix IDs of this mesh to be the (single) remix ID given.
    /// </summary>
    /// <param name="remixId">The new remixId to set, or null to mean none.</param>
    public void ChangeRemixId(string remixId) {
      if (remixId == null) {
        remixIds = null;
      } else {
        if (remixIds == null) {
          remixIds = new HashSet<string>();
        } else {
          remixIds.Clear();
        }
        remixIds.Add(remixId);
      }
    }

    private bool operationInProgress = false;
    
    /// <summary>
    /// Starts a GeometryOperation targeting this mesh.
    /// </summary>
    public GeometryOperation StartOperation() {
      #if UNITY_EDITOR
      // It may be possible to OT geometry ops someday, in which case this restriction won't be necessary
      if (operationInProgress) {
        throw new Exception("Attempted to start an operation on mesh " + id + " when one is already in progress");
      }
      #endif
      operationInProgress = true;
      return new GeometryOperation(this);
    }

    // Reset operationInProgress bit
    private void FinishOperation() {
      operationInProgress = false;
      #if MMESH_PARANOID_INTEGRITY_CHECK
        CheckReverseTableIntegrity();
      #endif
    }
    
    /// <summary>
    ///   Get the bounds for this mesh.
    /// </summary>
    /// <returns>The bounds, in model coordinates.</returns>
    public Bounds GetBounds() {
      return bounds;
    }

    /// <summary>
    ///   Calculates and returns the bounds of a face in this mesh, in model space.
    /// </summary>
    public Bounds CalculateFaceBoundsInModelSpace(int faceId) {
      Face face;
      if (!facesById.TryGetValue(faceId, out face)) {
        throw new Exception("Tried to get bounds for non-existent face");
      }

      // This code is duplicated in RecalcBounds below.
      float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
      float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
      foreach (int vertexId in face.vertexIds) {
        Vector3 loc = MeshCoordsToModelCoords(verticesById[vertexId].loc);
        minX = Mathf.Min(minX, loc.x);
        minY = Mathf.Min(minY, loc.y);
        minZ = Mathf.Min(minZ, loc.z);
        maxX = Mathf.Max(maxX, loc.x);
        maxY = Mathf.Max(maxY, loc.y);
        maxZ = Mathf.Max(maxZ, loc.z);
      }
      return new Bounds(
        /* center */ new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
        /* size */ new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
    }

    /// <summary>
    ///   Get the position of the given vertex id in model coordinates.
    /// </summary>
    public Vector3 VertexPositionInModelCoords(int vertexId) {
      return ToModelCoords(verticesById[vertexId]);
    }
    
    /// <summary>
    ///   Get the position of the given vertex id in mesh coordinates.
    /// </summary>
    public Vector3 VertexPositionInMeshCoords(int vertexId) {
      return verticesById[vertexId].loc;
    }

    public Vertex GetVertex(int vertexId) {
      return verticesById[vertexId];
    }

    /// <summary>
    /// Get vertex ids to iterate over.
    /// Returned as dictionary key collection for efficiency.
    /// </summary>
    public Dictionary<int, Vertex>.KeyCollection GetVertexIds() {
      return verticesById.Keys;
    }
    
    /// <summary>
    /// Returns whether the mesh contains a vertex with the given id.
    /// </summary>
    public bool HasVertex(int id) {
      return verticesById.ContainsKey(id);
    }
    
    /// <summary>
    /// Get vertices to iterate over.
    /// Returned as dictionary key collection for efficiency.
    /// </summary>
    public Dictionary<int, Vertex>.ValueCollection GetVertices() {
      return verticesById.Values;
    }
    
    /// <summary>
    /// Get vertex ids to iterate over.
    /// Returned as dictionary key collection for efficiency.
    /// </summary>
    public Dictionary<int, Face>.KeyCollection GetFaceIds() {
      return facesById.Keys;
    }

    /// <summary>
    /// Returns whether the mesh contains a face with the given id.
    /// </summary>
    public bool HasFace(int id) {
      return facesById.ContainsKey(id);
    }
    
    /// <summary>
    /// Get faces to iterate over.
    /// Returned as dictionary key collection for efficiency.
    /// </summary>
    public Dictionary<int, Face>.ValueCollection GetFaces() {
      return facesById.Values;
    }
    
    /// <summary>
    /// Gets the face with the supplied id.
    /// </summary>
    public Face GetFace(int id) {
      return facesById[id];
    }
    
    /// <summary>
    /// Tries to get the face with the supplied id.
    /// </summary>
    public bool TryGetFace(int id, out Face outFace) {
      return facesById.TryGetValue(id, out outFace);
    }

    /// <summary>
    /// Converts an arbitrary point in model space to this mesh's coordinate system.
    /// </summary>
    /// <param name="pointInModelSpace">Some point in model space.</param>
    /// <returns>Same point, but in mesh space.</returns>
    public Vector3 ModelCoordsToMeshCoords(Vector3 pointInModelSpace) {
      return Quaternion.Inverse(_rotation) * (pointInModelSpace - offset);
    }

    /// <summary>
    ///   Get the position of the given vertex in model coordinates.
    /// </summary>
    private Vector3 ToModelCoords(Vertex vertex) {
      return (_rotation * vertex.loc) + offset;
    }

    /// <summary>
    ///   Get the position of the given mesh-space position in model coordinates.
    /// </summary>
    public Vector3 MeshCoordsToModelCoords(Vector3 loc) {
      return (_rotation * loc) + offset;
    }

    /// <summary>
    ///   Recalculate the bounds for this mesh.  Should be called whenever any of the vertices move.
    ///   This is more efficient than Unity's default bounds calculation, as it can avoid calculating the center
    ///   and size until the end of the operation (whereas Unity's contract requires correct state after each
    ///   encapsulation).
    /// </summary>
    public void RecalcBounds() {
      // This code is duplicated in CalculateFaceBounds above for maximum efficiency, given that this method
      // is an extreme hotspot.
      float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
      float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
      foreach (Vertex vert in verticesById.Values) {
        Vector3 loc = MeshCoordsToModelCoords(vert.loc);
        minX = Mathf.Min(minX, loc.x);
        minY = Mathf.Min(minY, loc.y);
        minZ = Mathf.Min(minZ, loc.z);
        maxX = Mathf.Max(maxX, loc.x);
        maxY = Mathf.Max(maxY, loc.y);
        maxZ = Mathf.Max(maxZ, loc.z);
      }
      bounds = new Bounds(
        /* center */ new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
        /* size */ new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
    }
    
    /// <summary>
    ///   Calculate the bounds for this mesh, applying an additional offset and rotation on top of the mesh's current
    ///   offset and rotation.
    /// </summary>
    public Bounds CalculateBounds(Vector3 additionalOffset, Quaternion additionalRotation) {
      Vector3 totalOffset = this.offset + additionalOffset;
      Quaternion totalRotation = Math3d.Normalize(this._rotation * additionalRotation);
      
      // This code is duplicated in CalculateFaceBounds above for maximum efficiency, given that this method
      // is an extreme hotspot.
      float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
      float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
      foreach (Vertex vert in verticesById.Values) {
        Vector3 loc = (totalRotation * vert.loc) + totalOffset;
        minX = Mathf.Min(minX, loc.x);
        minY = Mathf.Min(minY, loc.y);
        minZ = Mathf.Min(minZ, loc.z);
        maxX = Mathf.Max(maxX, loc.x);
        maxY = Mathf.Max(maxY, loc.y);
        maxZ = Mathf.Max(maxZ, loc.z);
      }
      return new Bounds(
        /* center */ new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
          /* size */ new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
    }

    /// <summary>
    /// Recalculates the reverse table - a table that maps vertices to faces.
    /// Until all geometry modification has been converted to GeometryOperation, calling this is the only way to
    /// ensure that the reverse table is accurate.  After the GeometryOperation transition is done, the reverse table
    /// should always be valid and this can likely be deprecated as an external method.
    /// </summary>
    public void RecalcReverseTable() {
      reverseTable.Clear();
      foreach (Face face in facesById.Values) {
        for (int i = 0; i < face.vertexIds.Count; i++) {
          int curVert = face.vertexIds[i];
          HashSet<int> faceIds;
          if (!reverseTable.TryGetValue(curVert, out faceIds)) {
            faceIds = new HashSet<int>();
            reverseTable[curVert] = faceIds;
          }
          faceIds.Add(face.id);
        }
      }
    }

    /// <summary>
    /// Debug utility that prints the reverse table to console.
    /// Don't commit code that calls this.
    /// </summary>
    public void PrintReverseTable() {
      Debug.Log("Mesh " + id + " Reverse Table:");
      foreach (int vertId in reverseTable.Keys) {
        String line = vertId + ": [";
        foreach (int faceId in reverseTable[vertId]) {
          line += faceId + ", ";
        }
        line += "]";
        Debug.Log(line);
      }
    }

    /// <summary>
    /// Debug utility that prints all vertices to console.
    /// Don't commit code that calls this.
    /// </summary>
    public void PrintVerts() {
      Debug.Log("Mesh " + id + " Vertices");
      foreach (int vertId in verticesById.Keys) {
        Debug.Log("  " + vertId + ": " + MeshUtil.Vector3ToString(verticesById[vertId].loc));
      }
    }
    
    /// <summary>
    /// Debug utility that prints all faces vertex ids to console.
    /// Don't commit code that calls this.
    /// </summary>
    public void PrintFaces() {
      Debug.Log("Mesh " + id + " Faces");
      foreach (int faceId in facesById.Keys) {
        String faceString = "  " + faceId + ": [";
        for (int i = 0; i < facesById[faceId].vertexIds.Count; i++) {
          faceString += facesById[faceId].vertexIds[i] + ", ";
        }
        faceString += "]";
        Debug.Log(faceString);
      }
    }

    /// <summary>
    /// Generates a new ID that does not refer to any existing face.
    /// </summary>
    /// <returns>A new face id.</returns>
    public int GenerateFaceId() {
      int faceId;
      do {
        faceId = rand.Next();
      } while (facesById.ContainsKey(faceId));
      return faceId;
    }
    
    /// <summary>
    /// Generates a new ID that does not refer to any existing face.
    /// </summary>
    /// <returns>A new face id.</returns>
    public int GenerateFaceId(HashSet<int> excludedIds) {
      int faceId;
      do {
        faceId = rand.Next();
      } while (facesById.ContainsKey(faceId) || excludedIds.Contains(faceId));
      return faceId;
    }

    /// <summary>
    /// Generates a new ID that does not refer to any existing vertex.
    /// </summary>
    /// <returns>A new vertex id.</returns>
    public int GenerateVertexId() {
      int vertexId;
      do {
        vertexId = rand.Next();
      } while (verticesById.ContainsKey(vertexId));
      return vertexId;
    }
    
    /// <summary>
    /// Generates a new ID that does not refer to any existing vertex.
    /// </summary>
    /// <returns>A new vertex id.</returns>
    public int GenerateVertexId(HashSet<int> excludedIds) {
      int vertexId;
      do {
        vertexId = rand.Next();
      } while (verticesById.ContainsKey(vertexId) || excludedIds.Contains(vertexId));
      return vertexId;
    }

    // Override for the below.
    public static void AttachMeshToGameObject(
        WorldSpace worldSpace, GameObject gameObject, MMesh mesh,
        bool updateOnly = false, MaterialAndColor materialOverride = null) {
      Dictionary<int, MeshGenContext> components;
      AttachMeshToGameObject(worldSpace, gameObject, mesh, out components, updateOnly, materialOverride);
    }

    /// <summary>
    ///   Creates, or updates, the Mesh representation of an MMesh, implemented through our MeshWithMaterialRenderer.
    ///   The rotation and position/positionModelSpace of the incoming gameObject are irrelevant, as they will be
    ///   set at the end of this method.
    ///
    ///   The final product will be a Mesh with vertex co-ordinates in 'model space' (set around a centroid of Vector3.0)
    ///   attached to a GameObject with its positionModelSpace set to the mesh's offset, and its rotation
    ///   set to the mesh's rotation.
    /// </summary>
    /// <exception cref="System.Exception">Thrown if the GameObject does
    ///   not have the required Components.</exception>
    public static void AttachMeshToGameObject(
        WorldSpace worldSpace, GameObject gameObject, MMesh mesh,
        out Dictionary<int, MeshGenContext> components,
        bool updateOnly = false, MaterialAndColor materialOverride = null) {
      // Get or add renderer to GameObject.
      MeshWithMaterialRenderer renderer = gameObject.GetComponent<MeshWithMaterialRenderer>();
      if (renderer == null) {
        renderer = gameObject.AddComponent<MeshWithMaterialRenderer>();
        renderer.Init(worldSpace);
      }

      // Try to use the cached mesh and just update positions/normals.
      if (updateOnly) {
        components = null;
        if (materialOverride != null) {
          renderer.OverrideWithNewMaterial(materialOverride);
        }
        MeshHelper.UpdateMeshes(mesh, renderer.meshes);
      } else {
        // We get vert positions in model space, not mesh space.
        renderer.meshes = MeshHelper.MeshFromMMesh(mesh, /* useModelSpace */ false, out components, materialOverride);
      }

      // Position & rotate the gameObject to match the incoming mesh's position and rotation.
      renderer.SetPositionModelSpace(mesh.offset);
      renderer.SetOrientationModelSpace(mesh.rotation, /* smooth */ false);
    }

    public static void DetachMeshFromGameObject(WorldSpace worldSpace, GameObject gameObject) {
      MeshWithMaterialRenderer renderer = gameObject.GetComponent<MeshWithMaterialRenderer>();
      if (renderer == null) {
        renderer = gameObject.AddComponent<MeshWithMaterialRenderer>();
        renderer.Init(worldSpace);
      }
      renderer.meshes = new List<MeshWithMaterial>();
    }

    /// <summary>
    ///   Moves an MMesh by specific parameters.
    /// </summary>
    /// <param name="mesh">The Mesh.</param>
    /// <param name="positionDelta">The positional move delta.</param>
    /// <param name="rotDelta">The rotational move delta.</param>
    public static void MoveMMesh(MMesh mesh, Vector3 positionDelta, Quaternion rotDelta) {
      mesh._offset += positionDelta;
      mesh._rotation = Math3d.Normalize(mesh._rotation * rotDelta);
      mesh.RecalcBounds();
    }

    public Vector3 offset {
      get {
        return _offset;
      }
      set {
        Vector3 old = _offset;
        _offset = value;
        bounds = new Bounds(bounds.center + (_offset - old), bounds.size);
      }
    }

    public Quaternion rotation {
      get {
        return _rotation;
      }
      set {
        _rotation = Math3d.Normalize(value);
        RecalcBounds();
      }
    }

    public Matrix4x4 GetTransform() {
           return Matrix4x4.TRS(_offset, _rotation, Vector3.one);
         }

    public Matrix4x4 GetJitteredTransform() {
      return Matrix4x4.TRS(_offset + _offsetJitter, _rotation, Vector3.one);
    }
    
    // Serialize
    public void GetObjectData(SerializationInfo info, StreamingContext context) {
      info.AddValue("meshId", _id);
      info.AddValue("offset", new SerializableVector3(offset));
      info.AddValue("rotation", new SerializableQuaternion(rotation));
      info.AddValue("verticesById", verticesById);
      info.AddValue("facesById", facesById);
      info.AddValue("groupId", groupId);
    }

    // Deserialize
    private SerializableVector3 serializedOffset;
    private SerializableQuaternion serializedRotation;

    /// <summary>
    /// Writes to PolySerializer.
    /// </summary>
    public void Serialize(PolySerializer serializer) {
      serializer.StartWritingChunk(SerializationConsts.CHUNK_MMESH);
      serializer.WriteInt(_id);
      PolySerializationUtils.WriteVector3(serializer, offset);
      PolySerializationUtils.WriteQuaternion(serializer, rotation);
      serializer.WriteInt(groupId);

      // Write vertices.
      serializer.WriteCount(verticesById.Count);
      foreach (Vertex v in verticesById.Values) {
        serializer.WriteInt(v.id);
        PolySerializationUtils.WriteVector3(serializer, v.loc);
      }

      // Write faces.
      serializer.WriteCount(facesById.Count);
      foreach (Face face in facesById.Values) {
        serializer.WriteInt(face.id);
        serializer.WriteInt(face.properties.materialId);
        PolySerializationUtils.WriteIntList(serializer, face.vertexIds);
        // Repeat the face normal for backwards compatability.
        PolySerializationUtils.WriteVector3List(serializer, 
          Enumerable.Repeat(face.normal, face.vertexIds.Count).ToList());

        // DEPRECATED: Write holes.
        serializer.WriteCount(0);
      }
      serializer.FinishWritingChunk(SerializationConsts.CHUNK_MMESH);

      // If we have any remix IDs, also write a remix info chunk.
      // As per the design of the file format, this chunk will be automatically skipped by older versions
      // that don't expect remix IDs in the file.
      if (remixIds != null) {
        serializer.StartWritingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
        PolySerializationUtils.WriteStringSet(serializer, remixIds);
        serializer.FinishWritingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
      }
    }

    public int GetSerializedSizeEstimate() {
      int estimate = 256;  // Headers, offset, rotation, group ID, overhead.
      estimate += 8 + verticesById.Count * 16;  // count + (1 int + 3 floats) per vertex.
      foreach (Face face in facesById.Values) {
        estimate += 32;  // ID, material ID, headers.
        estimate += 8 + face.vertexIds.Count * 4;  // count + 1 int per vertex ID
        estimate += 8 + face.vertexIds.Count * 12;  // count + 3 floats per normal
      }
      if (remixIds != null) {
        estimate += 32; // list header overhead
        foreach (string remixId in remixIds) {
          estimate += 4 + remixId.Length;
        }
      }
      return estimate;
    }

    private void CheckReverseTableIntegrity() {
      foreach (int vertId in reverseTable.Keys) {
        if (!HasVertex(vertId)) {
          throw new Exception("Vert id " + vertId + " in reverse table does not exist in the mesh");
        }
        foreach (int faceId in reverseTable[vertId]) {
          if (!HasFace(faceId)) {
            throw new Exception("Face id " + faceId + " in reverse table does not exist in the mesh");
          }
        }
      }
      foreach (int vertId in verticesById.Keys) {
        if (!reverseTable.ContainsKey(vertId)) {
          throw new Exception("Vert id " + vertId + " in mesh is not in reverse table");
        }
      }
      foreach (Face face in facesById.Values) {
        foreach (int vertId in face.vertexIds) {
          if (!reverseTable[vertId].Contains(face.id)) {
            throw new Exception("Face id " + face.id + " in mesh is not in reverse table for vert " + vertId);
          }
        }
      }
    }

// Reads from PolySerializer.
    public MMesh(PolySerializer serializer) {
      serializer.StartReadingChunk(SerializationConsts.CHUNK_MMESH);
      _id = serializer.ReadInt();
      _offset = PolySerializationUtils.ReadVector3(serializer);
      _rotation = PolySerializationUtils.ReadQuaternion(serializer);
      groupId = serializer.ReadInt();

      verticesById = new Dictionary<int, Vertex>();
      facesById = new Dictionary<int, Face>();
      reverseTable = new Dictionary<int, HashSet<int>>();

      // Read vertices.
      int vertexCount = serializer.ReadCount(0, SerializationConsts.MAX_VERTICES_PER_MESH, "vertexCount");
      for (int i = 0; i < vertexCount; i++) {
        int vertexId = serializer.ReadInt();
        Vector3 vertexLoc = PolySerializationUtils.ReadVector3(serializer);
        verticesById[vertexId] = new Vertex(vertexId, vertexLoc);
      }

      // Read faces.
      int faceCount = serializer.ReadCount(0, SerializationConsts.MAX_FACES_PER_MESH, "faceCount");
      for (int i = 0; i < faceCount; i++) {
        int faceId = serializer.ReadInt();
        int materialId = serializer.ReadInt();
        List<int> vertexIds =
          PolySerializationUtils.ReadIntList(serializer, 0, SerializationConsts.MAX_VERTICES_PER_FACE, "vertexIds");
        
        List<Vector3> normals =
          PolySerializationUtils.ReadVector3List(serializer, 0, SerializationConsts.MAX_VERTICES_PER_FACE, "normals");

        // Holes are deprecated.  We read their data but don't do anything with it.
        int holeCount = serializer.ReadCount(0, SerializationConsts.MAX_HOLES_PER_FACE, "holes");
        for (int j = 0; j < holeCount; j++) {
          PolySerializationUtils.ReadIntList(serializer, 0,
            SerializationConsts.MAX_VERTICES_PER_HOLE, "hole vertexIds");
          PolySerializationUtils.ReadVector3List(serializer, 0,
            SerializationConsts.MAX_VERTICES_PER_HOLE, "hole normals");
        }

        // Once normal fixes are backfilled after http://bug we can use the deserialized normals directly.
        facesById[faceId] = new Face(faceId, vertexIds.AsReadOnly(), verticesById, new FaceProperties(materialId));
      }

      serializer.FinishReadingChunk(SerializationConsts.CHUNK_MMESH);

      // If the remix IDs chunk is present (it's optional), read it.
      if (serializer.GetNextChunkLabel() == SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS) {
        serializer.StartReadingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
        remixIds = PolySerializationUtils.ReadStringSet(serializer, 0, SerializationConsts.MAX_REMIX_IDS_PER_MMESH,
          "remixIds");
        serializer.FinishReadingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
      } else {
        // No remix IDs present in file.
        remixIds = null;
      }

      RecalcBounds();
      RecalcReverseTable();
      /// bug - orphan vertices from Zandria models were causing reverseTable lookup failures.  Fixing
      /// by cleaning up orphan vertices on import.
      HashSet<int> orphanVerts = new HashSet<int>();
      foreach (Vertex vert in verticesById.Values) {
        if (!reverseTable.ContainsKey(vert.id)) {
          orphanVerts.Add(vert.id);
        }
      }
      foreach (int vertId in orphanVerts) {
        verticesById.Remove(vertId);
      }
    }

    // Test method.
    public void SetBoundsForTest(Bounds bounds) {
      this.bounds = bounds;
    }
  }
}
