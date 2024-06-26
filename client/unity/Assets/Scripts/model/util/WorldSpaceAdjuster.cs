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

namespace com.google.apps.peltzer.client.model.util {
  /// <summary>
  /// A utility class to adjust the world space to the current model, to help place it at a
  /// comfortable and useful position relative to the user.
  /// </summary>
  public static class WorldSpaceAdjuster {
    /// <summary>
    /// Adjusts the world space (for example, after opening a new model), setting the world to model space transform
    /// such that the model appears at a convenient position.
    /// </summary>
    public static void AdjustWorldSpace() {
      // NOTE: for now, the adjustment we make is minimal: we just translate things so that no geometry spawns under
      // the floor (where it would be invisible). In the future, we can tweak this method to also adjust the world
      // transform to make the loaded model appear at a more natural position/orientation.
      Bounds boundsModelSpace = PeltzerMain.Instance.model.FindBoundsOfAllMeshes();

      // We want to prevent parts of the geometry from being under the floor (y = 0), so we want to set up the world
      // transform such that minModelY maps to 0 or above in world space.
      float minWorldY = PeltzerMain.Instance.worldSpace.ModelToWorld(boundsModelSpace.min).y;
      if (minWorldY < 0) {
        // Move the offset up by -minWorldY to cancel out the negative coordinate, making minModelY map
        // to y=0 in world space.
        PeltzerMain.Instance.worldSpace.offset += Vector3.up * -minWorldY;
      }
    }
  }
}
