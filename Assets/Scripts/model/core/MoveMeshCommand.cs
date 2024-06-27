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

using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {

  /// <summary>
  ///   Move a mesh to a new location.
  /// </summary>
  public class MoveMeshCommand : Command {
    public const string COMMAND_NAME = "move";

    internal readonly int meshId;
    internal Vector3 positionDelta;
    internal Quaternion rotDelta = Quaternion.identity;

    public MoveMeshCommand(int meshId, Vector3 positionDelta, Quaternion rotDelta) {
      this.meshId = meshId;
      this.positionDelta = positionDelta;
      this.rotDelta = rotDelta;
    }

    public void ApplyToModel(Model model) {
      MMesh mesh = model.GetMesh(meshId);
      MMesh.MoveMMesh(mesh, positionDelta, rotDelta);
      model.MeshUpdated(meshId, materialsChanged:false, geometryChanged:true, vertsOrFacesChanged:false);
    }

    public Command GetUndoCommand(Model model) {
      return new MoveMeshCommand(meshId, -positionDelta, Quaternion.Inverse(rotDelta));
    }
  }
}
