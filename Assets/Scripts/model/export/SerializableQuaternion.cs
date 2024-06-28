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
using System.Runtime.Serialization;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.export
{
    /// <summary>
    /// Unity's Quaternion is not serializable by default and we can't mess with their code. So, we wrap it.
    /// </summary>
    [Serializable]
    class SerializableQuaternion : ISerializable
    {
        public Quaternion quaternion = Quaternion.identity;

        public SerializableQuaternion(Quaternion quaternion)
        {
            this.quaternion = quaternion;
        }

        // Serialize
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("x", quaternion.x);
            info.AddValue("y", quaternion.y);
            info.AddValue("z", quaternion.z);
            info.AddValue("w", quaternion.w);
        }

        // Deserialize
        public SerializableQuaternion(SerializationInfo info, StreamingContext context)
        {
            float x = (float)info.GetValue("x", typeof(float));
            float y = (float)info.GetValue("y", typeof(float));
            float z = (float)info.GetValue("z", typeof(float));
            float w = (float)info.GetValue("w", typeof(float));
            quaternion = new Quaternion(x, y, z, w);
        }
    }
}
