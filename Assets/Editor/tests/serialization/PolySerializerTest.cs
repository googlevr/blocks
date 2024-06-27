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

namespace com.google.apps.peltzer.client.serialization
{
    [TestFixture]
    // Tests for PolySerializer.
    public class PolySerializerTest
    {
        [Test]
        public void TestBasicTypes()
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForWriting(16);
            Assert.IsFalse(serializer.IsChunkInProgress);

            serializer.StartWritingChunk(0xDEAD);
            Assert.IsTrue(serializer.IsChunkInProgress);
            serializer.WriteInt(1234);
            serializer.WriteInt(-5678);
            serializer.WriteInt(0);
            serializer.WriteInt(int.MinValue);
            serializer.WriteInt(int.MaxValue);
            serializer.WriteString("");
            serializer.WriteString("The quick brown fox jumps over the lazy dog.");
            serializer.WriteString("色は匂へど散りぬるを我が世誰ぞ常ならん有為の奥山今日越えて浅き夢見じ酔ひもせず");
            serializer.WriteString(null);
            serializer.WriteBool(true);
            serializer.WriteBool(false);
            serializer.WriteByte((byte)12);
            serializer.WriteByte((byte)251);
            serializer.FinishWritingChunk(0xDEAD);
            Assert.IsFalse(serializer.IsChunkInProgress);

            serializer.StartWritingChunk(0xBEEF);
            Assert.IsTrue(serializer.IsChunkInProgress);
            serializer.WriteFloat(0.0f);
            serializer.WriteFloat(1.0f);
            serializer.WriteFloat((float)Math.PI);
            serializer.WriteFloat(-(float)Math.PI);
            serializer.WriteFloat(1e8f);
            serializer.WriteFloat(-1e8f);
            serializer.WriteFloat(float.PositiveInfinity);
            serializer.WriteFloat(float.NegativeInfinity);
            serializer.WriteFloat(float.NaN);
            serializer.WriteFloat(float.Epsilon);
            serializer.FinishWritingChunk(0xBEEF);
            Assert.IsFalse(serializer.IsChunkInProgress);

            serializer.FinishWriting();
            byte[] output = serializer.ToByteArray();

            // Just to make things more interesting (and dangerous!), let's copy that into a larger buffer
            // and use an offset.
            byte[] largerBuffer = new byte[output.Length + 200];
            Buffer.BlockCopy(output, 0, largerBuffer, 17, output.Length);

            // Read back.
            serializer.SetupForReading(largerBuffer, /* startOffset */ 17, /* length */ output.Length);
            Assert.IsFalse(serializer.IsChunkInProgress);

            Assert.AreEqual(0xDEAD, serializer.GetNextChunkLabel());
            serializer.StartReadingChunk(0xDEAD);
            Assert.IsTrue(serializer.IsChunkInProgress);
            Assert.AreEqual(1234, serializer.ReadInt());
            Assert.AreEqual(-5678, serializer.ReadInt());
            Assert.AreEqual(0, serializer.ReadInt());
            Assert.AreEqual(int.MinValue, serializer.ReadInt());
            Assert.AreEqual(int.MaxValue, serializer.ReadInt());
            Assert.AreEqual("", serializer.ReadString());
            Assert.AreEqual("The quick brown fox jumps over the lazy dog.", serializer.ReadString());
            Assert.AreEqual("色は匂へど散りぬるを我が世誰ぞ常ならん有為の奥山今日越えて浅き夢見じ酔ひもせず",
              serializer.ReadString());
            Assert.AreEqual(null, serializer.ReadString());
            Assert.AreEqual(true, serializer.ReadBool());
            Assert.AreEqual(false, serializer.ReadBool());
            Assert.AreEqual((byte)12, serializer.ReadByte());
            Assert.AreEqual((byte)251, serializer.ReadByte());
            serializer.FinishReadingChunk(0xDEAD);
            Assert.IsFalse(serializer.IsChunkInProgress);

            Assert.AreEqual(0xBEEF, serializer.GetNextChunkLabel());
            serializer.StartReadingChunk(0xBEEF);
            Assert.AreEqual(0.0f, serializer.ReadFloat());
            Assert.AreEqual(1.0f, serializer.ReadFloat());
            Assert.AreEqual((float)Math.PI, serializer.ReadFloat());
            Assert.AreEqual(-(float)Math.PI, serializer.ReadFloat());
            Assert.AreEqual(1e8f, serializer.ReadFloat());
            Assert.AreEqual(-1e8f, serializer.ReadFloat());
            Assert.AreEqual(float.PositiveInfinity, serializer.ReadFloat());
            Assert.AreEqual(float.NegativeInfinity, serializer.ReadFloat());
            Assert.IsNaN(serializer.ReadFloat());
            Assert.AreEqual(float.Epsilon, serializer.ReadFloat());
            serializer.FinishReadingChunk(0xBEEF);
            Assert.IsFalse(serializer.IsChunkInProgress);

            Assert.AreEqual(-1, serializer.GetNextChunkLabel());
        }

        [Test]
        public void TestChunkSkipping()
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForWriting(16);

            // Add 50 chunks, labeled 0 to 49.
            for (int i = 0; i < 50; i++)
            {
                serializer.StartWritingChunk(i);
                serializer.WriteInt(i);
                serializer.WriteBool(true);
                serializer.WriteFloat(1.234f);
                serializer.FinishWritingChunk(i);
            }

            serializer.FinishWriting();
            byte[] buffer = serializer.ToByteArray();
            serializer.SetupForReading(buffer, 0, buffer.Length);

            // Check that we can freely skip ahead, and can also omit reading parts of chunks.
            serializer.StartReadingChunk(7);
            Assert.AreEqual(7, serializer.ReadInt());
            serializer.FinishReadingChunk(7);

            serializer.StartReadingChunk(15);
            Assert.AreEqual(15, serializer.ReadInt());
            Assert.True(serializer.ReadBool());
            serializer.FinishReadingChunk(15);

            serializer.StartReadingChunk(49);
            Assert.AreEqual(49, serializer.ReadInt());
            Assert.True(serializer.ReadBool());
            serializer.FinishReadingChunk(49);
        }

        [Test]
        public void TestReuse()
        {
            // First, write a long block of data.
            PolySerializer serializer = new PolySerializer();

            serializer.SetupForWriting(1024);
            serializer.StartWritingChunk(1111);
            for (int i = 0; i < 10; i++)
            {
                serializer.WriteInt(i);
            }
            serializer.FinishWritingChunk(1111);
            serializer.FinishWriting();
            byte[] firstOutput = serializer.ToByteArray();

            // Now reset and write shorter data. This should re-use the same buffer as before.
            serializer.SetupForWriting(512);
            serializer.StartWritingChunk(1111);
            serializer.WriteInt(1234);
            serializer.FinishWritingChunk(1111);
            serializer.FinishWriting();
            byte[] secondOutput = serializer.ToByteArray();

            // The second output should be smaller than the first, even though the backing
            // buffer wasn't resized.
            Assert.IsTrue(secondOutput.Length < firstOutput.Length);

            // Let's check that the data ends where we expect it to.
            serializer.SetupForReading(secondOutput, 0, secondOutput.Length);
            serializer.StartReadingChunk(1111);
            Assert.AreEqual(1234, serializer.ReadInt());
            // Shouldn't have any more data.
            Assert.Throws<Exception>(() => { serializer.ReadInt(); });
            serializer.FinishReadingChunk(1111);
        }

        [Test]
        public void TestSanityChecks()
        {
            // First let's make some example data.
            PolySerializer s = new PolySerializer();
            s.SetupForWriting(16);
            s.StartWritingChunk(1111);
            s.WriteInt(1234);
            s.WriteInt(5678);
            s.FinishWritingChunk(1111);
            s.StartWritingChunk(2222);
            s.WriteFloat(1.0f);
            s.WriteFloat(2.0f);
            s.FinishWritingChunk(2222);
            s.FinishWriting();
            byte[] exampleData = s.ToByteArray();

            // Reading without setting up should fail.
            Assert.Throws<Exception>(() => { new PolySerializer().StartReadingChunk(1111); });
            // Writing without setting up should fail.
            Assert.Throws<Exception>(() => { new PolySerializer().StartWritingChunk(1111); });

            // Reading data without starting a chunk is wrong.
            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForReading(exampleData, 0, exampleData.Length);
                serializer.ReadInt();
            });
            // Writing data without starting a chunk is wrong.
            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForWriting(16);
                serializer.WriteInt(1234);
            });

            // Writing data when open for reading is wrong.
            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForReading(exampleData, 0, exampleData.Length);
                serializer.StartReadingChunk(1111);
                serializer.WriteInt(1234);
            });
            // Reading data when open for writing is wrong.
            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForWriting(16);
                serializer.ReadInt();
            });

            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForReading(exampleData, 0, exampleData.Length);
                serializer.StartReadingChunk(1111);
                // Starting to read another chunk without finishing the current one should fail.
                serializer.StartReadingChunk(2222);
            });

            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForWriting();
                serializer.StartWritingChunk(1111);
                // Starting to write another chunk while in the middle of writing one chunk should fail,
                // as we don't allow nested chunks.
                serializer.StartWritingChunk(2222);
            });

            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForReading(exampleData, 0, exampleData.Length);
                serializer.StartReadingChunk(1111);
                serializer.ReadInt();
                serializer.ReadInt();
                // Reading past the end of the chunk should fail.
                serializer.ReadInt();
            });

            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForReading(exampleData, 0, exampleData.Length);
                // Requesting a chunk that doesn't exist should fail.
                serializer.StartReadingChunk(9999);
            });

            Assert.Throws<Exception>(() =>
            {
                PolySerializer serializer = new PolySerializer();
                serializer.SetupForWriting();
                serializer.StartWritingChunk(1111);
                // Trying to get the byte array without finishing writing should fail.
                serializer.ToByteArray();
            });
        }
    }
}