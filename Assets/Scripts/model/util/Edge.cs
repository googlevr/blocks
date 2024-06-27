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
using System.Text;

namespace com.google.apps.peltzer.client.model.util
{
    /// <summary>
    ///   A pair that represents a line segment.  Makes it easy to lookup segments in a Hashtable.
    /// </summary>
    internal struct Edge
    {
        private readonly int startId;
        private readonly int endId;

        internal Edge(int startId, int endId)
        {
            this.startId = startId;
            this.endId = endId;
        }

        public override bool Equals(object obj)
        {
            if (obj is Edge)
            {
                Edge other = (Edge)obj;
                return startId == other.startId && endId == other.endId;
            }
            return false;
        }

        public Edge Reverse()
        {
            return new Edge(endId, startId);
        }

        public override int GetHashCode()
        {
            int hc = 271;
            hc = (hc * 257) + startId;
            hc = (hc * 257) + endId;
            return hc;
        }

        public override string ToString()
        {
            return startId + " - " + endId;
        }
    }
}
