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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools.utils;
using UnityEngine;

namespace com.google.apps.peltzer.client.testing
{
    /// <summary>
    ///   Utility to stress-test Poly.
    /// </summary>
    class PerformanceTesting : MonoBehaviour
    {
        // Whether we're creating one complex mesh (if not, we're creating many small ones):
        private static readonly bool ONE_COMPLEX_MESH = false;

        // How many instances per axis, the total number of meshes will be the cube of this number (ish).
        // http://www.miniwebtool.com/cube-numbers-list/?to=1000 is useful.
        private static readonly float ENTRIES_PER_AXIS = 32;

        // What primitive to insert. Cubes have 6 faces/12 tris, spheres have 80 faces/240 tris.
        private static readonly Primitives.Shape primitive = Primitives.Shape.CUBE;

        // A counter of available memory. I haven't gotten this working yet, so I'm just trusting Task Manager
        // to give decent estimates right now. Unity has a memory profiler but it seems to only be profiling
        // objects managed by Unity: it tends to under-report memory usage by 3x compared to Task Manager and
        // so I don't trust it right now.
        System.Diagnostics.PerformanceCounter ramCounter;

        // To calculate a rolling FPS: http://wiki.unity3d.com/index.php?title=FramesPerSecond
        float deltaTime = 0.0f;

        // We'll create one mesh in Start() and then keep cloning it for our instancing.
        MMesh mesh;

        // The negative limit of any given axis, given our bounds.
        float minDimension;
        // The distance between centers of meshes on any given axis.
        float increment;

        // Keeping track of our instancing, I'm sure there's a prettier way but this works. We set the original
        // position of the mesh to the negative limit of the bounds. We then move along the xAxis by 'increment'
        // until hitting the positive X limit, then reset the X position and raise the Y position by 'increment'.
        // When we hit the positive Y limit, we reset the X and Y positions and raise the Z position by 'increment'
        int xChanges = 0;
        int yChanges = 0;
        int zChanges = 0;

        // We use ~10 different materials just to keep our ReMesher busy, as it groups by material. This doesn't
        // seem to have much of a noticable effect on anything right now.
        int materialId = 1;

        PeltzerMain peltzerMain;

        void Start()
        {
            // Instantiate the RAM profiler.
            System.Diagnostics.PerformanceCounterCategory.Exists("PerformanceCounter");
            ramCounter = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");

            peltzerMain = FindObjectOfType<PeltzerMain>();

            if (ONE_COMPLEX_MESH)
            {
                mesh = Primitives.AxisAlignedIcosphere(/* id */ 0,
                  /* Just in front of user*/ new Vector3(0f, 1f, 0.5f),
                  /* Big */ Vector3.one * (GridUtils.GRID_SIZE / 2.0f) * 15,
                  /* materialID */ 1,
                  /* recursionLevel */ 4);
                Debug.Log("Adding one complex mesh with " + mesh.vertexCount + " verts and " + mesh.faceCount + " faces");
            }
            else
            {
                // Determine the negative limit and the distance between centers given the bounds.
                minDimension = -PeltzerMain.DEFAULT_BOUNDS.extents.x + GridUtils.GRID_SIZE;
                increment = PeltzerMain.DEFAULT_BOUNDS.extents.x / (ENTRIES_PER_AXIS + 1) * 2;

                // Create a mesh and position it at the negative limits.
                if (primitive == Primitives.Shape.CUBE)
                {
                    mesh = Primitives.AxisAlignedBox(0, Vector3.one * minDimension,
                      Vector3.one * (GridUtils.GRID_SIZE / 2.0f), 1);
                }
                else if (primitive == Primitives.Shape.SPHERE)
                {
                    mesh = Primitives.AxisAlignedIcosphere(0, Vector3.one * minDimension,
                    Vector3.one * (GridUtils.GRID_SIZE / 2.0f), 1);
                }
            }
        }

        // Adds 1000 meshes per frame, unless we're just dealing with a single complex mesh.
        void Update()
        {
            // Ugly but means I don't have to think about startup ordering or make PeltzerMain aware of this class.
            Model model = peltzerMain.GetModel();
            if (model == null)
            {
                return;
            }

            if (ONE_COMPLEX_MESH)
            {
                if (model.GetNumberOfMeshes() >= 1)
                {
                    return;
                }
                else
                {
                    model.AddMesh(mesh);
                    return;
                }
            }

            // Stop inserting once we've filled all 3 axes.
            if (zChanges >= ENTRIES_PER_AXIS)
            {
                return;
            }

            // Keep track of FPS.
            deltaTime += (Time.deltaTime - deltaTime) * 0.1f;

            // Limiting to 1000 inserts per frame seems to keep Unity from crashing.
            for (int i = 0; i < 1000; i++)
            {
                // Abort the loop when done and print to console.
                if (zChanges >= ENTRIES_PER_AXIS)
                {
                    Debug.Log("Added " + model.GetNumberOfMeshes());
                    return;
                }

                if (xChanges < ENTRIES_PER_AXIS)
                {
                    // Move on the X axis.
                    xChanges++;
                    mesh.offset += new Vector3(increment, 0f, 0f);
                }
                else if (xChanges == ENTRIES_PER_AXIS)
                {
                    // If at the positive limit of the X axis, reset X position and bump up Y position.
                    if (yChanges == ENTRIES_PER_AXIS)
                    {
                        // If at the positive limit of the Y axis, reset X and Y position and bump up Z position.
                        xChanges = 0;
                        yChanges = 0;
                        zChanges++;
                        mesh.offset = new Vector3(minDimension, minDimension, mesh.offset.z + increment);

                        // Also switch up the material ID.
                        materialId++;
                        materialId %= MaterialRegistry.GetNumMaterials();
                        materialId++;
                        foreach (Face face in mesh.GetFaces())
                        {
                            face.SetProperties(new FaceProperties(materialId));
                        }
                    }
                    yChanges++;
                    mesh.offset = new Vector3(minDimension, mesh.offset.y + increment, mesh.offset.z);
                    xChanges = 0;
                }

                // Insert the new mesh.
                MMesh clone = mesh.CloneWithNewId(model.GenerateMeshId());
                if (!model.AddMesh(clone))
                {
                    // Should never happen, doesn't seem to, but why not.
                    // Debug.Log(clone.offset.x + ", " + clone.offset.y + ", " + clone.offset.z);
                }
                else
                {
                    int count = model.GetNumberOfMeshes();
                    if (count % 200 == 0)
                    {
                        // Some printing code, I've just been using Unity profiler for FPS though, and can't get the memory
                        // profiling working.
                        float msec = deltaTime * 1000.0f;
                        float fps = 1.0f / deltaTime;
                        //StringBuilder stringBuilder = new StringBuilder(string.Format("({0:0.} fps)", fps));
                        //stringBuilder.Append(ramCounter.NextValue()).Append("MB,");
                        //stringBuilder.Append(count).Append(" meshes");
                        //Debug.Log(stringBuilder.ToString());
                    }
                }
            }
        }
    }
}
