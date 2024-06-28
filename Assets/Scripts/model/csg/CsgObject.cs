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
    /// <summary>
    ///   A collection of CsgPolygons that close a space.
    /// </summary>
    public class CsgObject
    {
        public List<CsgPolygon> polygons { get; private set; }
        public List<CsgVertex> vertices { get; private set; }
        public Bounds bounds { get; private set; }

        public CsgObject(List<CsgPolygon> polygons, List<CsgVertex> vertices)
        {
            this.polygons = polygons;
            this.vertices = vertices;
            Bounds bounds = new Bounds(vertices[0].loc, Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i].loc);
            }
            this.bounds = bounds;
        }
    }
}
