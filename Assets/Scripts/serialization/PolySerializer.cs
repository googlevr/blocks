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
using System.Text;

namespace com.google.apps.peltzer.client.serialization
{
    /// <summary>
    /// Handles serialization to and from the Poly file format.
    ///
    /// DESIGN GOALS: implementing serialization using objects or protos in C# can lead to a lot of garbage,
    /// which was causing performance problems (bug) as it causes the GC to pause the main thread,
    /// leading to frame loss.
    ///
    /// This class is designed to allow serialization and deserialization while minimizing allocation
    /// and garbage generation.
    ///
    /// Also, this format is designed for backwards and forward compatibility: a newer version of the code should
    /// be able to read an older file version and an older version of the code should be able to read a newer
    /// file version. This is accomplished by grouping data into chunks as described below.
    ///
    /// CHUNKS
    /// A chunk of data is simply an array of bytes. The file consists of several such arrays, each with
    /// a label indicating what they are. This is used to ensure backwards/forward compatibility as described
    /// later.
    ///
    /// HOW TO USE
    /// To use this class, create an instance using the default constructor. Before using it, you must set it
    /// up for reading or writing:
    ///
    ///    serializer = new PolySerializer()
    ///    serializer.SetupForWriting();
    ///
    /// Now you can write chunks:
    ///
    ///    const int MY_CHUNK_LABEL = 1234;
    ///    serializer.StartWritingChunk(MY_CHUNK_LABEL);
    ///    serializer.WriteInt(10);
    ///    serializer.WriteFloat(1.235f);
    ///    //...etc...
    ///    serializer.FinishWritingChunk(MY_CHUNK_LABEL);
    ///
    /// When done, you can convert the result to a byte array:
    ///
    ///    serializer.ToByteArray();
    ///
    /// Or, to avoid an extra allocation, just get direct access to the buffer:
    ///
    ///    byte[] buffer;
    ///    int offset, length;
    ///    serializer.GetUnderlyingDataBuffer(out buffer, out offset, out length);
    ///    //...do something with buffer[offset..offset+length-1]...
    ///
    /// To use for reading:
    ///
    ///    byte[] inputBuffer = //...get the data from somewhere...
    ///
    ///    serializer = new PolySerializer();
    ///    serializer.SetupForReading(inputBuffer, 0, inputBuffer.Length);
    ///
    ///    serializer.StartReadingChunk(MY_CHUNK_LABEL);
    ///    int myInt = serializer.ReadInt();
    ///    float myFloat = serializer.ReadFloat();
    ///    //..etc..
    ///    serializer.FinishReadingChunk(MY_CHUNK_LABEL);
    ///
    /// DATA FORMAT
    ///
    /// Data is segmented into CHUNKS. Each chunk consists of a header and a body. The header is 12 bytes long
    /// and consists of:
    ///    * Chunk start mark (4 bytes). The literal integer 0x1337 (defined in the CHUNK_START_MARK constant below).
    ///    * Chunk label (4 bytes). An arbitrary (user-defined) integer describing what the chunk is.
    ///    * Chunk size (4 bytes). The size of the chunk in bytes, INCLUDING the header.
    ///
    /// After the chunk header comes the chunk body.
    /// The data in the chunk body is just raw bytes representing ints, floats, booleans, strings, etc. It's not
    /// annotated or delimited, so the reader is supposed to know how to parse it.
    /// Also, for simplicity, we do not allow nested chunks.
    ///
    /// |---- chunk header  ----|----   chunk body     ----|---- chunk header  ----|---- chunk body    ....
    ///
    /// +-------+-------+-------+--------------------------+-------+-------+-------+----------------------/
    /// | CSM   | label | size  |          data            | CSM   | label | size  |       data           \   ...
    /// +-------+-------+-------+--------------------------+-------+-------+-------+----------------------/
    ///
    /// (CSM = Chunk Start Mark. The literal integer 0x1337).
    ///
    /// NOTE ABOUT ENDIAN-NESS: Data is always written in little-endian format, regardless of the host architecture.
    /// So the integer 0x11223344 is written as 0x44, 0x33, 0x22, 0x11.
    ///
    /// BACKWARD COMPATIBILITY (NEWER CODE READING OLDER FORMAT)
    ///
    /// When newer code is reading old data, it can always query to see what the next chunk is before reading it,
    /// so it can detect whether or not newly defined chunks are present before attempting to read them. If they
    /// are present, it can read them. If they are not present, it can skip them. So, for example:
    ///
    ///     const int BORING_V1_STUFF_CHUNK = 9000;
    ///     const int ADVANCED_V2_STUFF_CHUNK = 9001;
    ///     const int EVEN_MORE_ADVANCED_V3_STUFF = 9002;
    ///
    ///     serializer.StartReadingChunk(BORING_V1_STUFF_CHUNK);
    ///     // ...read basic data that's been there since v1...
    ///     serializer.FinishReadingChunk(BORING_V1_STUFF_CHUNK);
    ///
    ///     if (serializer.GetNextChunkLabel() == ADVANCED_V2_STUFF_CHUNK) {
    ///       // Advanced V2 features are present, process them.
    ///       serializer.StartReadingChunk(ADVANCED_V2_STUFF_CHUNK);
    ///       // ...read data...
    ///       serializer.FinishReadingChunk(ADVANCED_V2_STUFF_CHUNK);
    ///     }
    ///     if (serializer.GetNextChunkLabel() == EVEN_MORE_ADVANCED_V3_STUFF) {
    ///       // Super advanced V3 features are present, process them.
    ///       serializer.StartReadingChunk(EVEN_MORE_ADVANCED_V3_STUFF);
    ///       // ...read data...
    ///       serializer.FinishReadingChunk(EVEN_MORE_ADVANCED_V3_STUFF);
    ///     }
    ///
    /// FORWARD COMPATIBILITY (OLDER CODE READING NEWER FORMAT)
    /// When older code comes across a file written by a newer version, it can still attempt to read the parts of
    /// it that it understands. This is simple, because when the code requests to read a chunk, the logic in this
    /// class will actually skip over any unidentified chunks that are in the way, so older code will just read
    /// the chunks that it can handle:
    ///
    ///    const int BORING_V1_STUFF_CHUNK = 9000;
    ///    const int SOMETHING_ELSE = 10000;
    ///
    ///    serializer.StartReadingChunk(BORING_V1_STUFF_CHUNK);
    ///    // ...read data...
    ///    serializer.FinishReadingChunk(BORING_V1_STUFF_CHUNK);
    ///
    ///    // Go on to something else:
    ///    serializer.StartReadingChunk(SOMETHING_ELSE);
    ///    // ...read data...
    ///
    /// At the point where we call StartReadingChunk(SOMETHING_ELSE), we will actually look at the file and
    /// skip over the two unidentified chunks 9001 and 9002 that contain the more advanced data, as if those
    /// chunks didn't exist at all.
    ///
    /// VERSION CUT-OFF
    /// This class does NOT implement a mechanism by which the client can tell that it's too out of date to
    /// read a given file. This must be implemented by the user of this class (an idea is to make the first
    /// chunk in the file contain a "minimum version" number).
    ///
    /// NOTE ABOUT AssertOrThrow:
    /// AssertOrThrow IS NOT used in the critical parts of the code because when the second argument is a complicated
    /// concatenation of strings, the concatenation will still have to be computed even if the condition is true,
    /// which defeats the purpose of not generating tons of garbage.
    /// </summary>
    public class PolySerializer
    {
        /// <summary>
        /// Marker used to indicate the start of a chunk.
        /// </summary>
        private const int CHUNK_START_MARK = 0x1337;

        /// <summary>
        /// Size of the chunk header.
        /// </summary>
        private const int CHUNK_HEADER_SIZE = 12;

        /// <summary>
        /// Marker used to represent a null string.
        /// </summary>
        private const int NULL_STRING_MARKER = -9999;

        private const int DEFAULT_INITIAL_CAPACITY = 128 * 1024;

        /// <summary>
        /// Marker that indicates something is a field that contains the count of something.
        /// See ReadCount() for more info.
        /// </summary>
        private const int COUNT_FIELD_MARKER = 0xc0c0;

        /// <summary>
        /// Mode of operation (reading or writing).
        /// </summary>
        private enum Mode
        {
            // Mode not set (uninitialized).
            UNSET,
            // Open for reading (can read chunks from the buffer).
            READING,
            // Open for writing (can write chunks to the buffer).
            WRITING,
            // Finished writing. In this state writing has finished, and nothing else can be written.
            // But the buffer can be queried for the results of the write operation.
            FINISHED_WRITING,
        }

        /// <summary>
        /// Our current mode.
        /// </summary>
        private Mode mode = Mode.UNSET;

        /// <summary>
        /// Indicates if we are in the middle of reading/writing a chunk.
        /// </summary>
        private bool isChunkInProgress;

        /// <summary>
        /// Buffer that contains the data. May be oversized (the correct start and length of the data in the buffer
        /// is given by the dataStartOffset and dataLength field).
        /// </summary>
        private byte[] dataBuffer;

        /// <summary>
        /// The offset in the dataBuffer where the data starts.
        /// Only the bytes in dataBuffer[dataStartOffset..dataStart+dataLength-1] are valid data.
        /// </summary>
        private int dataStartOffset;

        /// <summary>
        /// Length of the data in the buffer.
        /// Only the bytes in dataBuffer[dataStartOffset..dataStart+dataLength-1] are valid data.
        /// </summary>
        private int dataLength;

        /// <summary>
        /// Current read/write offset in the data.
        /// </summary>
        private int curOffset;

        /// <summary>
        /// Offset where the current chunk started.
        /// </summary>
        private int chunkStartOffset = -1;

        /// <summary>
        /// Label of the current chunk being written or read.
        /// </summary>
        private int chunkLabel = -1;

        /// <summary>
        /// Size of the current chunk we are reading.
        /// Only valid when READING. We don't keep this up to date during the process of writing a
        /// chunk. We compute the size only when we finish the writing the chunk.
        /// </summary>
        private int chunkSizeForReading;

        /// <summary>
        /// Creates a new PolySerializer. Before using, it must be set up with one of the Setup*() methods.
        /// </summary>
        public PolySerializer() { }

        /// <summary>
        /// Sets up the serializer for reading from the given byte array.
        /// This object will use the buffer directly, so the caller must not modify it while this class
        /// is using it.
        /// </summary>
        /// <param name="buffer">The buffer to use. The implementation uses the buffer directly, not a copy, so the
        /// caller MUST NOT modify the buffer (or at least the part of the buffer between dataOffset and
        /// dataOffset+dataLength-1) while this class is using it.</param>
        /// <param name="startOffset">The offset in the buffer where the data starts.</param>
        /// <param name="length">The length of the data in the buffer.</param>
        public void SetupForReading(byte[] buffer, int startOffset, int length)
        {
            Setup(Mode.READING, buffer, startOffset, length);
        }

        /// <summary>
        /// Sets up the serializer for writing. It will use an internally allocated byte array which will
        /// get resized as needed. The caller can specify an initial capacity for it.
        /// </summary>
        /// <param name="minInitialCapacity">Initial capacity of the buffer. The caller should supply
        /// an approximate guess to how big the data will be. The more accurate the guess, the fewer
        /// re-allocations of the buffer will be made, so less garbage will be produced.</param>
        public void SetupForWriting(int minInitialCapacity = DEFAULT_INITIAL_CAPACITY)
        {
            // Reuse our buffer, if it's big enough and we were already in write mode before.
            // This allows us to reduce allocation.
            byte[] bufferToUse = (mode == Mode.WRITING && dataBuffer != null && dataBuffer.Length >= minInitialCapacity) ?
              dataBuffer : new byte[minInitialCapacity];
            // Note: dataLength is 0 for writing because we start with empty data.
            Setup(Mode.WRITING, bufferToUse, 0, 0);
        }

        private void Setup(Mode mode, byte[] buffer, int startOffset, int length)
        {
            if (startOffset + length > buffer.Length)
            {
                Throw("Data start offset (" + startOffset + ") + data length (" + length +
                  ") can't be larger than buffer (" + buffer.Length + ")");
            }
            this.mode = mode;
            dataBuffer = buffer;
            dataLength = length;
            dataStartOffset = startOffset;
            curOffset = startOffset;
            isChunkInProgress = false;
            chunkSizeForReading = -1;
            chunkLabel = -1;
            chunkStartOffset = -1;
        }

        /// <summary>
        /// Convenience method for checking that a buffer appears to have a valid header, before trying to process it.
        /// This is useful as a quick check to see if a buffer is in the right file format before trying to
        /// deserialize it (when multiple possible serialization formats are allowed).
        /// </summary>
        /// <param name="buffer">The buffer to check.</param>
        /// <param name="offset">The offset in the buffer where the data starts.</param>
        /// <param name="length">The length of the data in the buffer.</param>
        /// <returns>True if the data has a valid header, false if not.</returns>
        public static bool HasValidHeader(byte[] buffer, int offset, int length)
        {
            return
              // Must be long enough to contain a chunk header.
              (length >= CHUNK_HEADER_SIZE) && (offset + CHUNK_HEADER_SIZE <= buffer.Length) &&
              // ..and the chunk start mark must be present at the beginning.
              CHUNK_START_MARK == DecodeInt(buffer, offset);
        }

        /// <summary>
        /// Returns whether or not a chunk is currently open for reading/writing. This will be true between the
        /// call to StartWritingChunk or StartReadingChunk, and the corresponding call to FinishWritingChunk
        /// or FinishReadingChunk.
        /// </summary>
        public bool IsChunkInProgress { get { return isChunkInProgress; } }

        /// <summary>
        /// Starts writing a chunk.
        /// It is an error to call this if not in write mode, or if a chunk is already being written (there are
        /// no nested chunks).
        /// </summary>
        /// <param name="label">The user-defined label of the chunk.</param>
        public void StartWritingChunk(int label)
        {
            if (mode != Mode.WRITING) Throw("Can't write chunk. Not set up for writing.");
            if (isChunkInProgress) Throw("Can't start writing chunk " + label + ", was already writing one.");
            // Make sure we have enough space for the chunk header (12 bytes).
            PrepareToWrite(CHUNK_HEADER_SIZE);
            // Memorize that this is where the current chunk starts.
            chunkStartOffset = curOffset;
            chunkLabel = label;
            // Leave space for the chunk header (which we will come back and write later, in FinishWritingChunk).
            curOffset += CHUNK_HEADER_SIZE;
            // Ready to start writing chunk data.
            isChunkInProgress = true;
        }

        /// <summary>
        /// Finishes writing the current chunk.
        /// </summary>
        /// <param name="label">The label of the chunk to finish writing. This is for sanity-checking.</param>
        public void FinishWritingChunk(int label)
        {
            // Sanity check that we are ending the same chunk that we started.
            if (mode != Mode.WRITING) Throw("Can't finish writing chunk. Not set up for writing.");
            if (!isChunkInProgress) Throw("Can't finish writing chunk. No write in progress.");
            if (chunkLabel != label)
            {
                Throw("Wrong label on FinishWritingChunk, expected " + chunkLabel + ", got " + label);
            }

            // Write the chunk header (we left space for it in StartWritingChunk).
            // The chunk size is INCLUSIVE of the header, so it's just the difference from the
            // current offset to the offset where the chunk started.
            EncodeChunkHeader(chunkStartOffset, chunkLabel, curOffset - chunkStartOffset);

            isChunkInProgress = false;
        }

        /// <summary>
        /// Writes an int to the current chunk.
        /// Can only be called when currently writing a chunk.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteInt(int value)
        {
            if (mode != Mode.WRITING) Throw("Can't write. Not set up for writing.");
            if (!isChunkInProgress) Throw("Can't write. Not currently in a chunk.");
            PrepareToWrite(4);
            EncodeInt(curOffset, value);
            curOffset += 4;
        }

        /// <summary>
        /// Writes a byte to the current chunk.
        /// Can only be called when currently writing a chunk.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteByte(byte value)
        {
            if (mode != Mode.WRITING) Throw("Can't write. Not set up for writing.");
            if (!isChunkInProgress) Throw("Can't write. Not currently in a chunk.");
            PrepareToWrite(1);
            dataBuffer[curOffset] = value;
            curOffset++;
        }

        /// <summary>
        /// Writes a boolean to the current chunk.
        /// Can only be called when currently writing a chunk.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteBool(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Writes a floating point value to the current chunk.
        /// Can only be called when currently writing a chunk.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteFloat(float value)
        {
            if (mode != Mode.WRITING) Throw("Can't write. Not set up for writing.");
            if (!isChunkInProgress) Throw("Can't write. Not currently in a chunk.");
            PrepareToWrite(4);
            EncodeFloat(curOffset, value);
            curOffset += 4;
        }

        /// <summary>
        /// Writes a string to the current chunk.
        /// Can only be called when currently writing a chunk.
        /// </summary>
        /// <param name="value">The value to write. A null strings are handled correctly and will be read back
        /// as a null string.</param>
        public void WriteString(string value)
        {
            if (mode != Mode.WRITING) Throw("Can't write. Not set up for writing.");
            if (!isChunkInProgress) Throw("Can't write. Not currently in a chunk.");

            // Special case for writing NULL: write a null marker instead of the length.
            if (value == null)
            {
                WriteInt(NULL_STRING_MARKER);
                return;
            }

            int byteCount = Encoding.UTF8.GetByteCount(value);
            // We will write 4 bytes for the buffer length and then the bytes.
            WriteInt(byteCount);
            PrepareToWrite(byteCount);
            int actualByteCount = Encoding.UTF8.GetBytes(value, 0, value.Length, dataBuffer, curOffset);
            // The actual bytes written should be the same as our pre-calculated amount. This is guaranteed
            // to be true by the UTF8 contract, but just as a sanity check, let's verify:
            if (byteCount != actualByteCount)
            {
                Throw("UTF8 encoding error, expected " + byteCount + " bytes, got " + actualByteCount + " bytes");
            }

            curOffset += byteCount;
        }

        /// <summary>
        /// Writes a count. A count is just an int with a special marker indicating that it's a count.
        /// </summary>
        /// <param name="count"></param>
        public void WriteCount(int count)
        {
            WriteInt(COUNT_FIELD_MARKER);
            WriteInt(count);
        }

        /// <summary>
        /// Indicates that writing has finished. After calling this, you can query for the buffer with
        /// ToByteArray() or GetUnderlyingDataBuffer(). This can only be called in write mode, and when
        /// a chunk is not currently in progress.
        /// </summary>
        public void FinishWriting()
        {
            if (mode != Mode.WRITING) Throw("Can't finish writing. Not currently writing.");
            if (isChunkInProgress) Throw("Can't finish writing. A chunk is still in progress.");
            mode = Mode.FINISHED_WRITING;
        }

        /// <summary>
        /// Obtains a reference the underlying buffer, data offset and data length.
        /// This can only be called after writing is finished (after a call to FinishWriting()).
        /// The returned buffer IS OWNED BY THIS INSTANCE and should not be modified by the caller.
        /// </summary>
        public void GetUnderlyingDataBuffer(out byte[] buffer, out int offset, out int length)
        {
            if (mode != Mode.FINISHED_WRITING) Throw("Can't get byte array. Not in write finished mode.");
            buffer = dataBuffer;
            offset = dataStartOffset;
            length = dataLength;
        }

        /// <summary>
        /// Converts the resulting buffer into a byte array. The returned byte array will have exactly the
        /// contents of the data.
        /// This can only be called when the serializer is in write mode, and when not writing a chunk.
        /// For performance reasons, it is preferrable to use GetUnderlyingDataBuffer() instead, as that avoids
        /// the allocation of a new buffer.
        /// </summary>
        /// <returns></returns>
        public byte[] ToByteArray()
        {
            if (mode != Mode.FINISHED_WRITING) Throw("Can't get byte array. Not in write finished mode.");
            byte[] result = new byte[dataLength];
            Buffer.BlockCopy(
              /* src */ dataBuffer, /* srcOffset */ dataStartOffset,
              /* dst */ result, /* dstOffset */ 0,
              /* count */ dataLength);
            return result;
        }

        /// <summary>
        /// Returns the label of the next chunk.
        /// </summary>
        /// <returns>The label of the next chunk in the data, or -1 if the data ends.</returns>
        public int GetNextChunkLabel()
        {
            if (mode != Mode.READING) Throw("Can't read next chunk label. Not set up for reading.");
            if (isChunkInProgress) Throw("Can't read next chunk label. Chunk currently in progress.");

            if (!HasEnoughDataForChunkHeader(curOffset))
            {
                // End of data, no more chunks.
                return -1;
            }
            int label;
            int unused;
            DecodeChunkHeader(curOffset, out label, out unused);
            return label;
        }

        /// <summary>
        /// Seeks ahead until we find the beginning of the chunk with the given label.
        /// Ignores other chunks that are found before that.
        /// </summary>
        /// <param name="chunkLabelToRead">The label to advance to.</param
        public void StartReadingChunk(int chunkLabelToRead)
        {
            if (mode != Mode.READING) Throw("Can't start reading chunk. Not set up for reading.");
            if (isChunkInProgress) Throw("Can't start reading chunk " + chunkLabelToRead + ". Was already reading one.");

            int thisLabel, thisSize;

            // Skip chunks until we find the one we want.
            // This allows files generated with newer versions of the serialization code to include chunks for
            // new functionality, which older versions will just ignore.
            while (true)
            {
                // If we got to the end and didn't find the chunk, abort.
                if (!HasEnoughDataForChunkHeader(curOffset)) Throw("Chunk not found: " + chunkLabel);

                // Check if this chunk is the one we're looking for.
                DecodeChunkHeader(curOffset, out thisLabel, out thisSize);

                // If it's the right label, stop here. This is the chunk we want.
                if (thisLabel == chunkLabelToRead) break;

                // Not the right label, so skip to the next chunk.
                curOffset += thisSize;
            }

            // At this point, we are now positioned at the start of the correct chunk.
            isChunkInProgress = true;

            chunkLabel = thisLabel;
            chunkStartOffset = curOffset;
            chunkSizeForReading = thisSize;

            // Skip the header, start reading from body of chunk.
            curOffset += CHUNK_HEADER_SIZE;
        }

        /// <summary>
        /// Finishes reading a chunk.
        /// </summary>
        /// <param name="chunkLabelToFinish">The chunk label of the chunk to finish. For sanity checking.</param>
        public void FinishReadingChunk(int chunkLabelToFinish)
        {
            if (mode != Mode.READING) Throw("Can't finish reading chunk. Not set up for reading.");
            if (!isChunkInProgress) Throw("Can't finish reading chunk " + chunkLabelToFinish + ". Was not reading one.");
            if (chunkLabel != chunkLabelToFinish)
            {
                Throw("Wrong chunk label when finishing reading, expected " + chunkLabel + " got " + chunkLabelToFinish);
            }

            // Advance to the end of the chunk, skipping any portion of the chunk that wasn't read.
            curOffset = chunkStartOffset + chunkSizeForReading;
            isChunkInProgress = false;
        }

        /// <summary>
        /// Reads an int from the current chunk.
        /// Can only be called in read mode, and when reading a chunk.
        /// </summary>
        /// <returns>The value.</returns>
        public int ReadInt()
        {
            AssertCanRead(4);
            int value = DecodeInt(curOffset);
            curOffset += 4;
            return value;
        }

        /// <summary>
        /// Reads a count, optionally with range checking. A count is just an integer, but it's annotated in a special
        /// way for sanity checking because reading the wrong thing as a count can lead to bizarre results (for example,
        /// erroneously believing that a mesh has 1,000,000,000 faces will likely exhaust memory during load).
        /// </summary>
        /// <param name="min">The minimum acceptable value for the count. Defaults to 0.</param>
        /// <param name="max">The maximum acceptable value for the count. Defaults to INT_MAX.</param>
        /// <param name="name">Name of the count, for debug purposes (if an exception is thrown).</param>
        /// <returns>The count.</returns>
        public int ReadCount(int min = 0, int max = int.MaxValue, string name = "untitled")
        {
            int marker = ReadInt();
            if (marker != COUNT_FIELD_MARKER) Throw("Expected count field marker, saw instead " + marker);
            int count = ReadInt();
            if (count < min || count > max)
            {
                Throw("Count (" + name + ") out of acceptable range [" + min + ", " + max + "]");
            }
            return count;
        }

        /// <summary>
        /// Reads a byte from the current chunk.
        /// Can only be called in read mode, and when reading a chunk.
        /// </summary>
        /// <returns>The value.</returns>
        public byte ReadByte()
        {
            AssertCanRead(1);
            byte value = dataBuffer[curOffset];
            curOffset++;
            return value;
        }

        /// <summary>
        /// Reads a boolean from the current chunk.
        /// Can only be called in read mode, and when reading a chunk.
        /// </summary>
        /// <returns>The value.</returns>
        public bool ReadBool()
        {
            return ReadByte() != 0;
        }

        /// <summary>
        /// Reads a floating point value from the current chunk.
        /// Can only be called in read mode, and when reading a chunk.
        /// </summary>
        /// <returns>The value.</returns>
        public float ReadFloat()
        {
            AssertCanRead(4);
            float value = DecodeFloat(curOffset);
            curOffset += 4;
            return value;
        }

        /// <summary>
        /// Reads a string value from the current chunk.
        /// Can only be called in read mode, and when reading a chunk.
        /// </summary>
        /// <returns>The string. Returns null to represent that a null string was actually present
        /// in the file (null is a valid value that can be written and read back).</returns>
        public string ReadString()
        {
            AssertCanRead(4);
            // First read the length of the byte array.
            int byteCount = ReadInt();
            if (byteCount == NULL_STRING_MARKER)
            {
                // Special case for the null string.
                return null;
            }
            else if (byteCount < 0)
            {
                Throw("Invalid byte count for string: " + byteCount);
            }
            // Now read the byte array.
            AssertCanRead(byteCount);
            string value = Encoding.UTF8.GetString(dataBuffer, curOffset, byteCount);
            curOffset += byteCount;
            return value;
        }

        private void PrepareToWrite(int amount)
        {
            int required = curOffset + amount;
            if (required > dataBuffer.Length)
            {
                byte[] newBuffer = new byte[required * 2];
                Buffer.BlockCopy(
                  /* src */ dataBuffer, /* srcOffset */ dataStartOffset,
                  /* dst */ newBuffer, /* dstOffset */ dataStartOffset,
                  /* count */ dataLength);
                dataBuffer = newBuffer;
            }
            dataLength = Math.Max(dataLength, curOffset + amount - dataStartOffset);
        }

        private bool HasEnoughDataForChunkHeader(int offset)
        {
            return offset + CHUNK_HEADER_SIZE <= dataStartOffset + dataLength;
        }

        private void AssertCanRead(int bytes)
        {
            if (mode != Mode.READING) Throw("Can't read. Not set up for reading.");
            if (!isChunkInProgress) Throw("Can't read. Chunk not open for reading.");
            if (curOffset + bytes > chunkStartOffset + chunkSizeForReading)
            {
                Throw("Unexpected end of chunk. Can't read " + bytes + " bytes.");
            }
            // These should not normally happen on well-formed chunks, but let's check anyway:
            if (curOffset + bytes > dataStartOffset + dataLength)
            {
                Throw("Unexpected end of data. Can't read " + bytes + " bytes.");
            }
            if (curOffset + bytes > dataBuffer.Length)
            {
                Throw("Unexpected end of buffer. Can't read " + bytes + " bytes.");
            }
        }

        private void EncodeInt(int offset, int value)
        {
            uint uvalue = (uint)value;
            // Little-endian (least significant byte first).
            dataBuffer[offset] = (byte)uvalue;
            dataBuffer[offset + 1] = (byte)(uvalue >> 8);
            dataBuffer[offset + 2] = (byte)(uvalue >> 16);
            dataBuffer[offset + 3] = (byte)(uvalue >> 24);
        }

        private int DecodeInt(int offset)
        {
            return DecodeInt(dataBuffer, offset);
        }

        private static int DecodeInt(byte[] buffer, int offset)
        {
            // Little-endian (least significant byte first).
            uint uvalue =
              ((uint)buffer[offset]) |
              ((uint)buffer[offset + 1] << 8) |
              ((uint)buffer[offset + 2] << 16) |
              ((uint)buffer[offset + 3] << 24);
            return (int)uvalue;
        }

        private void EncodeFloat(int offset, float value)
        {
            // While we COULD in theory do complicated math to encode a float without relying on platform endianness,
            // it's easy to just look in memory using an unsafe{} block.
            unsafe
            {
                byte* ptr = (byte*)&value;
                if (isPlatformBigEndian)
                {
                    dataBuffer[offset] = ptr[3];
                    dataBuffer[offset + 1] = ptr[2];
                    dataBuffer[offset + 2] = ptr[1];
                    dataBuffer[offset + 3] = ptr[0];
                }
                else
                {
                    dataBuffer[offset] = ptr[0];
                    dataBuffer[offset + 1] = ptr[1];
                    dataBuffer[offset + 2] = ptr[2];
                    dataBuffer[offset + 3] = ptr[3];
                }
            }
        }

        private float DecodeFloat(int offset)
        {
            float value = 0;
            unsafe
            {
                byte* ptr = (byte*)&value;
                if (isPlatformBigEndian)
                {
                    ptr[3] = dataBuffer[offset];
                    ptr[2] = dataBuffer[offset + 1];
                    ptr[1] = dataBuffer[offset + 2];
                    ptr[0] = dataBuffer[offset + 3];
                }
                else
                {
                    ptr[0] = dataBuffer[offset];
                    ptr[1] = dataBuffer[offset + 1];
                    ptr[2] = dataBuffer[offset + 2];
                    ptr[3] = dataBuffer[offset + 3];
                }
            }
            return value;
        }

        private void DecodeChunkHeader(int offset, out int label, out int size)
        {
            int mark = DecodeInt(offset);
            if (mark != CHUNK_START_MARK)
            {
                Throw("Chunk start mark not found at " + offset + ", expected " + CHUNK_START_MARK +
                  ", found instead " + mark);
            }
            label = DecodeInt(offset + 4);
            size = DecodeInt(offset + 8);
        }

        private void EncodeChunkHeader(int offset, int label, int size)
        {
            EncodeInt(offset, CHUNK_START_MARK);
            EncodeInt(offset + 4, label);
            EncodeInt(offset + 8, size);
        }

        private void Throw(string error)
        {
            string message = new StringBuilder()
              .Append("PolySerializer error: ")
              .Append(error)
              .Append(". ")
              .Append("dataBuffer length: ").Append(dataBuffer != null ? dataBuffer.Length.ToString() : "(null)")
              .Append("dataStartOffset: ").Append(dataStartOffset)
              .Append("dataLength: ").Append(dataLength)
              .Append("curOffset: ").Append(curOffset)
              .Append("mode: ").Append(mode)
              .Append("isChunkInProgress: ").Append(isChunkInProgress)
              .Append("chunkStartOffset: ").Append(chunkStartOffset)
              .Append("chunkLabel: ").Append(chunkLabel)
              .Append("chunkSizeForReading: ").Append(chunkSizeForReading)
              .ToString();
            throw new Exception(message);
        }

        private static bool isPlatformBigEndian;
        static PolySerializer()
        {
            ushort testValue = 0xBEEF;
            byte firstByte;
            unsafe
            {  // totally safe, though.
               // Get the first byte of the representation.
                firstByte = *((byte*)&testValue);
            }
            if (firstByte != 0xBE && firstByte != 0xEF) throw new Exception("Can't determine platform endian-ness.");
            // Big-endian platforms put the most significant byte (0xBE in our case) first.
            isPlatformBigEndian = (0xBE == firstByte);
        }
    }
}
