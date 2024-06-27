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
using System.Collections.Generic;
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  /// Snaps a rotation to 90 degree angles.
  /// Rather than just snap to the closest 90 degree angle (for which a helper class wouldn't be needed at all),
  /// this class maintains state to make the current snapped angle "sticky", requiring the user to actually
  /// rotate significantly away from the current snapped angle in order to snap to the next one. This makes the
  /// snapping feel more stable, as there is no "border region" where snapping would keep changing constantly
  /// as would be the case with plain snapping.
  /// </summary>
  public class StickyRotationSnapper {
    // How much of an angle the user has to rotate away from the current snapped rotation in order to
    // change the snapped rotation. This controls how "sticky" the snapping is.
    private const float ANGLE_THRESHOLD = 55.0f;

    public Quaternion snappedRotation { get; private set; }
    public Quaternion unsnappedRotation { get; private set; }

    public StickyRotationSnapper(Quaternion initialRotation) {
      unsnappedRotation = initialRotation;
      snappedRotation = GridUtils.SnapToNearest(unsnappedRotation, Quaternion.identity, 90f);
    }

    public Quaternion UpdateRotation(Quaternion currentRotation) {
      unsnappedRotation = currentRotation;
      if (Quaternion.Angle(snappedRotation, unsnappedRotation) >= ANGLE_THRESHOLD) {
        snappedRotation = GridUtils.SnapToNearest(unsnappedRotation, Quaternion.identity, 90f);
      }
      return snappedRotation;
    }
  }
}
