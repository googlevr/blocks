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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.google.apps.peltzer.client.alignment {
  /// <summary>
  ///   Holds the position and rotation for a snap.
  /// </summary>
  public class SnapTransform {
    public Vector3 position;
    public Quaternion rotation;

    public SnapTransform(Vector3 position, Quaternion rotation) {
      this.position = position;
      this.rotation = rotation;
    }
  }
}
