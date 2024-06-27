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

using System.Collections.ObjectModel;
using UnityEngine;

using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{

    /// <summary>
    ///   A hole in a Face.
    /// </summary>
    public class Hole
    {
        private readonly ReadOnlyCollection<int> _vertexIds;
        private ReadOnlyCollection<Vector3> _normals;

        // Read-only getters.
        public ReadOnlyCollection<int> vertexIds { get { return _vertexIds; } }
        public ReadOnlyCollection<Vector3> normals { get { return _normals; } }

        /// <summary>
        ///   Create a new hole.
        /// </summary>
        /// <param name="vertexIds">Vertex ids of the border in counterclockwise order.</param>
        /// <param name="normals">Normals for the given vertices.</param>
        public Hole(ReadOnlyCollection<int> vertexIds, ReadOnlyCollection<Vector3> normals)
        {
            AssertOrThrow.True(vertexIds.Count == normals.Count,
              "Must have same number of vertices and normals.");
            _vertexIds = vertexIds;
            _normals = normals;
        }
    }
}
