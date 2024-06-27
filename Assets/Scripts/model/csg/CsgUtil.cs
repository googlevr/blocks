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

namespace com.google.apps.peltzer.client.model.csg
{
    public class PolyEdge
    {
        CsgVertex a;
        CsgVertex b;

        public PolyEdge(CsgVertex a, CsgVertex b)
        {
            this.a = a;
            this.b = b;
        }

        public PolyEdge Reversed()
        {
            return new PolyEdge(b, a);
        }

        public override bool Equals(object obj)
        {
            if (obj is PolyEdge)
            {
                PolyEdge other = (PolyEdge)obj;
                return a == other.a && b == other.b;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hc = 13;
            hc = (hc * 31) + a.GetHashCode();
            hc = (hc * 31) + b.GetHashCode();
            return hc;
        }
    }

    public class CsgUtil
    {

        // Do some sanity checks on a polygon split:
        //  1) All polys should have the same normal.
        //  2) Each vertex from the original poly should be in at least one splitPoly
        //  3) Each split poly should share at least one edge with one other (the edge should be reversed)
        //  4) No edge should be in more than one poly (in the same order)
        //  5) No vertex should be in the same poly more than once
        //  6) Every edge in the initial polygon should be in a split, except those *edges* that were split.
        //     We pass in numSplitEdges to tell the test how many that should be.
        public static bool IsValidPolygonSplit(CsgPolygon initialPoly, List<CsgPolygon> splitPolys, int numSplitEdges)
        {
            List<HashSet<CsgVertex>> vertsForPolys = new List<HashSet<CsgVertex>>();
            List<HashSet<PolyEdge>> edgesForPolys = new List<HashSet<PolyEdge>>();

            // Set up some datastructures, check normals while we are looping.
            foreach (CsgPolygon poly in splitPolys)
            {
                vertsForPolys.Add(new HashSet<CsgVertex>(poly.vertices));
                edgesForPolys.Add(Edges(poly));
                if (Vector3.Distance(initialPoly.plane.normal, poly.plane.normal) > 0.001f)
                {
                    Console.Write("Normals do not match: " + initialPoly.plane.normal + " vs " + poly.plane.normal);
                    return false;
                }
            }

            // Look for each vertex from the original poly:
            foreach (CsgVertex vert in initialPoly.vertices)
            {
                bool found = false;
                foreach (HashSet<CsgVertex> verts in vertsForPolys)
                {
                    if (verts.Contains(vert))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Console.Write("Vertex from original poly is missing from split polys");
                    return false;
                }
            }

            // For each poly, find another poly with a matching edge (going the other direction)
            for (int i = 0; i < edgesForPolys.Count; i++)
            {
                HashSet<PolyEdge> polyEdges = edgesForPolys[i];
                bool foundEdge = false;
                for (int j = 0; j < edgesForPolys.Count; j++)
                {
                    if (i == j)
                    {
                        continue;  // Don't compare polygon to itself
                    }
                    foreach (PolyEdge edge in polyEdges)
                    {
                        if (edgesForPolys[j].Contains(edge.Reversed()))
                        {
                            foundEdge = true;
                        }
                    }
                }
                if (!foundEdge)
                {
                    Console.Write("Poly " + i + " does not have any edges in other polys");
                    return false;
                }
            }

            // Check that the total number of edges is the same as the sum of all edges in all splits
            // i.e. there are no duplicate edges.
            HashSet<PolyEdge> alledges = new HashSet<PolyEdge>();
            int sum = 0;
            foreach (HashSet<PolyEdge> edges in edgesForPolys)
            {
                sum += edges.Count;
                alledges.UnionWith(edges);
            }
            if (sum != alledges.Count)
            {
                Console.Write("Found duplicate edges.");
                return false;
            }

            // Check to make sure no polys have the same vertex more than once.
            for (int i = 0; i < vertsForPolys.Count; i++)
            {
                // The 'Set' should have the same number of verts as the 'List'
                if (vertsForPolys[i].Count != splitPolys[i].vertices.Count)
                {
                    Console.Write("Found duplicate vertex");
                    return false;
                }
            }

            // Look for all edges in the list above.  The count should be the same number of edges
            // in the initial poly minus the number of edges that were split.
            int count = numSplitEdges;
            HashSet<PolyEdge> initialEdges = Edges(initialPoly);
            foreach (PolyEdge initialEdge in initialEdges)
            {
                if (alledges.Contains(initialEdge))
                {
                    count++;
                }
            }
            if (initialEdges.Count != count)
            {
                Console.Write("Edges from initial poly are missing");
                return false;
            }

            return true;
        }

        private static HashSet<PolyEdge> Edges(CsgPolygon poly)
        {
            HashSet<PolyEdge> edges = new HashSet<PolyEdge>();

            for (int i = 0; i < poly.vertices.Count; i++)
            {
                CsgVertex a = poly.vertices[i];
                CsgVertex b = poly.vertices[(i + 1) % poly.vertices.Count];
                edges.Add(new PolyEdge(a, b));
            }

            return edges;
        }
    }
}

