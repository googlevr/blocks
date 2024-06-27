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

namespace com.google.apps.peltzer.client.tools.utils {
  /// <summary>
  ///   Class to efficiently recycle temporary mesh object so that we don't risk memory leaks of Mesh objects.
  /// </summary>
  public class MeshCycler {
    // Don't need a queue per se, but it has fast insert and remove ops.
    private static Queue<Mesh> meshPool = new Queue<Mesh>();
    // A dictionary from meshId/materialId to Unity Mesh, structured as:
    // Key: MeshID
    // Value: A dictionary of:
    //   Key: MaterialID
    //   Value: Unity Mesh
    private static Dictionary<int, Dictionary<int, Mesh>> meshDict = new Dictionary<int, Dictionary<int, Mesh>>();

    /// <summary>
    ///   Returns a Unity Mesh corresponding to the given meshId/materialId pair.
    ///   Note that THE CALLER SHOULD NOT HOLD A REFERENCE TO THIS MESH as it will be recycled.
    /// </summary>
    /// <param name="meshId">A Mesh ID</param>
    /// <param name="materialId">A Material ID</param>
    /// <param name="createdMesh">
    ///   Whether the mesh needed to be created (in which case its vertices need to be set.
    /// </param>
    public static Mesh GetTempMeshForMeshMatId(int meshId, int materialId, out bool createdMesh) {
      // Get, or create, the dictionary of materials-to-Unity Meshes for this mesh ID.
      Dictionary<int, Mesh> matDict;
      if (!meshDict.TryGetValue(meshId, out matDict)) {
        matDict = new Dictionary<int, Mesh>();
        meshDict[meshId] = matDict;
      }

      // Get the Unity Mesh for this material ID if it already exists.
      Mesh mesh;
      if (matDict.TryGetValue(materialId, out mesh)) {
        createdMesh = false;
        return mesh;
      }

      // Create the Unity Mesh for this material ID. Try and fetch a spare mesh from the pool if possible,
      // else create a new Mesh.
      createdMesh = true;
      if (meshPool.Count > 0) {
        mesh = meshPool.Dequeue();
      } else {
        mesh = new Mesh();
      }

      matDict[meshId] = mesh;
      return mesh;
    }

    public static void ResetCycler() {
      foreach (Dictionary<int, Mesh> matDict in meshDict.Values) {
        foreach (Mesh mesh in matDict.Values) {
          mesh.Clear();
          meshPool.Enqueue(mesh);
        }
      }
      meshDict.Clear();
    }
  }
}
