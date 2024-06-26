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

#undef GEOM_OP_VERBOSE_LOGGING

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {
  // Partial to allow GeometryOperation to be an inner class of MMesh without needing to live in MMesh.cs
  public partial class MMesh {
    /// <summary>
    /// Represents and manages an operation on the geometry of a MMesh.  Use pattern is to call MMesh.StartOperation to
    /// begin the operation, the various Add/Modify/Delete methods to compose the operation, and then Commit() to commit
    /// the operation.
    /// Changes are applied in the following order:
    /// 1. Deletion of Faces
    /// 2. Deletion of Vertices
    /// 3. Addition/Modification of Faces
    /// 4. Addition/Modification of Vertices
    /// 5. Update of reverse table
    /// 
    /// No validation of operations (ie, can't create a face using a deleted vertex id) is performed yet, but may be done
    /// in the future (perhaps only in editor mode).
    /// </summary>
    public class GeometryOperation {
      // Has this operation already been committed - an operation should not be committed twice.
      private bool committed;

      // The MMesh the geometry operations this class contains is targeting.
      private MMesh targetMesh;

      // Treat as if friend-classed to MMesh.  Don't directly use from anywhere else.
      public HashSet<int> addedVertices;

      // Treat as if friend-classed to MMesh.  Don't directly use from anywhere else.
      public Dictionary<int, Vertex> modifiedVertices;

      // Treat as if friend-classed to MMesh.  Don't directly use from anywhere else.
      public HashSet<int> deletedVertices;

      // Treat as if friend-classed to MMesh.  Don't directly use from anywhere else.
      public HashSet<int> addedFaces;

      // Treat as if friend-classed to MMesh.  Don't directly use from anywhere else.
      public Dictionary<int, Face> modifiedFaces;

      // Treat as if friend-classed to MMesh.  Don't directly use from anywhere else.
      public HashSet<int> deletedFaces;

      private readonly System.Random random = new System.Random();

      /// <summary>
      /// DO NOT USE THIS CONSTRUCTOR.
      /// 
      /// Unless you're in MMesh, then it's okay.
      /// 
      /// Blame C# for not having anything analagous to package private or friend classes.
      /// </summary>
      /// <param name="targetMesh">The mmesh this operation is targeting.</param>
      public GeometryOperation(MMesh targetMesh) {
        this.targetMesh = targetMesh;
        committed = false;
        addedVertices = new HashSet<int>();
        modifiedVertices = new Dictionary<int, Vertex>();
        deletedVertices = new HashSet<int>();

        addedFaces = new HashSet<int>();
        modifiedFaces = new Dictionary<int, Face>();
        deletedFaces = new HashSet<int>();
      }

      /// <summary>
      /// Gets the position of the specified vertex in model space, using updated data from the GeometryOperation if
      /// possible.
      /// </summary>
      public Vector3 GetCurrentVertexPositionModelSpace(int id) {
        Vertex vertex;
        if (modifiedVertices.TryGetValue(id, out vertex)) {
          return targetMesh.MeshCoordsToModelCoords(vertex.loc);
        }
#if UNITY_EDITOR
        if (deletedVertices.Contains(id)) {
          throw new Exception("Tried to get position of deleted vertex: " + id);
        }
#endif
        return targetMesh.VertexPositionInModelCoords(id);
      }

      /// <summary>
      /// Gets the position of the specified vertex in mesh space, using updated data from the GeometryOperation if
      /// possible.
      /// </summary>
      public Vector3 GetCurrentVertexPositionMeshSpace(int id) {
        Vertex vertex;
        if (modifiedVertices.TryGetValue(id, out vertex)) {
          return vertex.loc;
        }
#if UNITY_EDITOR
        if (deletedVertices.Contains(id)) {
          throw new Exception("Tried to get position of deleted vertex: " + id);
        }
#endif
        return targetMesh.VertexPositionInMeshCoords(id);
      }

      /// <summary>
      /// Gets the Vertex, using updated data from the GeometryOperation if possible;
      /// </summary>
      public Vertex GetCurrentVertex(int id) {
        Vertex vertex;
        if (modifiedVertices.TryGetValue(id, out vertex)) {
          return vertex;
        }
#if UNITY_EDITOR
        if (deletedVertices.Contains(id)) {
          throw new Exception("Tried to get position of deleted vertex: " + id);
        }
#endif
        return targetMesh.verticesById[id];
      }

      /// <summary>
      /// Gets the data for the specified face, using updated data from the GeometryOperation if possible.
      /// </summary>
      public Face GetCurrentFace(int id) {
        Face face;
        if (modifiedFaces.TryGetValue(id, out face)) {
          return face;
        }
#if UNITY_EDITOR
        // Only perform check when in editor.
        if (deletedFaces.Contains(id)) {
          throw new Exception("GetCurrentFace called for deleted face " + id);
        }
#endif

        return targetMesh.GetFace(id);
      }

      /// <summary>
      /// Gets the data for the specified face, using updated data from the GeometryOperation if possible.
      /// </summary>
      public bool TryGetCurrentFace(int id, out Face outFace) {
        outFace = null;

        if (modifiedFaces.TryGetValue(id, out outFace)) {
          return true;
        }
        if (deletedFaces.Contains(id)) {
          return false;
        }

        return targetMesh.TryGetFace(id, out outFace);
      }

      private int GenerateVertexId() {
        return targetMesh.GenerateVertexId(addedVertices);
      }

      private int GenerateFaceId() {
        return targetMesh.GenerateFaceId(addedFaces);
      }

      /// <summary>
      /// Adds a vertex with the given model space position to the mesh.
      /// </summary>
      /// <param name="pos">The model space coordinates of the new vertex.</param>
      public Vertex AddVertexModelSpace(Vector3 pos) {
        Vertex vertex = new Vertex(GenerateVertexId(), targetMesh.ModelCoordsToMeshCoords(pos));
#if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Adding vertex " + vertex.id);
        #endif
        addedVertices.Add(vertex.id);
        modifiedVertices[vertex.id] = vertex;
        return vertex;
      }

      /// <summary>
      /// Adds a vertex with the given mesh space position to the mesh.
      /// </summary>
      /// <param name="pos">The mesh space coordinates of the new vertex.</param>
      public Vertex AddVertexMeshSpace(Vector3 pos) {
        Vertex vertex = new Vertex(GenerateVertexId(), pos);
#if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Adding vertex " + vertex.id);
        #endif

        addedVertices.Add(vertex.id);
        modifiedVertices[vertex.id] = vertex;
        return vertex;
      }

      /// <summary>
      /// Modifies the vertex with the supplied ID, moving it to the supplied model space coordinates.
      /// </summary>
      /// <param name="id">The id of the target vertex.</param>
      /// <param name="pos">The model space coordinates to move the vertex to.</param>
      public Vertex ModifyVertexModelSpace(int id, Vector3 pos) {
#if UNITY_EDITOR
        CheckVertexIsModifiable(id);
#endif
        Vertex vertex = new Vertex(id, targetMesh.ModelCoordsToMeshCoords(pos));
        modifiedVertices[id] = vertex;
        return vertex;
      }

      /// <summary>
      /// Modifies the vertex with the supplied ID, moving it to the supplied mesh space coordinates.
      /// </summary>
      /// <param name="id">The id of the target vertex.</param>
      /// <param name="pos">The mesh space coordinates to move the vertex to.</param>
      public Vertex ModifyVertexMeshSpace(int id, Vector3 pos) {
#if UNITY_EDITOR
        CheckVertexIsModifiable(id);
#endif
        Vertex vertex = new Vertex(id, pos);
        modifiedVertices[id] = vertex;
        return vertex;
      }

      /// <summary>
      /// Modifies the vertex with the id and mesh position contained in the supplied Vertex.
      /// </summary>
      /// <param name="vertex">Vertex containing updated data.</param>
      public void ModifyVertex(Vertex vertex) {
#if UNITY_EDITOR
        CheckVertexIsModifiable(vertex.id);
#endif

        modifiedVertices[vertex.id] = vertex;
      }

      /// <summary>
      /// Modifies the vertices with the ids and mesh space position contained in the supplied Dictionary.
      /// </summary>
      /// <param name="vertices">Dictionary containing id to updated Vertex map.</param>
      public void ModifyVertices(Dictionary<int, Vertex> vertices) {
        foreach (KeyValuePair<int, Vertex> pair in vertices) {
#if UNITY_EDITOR
          CheckVertexIsModifiable(pair.Key);
#endif
          modifiedVertices[pair.Key] = pair.Value;
        }
      }

      /// <summary>
      /// Modifies the vertices with the ids and mesh space position contained in the supplied Vertex enumerable.
      /// </summary>
      /// <param name="vertices">Dictionary containing id to updated Vertex map.</param>
      public void ModifyVertices(Dictionary<int, Vertex>.ValueCollection vertices) {
        foreach (Vertex vertex in vertices) {
#if UNITY_EDITOR
          CheckVertexIsModifiable(vertex.id);
#endif
          modifiedVertices[vertex.id] = vertex;
        }
      }

      /// <summary>
      /// Deletes the vertex with the supplied ID.
      /// </summary>
      public void DeleteVertex(int id) {
#if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Deleting vertex " + id);
        #endif
        deletedVertices.Add(id);
      }

      /// <summary>
      /// Returns the target MMesh for this operation.
      /// </summary>
      public MMesh GetMesh() {
        return targetMesh;
      }

      /// <summary>
      /// Adds a face composed of the supplied vertex indices, and with the supplied properties.
      /// </summary>
      /// <param name="indices">The indices that should form the face</param>
      /// <param name="properties">The face properties of the new face</param>
      public Face AddFace(List<int> indices, FaceProperties properties) {
        Face face = Face.FaceWithPendingNormal(GenerateFaceId(),
          indices.AsReadOnly(),
          properties);
        addedFaces.Add(face.id);
        modifiedFaces[face.id] = face;
        return face;
      }

      /// <summary>
      /// Adds a face composed of the supplied vertex indices, and with the supplied properties.
      /// </summary>
      /// <param name="vertices">The vertices that should form the face</param>
      /// <param name="properties">The face properties of the new face</param>
      public Face AddFace(List<Vertex> vertices, FaceProperties properties) {
        Face face = Face.FaceWithPendingNormal(GenerateFaceId(),
          vertices,
          properties);
        addedFaces.Add(face.id);
        modifiedFaces[face.id] = face;
        return face;
      }

      /// <summary>
      /// Replaces the face with the supplied id, changing its indices and properties to the supplied ones.
      /// </summary>
      /// <param name="id">The face id to change</param>
      /// <param name="indices">The updated indices that should comprise the face</param>
      /// <param name="properties">The updated face properties of the face</param>
      public Face ModifyFace(int id, List<int> indices, FaceProperties properties) {
#if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Modifying face " + id);
        #endif
#if UNITY_EDITOR
        CheckFaceIsModifiable(id);
#endif
        Face face = Face.FaceWithPendingNormal(id,
          indices.AsReadOnly(),
          properties);
        modifiedFaces[id] = face;
        return face;
      }

      /// <summary>
      /// Replaces the face with the supplied id, changing its indices and properties to the supplied ones.
      /// </summary>
      /// <param name="id">The face id to change</param>
      /// <param name="indices">The updated indices that should comprise the face</param>
      /// <param name="properties">The updated face properties of the face</param>
      public Face ModifyFace(int id, ReadOnlyCollection<int> indices, FaceProperties properties) {
#if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Modifying face " + id);
          foreach (int vertId in indices) {
            Debug.Log("   " + vertId);
          }
        #endif
#if UNITY_EDITOR
        CheckFaceIsModifiable(id);
#endif
        Face face = Face.FaceWithPendingNormal(id,
          indices,
          properties);
        modifiedFaces[id] = face;
        return face;
      }

      private void CheckFaceIsModifiable(int id) {
        if (deletedFaces.Contains(id)) {
          throw new Exception("Face " + id + " is in deletedFaces and can't be modified.");
        }
        if (!(addedFaces.Contains(id) || targetMesh.HasFace(id))) {
          throw new Exception("Face " + id + " doesn't exist either in mesh or in added faces.");
        }
      }

      private void CheckVertexIsModifiable(int id) {
        if (deletedVertices.Contains(id)) {
          throw new Exception("Vertex " + id + " is in deletedVertices and can't be modified.");
        }
        if (!(addedVertices.Contains(id) || targetMesh.verticesById.ContainsKey(id))) {
          throw new Exception("Vertex " + id + " doesn't exist either in mesh or in added vertices.");
        }
      }

      /// <summary>
      /// Uses the supplied face to replace the face that has the same id.
      /// </summary>
      public Face ModifyFace(Face face) {
        #if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Modifying face " + face.id + " to");
          foreach (int vertId in face.vertexIds) {
            Debug.Log("   " + vertId);
          }
        #endif
        #if UNITY_EDITOR
          CheckFaceIsModifiable(face.id);
        #endif
        modifiedFaces[face.id] = face;
        return face;
      }

      /// <summary>
      /// Deletes the face with the supplied id.
      /// </summary>
      public void DeleteFace(int id) {
        #if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Deleting face " + id);
        #endif
        deletedFaces.Add(id);
      }

      /// <summary>
      /// Commits a geometry operation targeting this mesh.
      /// </summary>
      /// <returns></returns>  
      private void CommitOperation(bool recalculateNormals) {
        // Until all tools cause the reverse table to be updated - it will enter inaccurate
        // states.  Until then, we need to disable updates and use of it (while still letting allowing us to build out and
        // test its functionality for controlled cases).

        #if UNITY_EDITOR
          if (!targetMesh.operationInProgress) {
            throw new Exception("Attempted to commit operation when mesh has no operation in progress.");
          }
        #endif

        foreach (int id in deletedFaces) {
          foreach (int vertIndex in targetMesh.facesById[id].vertexIds) {
            #if GEOM_OP_VERBOSE_LOGGING
              Debug.Log("RT Update: Removing face " + id + " from vert " + vertIndex);
            #endif
            targetMesh.reverseTable[vertIndex].Remove(id);
          }
          
          targetMesh.facesById.Remove(id);
        }

        foreach (int id in deletedVertices) {
          targetMesh.verticesById.Remove(id);
          #if GEOM_OP_VERBOSE_LOGGING
            Debug.Log("RT Update: Removing vertex " + id);
          #endif
          targetMesh.reverseTable.Remove(id);
          
        }

        if (modifiedFaces.Count > 0 || modifiedVertices.Count > 0) {
          HashSet<int> faceIdsToRecalc = new HashSet<int>();

          // Handle new and modified faces
          foreach (KeyValuePair<int, Face> pair in modifiedFaces) {
            Face oldFace;
            if (targetMesh.TryGetFace(pair.Key, out oldFace)) {
              foreach (int vertIndex in oldFace.vertexIds) {
                if(deletedVertices.Contains(vertIndex)) continue;
                #if GEOM_OP_VERBOSE_LOGGING
                  Debug.Log("RT Update: Removing face " + pair.Key + " from vert " + vertIndex);
                #endif
                targetMesh.reverseTable[vertIndex].Remove(pair.Key);
              }
            }
            
            targetMesh.facesById[pair.Key] = pair.Value;
            
            foreach (int vertIndex in pair.Value.vertexIds) {
              HashSet<int> faceSet;
              if (!targetMesh.reverseTable.TryGetValue(vertIndex, out faceSet)) {
                faceSet = new HashSet<int>();
                #if GEOM_OP_VERBOSE_LOGGING
                  Debug.Log("RT Update: Adding vertex " + vertIndex);
                #endif
                targetMesh.reverseTable[vertIndex] = faceSet;
              }
              #if GEOM_OP_VERBOSE_LOGGING
                Debug.Log("RT Update: Adding face " + pair.Key + " to vert " + vertIndex);
              #endif
              faceSet.Add(pair.Key);
              
            }
            faceIdsToRecalc.Add(pair.Key);
          }

          // Handle new and modified verts
          foreach (KeyValuePair<int, Vertex> pair in modifiedVertices) {
            #if GEOM_OP_VERBOSE_LOGGING
              Debug.Log("RT Update: Modifying vertex " + pair.Key);
            #endif
            targetMesh.verticesById[pair.Key] = pair.Value;
            foreach (int faceId in targetMesh.reverseTable[pair.Key]) {
              faceIdsToRecalc.Add(faceId);
            }
          }

          foreach (int faceId in faceIdsToRecalc) {
            Face face = targetMesh.facesById[faceId];
            face.InvalidateVertexCache();
            if (recalculateNormals) {
              face.RecalculateNormal(targetMesh.verticesById);
            }
          }
        }

        #if UNITY_EDITOR
        try {
        #endif
          targetMesh.FinishOperation();
        #if UNITY_EDITOR
        }
        catch (Exception ex) {
          DumpOperation();
          throw ex;
        }
        #endif
      }

      /// <summary>
      /// Commits the changes in this geometry operation to the target MMesh.  Should only be called once.
      /// </summary>
      /// <exception cref="Exception">If in Unity Editor, this can throw an exception if the operation has already been
      /// committed.</exception>
      public void Commit() {
        if (!committed) {
        #if GEOM_OP_VERBOSE_LOGGING
          Debug.Log("Mesh " + targetMesh.id + " commit info.");
          foreach (Vertex vert in targetMesh.verticesById.Values) {
            Debug.Log("Mesh has vert " + vert.id);
          }
          
          foreach (Face face in targetMesh.facesById.Values) {
            Debug.Log("Mesh has face " + face.id);
          }
          
          targetMesh.PrintReverseTable();
          
          
          foreach (Vertex vert in modifiedVertices.Values) {
            Debug.Log("Modification has vert " + vert.id);
          }
          #endif

          CommitOperation(true /* recalculateNormals */);
          return;
        }
        #if UNITY_EDITOR
          throw new Exception("Attemped to commit an already committed GeometryOperation.");
        #endif
      }

      /// <summary>
      /// Commits the changes in this geometry operation to the target MMesh without triggering recalculation for modified
      /// faces.  Should only be called once.
      /// </summary>
      public void CommitWithoutRecalculation() {
        if (!committed) {
          CommitOperation(false /* recalculateNormals */);
          return;
        }
        #if UNITY_EDITOR
          throw new Exception("Attemped to commit an already committed GeometryOperation.");
        #endif
      }

      private void DumpOperation() {
        foreach (int vertId in addedVertices) {
          Debug.Log("  adding vert: " + vertId);
        }
        foreach (int vertId in deletedVertices) {
          Debug.Log("  deleting vert: " + vertId);
        }
        foreach (int vertId in modifiedVertices.Keys) {
          Debug.Log("  modifying vert: " + vertId + " to " + MeshUtil.Vector3ToString(modifiedVertices[vertId].loc));
        }
        
        foreach (int faceId in addedFaces) {
          Debug.Log("  adding face: " + faceId);
        }
        foreach (int faceId in deletedFaces) {
          Debug.Log("  deleting face: " + faceId);
        }
        foreach (int faceId in modifiedFaces.Keys) {
          Debug.Log("  modifying face: " + faceId + " to :");
          String outString = "[";
          foreach (int vertId in modifiedFaces[faceId].vertexIds) {
            outString += vertId + ", ";
          }
          outString += "]";
          Debug.Log("    " + outString);
        }
      }
    }
  }
}