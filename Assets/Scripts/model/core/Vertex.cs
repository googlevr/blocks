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

namespace com.google.apps.peltzer.client.model.core
{

    /// <summary>
    ///   A shared vertex.  Represents a location in space that can be
    ///   shared by multiple faces in a single MMesh.
    /// </summary>
    public class Vertex
    {
        private readonly int _id;
        private Vector3 _loc;

        // Read-only getters.
        public int id { get { return _id; } }
        public Vector3 loc { get { return _loc; } }

        public Vertex(int id, Vector3 loc)
        {
            _id = id;
            _loc = loc;
        }
    }
}
