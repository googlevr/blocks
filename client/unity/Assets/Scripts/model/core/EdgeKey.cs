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

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  /// A canonical id for an edge, which includes the id of the mesh it belongs to.
  /// </summary>
  public class EdgeKey {
    private readonly int _meshId;
    private readonly int _vertexId1;
    private readonly int _vertexId2;
    private readonly int _hashCode;

    public EdgeKey(int meshId, int vertexId1, int vertexId2) {
      this._meshId = meshId;
      if (vertexId1 < vertexId2) {
        this._vertexId1 = vertexId1;
        this._vertexId2 = vertexId2;
      } else {
        this._vertexId1 = vertexId2;
        this._vertexId2 = vertexId1;
      }
      // Hashcode suggested by Effective Java and Jon Skeet:
      // http://stackoverflow.com/questions/11742593/what-is-the-hashcode-for-a-custom-class-having-just-two-int-properties
      _hashCode = 17;
      _hashCode = _hashCode * 31 + _meshId;
      _hashCode = _hashCode * 31 + _vertexId1;
      _hashCode = _hashCode * 31 + _vertexId2;
    }

    public override bool Equals(object obj) {
      return Equals(obj as EdgeKey);
    }

    public bool Equals(EdgeKey otherKey) {
      return otherKey != null
        && _meshId == otherKey._meshId
        && _vertexId1 == otherKey._vertexId1
        && _vertexId2 == otherKey._vertexId2;
    }

    public override int GetHashCode() {
      return _hashCode;
    }

    public bool ContainsVertex(int vertexId) {
      return _vertexId1 == vertexId || _vertexId2 == vertexId;
    }

    public int meshId { get { return _meshId; } }
    public int vertexId1 { get { return _vertexId1; } }
    public int vertexId2 { get { return _vertexId2; } }
  }
}
