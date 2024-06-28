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
using System.Collections.Generic;
using System.IO;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.render;
using System.Linq;
using System;

namespace com.google.apps.peltzer.client.model.import
{
    /// <summary>
    /// Imports obj and mtl files.
    /// </summary>
    public static class ObjImporter
    {
        /// <summary>
        ///   Creates an MMesh from the contents of a .obj file and the contents of a .mtl file, with the given id.
        ///   Generally an OBJ file will not create meshes that are "topologically correct", so they won't work right
        ///   with a lot of our tools, but should at least be moveable if nothing else.
        /// </summary>
        /// <param name="objFileContents">The contents of a .obj file.</param>
        /// <param name="mtlFileContents">The contents of a .mtl file.</param>
        /// <param name="id">The id of the new MMesh.</param>
        /// <param name="result">The created mesh, or null if it could not be created.</param>
        /// <returns>Whether the MMesh could be created.</returns>
        public static bool MMeshFromObjFile(string objFileContents, string mtlFileContents, int id, out MMesh result)
        {
            Dictionary<string, Material> materials = ImportMaterials(mtlFileContents);
            Dictionary<Material, List<MeshVerticesAndTriangles>> materialsAndMeshes;
            if (ImportMeshes(objFileContents, materials, out materialsAndMeshes))
            {
                result = MeshHelper.MMeshFromMeshes(id, materialsAndMeshes);
                return true;
            }
            result = null;
            return false;
        }

        public static Dictionary<string, Material> ImportMaterials(string materialsString)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            if (materialsString == null || materialsString.Length == 0)
                return materials;

            using (StringReader reader = new StringReader(materialsString))
            {
                string currentText = reader.ReadLine().Trim();
                while (currentText != null)
                {
                    if (currentText.StartsWith("newmtl"))
                    {
                        string materialName = currentText.Split(' ')[1];
                        Color materialColor = Color.white;

                        currentText = reader.ReadLine();
                        while (currentText != null && !currentText.StartsWith("newmtl"))
                        {
                            currentText = currentText.Trim();
                            if (currentText.StartsWith("Ka"))
                            {
                                string[] colorString = currentText.Split(' ');
                                materialColor = new Color(float.Parse(colorString[1]), float.Parse(colorString[2]),
                                  float.Parse(colorString[3]));
                            }
                            else if (currentText.StartsWith("Kd"))
                            {
                                string[] colorString = currentText.Split(' ');
                                materialColor = new Color(float.Parse(colorString[1]), float.Parse(colorString[2]),
                                  float.Parse(colorString[3]));
                            }
                            currentText = reader.ReadLine();
                        }

                        Material material = null;
                        if (materialName.StartsWith("mat"))
                        {
                            int potentialMaterialId;
                            if (int.TryParse(materialName.Substring("mat".Length), out potentialMaterialId))
                            {
                                material = MaterialRegistry.GetMaterialAndColorById(potentialMaterialId).material;
                                material.name = materialName;
                            }
                        }
                        if (material == null)
                        {
                            material = new Material(Shader.Find("Diffuse"));
                            material.name = materialName;
                            material.color = materialColor;
                        }

                        materials.Add(materialName, material);
                    }
                    else
                    {
                        currentText = reader.ReadLine();
                    }
                }
            }
            return materials;
        }

        private class Face
        {
            public List<int> vertexIds = new List<int>();
        }

        public static bool ImportMeshes(string objFileContents,
          Dictionary<string, Material> materials, out Dictionary<Material, List<MeshVerticesAndTriangles>> meshes)
        {
            meshes = new Dictionary<Material, List<MeshVerticesAndTriangles>>();
            if (objFileContents == null || objFileContents.Length == 0)
            {
                return false;
            }

            // Default current material, in case they don't have an MTL file.
            bool mtlFileWasSupplied = materials.Count > 0;
            string currentMaterial = "mat0";
            if (!mtlFileWasSupplied)
            {
                materials.Add(currentMaterial, MaterialRegistry.GetMaterialAndColorById(0).material);
            }

            List<Vector3> allVertices = new List<Vector3>();
            List<Vector2> allTexVertices = new List<Vector2>();
            Dictionary<string, List<Face>> faces = new Dictionary<string, List<Face>>();

            string[] parts;
            char[] sep = { ' ' };
            char[] sep2 = { ':' };
            char[] sep3 = { '/' };
            string[] sep4 = { "//" };
            using (StringReader reader = new StringReader(objFileContents))
            {
                string line = reader.ReadLine();
                while (line != null)
                {
                    if (line.StartsWith("v "))
                    {
                        parts = line.Trim().Split(sep);
                        if (parts.Count() < 4)
                        {
                            Debug.Log("Not enough vertex values");
                            Debug.Log(line);
                            return false;
                        }
                        try
                        {
                            allVertices.Add(new Vector3(Convert.ToSingle(parts[1]), Convert.ToSingle(parts[2]), Convert.ToSingle(parts[3])));
                        }
                        catch (FormatException)
                        {
                            Debug.Log("Unexpected vertex value");
                            Debug.Log(line);
                            return false;
                        }
                    }
                    else if (line.StartsWith("vt "))
                    {
                        parts = line.Trim().Split(sep);
                        if (parts.Count() < 3)
                        {
                            Debug.Log("Not enough tex vertex values");
                            Debug.Log(line);
                            return false;
                        }
                        try
                        {
                            allTexVertices.Add(new Vector2(Convert.ToSingle(parts[1]), Convert.ToSingle(parts[2])));
                        }
                        catch (FormatException)
                        {
                            Debug.Log("Unexpected tex vertex value");
                            Debug.Log(line);
                            return false;
                        }
                    }
                    else if (line.StartsWith("usemtl ") && mtlFileWasSupplied)
                    {
                        parts = line.Trim().Split(sep);
                        if (parts[1].Contains(sep2[0]))
                        {
                            currentMaterial = parts[1].Split(sep2)[1];
                        }
                        else
                        {
                            currentMaterial = parts[1];
                        }
                    }
                    else if (line.StartsWith("f "))
                    {
                        parts = line.Trim().Split(sep);
                        if (parts.Length < 4)
                        {
                            Debug.Log("Not vertex values in a face");
                            Debug.Log(line);
                            return false;
                        }
                        Face face = new Face();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (parts[i].Contains(sep4[0]))
                            {
                                // -1 as vertices are 0-indexed when read but 1-indexed when referenced.
                                face.vertexIds.Add(int.Parse(parts[i].Split(sep4, StringSplitOptions.None)[0]) - 1);
                            }
                            else if (parts[i].Contains(sep3[0]))
                            {
                                // -1 as vertices are 0-indexed when read but 1-indexed when referenced.
                                face.vertexIds.Add(int.Parse(parts[i].Split(sep3)[0]) - 1);
                                //face.uvIds.Add(int.Parse(vIds[1]));
                            }
                            else
                            {
                                // -1 as vertices are 0-indexed when read but 1-indexed when referenced.
                                face.vertexIds.Add(int.Parse(parts[i]) - 1);
                            }
                        }
                        if (!faces.ContainsKey(currentMaterial))
                        {
                            faces.Add(currentMaterial, new List<Face>());
                        }
                        faces[currentMaterial].Add(face);
                    }
                    line = reader.ReadLine();
                }
            }

            // Create one mesh per entry in faceList, as all faces will have the same material.
            foreach (KeyValuePair<string, List<Face>> faceList in faces)
            {
                // A list of vertics in this mesh.
                List<Vector3> meshVertices = new List<Vector3>();
                // Used to translate a vertex id from the obj file to an index into meshVertices.
                Dictionary<int, int> localVertexIds = new Dictionary<int, int>();
                // A list of triangles in this mesh.
                List<int> triangles = new List<int>();

                foreach (Face face in faceList.Value)
                {
                    foreach (int idx in face.vertexIds)
                    {
                        if (!localVertexIds.ContainsKey(idx))
                        {
                            localVertexIds.Add(idx, meshVertices.Count);
                            meshVertices.Add(allVertices[idx]);
                        }
                    }
                    for (int i = 2; i < face.vertexIds.Count; i++)
                    {
                        triangles.Add(localVertexIds[face.vertexIds[i - 2]]);
                        triangles.Add(localVertexIds[face.vertexIds[i - 1]]);
                        triangles.Add(localVertexIds[face.vertexIds[i]]);
                    }
                } // foreach face
                meshes.Add(materials[faceList.Key], BreakIntoMultipleMeshes(meshVertices, triangles));
            } // foreach facelist
            return true;
        }

        private static List<MeshVerticesAndTriangles>
          BreakIntoMultipleMeshes(List<Vector3> meshVertices, List<int> triangles)
        {
            if (meshVertices.Count < 65000)
            {
                return new List<MeshVerticesAndTriangles>() {
          new MeshVerticesAndTriangles(meshVertices.ToArray(), triangles.ToArray())
        };
            }
            List<MeshVerticesAndTriangles> subMeshes = new List<MeshVerticesAndTriangles>();
            List<int> subMeshTriangles = new List<int>();
            List<Vector3> subMeshVertices = new List<Vector3>();
            Dictionary<int, int> triangleMapping = new Dictionary<int, int>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int t1 = triangles[i];
                int t2 = triangles[i + 1];
                int t3 = triangles[i + 2];
                if (!triangleMapping.ContainsKey(t1))
                {
                    triangleMapping.Add(t1, subMeshVertices.Count);
                    subMeshVertices.Add(meshVertices[t1]);
                }
                if (!triangleMapping.ContainsKey(t2))
                {
                    triangleMapping.Add(t2, subMeshVertices.Count);
                    subMeshVertices.Add(meshVertices[t2]);
                }
                if (!triangleMapping.ContainsKey(t3))
                {
                    triangleMapping.Add(t3, subMeshVertices.Count);
                    subMeshVertices.Add(meshVertices[t3]);
                }
                subMeshTriangles.Add(triangleMapping[t1]);
                subMeshTriangles.Add(triangleMapping[t2]);
                subMeshTriangles.Add(triangleMapping[t3]);
                if (subMeshVertices.Count > 64000)
                {
                    subMeshes.Add(new MeshVerticesAndTriangles(subMeshVertices.ToArray(), subMeshTriangles.ToArray()));
                    subMeshVertices = new List<Vector3>();
                    subMeshTriangles = new List<int>();
                    triangleMapping.Clear();
                }
            }
            if (subMeshVertices.Count > 0)
            {
                subMeshes.Add(new MeshVerticesAndTriangles(subMeshVertices.ToArray(), subMeshTriangles.ToArray()));
            }
            return subMeshes;
        }
    }
}
