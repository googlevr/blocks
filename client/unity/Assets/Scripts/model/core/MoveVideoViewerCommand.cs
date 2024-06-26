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

using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  /// This command moves the video viewer. It does not affect the Model.
  /// </summary>
  public class MoveVideoViewerCommand : Command {
    private Vector3 positionDelta;
    private Quaternion rotDelta = Quaternion.identity;

    public MoveVideoViewerCommand(Vector3 positionDelta, Quaternion rotDelta) {
      this.positionDelta = positionDelta;
      this.rotDelta = rotDelta;
    }

    public void ApplyToModel(Model model) {
      GameObject videoViewer = PeltzerMain.Instance.GetVideoViewer();
      videoViewer.transform.position += positionDelta;
      PeltzerMain.Instance.GetVideoViewer().transform.rotation *= rotDelta;
    }

    public Command GetUndoCommand(Model model) {
      return new MoveVideoViewerCommand(-positionDelta, Quaternion.Inverse(rotDelta));
    }
  }
}