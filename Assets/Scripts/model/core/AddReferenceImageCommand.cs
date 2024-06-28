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

using com.google.apps.peltzer.client.desktop_app;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    /// This command adds a reference image to the scene. This is different from other commands in the following ways:
    /// * It does not modify the model
    /// * It adds GameObjects to the scene
    /// * It does not serialize into a peltzer file
    /// * See bug for a little more information/background
    /// </summary>
    public class AddReferenceImageCommand : Command
    {
        private MoveableReferenceImage.SetupParams setupParams;

        public AddReferenceImageCommand(MoveableReferenceImage.SetupParams setupParams)
        {
            this.setupParams = setupParams;
        }

        public void ApplyToModel(Model model)
        {
            PeltzerMain.Instance.referenceImageManager.CreateReferenceImage(setupParams);
        }

        public Command GetUndoCommand(Model model)
        {
            return new DeleteReferenceImageCommand(setupParams);
        }
    }
}
