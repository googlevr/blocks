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
  /// This class supplies instances of a material so that we don't need to reinstantiate, which can leak Materials.
  /// The queue of instances is for situations where a draw call needs a unique instance (one of the properties is
  /// animated in a way that may be different per-draw call, for example).  If more unique instances are requested than
  /// exist, it will start reusing - this will cause a visual artifact, but as most of these animations are quite fast
  /// it should be hard to trigger.
  /// </summary>
  public class MaterialCycler {
    private Queue<Material> matQueue;
    private Material fixedInstance;

    public MaterialCycler(Material baseMaterial, int instanceSize) {
      fixedInstance = new Material(baseMaterial);
      matQueue = new Queue<Material>();
      for (int i = 0; i < instanceSize; i++) {
        matQueue.Enqueue(new Material(baseMaterial));
      }
    }

    public Material GetFixedMaterial() {
      return fixedInstance;
    }

    public Material GetInstanceOfMaterial() {
      // Pull the material off the front of the queue, add it back to the end, then return it.
      Material front = matQueue.Dequeue();
      matQueue.Enqueue(front);
      return front;
    }
  }
}