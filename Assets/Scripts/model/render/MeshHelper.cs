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
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.import;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.render {
  public class MeshGenContext {
    public List<Vector3> verts = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Color32> colors = new List<Color32>();
    public List<Vector3> normals = new List<Vector3>();
  }

  /// <summary>
  ///   Helper methods for MeshRendering.
  /// </summary>
  public class MeshHelper {
    private static readonly System.Random RANDOM = new System.Random();

    /// <summary>
    ///   The material to be used for highlighted meshes.
    /// </summary>
    public static MaterialAndColor highlightSilhouetteMaterial;
    /// <summary>
    ///   All rendered vertices will be perturbed by a random amount no greater than this constant, in all three
    ///   dimensions. This amount should be imperceptible. This is to mitigate the effects of z-fighting.
    /// </summary>
    private const float wiggleRoom = 0.00004f;

    /// <summary>
    ///   Gets the components (triangles, and verts, by triangulating) from an MMesh on a background thread, then calls
    ///   a given callback.
    /// </summary>
    private class GetComponentsFromMeshesWork : BackgroundWork {
      List<Dictionary<int, MeshGenContext>> output;
      IEnumerable<MMesh> meshes;
      bool useModelSpace;
      Action<List<Dictionary<int, MeshGenContext>>> callback;

      public GetComponentsFromMeshesWork(IEnumerable<MMesh> meshes, bool useModelSpace,
        Action<List<Dictionary<int, MeshGenContext>>> callback) {
        this.meshes = meshes;
        this.useModelSpace = useModelSpace;
        this.callback = callback;
      }

      public void BackgroundWork() {
        output = MeshComponentsForMenu(meshes);
      }

      public void PostWork() {
        callback(output);
      }
    }

    /// <summary>
    ///   Creates a Unity Mesh that draws an MMesh.
    /// </summary>
    /// <param name="mmesh">The MMesh.</param>
    /// <param name="useModelSpace">Whether to take model-space co-ordinates from the MMesh.</param>
    /// <param name="materialOverride">An override material to use for the entire generated Mesh, if desired.</param>
    /// <returns>The Unity Mesh.</returns>
    public static List<MeshWithMaterial> MeshFromMMesh(MMesh mmesh, bool useModelSpace,
      out Dictionary<int, MeshGenContext> components, MaterialAndColor materialOverride = null) {
      components = MeshComponentsFromMMesh(mmesh, useModelSpace);
      return ToMeshes(components, materialOverride);
    }

    /// <summary>
    ///   Generates the components of a Unity Mesh that represents a group of MMeshes, doing its work on the
    ///   background thread and calling the given callback once finished.
    /// </summary>
    private static void ComponentsFromMMeshesOnBackground(IEnumerable<MMesh> mmeshes, bool useModelSpace,
      Action<List<MeshWithMaterial>> callback) {
      PeltzerMain.Instance.DoBackgroundWork(new GetComponentsFromMeshesWork(mmeshes, useModelSpace,
        (List<Dictionary<int, MeshGenContext>> output) => {
          List<MeshWithMaterial> allMeshes = new List<MeshWithMaterial>();
          foreach (Dictionary<int, MeshGenContext> setOfMeshes in output) {
            allMeshes.AddRange(ToMeshes(setOfMeshes));
          }
          callback(allMeshes);
        }));
    }

    /// <summary>
    ///   Update the positions and recalculate bounds and normals for a mesh that hasn't changed geometry.
    /// </summary>
    /// <param name="updatedMesh">The MMesh.</param>
    /// <param name="existing">
    ///   Existing list of MeshWithMaterials from a previous meshFromMMesh call.
    /// </param>
    /// <returns>The Unity Mesh.</returns>
    public static void UpdateMeshes(MMesh updatedMesh, List<MeshWithMaterial> existing) {
      Vector3 wiggleVector = RandomWiggleVector();

      // If the MMesh has some opaque faces and some transparent faces, we also want to draw
      // the inside of all of the opaque faces.
      bool hasMixedFaces = HasMixedFaces(updatedMesh);

      // Simpler version that replicates adding vertices in the same order so indices match up.
      Dictionary<MaterialAndColor, List<Vector3>> newPositionsPerMaterial = new Dictionary<MaterialAndColor, List<Vector3>>();
      foreach (Face face in updatedMesh.GetFaces()) {
        List<Vector3> newPos;
        List<Color32> newColors = new List<Color32>();
        List<Vector3> newNormals = new List<Vector3>();
        MaterialAndColor faceMaterialAndColor = MaterialRegistry.GetMaterialAndColorById(face.properties.materialId);
        if (!newPositionsPerMaterial.TryGetValue(faceMaterialAndColor, out newPos)) {
          newPositionsPerMaterial[faceMaterialAndColor] = new List<Vector3>();
          newPos = newPositionsPerMaterial[faceMaterialAndColor];
        }
        bool drawTriangleBackside = hasMixedFaces
          && !MaterialRegistry.IsMaterialTransparent(face.properties.materialId);
        // This method is used to update a GameObject, and as such we do not want the vert positions in world space,
        // it is the gameObject that will be placed and rotated in the world.
        AddFaceVertices(updatedMesh, wiggleVector, face, ref newPos, ref newColors, ref newNormals, 
          /* useWorldSpace */ false);

        if (drawTriangleBackside) {
          // This method is used to update a GameObject, and as such we do not want the vert positions in world space,
          // it is the gameObject that will be placed and rotated in the world.
          AddFaceVertices(updatedMesh, wiggleVector, face, ref newPos, ref newColors, ref newNormals,
            /* useWorldSpace */ false);
        }
      }

      // Go through the existing meshes and update positions.
      foreach (MeshWithMaterial uMesh in existing) {
        List<Vector3> newPos;
        if (!newPositionsPerMaterial.TryGetValue(uMesh.materialAndColor, out newPos) ||
            newPos.Count != uMesh.mesh.vertices.Count()) {
          // If materials changed, easiest action is to remesh everything.
          existing.Clear();
          // This method is used to update a GameObject, and as such we do not want the vert positions in model space,
          // it is the gameObject that will be placed and rotated in the world.
          foreach (MeshWithMaterial matMesh in ToMeshes(MeshComponentsFromMMesh(updatedMesh,
            /* useModelSpace */ false))) {
            existing.Add(matMesh);
          }
          return;
        }
        uMesh.mesh.SetVertices(newPos);
        uMesh.mesh.RecalculateBounds();
        uMesh.mesh.RecalculateNormals();
      }
    }

    /// <summary>
    ///   Fetches the components (triangles, verts, vertex colours) for a Unity mesh from a given group of MMeshes,
    ///   optionally using world-space positions (else model-space positions).  This method should only be used for
    ///   meshes displayed in the menu - all other uses should go down one of the mesh-space only paths for performance.
    ///   Guarantees that the components in any given dictionary in the output will fit into a Unity mesh.
    /// </summary>
    private static List<Dictionary<int, MeshGenContext>> MeshComponentsForMenu(IEnumerable<MMesh> mmeshes) {
      List<Dictionary<int, MeshGenContext>> allMeshContexts = new List<Dictionary<int, MeshGenContext>>();
      foreach (MMesh mmesh in mmeshes) {
        // First, fetch the components for this mesh, keyed by material.
        Dictionary<int, MeshGenContext> contextByMaterialId = InternalMeshComponentsFromMMesh(mmesh, 
          useModelSpace:true);

        // Then, for each material, find an available dictionary for its components.
        foreach (KeyValuePair<int, MeshGenContext> pair in contextByMaterialId) {
          int material = pair.Key;
          MeshGenContext newContext = pair.Value;
          bool addedNewContext = false;

          foreach (Dictionary<int, MeshGenContext> contextDict in allMeshContexts) {
            if (!contextDict.ContainsKey(material)) {
              // If this dictionary has no entries for this material, add the new context and end the search.
              contextDict.Add(material, newContext);
              break;
            }

            MeshGenContext existingContext = contextDict[material];
            if (existingContext.verts.Count + newContext.verts.Count > ReMesher.MAX_VERTS_PER_MESH) {
              // If adding the new context to this dictionary would exceed the limits of 
              // a Unity mesh, continue searching.
              continue;
            } else {
              // Else, if this new context fits into this dictionary, add it and end the search.
              CombineContexts(newContext, existingContext);
              addedNewContext = true;
              break;
            }
          }

          if (!addedNewContext) {
            // If no existing dictionary was able to hold this new context, create a new one and populate it with
            // this entry.
            Dictionary<int, MeshGenContext> contextDict = new Dictionary<int, MeshGenContext>();
            contextDict[material] = newContext;
            allMeshContexts.Add(contextDict);
          }
        }
      }
      return allMeshContexts;
    }

    /// <summary>
    ///   Add vertices and triangles to a MeshInfo.  This method assumes we've ensure there is "room" in the
    ///   MeshInfo for the new components.
    /// </summary>
    public static void CombineContexts(MeshGenContext source, MeshGenContext target) {
      int curSize = target.verts.Count;
      target.verts.AddRange(source.verts);
      target.normals.AddRange(source.normals);
      target.colors.AddRange(source.colors);
      for (int i = 0; i < source.triangles.Count; i++) {
        target.triangles.Add(source.triangles[i] + curSize);
      }
    }

    /// <summary>
    ///   Triangulates a mesh and returns its faces and vertices in a dictionary keyed by material id.
    /// </summary>
    /// <param name="mmesh">The input mesh</param>
    /// <param name="useModelSpace">
    ///   If true, the vertex locations in the dictionary will be in model space, else they'll be in mesh space.
    /// </param>
    /// <returns></returns>
    public static Dictionary<int, MeshGenContext> MeshComponentsFromMMesh(MMesh mmesh, bool useModelSpace) {
      return InternalMeshComponentsFromMMesh(mmesh, useModelSpace);
    }


    private static Dictionary<int, MeshGenContext> InternalMeshComponentsFromMMesh(MMesh mmesh, bool useModelSpace) {
      Dictionary<int, MeshGenContext> contextByMaterialId = new Dictionary<int, MeshGenContext>();
      Vector3 wiggleVector = RandomWiggleVector();

      // If the MMesh has some opaque faces and some transparent faces, we also want to draw
      // the inside of all of the opaque faces.
      bool hasMixedFaces = HasMixedFaces(mmesh);

      foreach (Face face in mmesh.GetFaces()) {
        int materialId = face.properties.materialId;
        MeshGenContext context;
        if (!contextByMaterialId.TryGetValue(face.properties.materialId, out context)) {
          context = new MeshGenContext();
          contextByMaterialId[materialId] = context;
        }

        // Should we also draw the inside of the faces:
        bool drawTriangleBackside = hasMixedFaces
          && !MaterialRegistry.IsMaterialTransparent(face.properties.materialId);

        int offset = context.verts.Count;
        int backOffset = -1;

        AddFaceVertices(mmesh, wiggleVector, face, ref context.verts, ref context.colors, ref context.normals,
          useModelSpace);

        if (drawTriangleBackside) {
          // The back side of the face will have different vertex normals,
          // so we need to make new vertices for those triangles:
          backOffset = context.verts.Count;
          AddFaceVertices(mmesh, wiggleVector, face, ref context.verts, ref context.colors, ref context.normals,
            useModelSpace);
        }
  
        List<Triangle> tris = face.GetRenderTriangulation(mmesh);
        foreach (Triangle tri in tris) {
          context.triangles.Add(tri.vertId0 + offset);
          context.triangles.Add(tri.vertId1 + offset);
          context.triangles.Add(tri.vertId2 + offset);
          if (drawTriangleBackside) {
            // Changing the order of the triangle vertices will draw them on the other side:
            context.triangles.Add(tri.vertId0 + backOffset);
            context.triangles.Add(tri.vertId2 + backOffset);
            context.triangles.Add(tri.vertId1 + backOffset);
          }
        }
      }

      return contextByMaterialId;
    }

    private static bool HasMixedFaces(MMesh mmesh) {
      bool hasOpaqueFaces = false;
      bool hasTransparentFaces = false;

      foreach (Face face in mmesh.GetFaces()) {
        if (MaterialRegistry.IsMaterialTransparent(face.properties.materialId)) {
          hasTransparentFaces = true;
        } else {
          hasOpaqueFaces = true;
        }
      }

      return hasOpaqueFaces && hasTransparentFaces;
    }

    /// <summary>
    ///   Adds the locations of the vertices of a given face to a given list, applying a 'wiggle' to them to 
    ///   avoid z-fighting.
    /// </summary>
    /// <param name="mmesh">The mesh containing the face</param>
    /// <param name="wiggleVector">The wiggle to avoid z-fighting</param>
    /// <param name="face">The face with the verts to be added</param>
    /// <param name="vertList">The vert list to which the verts will be added</param>
    /// <param name="colorList">The color list to which colors will be added</param>
    /// <param name="normalList">The normal list to which the normals will be added</param>
    /// <param name="useModelSpace">
    ///   If true, the added vertex locations will be in model space, else they'll be in mesh space.
    /// </param>
    private static void AddFaceVertices(MMesh mmesh, 
      Vector3 wiggleVector, 
      Face face, 
      ref List<Vector3> vertList, 
      ref List<Color32> colorList, 
      ref List<Vector3> normalList, 
      bool useModelSpace) {
      
      if (useModelSpace) {
        List<Vector3> meshSpaceVerts = face.GetMeshSpaceVertices(mmesh);
        for (int i = 0; i < meshSpaceVerts.Count; i++) {
          vertList.Add((mmesh.rotation * meshSpaceVerts[i]) + mmesh.offset);
        }
      }
      else {
        vertList.AddRange(face.GetMeshSpaceVertices(mmesh));
      }
      colorList.AddRange(face.GetColors());
      normalList.AddRange(face.GetRenderNormals(mmesh));
    }

    /// <summary>
    ///   Generates a GameObject which looks identical to an MMesh.
    /// </summary>
    /// <param name="worldSpace">The transform to the world's co-ordinate system</param>
    /// <param name="mesh">The mesh to imitate with a GameObject</param>
    /// <param name="materialOverride">If passed, an override of the mesh's current material</param>
    /// <returns>The imitation of the mesh as a GameObject</returns>
    public static GameObject GameObjectFromMMesh(WorldSpace worldSpace, MMesh mesh, MaterialAndColor materialOverride = null) {
      // Set up a GameObject and attach it to the GameObject for previewing.
      GameObject meshHighlight = new GameObject();
      MMesh.AttachMeshToGameObject(
        worldSpace, meshHighlight, mesh, /* updateOnly */ false, materialOverride);

      return meshHighlight;
    }

    /// <summary>
    ///   A helper method for rendering a group of MMeshes.  Takes a GameObject ensures it has a
    ///   MeshWithMaterialRenderer and adds the Meshes corresponding to the MMeshes and a Script that will draw them.
    /// </summary>
    private static void AttachMeshesToGameObject(
        WorldSpace worldSpace, GameObject gameObject, List<MMesh> meshes, Action callback,
        bool updateOnly = false) {
      // Add renderer to GameObject.
      MeshWithMaterialRenderer renderer = gameObject.AddComponent<MeshWithMaterialRenderer>();
      renderer.Init(worldSpace);
      renderer.meshes = new List<MeshWithMaterial>();

      // Position the gameObject so that the mesh appears to have the correct position.
      Vector3 centroid = Math3d.FindCentroid(meshes);
      renderer.SetPositionModelSpace(centroid);

      foreach (MMesh mesh in meshes) {
        Vector3 offsetFromCentroid = mesh.offset - centroid;
        mesh.offset = gameObject.transform.position + offsetFromCentroid;
      }

      ComponentsFromMMeshesOnBackground(meshes, /* useModelSpace */ false, (List<MeshWithMaterial> output) => {
        renderer.meshes = output;

        if (callback != null) {
          callback();
        }
      });
    }
    
    /// <summary>
    ///   Generates a GameObject which looks identical to a group of MMeshes. Calls back with the GameObject after the
    ///   MMeshes have been attached to it on a background thread.  This method should only be used for
    ///   meshes displayed in the menu - all other uses should go down one of the mesh-space only paths for performance.
    /// </summary>
    /// <param name="worldSpace">The transform to the world's co-ordinate system.</param>
    /// <param name="meshes">The meshes to imitate with a GameObject.</param>
    /// <param name="materialOverride">If passed, an override of the mesh's current material</param>
    /// <param name="callback">The callback for the game object.</param>
    public static void GameObjectFromMMeshesForMenu(WorldSpace worldSpace, List<MMesh> meshes,
      Action<GameObject> callback, MaterialAndColor materialOverride = null) {
      // Set up a GameObject and attach it to the GameObject for previewing.
      GameObject meshHighlight = new GameObject();
      AttachMeshesToGameObject(worldSpace, meshHighlight, meshes, delegate () {
        callback(meshHighlight);
      });
    }

    /// <summary>
    /// Create an mmesh from a set of meshes
    /// </summary>
    /// <param name="id">id for new mesh</param>
    /// <param name="meshes">the meshes to construct the mmesh from</param>
    /// <returns></returns>
    public static MMesh MMeshFromMeshes(int id, Dictionary<Material, List<MeshVerticesAndTriangles>> materialsAndMeshes) {
      Dictionary<int, Vertex> verticesById = new Dictionary<int, Vertex>();
      Dictionary<int, Face> facesById = new Dictionary<int, Face>();
      int vIdx = 0;
      int faceIdx = 0;
      foreach (KeyValuePair<Material, List<MeshVerticesAndTriangles>> pair in materialsAndMeshes) {
        Material material = pair.Key;
        foreach (MeshVerticesAndTriangles meshVerticesAndTriangles in pair.Value) {
          Dictionary<int, Vertex> meshVertices = new Dictionary<int, Vertex>();
          for (int i = 0; i < meshVerticesAndTriangles.meshVertices.Length; i++) {
            Vector3 v = meshVerticesAndTriangles.meshVertices[i];
            int vertexId = vIdx++;
            Vertex vertex = new Vertex(vertexId, v);
            verticesById.Add(vertexId, vertex);
            meshVertices.Add(i, vertex);
          }
          for (int triangleIdxIdx = 0; triangleIdxIdx < meshVerticesAndTriangles.triangles.Length; triangleIdxIdx += 3) {
            int idx1 = meshVerticesAndTriangles.triangles[triangleIdxIdx];
            int idx2 = meshVerticesAndTriangles.triangles[triangleIdxIdx + 1];
            int idx3 = meshVerticesAndTriangles.triangles[triangleIdxIdx + 2];

            int newFaceId = faceIdx++;
            Face face = new Face(newFaceId, new List<int>() {
              meshVertices[idx1].id,
              meshVertices[idx2].id,
              meshVertices[idx3].id,
            }.AsReadOnly(), verticesById, new FaceProperties(TryGetMaterialId(material)));
            facesById.Add(newFaceId, face);
          }
        }
      }
      return new MMesh(id, Vector3.zero, Quaternion.identity, verticesById, facesById);
    }

    private static int TryGetMaterialId(Material material) {
      if (material.name.StartsWith("mat")) {
        int val = 1;
        if (int.TryParse(material.name.Substring(3), out val)) {
          return val;
        }
      }
      return 1;
    }

    private static Vector3 RandomWiggleVector() {
      return new Vector3(
        ((float)RANDOM.NextDouble() * 2f - 1f) * wiggleRoom,
        ((float)RANDOM.NextDouble() * 2f - 1f) * wiggleRoom,
        ((float)RANDOM.NextDouble() * 2f - 1f) * wiggleRoom);
    }

    public static List<MeshWithMaterial> ToMeshes(Dictionary<int, MeshGenContext> contextByMaterialId,
      MaterialAndColor materialOverride = null, 
      Quaternion? rotationalOffset = null, Vector3? positionalOffset = null) {
      List<MeshWithMaterial> meshes = new List<MeshWithMaterial>(contextByMaterialId.Count);
      foreach (KeyValuePair<int, MeshGenContext> pair in contextByMaterialId) {
        int materialId = pair.Key;
        MeshGenContext context = pair.Value;
        Mesh mesh = new Mesh();

        // Offset the vertices, if required.
        if (rotationalOffset != null && positionalOffset != null) {
          List<Vector3> vertsInMeshSpace = new List<Vector3>(context.verts.Count);
          Quaternion invertedRotationalOffset = Quaternion.Inverse(rotationalOffset.Value);
          foreach (Vector3 vertInModelSpace in context.verts) {
            vertsInMeshSpace.Add(invertedRotationalOffset * (vertInModelSpace - positionalOffset.Value));
          }
          mesh.SetVertices(vertsInMeshSpace);
        } else {
          mesh.SetVertices(context.verts);
        }
        mesh.SetNormals(context.normals);
        mesh.SetTriangles(context.triangles, /* Submesh */ 0);
        mesh.RecalculateBounds();

        // Add vertex colors.
        MaterialAndColor materialAndColor = MaterialRegistry.GetMaterialAndColorById(materialId);
        Color32[] colors = new Color32[context.verts.Count];
        Color32 color = materialOverride == null ? materialAndColor.color : materialOverride.color;
        for (int i = 0; i < colors.Length; i++) {
          colors[i] = color;
        }
        mesh.colors32 = colors;

        meshes.Add(new MeshWithMaterial(mesh, materialAndColor));
      }
      return meshes;
    }

    /// <summary>
    /// Returns a list of Unity meshes corresponding to the given MMesh.
    /// </summary>
    /// <returns></returns>
    public static List<Mesh> ToUnityMeshes(MeshRepresentationCache cache, MMesh mesh) {
      return ToUnityMeshes(cache.FetchComponentsForMesh(mesh));
    }

    private static List<Mesh> ToUnityMeshes(Dictionary<int, MeshGenContext> contextByMaterialId) {
      List<Mesh> meshes = new List<Mesh>(contextByMaterialId.Count);
      foreach (KeyValuePair<int, MeshGenContext> pair in contextByMaterialId) {
        int materialId = pair.Key;
        MeshGenContext context = pair.Value;
        Mesh mesh = new Mesh();
        mesh.SetVertices(context.verts);
        mesh.SetTriangles(context.triangles, /* Submesh */ 0);
        mesh.SetNormals(context.normals);
        mesh.SetColors(context.colors);
        mesh.RecalculateBounds();


        // Add vertex colors.

        meshes.Add(mesh);
      }
      return meshes;
    }

    public static bool IsQuadFaceConvex(MMesh mesh, Face face) {
      AssertOrThrow.True(face.vertexIds.Count == 4, "IsQuadFaceConvex can only be used in quads.");
      Vector3 a = mesh.VertexPositionInMeshCoords(face.vertexIds[0]);
      Vector3 b = mesh.VertexPositionInMeshCoords(face.vertexIds[1]);
      Vector3 c = mesh.VertexPositionInMeshCoords(face.vertexIds[2]);
      Vector3 d = mesh.VertexPositionInMeshCoords(face.vertexIds[3]);
      Vector3 normal = MeshMath.CalculateNormal(a, b, c);
      // Vertices are in clockwise order:
      //    a +-------------+ b
      //     /               \
      //    /                 \
      // d +-------------------+ c
      // Check if each one is convex:
      return Math3d.IsConvex(/*check*/a, /*prev*/d, /*next*/b, normal) &&
        Math3d.IsConvex(/*check*/b, /*prev*/a, /*next*/c, normal) &&
        Math3d.IsConvex(/*check*/c, /*prev*/b, /*next*/d, normal) &&
        Math3d.IsConvex(/*check*/d, /*prev*/c, /*next*/a, normal);
    }

    public static List<Triangle> TriangulateFace(MMesh mesh, Face face) {
      return face.GetRenderTriangulation(mesh);
    }

    /// <summary>
    /// Creates triangles for the vertices of a face.
    /// </summary>
    /// <param name="numberOfVertices">Number of vertices of the face.</param>
    /// <returns>List of indices representing the triangles for the face.</returns>
    public static List<int> GetTrianglesAsFan(int numberOfVertices) {
      List<int> triangles = new List<int>();
      for (int i = 1; i < (numberOfVertices - 1); i++) {
        triangles.Add(0);
        triangles.Add(i);
        triangles.Add(i + 1);
      }
      return triangles;
    }
  }
}
