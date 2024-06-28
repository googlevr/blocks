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

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   Delete a mesh from the model.
    /// </summary>
    public class DeleteMeshCommand : Command
    {
        private readonly int meshId;

        public DeleteMeshCommand(int meshId)
        {
            this.meshId = meshId;
        }

        public void ApplyToModel(Model model)
        {
            model.DeleteMesh(meshId);
        }

        public Command GetUndoCommand(Model model)
        {
            return new AddMeshCommand(model.GetMesh(meshId));
        }

        public int MeshId { get { return meshId; } }
    }
}
