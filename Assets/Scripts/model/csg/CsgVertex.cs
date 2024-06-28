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
using System.Linq;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.csg
{
    public enum VertexStatus
    {
        UNKNOWN,
        INSIDE,
        OUTSIDE,
        BOUNDARY
    }

    /// <summary>
    ///   A vertex with an associated 'status'.  The status determines whether a given vertex is inside another
    ///   object (or outside or on its boundary).
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    public class CsgVertex
    {
        public Vector3 loc { get; private set; }
        public HashSet<CsgVertex> neighbors { get; private set; }
        public VertexStatus status { get; set; }
        private readonly String asString;

        public CsgVertex(Vector3 loc)
        {
            this.loc = loc;
            this.neighbors = new HashSet<CsgVertex>();
            this.status = VertexStatus.UNKNOWN;
            this.asString = "(" + loc.x.ToString("0.000") + ", " + loc.y.ToString("0.000") + ", " + loc.z.ToString("0.000") + ")";
        }

        public override string ToString()
        {
            return asString;
        }
    }
}
