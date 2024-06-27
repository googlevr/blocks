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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;
using System.Collections.Generic;
using System.IO;

using System.Text;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.export {
  /// <summary>
  ///   Simple Obj file exporter.
  /// </summary>
  public class ObjFileExporter {
    public static string RandomOpaqueId() {
      StringBuilder sb = new StringBuilder("");
      System.Random r = new System.Random();
      // 300,000,000 combinations.  Hopefully no collisions :)
      for (int i = 0; i < 6; i++) {
        sb.Append((char)('a' + r.Next(26)));
      }
      return sb.ToString();
    }

    /// <summary>
    ///   Generate an MTL for our materials from a set of material ids.
    /// </summary>
    /// <returns></returns>
    public static byte[] MtlFileFromSet(HashSet<int> materialIds) {
      MemoryStream stream = new MemoryStream();
      StreamWriter sw = new StreamWriter(stream);

      foreach (int matId in materialIds) {
        // Don't try and export wireframes, bug
        if (matId == MaterialRegistry.GREEN_WIREFRAME_ID || matId == MaterialRegistry.PINK_WIREFRAME_ID) {
          continue;
        }
        sw.WriteLine("newmtl mat" + matId);
        if (matId == MaterialRegistry.GLASS_ID || matId == MaterialRegistry.GEM_ID) {
          if (matId == MaterialRegistry.GLASS_ID) {
            sw.WriteLine("  Ka 0.58 0.65 1.00");
          } else if (matId == MaterialRegistry.GEM_ID) {
            sw.WriteLine("  Ka 1.00 0.65 0.67");
          }

          sw.WriteLine("  Kd 0.92 0.95 0.94");
          sw.WriteLine("  Ks 1 1 1");
          sw.WriteLine("  illum 9");
          sw.WriteLine("  Ns 300");
          sw.WriteLine("  d 0.4");
          sw.WriteLine("  Ni 1.5");
        } else {
          Color c = MaterialRegistry.GetMaterialColorById(matId);
          sw.WriteLine("  Kd " + c.r.ToString("0.00") + " " + c.g.ToString("0.00") + " " + c.b.ToString("0.00"));
        }
        sw.WriteLine("");
      }

      sw.Close();

      return stream.ToArray();
    }

    // These are two vectors that don't point in the same direction - meaning that at least one of them won't be
    // parallel to any given face normal.  We use these to generate UVs using tangent space coordinates.
    private static Vector3 arbitraryVector;
    private static Vector3 alternateArbitraryVector;

    private class MeshExportInfo {
      public int meshId;
      public List<int> meshFaceColors;
      public List<int[]> meshFaceVerts;
      public List<int> meshNormals;
      public List<int[]> meshFaceUvs;

      public MeshExportInfo(int id) {
        meshId = id;
        meshFaceColors = new List<int>();
        meshFaceVerts = new List<int[]>();
        meshFaceUvs = new List<int[]>();
        meshNormals = new List<int>();
      }
    }

    /// <summary>
    ///   Write collection of meshes into an obj file (as a byte array). The vertex coordinates in the OBJ file will be
    ///   generated such that the centroid of the given meshes is at the origin. This addresses the problem of OBJ files
    ///   having seemingly arbitrary origin points that are sometimes outside the object, making it difficult
    ///   to import/position them in Unity and other tools.
    ///
    ///   Sets the output bytes and polyCount as out parameters.
    /// </summary>
    public static void ObjFileFromMeshes(ICollection<MMesh> meshes, string mtlFileName,
      MeshRepresentationCache meshRepresentationCache, ref HashSet<int> materials, bool triangulated,
      out byte[] bytes, out int polyCount) {
      List<Vector3> vertices = new List<Vector3>();
      List<Vector2> uvs = new List<Vector2>();
      Dictionary<int, MeshExportInfo> exportInfos = new Dictionary<int, MeshExportInfo>();
      List<Vector3> polyNormals = new List<Vector3>();
      polyCount = 0;

      // Initialize arbitrary vectors once per export. Only criteria is that they are not parallel.
      arbitraryVector = new Vector3(0.42f, -0.21f, 0.15f).normalized;
      alternateArbitraryVector = new Vector3(0.43f, 1.5f, 0.15f).normalized;

      MemoryStream stream = new MemoryStream();
      StreamWriter sw = new StreamWriter(stream);

      StringBuilder sb = new StringBuilder("mtllib ");
      sb.Append(mtlFileName);
      sw.WriteLine(sb.ToString());

      HashSet<MMesh> ungroupedMeshes = new HashSet<MMesh>();
      Dictionary<int, List<MMesh>> groupedMeshes = new Dictionary<int, List<MMesh>>();
      foreach (MMesh mesh in meshes) {
        if (mesh.groupId != MMesh.GROUP_NONE) {
          if (!groupedMeshes.ContainsKey(mesh.groupId)) {
            groupedMeshes[mesh.groupId] = new List<MMesh>();
          }
          groupedMeshes[mesh.groupId].Add(mesh);
        }
        else {
          ungroupedMeshes.Add(mesh);
        }
      }

      foreach (MMesh mesh in meshes) {
        MeshExportInfo info = new MeshExportInfo(mesh.id);
        info.meshFaceVerts = new List<int[]>();
        if (triangulated) {
          AddTriangulatedMesh(mesh, meshRepresentationCache, ref vertices, ref uvs, ref info, ref polyNormals,
            ref materials, ref polyCount);
        } else {
          AddMesh(mesh, ref vertices, ref uvs, ref info, ref polyNormals, ref materials, ref polyCount);
        }
        exportInfos[mesh.id] = info;
      }

      Vector3 centroid = Math3d.FindCentroid(meshes);
      for (int i = 0; i < vertices.Count; i++) {
        // Translate vertex such that centroid is at the origin. To do this, all we have to do is subtract the
        // centroid position from the vertex coordinates.
        Vector3 vert = vertices[i] - centroid;

        // Swap X for OBJ file(?)
        sb = new StringBuilder("v ");
        sb.Append(-vert.x).Append(" ").Append(vert.y).Append(" ").Append(vert.z);
        sw.WriteLine(sb.ToString());
      }
      for (int i = 0; i < polyNormals.Count; i++) {
        Vector3 polyNormal = polyNormals[i];

        // Swap X for OBJ file(?)
        sb = new StringBuilder("vn ");
        sb.Append(-polyNormal.x).Append(" ").Append(polyNormal.y).Append(" ").Append(polyNormal.z);
        sw.WriteLine(sb.ToString());
      }
      for (int i = 0; i < uvs.Count; i++) {
        Vector2 uv = uvs[i];

        sb = new StringBuilder("vt ");
        sb.Append(uv.x).Append(" ").Append(uv.y);
        sw.WriteLine(sb.ToString());
      }
      

      List<MMesh> singleElementMesh = new List<MMesh>(1);
      //Make sure it has a single element - we're going to keep on replacing that element to export all single meshes.
      singleElementMesh.Add(null);
      foreach (MMesh mesh in ungroupedMeshes) {
        sw.WriteLine(new StringBuilder("o group").Append(mesh.id));
        sw.WriteLine(new StringBuilder("g mesh").Append(mesh.id));
        singleElementMesh[0] = mesh;
        WriteMesh(singleElementMesh, exportInfos, sw);
      }
      foreach (int key in groupedMeshes.Keys) {
        sw.WriteLine(new StringBuilder("o group").Append(key));
        sw.WriteLine(new StringBuilder("g mesh").Append(key));
        WriteMesh(groupedMeshes[key], exportInfos, sw);
      }
       
      sw.Close();
      bytes = stream.ToArray();
    }

    private static void WriteMesh(List<MMesh> inputMeshes, 
      Dictionary<int, MeshExportInfo> exportInfos,
      StreamWriter sw) {
      Dictionary<string, List<string>> materialsAndFaces = new Dictionary<string, List<string>>();

      foreach (MMesh mmesh in inputMeshes) {
        List<int> faceColors = exportInfos[mmesh.id].meshFaceColors;
        List<int[]> polys = exportInfos[mmesh.id].meshFaceVerts;
        List<int> normals = exportInfos[mmesh.id].meshNormals;
        List<int[]> uvs = exportInfos[mmesh.id].meshFaceUvs;

        for (int i = 0; i < polys.Count; i++) {
          int[] verts = polys[i];
          int[] rawUvs = uvs[i];
          StringBuilder faceSB = new StringBuilder("f");
          for (int j = 0; j < verts.Length; j++) {
            // Swapping X, need to reverse order of poly vertices.
            int idx = verts.Length - j - 1;
            faceSB.Append(" ").Append(verts[idx]);
            // Append the poly normal to each vertex reference. Note that .obj is 1-indexed.
            faceSB.Append("/").Append(rawUvs[idx]).Append("/").Append(normals[i] + 1);
          }

          string usemtl = "usemtl mat" + faceColors[i];
          if (!materialsAndFaces.ContainsKey(usemtl)) {
            materialsAndFaces.Add(usemtl, new List<string>());
          }
          materialsAndFaces[usemtl].Add(faceSB.ToString());
        }
      }

      foreach (KeyValuePair<string, List<string>> pair in materialsAndFaces) {
        sw.WriteLine(pair.Key);
        foreach (string face in pair.Value) {
          sw.WriteLine(face);
        }
      }
    }

    /// <summary>
    /// Calculate a dummy UV value for the vertex.
    /// </summary>
    /// <param name="tangent">A vector tangent to the vertex in an arbitrary direction.</param>
    /// <param name="binormal">A vector orthogonal to both the tangent and normal vectors.</param>
    /// <param name="vertex">The vertex we are calculating a UV value for.</param>
    /// <returns></returns>
    private static Vector2 GetVertexUv(Vector3 tangent, Vector3 binormal, Vector3 vertex) {
      // Divide by 20 and add 0.5 to ideally keep the UV values scaled between 0 and 1, as some applications
      // assume UVs between 0 and 1. We assume the original vertex coordinates will be within the -10, 10 range
      // because this is the size of the workspace. TODO(bug) tracks improving this system.
      return new Vector2(Vector3.Dot(tangent, vertex) / 20f + 0.5f, Vector3.Dot(binormal, vertex) / 20f + 0.5f);
    }

    /// <summary>
    /// Calculate the tangent space of the vertex's UV value.
    /// </summary>
    /// <param name="normal">The normal of the vertex we are calculating a UV for.</param>
    /// <param name="tangent">A vector tangent to the vertex in an arbitrary direction.</param>
    /// <param name="binormal">A vector orthogonal to both the tangent and normal vectors.</param>
    private static void GetTangentSpaceBasis(Vector3 normal, out Vector3 tangent, out Vector3 binormal) {
      // If arbitrary vector is parallel to the normal, choose a different one.
      if (Mathf.Abs(Vector3.Dot(normal, arbitraryVector)) < 1f) {
        tangent = Vector3.Cross(normal, arbitraryVector).normalized;
        binormal = Vector3.Cross(normal, tangent).normalized;
      } else {
        tangent = Vector3.Cross(normal, alternateArbitraryVector).normalized;
        binormal = Vector3.Cross(normal, tangent).normalized;
      }
    }

    /// <summary>
    /// Returns the index of the vertex in the vertices list, inserting the vertex if it is not already there.
    /// Note that vertex indices are 1-indexed as per the OBJ file spec.
    /// </summary>
    /// <param name="vertex">The Vector3 position of the vertex whose index is being sought.</param>
    /// <param name="vertexPositionToIndex">The dictionary maintaining a record of
    /// (vertex position, vertex list index).</param>
    /// <param name="vertices">The list of vertices in the mesh being processed.</param>
    private static int GetVertexIndex(Vector3 vertex, ref Dictionary<Vector3, int> vertexPositionToIndex,
      ref List<Vector3> vertices) {
      int vertexIndex;
      if (!vertexPositionToIndex.TryGetValue(vertex, out vertexIndex)) {
        vertices.Add(vertex);
        vertexIndex = vertices.Count;
        vertexPositionToIndex.Add(vertex, vertexIndex);
      }
      return vertexIndex;
    }

    /// <summary>
    /// Adds a triangulated MMesh's export information to the given lists of vertices, uvs, mesh normals, and materials.
    /// </summary>
    /// <param name="mesh">The MMesh to be added.</param>
    /// <param name="meshRepresentationCache">A cache of triangulated meshes.</param>
    /// <param name="vertices">The list of vertices to be updated.</param>
    /// <param name="uvs">The UV vertices to be calculated.</param>
    /// <param name="meshExportInfo">Export information about the mesh faces, including face normals, colors, uvs,
    /// and vertices to be calculated.</param>
    /// <param name="meshNormals">The face normals of the mesh to be calculated.</param>
    /// <param name="materials">The set of materials that have been seen and should be added to the .mtl file.</param>
    /// <param name="polyCount">A running total of the poly count of this export.</param>
    private static void AddTriangulatedMesh(MMesh mesh, MeshRepresentationCache meshRepresentationCache, 
      ref List<Vector3> vertices,
      ref List<Vector2> uvs,
      ref MeshExportInfo meshExportInfo,
      ref List<Vector3> meshNormals,
      ref HashSet<int> materials,
      ref int polyCount) {
      meshExportInfo.meshFaceColors = new List<int>();

      // Maintain a dictionary of vertex position keyed to its index in the vertices list this method updates.
      // We use <position, index> because vertices are stored in the triangulated MeshGenContext as Vector3s,
      // not Blocks vertex ids. This lets us not duplicate vertices that are shared across faces in a mesh.
      Dictionary<Vector3, int> vertexPositionToIndex = new Dictionary<Vector3, int>();
      // We attempt to look up the triangulation in a cache for efficiency; if it is not there it is triangulated
      // on the fly.
      Dictionary<int, MeshGenContext> meshInfoByMaterial =
        meshRepresentationCache.FetchComponentsForMesh(mesh.id, /* abortOnCacheMiss */ false);
      // Note that meshInfoByMaterial contains a "sub-mesh" for each material. Next we will process each of these
      // sub-meshes in turn.
      foreach (KeyValuePair<int, MeshGenContext> pair in meshInfoByMaterial) {
        int materialId = pair.Key;
        MeshGenContext meshGenContext = pair.Value;

        List<int> triangles = meshGenContext.triangles;
        for (int i = 0; i < triangles.Count; i += 3) {
          polyCount++;
          int vertexIndex1 = GetVertexIndex(meshGenContext.verts[triangles[i]], ref vertexPositionToIndex,
            ref vertices);
          int vertexIndex2 = GetVertexIndex(meshGenContext.verts[triangles[i + 1]], ref vertexPositionToIndex,
            ref vertices);
          int vertexIndex3 = GetVertexIndex(meshGenContext.verts[triangles[i + 2]], ref vertexPositionToIndex,
            ref vertices);

          meshExportInfo.meshFaceVerts.Add(new int[] { vertexIndex1, vertexIndex2, vertexIndex3 });
          meshExportInfo.meshFaceColors.Add(materialId);
          materials.Add(materialId);

          // TODO (bug) This normal calculation is duplicate work (calculating the normal for
          // every triangle in a triangulated face) and should be made more efficient.
          Vector3 normal = MeshMath.CalculateNormal(
            meshGenContext.verts[triangles[i]],
            meshGenContext.verts[triangles[i + 1]],
            meshGenContext.verts[triangles[i + 2]]);

          Vector3 tangent, binormal;
          GetTangentSpaceBasis(normal, out tangent, out binormal);

          uvs.Add(GetVertexUv(tangent, binormal, meshGenContext.verts[triangles[i]]));
          uvs.Add(GetVertexUv(tangent, binormal, meshGenContext.verts[triangles[i + 1]]));
          uvs.Add(GetVertexUv(tangent, binormal, meshGenContext.verts[triangles[i + 2]]));

          int[] uvsIdsForFace = new int[] { uvs.Count - 2, uvs.Count - 1, uvs.Count };
          meshExportInfo.meshFaceUvs.Add(uvsIdsForFace);
          meshExportInfo.meshNormals.Add(meshNormals.Count);
          meshNormals.Add(normal);
        }
      }
    }

    /// <summary>
    /// Adds a MMesh's export information to the given lists of vertices, uvs, mesh normals, and materials.
    /// </summary>
    /// <param name="mesh">The given MMesh</param>
    /// <param name="vertices">The list of vertices to be updated.</param>
    /// <param name="uvs">The UV vertices to be calculated.</param>
    /// <param name="meshExportInfo">Export information about the mesh faces, including face normals, colors, uvs,
    /// and vertices to be calculated.</param>
    /// <param name="meshNormals">The face normals of the mesh to be calculated.</param>
    /// <param name="materials">The set of materials that have been seen and should be added to the .mtl file.</param>
    /// <param name="polyCount">A running total of the poly count of this export.</param>
    private static void AddMesh(MMesh mesh,
      ref List<Vector3> vertices,
      ref List<Vector2> uvs,
      ref MeshExportInfo meshExportInfo,
      ref  List<Vector3> meshNormals,
      ref HashSet<int> materials,
      ref int polyCount) {
      meshExportInfo.meshFaceColors = new List<int>(mesh.faceCount);

      // We do not wish to duplicate vertices that are shared across faces. As such, we maintain a dictionary from
      // Vertex.id to the index in the vertices list this method updates. We only need to maintain this dictionary
      // per mesh, as Vertex.id is only shared within a single MMesh.
      Dictionary<int, int> vertexIdToIndex = new Dictionary<int, int>(mesh.vertexCount);

      foreach (Face face in mesh.GetFaces()) {
        polyCount++;
        meshExportInfo.meshFaceColors.Add(face.properties.materialId);
        // Record face color for .mtl file, if not seen already.
        materials.Add(face.properties.materialId);
        // We cannot use poly vertex ids, we need the position of the vertex in 'vertices' as the identifier.
        int[] vertexIdsForFace = new int[face.vertexIds.Count];
        int[] uvsIdsForFace = new int[face.vertexIds.Count];

        // TODO(64715939): Calculate the normal for each face - once we're more confident in the normals stored in each 
        // face, we can switch to using them directly.
        List<Vector3> faceVertexPositions = new List<Vector3>(face.vertexIds.Count);
        for (int i = 0; i < face.vertexIds.Count; i++) {
          faceVertexPositions.Add(mesh.VertexPositionInMeshCoords(face.vertexIds[i]));
        }
        Vector3 curNormal = (mesh.rotation * MeshMath.CalculateNormal(faceVertexPositions)).normalized;

        Vector3 tangent;
        Vector3 binormal;
        GetTangentSpaceBasis(curNormal, out tangent, out binormal);

        for (int i = 0; i < face.vertexIds.Count; i++) {
          int vertexId = face.vertexIds[i];
          int vertexIndex;
          Vector3 modelCoords = mesh.VertexPositionInModelCoords(face.vertexIds[i]);
          if (!vertexIdToIndex.TryGetValue(vertexId, out vertexIndex)) {
            vertices.Add(modelCoords);
            vertexIndex = vertices.Count;
            vertexIdToIndex.Add(vertexId, vertexIndex);
          }

          uvs.Add(new Vector2(Vector3.Dot(tangent, modelCoords) / 10f, Vector3.Dot(binormal, modelCoords)/ 10f));
          vertexIdsForFace[i] = vertexIndex;
          uvsIdsForFace[i] = uvs.Count;
        }

        meshExportInfo.meshFaceVerts.Add(vertexIdsForFace);
        meshExportInfo.meshFaceUvs.Add(uvsIdsForFace);
        meshExportInfo.meshNormals.Add(meshNormals.Count);     
        meshNormals.Add(curNormal);
      }
    }
  }
}
