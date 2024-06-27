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

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  /// This command deletes a reference image from the scene. It's different from other commands in the following ways:
  /// * It does not modify the model
  /// * It deletes a GameObject from the scene
  /// * It does not serialize into a peltzer file
  /// * See bug for a little more information/background
  /// </summary>
  public class DeleteReferenceImageCommand : Command {
    private MoveableReferenceImage.SetupParams setupParams;

    public DeleteReferenceImageCommand(MoveableReferenceImage.SetupParams setupParams) {
      this.setupParams = setupParams;
    }

    public void ApplyToModel(Model model) {
      PeltzerMain.Instance.referenceImageManager.DeleteReferenceImage(setupParams.refImageId);

      // In case a reference image has been removed with an undo action, make sure the palette
      // and menu are reenabled.
      PeltzerMain.Instance.restrictionManager.paletteAllowed = true;
      PeltzerMain.Instance.restrictionManager.menuActionsAllowed = true;
    }

    public Command GetUndoCommand(Model model) {
      // When a delete is undone, we wish for the reference image to be re-attached to the controller. See bug.
      MoveableReferenceImage.SetupParams newSetupParams = setupParams;
      newSetupParams.attachToController = newSetupParams.initialInsertion;
      return new AddReferenceImageCommand(newSetupParams);
    }
  }
}
