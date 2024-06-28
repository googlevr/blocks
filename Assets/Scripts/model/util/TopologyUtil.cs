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

namespace com.google.apps.peltzer.client.model.util
{
    public class TopologyUtil
    {

        /// <summary>
        ///   Checks that a given mesh has a valid topology.  Basically that it is a closed mesh without "manifold" edges.
        /// </summary>
        public static bool HasValidTopology(MMesh mesh, bool logInfo = false)
        {
            bool valid = true;

            //------ Check that each vertex is used at least twice.
            MultiDict<int, int> facesUsingVertex = new MultiDict<int, int>();

            foreach (Face face in mesh.GetFaces())
            {
                foreach (int vertexId in face.vertexIds)
                {
                    facesUsingVertex.Add(vertexId, face.id);
                }
            }

            foreach (int vertexId in mesh.GetVertexIds())
            {
                List<int> faces;
                if (facesUsingVertex.TryGetValues(vertexId, out faces))
                {
                    if (faces.Count < 2)
                    {
                        valid = false;
                        Console.WriteLine("Vertex " + vertexId + " is only used by " + faces.Count + " faces.");
                    }
                }
                else
                {
                    valid = false;
                    Console.WriteLine("Vertex " + vertexId + " is unused.");
                }
            }

            //------ Check that each edges is used exactly twice, once in each direction.
            Dictionary<Edge, int> edges = new Dictionary<Edge, int>();

            // Load edges, look for dupes along the way.
            foreach (Face face in mesh.GetFaces())
            {
                for (int i = 0; i < face.vertexIds.Count; i++)
                {
                    Edge edge = new Edge(face.vertexIds[i], face.vertexIds[(i + 1) % face.vertexIds.Count]);
                    if (edges.ContainsKey(edge))
                    {
                        valid = false;
                        if (logInfo)
                        {
                            Console.WriteLine("Non-manifold edge " + edge + " between faces " + face.id + " and " + edges[edge]);
                        }
                    }
                    else
                    {
                        edges[edge] = face.id;
                    }
                }
            }

            // Now ensure that each edge also has it's mirror in the set.
            foreach (Edge edge in edges.Keys)
            {
                if (!edges.ContainsKey(edge.Reverse()))
                {
                    valid = false;
                    if (logInfo)
                    {
                        Console.WriteLine("Edge " + edge + " in face " + edges[edge] + " is not joined to another face.");
                    }
                }
            }

            return valid;
        }
    }
}
