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

using System.Collections.Generic;
using UnityEngine;

namespace com.google.apps.peltzer.client.serialization
{
    /// <summary>
    /// Utility methods for serializing higher level structures (vectors, quaternions, lists, etc).
    /// </summary>
    public static class PolySerializationUtils
    {
        public static void WriteVector3(PolySerializer serializer, Vector3 v)
        {
            serializer.WriteFloat(v.x);
            serializer.WriteFloat(v.y);
            serializer.WriteFloat(v.z);
        }

        public static void WriteQuaternion(PolySerializer serializer, Quaternion q)
        {
            serializer.WriteFloat(q.x);
            serializer.WriteFloat(q.y);
            serializer.WriteFloat(q.z);
            serializer.WriteFloat(q.w);
        }

        public static void WriteIntList(PolySerializer serializer, IList<int> list)
        {
            serializer.WriteCount(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                serializer.WriteInt(list[i]);
            }
        }

        public static void WriteVector3List(PolySerializer serializer, IList<Vector3> list)
        {
            serializer.WriteCount(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                WriteVector3(serializer, list[i]);
            }
        }

        public static void WriteStringSet(PolySerializer serializer, HashSet<string> stringSet)
        {
            serializer.WriteCount(stringSet.Count);
            foreach (string s in stringSet)
            {
                serializer.WriteString(s);
            }
        }

        public static Vector3 ReadVector3(PolySerializer serializer)
        {
            float x = serializer.ReadFloat();
            float y = serializer.ReadFloat();
            float z = serializer.ReadFloat();
            return new Vector3(x, y, z);
        }

        public static Quaternion ReadQuaternion(PolySerializer serializer)
        {
            float x = serializer.ReadFloat();
            float y = serializer.ReadFloat();
            float z = serializer.ReadFloat();
            float w = serializer.ReadFloat();
            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Reads a list of integers.
        /// </summary>
        /// <param name="serializer">The serializer to read from.</param>
        /// <param name="min">Minimum acceptable size of the list.</param>
        /// <param name="max">Maximum acceptable size of the list.</param>
        /// <param name="listName">Name of the list (for debugging purposes, used in exceptions).</param>
        /// <returns>The list.</returns>
        public static List<int> ReadIntList(PolySerializer serializer, int min = 0, int max = int.MaxValue,
            string listName = "untitled")
        {
            int count = serializer.ReadCount(min, max, listName);
            List<int> result = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(serializer.ReadInt());
            }
            return result;
        }

        /// <summary>
        /// Reads a list of Vector3s.
        /// </summary>
        /// <param name="serializer">The serializer to read from.</param>
        /// <param name="min">Minimum acceptable size of the list.</param>
        /// <param name="max">Maximum acceptable size of the list.</param>
        /// <param name="listName">Name of the list (for debugging purposes, used in exceptions).</param>
        /// <returns>The list.</returns>
        public static List<Vector3> ReadVector3List(PolySerializer serializer, int min = 0, int max = int.MaxValue,
            string listName = "untitled")
        {
            int count = serializer.ReadCount(min, max, listName);
            List<Vector3> result = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(ReadVector3(serializer));
            }
            return result;
        }

        /// <summary>
        /// Reads a set of strings from the PolySerializer.
        /// </summary>
        /// <param name="serializer">The serializer to read from.</param>
        /// <param name="min">Minimum acceptable size of the set.</param>
        /// <param name="max">Maximum acceptable size of the set.</param>
        /// <param name="listName">Name of the set (for debugging purposes, used in exceptions).</param>
        /// <returns>The set.</returns>
        public static HashSet<string> ReadStringSet(PolySerializer serializer, int min = 0, int max = int.MaxValue,
            string listName = "untitled")
        {
            int count = serializer.ReadCount(min, max, listName);
            HashSet<string> result = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                result.Add(serializer.ReadString());
            }
            return result;
        }
    }
}
