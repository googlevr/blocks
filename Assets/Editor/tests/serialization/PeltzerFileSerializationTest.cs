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
using com.google.apps.peltzer.client.model.export;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.google.apps.peltzer.client.serialization
{
    [TestFixture]
    // Tests for serializting a PeltzerFile (including MMeshes and stuff).
    public class PeltzerFileSerializationTest
    {
        private const float EPSILON = 1e-4f;

        [Test]
        public void TestSerializePeltzerFile()
        {
            // Let's create a test file with a few meshes.
            Metadata metadata = new Metadata("Randall Peltzer", "Jun 8, 1984", "1.0");
            List<PeltzerMaterial> materials = new List<PeltzerMaterial>();
            materials.Add(new PeltzerMaterial(/* materialId */ 100, /* color */ 0x101010));
            materials.Add(new PeltzerMaterial(/* materialId */ 200, /* color */ 0x202020));
            materials.Add(new PeltzerMaterial(/* materialId */ 300, /* color */ 0x303030));
            List<MMesh> meshes = new List<MMesh>();

            // Add three highly exciting primitives.
            MMesh box =
              Primitives.AxisAlignedBox(1000, new Vector3(1.0f, 2.0f, 3.0f), new Vector3(1.0f, 20.0f, 5.0f), 100);
            MMesh cylinder =
              Primitives.AxisAlignedCylinder(2000, new Vector3(1.0f, 2.0f, 3.0f), new Vector3(1.0f, 20.0f, 5.0f),
                /* holeRadius */null, 200);
            MMesh sphere =
              Primitives.AxisAlignedIcosphere(3000, new Vector3(2000.0f, -4000.0f, 6000.0f), Vector3.one, 300);
            meshes.Add(box);
            meshes.Add(cylinder);
            meshes.Add(sphere);

            List<Command> allCommands = new List<Command>();
            List<Command> undoStack = new List<Command>();
            List<Command> redoStack = new List<Command>();

            PeltzerFile savedFile = new PeltzerFile(metadata, /* zoomFactor */ 1.5f, materials, meshes);
            int estimate = savedFile.GetSerializedSizeEstimate();

            // Serialize to a byte buffer.
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForWriting(16);
            savedFile.Serialize(serializer);
            serializer.FinishWriting();

            byte[] output = serializer.ToByteArray();
            Assert.IsTrue(output.Length <= estimate);

            // Now let's read back the buffer and check that everything looks good.
            serializer.SetupForReading(output, 0, output.Length);
            PeltzerFile loadedFile = new PeltzerFile(serializer);

            Assert.AreEqual("Randall Peltzer", loadedFile.metadata.creatorName);
            Assert.AreEqual("Jun 8, 1984", loadedFile.metadata.creationDate);
            Assert.AreEqual("1.0", loadedFile.metadata.version);
            Assert.AreEqual(3, loadedFile.materials.Count);
            Assert.AreEqual(100, loadedFile.materials[0].materialId);
            Assert.AreEqual(0x101010, loadedFile.materials[0].color);
            Assert.AreEqual(200, loadedFile.materials[1].materialId);
            Assert.AreEqual(0x202020, loadedFile.materials[1].color);
            Assert.AreEqual(300, loadedFile.materials[2].materialId);
            Assert.AreEqual(0x303030, loadedFile.materials[2].color);
            Assert.AreEqual(3, loadedFile.meshes.Count);
            Dictionary<int, MMesh> meshDict = new Dictionary<int, MMesh>();
            foreach (MMesh mesh in loadedFile.meshes)
            {
                meshDict[mesh.id] = mesh;
            }
            AssertMeshesEqual(box, meshDict[1000]);
            AssertMeshesEqual(cylinder, meshDict[2000]);
            AssertMeshesEqual(sphere, meshDict[3000]);
        }

        private static void AssertMeshesEqual(MMesh a, MMesh b)
        {
            Assert.AreEqual(a.id, b.id);
            Assert.IsTrue((a.offset - b.offset).magnitude < EPSILON);
            Assert.IsTrue((Quaternion.Angle(a.rotation, b.rotation) < EPSILON));
            Assert.AreEqual(a.groupId, b.groupId);
            foreach (Vertex vertexInA in a.GetVertices())
            {
                Assert.IsTrue(b.HasVertex(vertexInA.id));
                Vertex vertexInB = b.GetVertex(vertexInA.id);
                Assert.IsTrue((vertexInA.loc - vertexInB.loc).magnitude < EPSILON);
            }
            foreach (Vertex vertexInB in b.GetVertices())
            {
                Assert.IsTrue(a.HasVertex(vertexInB.id));
            }
            foreach (Face faceInA in a.GetFaces())
            {
                Face faceInB;
                Assert.IsTrue(b.HasFace(faceInA.id));
                faceInB = b.GetFace(faceInA.id);

                Assert.AreEqual(faceInA.properties.materialId, faceInB.properties.materialId);
                Assert.AreEqual(faceInA.vertexIds.Count, faceInB.vertexIds.Count);
                for (int i = 0; i < faceInA.vertexIds.Count; i++)
                {
                    Assert.AreEqual(faceInA.vertexIds[i], faceInB.vertexIds[i]);
                }

                Assert.IsTrue((faceInA.normal - faceInB.normal).magnitude < EPSILON);

            }
            foreach (Face faceInB in b.GetFaces())
            {
                Assert.IsTrue(a.HasFace(faceInB.id));
            }
        }
    }
}