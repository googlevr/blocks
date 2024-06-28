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
using com.google.apps.peltzer.client.model.core;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.util
{
    /// <summary>
    ///   Debug utilities.
    /// </summary>
    public class DebugUtils
    {
        /// <summary>
        /// Converts a Vector3 to string. Use this instead of Vector3.ToString() because we need 3 decimal
        /// places of precision (Vector3.ToString uses only 1).
        /// </summary>
        public static string Vector3ToString(Vector3 v)
        {
            return string.Format("{0:F3},{1:F3},{2:F3}", v.x, v.y, v.z);
        }
        /// <summary>
        /// Converts a Bounds object to string, including center, size, extents, min, max (which are not
        /// included in the regular Bounds.ToString() method).
        /// </summary>
        public static string BoundsToString(Bounds b)
        {
            return string.Format("Center={0}, Size={1}, Extents={2}, Min={3}, Max={4}",
              Vector3ToString(b.center), Vector3ToString(b.size), Vector3ToString(b.extents), Vector3ToString(b.min),
              Vector3ToString(b.max));
        }
        /// <summary>
        /// Converts a Vector3 to string. Use this instead of Vector3.ToString() because we need 3 decimal
        /// places of precision (Vector3.ToString uses only 1).
        /// </summary>
        public static string Vector3sToString(IEnumerable<Vector3> vs)
        {
            StringBuilder outString = new StringBuilder();
            int count = 1;
            outString.Append("[");
            foreach (Vector3 vec in vs)
            {
                if (count < vs.Count())
                {
                    outString.Append(string.Format("<{0:F3},{1:F3},{2:F3}>, ", vec.x, vec.y, vec.z));
                }
                else
                {
                    outString.Append(string.Format("<{0:F3},{1:F3},{2:F3}>]", vec.x, vec.y, vec.z));
                }
                count++;
            }
            return outString.ToString();
        }

        /// <summary>
        /// Converts a Vector3 to string. Use this instead of Vector3.ToString() because we need 3 decimal
        /// places of precision (Vector3.ToString uses only 1).
        /// </summary>
        public static string MMeshVertsToString(MMesh mesh)
        {
            StringBuilder outString = new StringBuilder();
            int count = 1;
            outString.Append("[");
            foreach (Vertex vec in mesh.GetVertices())
            {
                if (count < mesh.vertexCount)
                {
                    outString.Append(string.Format("<{0:F3},{1:F3},{2:F3}>, ", vec.loc.x, vec.loc.x, vec.loc.x));
                }
                else
                {
                    outString.Append(string.Format("<{0:F3},{1:F3},{2:F3}>]", vec.loc.x, vec.loc.x, vec.loc.x));
                }
                count++;
            }
            return outString.ToString();
        }
    }
}
