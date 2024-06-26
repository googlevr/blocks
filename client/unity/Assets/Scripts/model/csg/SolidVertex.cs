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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.csg {
  public struct SolidVertex {
    public int vertexId { get; private set; }
    internal Vector3 position;
    internal Vector3 normal;

    public SolidVertex(int vertexId, Vector3 pos, Vector3 norm) {
      this.vertexId = vertexId;
      this.position = pos;
      this.normal = norm;
    }

    public SolidVertex Flip() {
      return new SolidVertex(vertexId, position, -normal);
    }

    public SolidVertex Interpolate(int vertexId, SolidVertex other, float t) {
      return new SolidVertex(
        vertexId,
        Vector3.Lerp(position, other.position, t),
        Vector3.Lerp(normal, other.normal, t).normalized);
    }
  }
}
