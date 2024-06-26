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

namespace com.google.apps.peltzer.client.model.render {
  /// <summary>
  ///   Holds a pair of Mesh and Material.
  /// </summary>
  public struct MeshWithMaterial {
    public Mesh mesh { get; private set; }
    public MaterialAndColor materialAndColor { get; set; }

    public MeshWithMaterial(Mesh mesh, MaterialAndColor materialAndColor) {
      this.mesh = mesh;
      this.materialAndColor = materialAndColor;
    }
  }
}
