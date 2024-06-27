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
using UnityEngine;
using NUnit.Framework;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.render
{
    [TestFixture]
    // Tests for ReMesher.
    public class ReMesherTest
    {
        [Test]
        public void SanityTest()
        {
            // Hard to test rendering is correct.  But let's at least add a few items
            // to make sure nothing blows up.
            ReMesher remesher = new ReMesher();
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 5);
            WorldSpace worldSpace = new WorldSpace(bounds);
            Model model = new Model(bounds);
            GameObject go = new GameObject();
            MeshRepresentationCache meshRepresentationCache = new MeshRepresentationCache();
            meshRepresentationCache.Setup(model, PeltzerMain.Instance.worldSpace);
            remesher.AddMesh(Primitives.AxisAlignedBox(
              1, Vector3.zero, Vector3.one, /* Material id*/ 2));

            remesher.AddMesh(Primitives.AxisAlignedBox(
              2, Vector3.one, Vector3.one, /* Material id*/ 3));

            remesher.Render(model);

            remesher.RemoveMesh(1);

            remesher.Render(model);

            // Delete a mesh that isn't there.
            try
            {
                remesher.RemoveMesh(1);
                Assert.True(false, "Expected exception.");
            }
            catch (Exception)
            {
                // Expected.
            }

            // Add a duplicate mesh.
            try
            {
                remesher.AddMesh(Primitives.AxisAlignedBox(
                  2, Vector3.one, Vector3.one, /* Material id*/ 3));
                Assert.True(false, "Expected exception.");
            }
            catch (Exception)
            {
                // Expected.
            }
        }

        [Test]
        public void TestCoalescing()
        {
            ReMesher remesher = new ReMesher();
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 5);
            WorldSpace worldSpace = new WorldSpace(bounds);
            Model model = new Model(bounds);
            GameObject go = new GameObject();
            MeshRepresentationCache meshRepresentationCache = new MeshRepresentationCache();
            meshRepresentationCache.Setup(model, PeltzerMain.Instance.worldSpace);

            //  Add 100 multicolor meshes:
            for (int i = 0; i < 100; i++)
            {
                remesher.AddMesh(multiColorMesh(i, i % 5, (i + 1) % 5, (i + 2) % 5));
            }

            // Make sure they are all there:
            for (int i = 0; i < 100; i++)
            {
                NUnit.Framework.Assert.IsTrue(remesher.HasMesh(i));
                // Has three colors, should be in exactly three meshInfos.
                NUnit.Framework.Assert.AreEqual(3, remesher.MeshInMeshInfosCount(i));
            }

            // Now remove half of them:
            for (int i = 0; i < 100; i += 2)
            {
                remesher.RemoveMesh(i);
            }

            // Make sure the ReMesher has what we think it should have:
            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                {
                    // Was deleted.  Make sure it isn't anywhere:
                    NUnit.Framework.Assert.IsFalse(remesher.HasMesh(i));
                    NUnit.Framework.Assert.AreEqual(0, remesher.MeshInMeshInfosCount(i));
                }
                else
                {
                    NUnit.Framework.Assert.IsTrue(remesher.HasMesh(i));
                    NUnit.Framework.Assert.AreEqual(3, remesher.MeshInMeshInfosCount(i));
                }
            }

            // Add some new ones back:
            for (int i = 100; i < 200; i++)
            {
                remesher.AddMesh(multiColorMesh(i, i % 5, (i + 1) % 5, (i + 2) % 5));
            }

            // Check everything again:
            for (int i = 0; i < 200; i++)
            {
                if (i < 100 && i % 2 == 0)
                {
                    // Was deleted.  Make sure it isn't anywhere:
                    NUnit.Framework.Assert.IsFalse(remesher.HasMesh(i));
                    NUnit.Framework.Assert.AreEqual(0, remesher.MeshInMeshInfosCount(i));
                }
                else
                {
                    NUnit.Framework.Assert.IsTrue(remesher.HasMesh(i));
                    NUnit.Framework.Assert.AreEqual(3, remesher.MeshInMeshInfosCount(i));
                }
            }

            // Now delete everything and make sure there aren't any MeshInfos:
            for (int i = 0; i < 200; i++)
            {
                if (i >= 100 || i % 2 != 0)
                {
                    remesher.RemoveMesh(i);
                }
            }

            for (int i = 0; i < 200; i++)
            {
                NUnit.Framework.Assert.IsFalse(remesher.HasMesh(i));
                NUnit.Framework.Assert.AreEqual(0, remesher.MeshInMeshInfosCount(i));
            }
        }

        private MMesh multiColorMesh(int id, int color1, int color2, int color3)
        {
            MMesh mesh = Primitives.AxisAlignedBox(id, Vector3.zero, Vector3.one, color1);

            mesh.GetFace(0).SetProperties(new FaceProperties(color2));
            mesh.GetFace(1).SetProperties(new FaceProperties(color3));

            return mesh;
        }
    }
}
