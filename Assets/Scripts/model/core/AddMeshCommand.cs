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
    ///   Command that adds an MMesh to the model.
    /// </summary>
    public class AddMeshCommand : Command
    {
        public const string COMMAND_NAME = "add";

        private readonly MMesh mesh;
        private readonly bool useInsertEffect;

        public AddMeshCommand(MMesh mesh, bool useInsertEffect = false)
        {
            this.mesh = mesh.Clone();
            this.useInsertEffect = useInsertEffect;
        }

        public void ApplyToModel(Model model)
        {
            // Clone this mesh so that the mutable one added to the model doesn't affect
            // this immutable command.
            model.AddMesh(mesh, useInsertEffect);
        }

        public Command GetUndoCommand(Model model)
        {
            return new DeleteMeshCommand(mesh.id);
        }

        public MMesh GetMeshClone()
        {
            return mesh.Clone();
        }

        public int GetMeshId()
        {
            return mesh.id;
        }
    }
}
