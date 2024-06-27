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
using System.Runtime.Serialization;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.export {
  /// <summary>
  /// Unity's Vector3 is not serializable by default and we can't mess with their code. So, we wrap it.
  /// </summary>
  [Serializable]
  class SerializableVector3 : ISerializable {
    public Vector3 vector3;

    public SerializableVector3(Vector3 vector3) {
      this.vector3 = vector3;
    }

    public static List<SerializableVector3> CreateSerializableList(IEnumerable<Vector3> vector3s) {
      List<SerializableVector3> serializableVector3s = new List<SerializableVector3>();
      foreach (Vector3 vector3 in vector3s) {
        serializableVector3s.Add(new SerializableVector3(vector3));
      }
      return serializableVector3s;
    }

    public static List<Vector3> CreateUnserializedList(IEnumerable<SerializableVector3> serializedVector3s) {
      List<Vector3> vector3s = new List<Vector3>();
      foreach (SerializableVector3 serializedVector3 in serializedVector3s) {
        vector3s.Add(serializedVector3.vector3);
      }
      return vector3s;
    }

    // Serialize
    public void GetObjectData(SerializationInfo info, StreamingContext context) {
      info.AddValue("x", vector3.x);
      info.AddValue("y", vector3.y);
      info.AddValue("z", vector3.z);
    }

    // Deserialize
    public SerializableVector3(SerializationInfo info, StreamingContext context) {
      float x = (float)info.GetValue("x", typeof(float));
      float y = (float)info.GetValue("y", typeof(float));
      float z = (float)info.GetValue("z", typeof(float));
      vector3 = new Vector3(x, y, z);
    }
  }
}