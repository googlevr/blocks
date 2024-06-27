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

using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.google.apps.peltzer.client.serialization
{
    [TestFixture]
    // Tests for PolySerializationUtilsTest.
    public class PolySerializationUtilsTest
    {
        [Test]
        public void TestVectorsAndQuaternions()
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForWriting(16);
            serializer.StartWritingChunk(123);
            PolySerializationUtils.WriteVector3(serializer, Vector3.forward);
            PolySerializationUtils.WriteVector3(serializer, new Vector3(1000.0f, -0.1f, 1e8f));
            PolySerializationUtils.WriteQuaternion(serializer, Quaternion.identity);
            PolySerializationUtils.WriteQuaternion(serializer, Quaternion.Euler(10.0f, -20.0f, 30.0f));
            serializer.FinishWritingChunk(123);
            serializer.FinishWriting();

            byte[] output = serializer.ToByteArray();

            serializer.SetupForReading(output, 0, output.Length);
            serializer.StartReadingChunk(123);
            Assert.AreEqual(Vector3.forward, PolySerializationUtils.ReadVector3(serializer));
            Assert.AreEqual(new Vector3(1000.0f, -0.1f, 1e8f), PolySerializationUtils.ReadVector3(serializer));
            Assert.AreEqual(Quaternion.identity, PolySerializationUtils.ReadQuaternion(serializer));
            Assert.AreEqual(Quaternion.Euler(10.0f, -20.0f, 30.0f), PolySerializationUtils.ReadQuaternion(serializer));
            serializer.FinishReadingChunk(123);
        }

        [Test]
        public void TestLists()
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForWriting(16);
            serializer.StartWritingChunk(123);
            PolySerializationUtils.WriteIntList(serializer, new int[] { 100, 200, 300, 400 });
            PolySerializationUtils.WriteVector3List(serializer, new Vector3[] {
        Vector3.up, Vector3.down, Vector3.up, Vector3.down,
        Vector3.left, Vector3.right, Vector3.left, Vector3.right });  // ..., A, B, start! :-)

            serializer.FinishWritingChunk(123);
            serializer.FinishWriting();

            byte[] output = serializer.ToByteArray();

            serializer.SetupForReading(output, 0, output.Length);
            serializer.StartReadingChunk(123);

            List<int> list = PolySerializationUtils.ReadIntList(serializer);
            Assert.AreEqual(4, list.Count);
            Assert.AreEqual(100, list[0]);
            Assert.AreEqual(200, list[1]);
            Assert.AreEqual(300, list[2]);
            Assert.AreEqual(400, list[3]);

            List<Vector3> vectorList = PolySerializationUtils.ReadVector3List(serializer);
            Assert.AreEqual(8, vectorList.Count);
            Assert.AreEqual(Vector3.up, vectorList[0]);
            Assert.AreEqual(Vector3.down, vectorList[1]);
            Assert.AreEqual(Vector3.up, vectorList[2]);
            Assert.AreEqual(Vector3.down, vectorList[3]);
            Assert.AreEqual(Vector3.left, vectorList[4]);
            Assert.AreEqual(Vector3.right, vectorList[5]);
            Assert.AreEqual(Vector3.left, vectorList[6]);
            Assert.AreEqual(Vector3.right, vectorList[7]);

            serializer.FinishReadingChunk(123);
        }
    }
}