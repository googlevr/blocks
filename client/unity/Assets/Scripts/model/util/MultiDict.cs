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
using System.Linq;
using System.Text;

namespace com.google.apps.peltzer.client.model.util {
  /// <summary>
  ///   Dictionary that stores a list of values for a key.
  /// </summary>
  public class MultiDict<K, V> {
    private readonly Dictionary<K, List<V>> mainDict = new Dictionary<K, List<V>>();

    public void Add(K key, V value) {
      List<V> values;
      if (!mainDict.TryGetValue(key, out values)) {
        values = new List<V>();
        mainDict[key] = values;
      }
      values.Add(value);
    }

    public bool TryGetValues(K key, out List<V> values) {
      return mainDict.TryGetValue(key, out values);
    }

    public List<V> GetValues(K key) {
      return mainDict[key];
    }

    public Dictionary<K, List<V>>.KeyCollection Keys {
      get { return mainDict.Keys; }
    }

    public bool ContainsKey(K id) {
      return mainDict.ContainsKey(id);
    }
  }
}
