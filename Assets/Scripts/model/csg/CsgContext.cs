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
using UnityEngine;

using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.csg
{
    /// <summary>
    ///   Context for CSG operations.  Unifies vertices.
    /// </summary>
    public class CsgContext
    {
        // We allow new points to be added if they are at least 3 epsilons away from existing points.
        private readonly int WIGGLE_ROOM = 3;
        private CollisionSystem<CsgVertex> tree;

        public CsgContext(Bounds bounds)
        {
            tree = new NativeSpatial<CsgVertex>();
        }

        public CsgVertex CreateOrGetVertexAt(Vector3 loc)
        {
            Bounds bb = new Bounds(loc, Vector3.one * CsgMath.EPSILON * WIGGLE_ROOM);
            CsgVertex closest = null;
            HashSet<CsgVertex> vertices;
            if (tree.IntersectedBy(bb, out vertices))
            {
                float closestDist = 1000;
                foreach (CsgVertex potential in vertices)
                {
                    float d = Vector3.Distance(loc, potential.loc);
                    if (d < CsgMath.EPSILON && d < closestDist)
                    {
                        closest = potential;
                        closestDist = d;
                    }
                }
            }
            if (closest == null)
            {
                closest = new CsgVertex(loc);
                tree.Add(closest, new Bounds(loc, Vector3.zero));
            }
            return closest;
        }
    }
}
