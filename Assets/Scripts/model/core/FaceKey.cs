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

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    /// A canonical id for a face, which includes the id of the mesh it belongs to.
    /// </summary>
    public class FaceKey
    {
        private readonly int _meshId;
        private readonly int _faceId;
        private readonly int _hashCode;

        public FaceKey(int meshId, int faceId)
        {
            _meshId = meshId;
            _faceId = faceId;
            // 31 is a good number: http://stackoverflow.com/questions/299304/why-does-javas-hashcode-in-string-use-31-as-a-multiplier
            _hashCode = (151 + meshId) * 31 + faceId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FaceKey);
        }

        public bool Equals(FaceKey otherKey)
        {
            return otherKey != null && _faceId == otherKey._faceId && _meshId == otherKey._meshId;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public int meshId { get { return _meshId; } }
        public int faceId { get { return _faceId; } }
    }
}
