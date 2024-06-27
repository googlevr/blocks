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

using com.google.apps.peltzer.client.model.util;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.google.apps.peltzer.client.model.main {
  /// <summary>
  /// Responsible for finding unique objects in the scene.
  /// Unique objects are those whose names begin with "ID_" (case sensitive).
  /// These objects must be STATICALLY in the scene (added through the Unity editor). Dynamically created objects
  /// will not be found by this class.
  /// </summary>
  public class ObjectFinder {
    /// <summary>
    /// Prefix that unique objects should have.
    /// </summary>
    private const string ID_PREFIX = "ID_";

    /// <summary>
    /// Dictionary from object ID to GameObject. Lazily initialized on the first lookup.
    /// </summary>
    private static Dictionary<string, GameObject> cache;

    /// <summary>
    /// Lock that protects the cache.
    /// </summary>
    private static object cacheLock = new object();

    /// <summary>
    /// Creates the cache of objects by going through all the objects in the scene to find
    /// objects tagged with the ID_PREFIX. This is relatively expensive and should be done
    /// only once at startup.
    /// </summary>
    /// <returns></returns>
    private static Dictionary<string, GameObject> CreateCache() {
      Scene activeScene = SceneManager.GetActiveScene();
      AssertOrThrow.NotNull(activeScene, "Active scene is unexpectedly null. The universe is broken.");

      Dictionary<string, GameObject> result = new Dictionary<string, GameObject>();
      foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>()) {
        // We only care about objects in this scene.
        // We have to check for each object's current scene because FindObjectsOfTypeAll() returns Unity's internal
        // objects as well, which are not part of this scene.
        if (obj.scene != activeScene) {
          continue;
        }

        // We only care about normal objects that appear in the editor's hierarchy (hideFlags == HideFlags.None).
        // FindObjectsOfTypeAll() returns all objects, including objects that normally don't appear in the editor
        // such as prefabs (not to be confused with prefab *instances*, which are shown in the hierarchy).
        // We have to be careful to ignore those.
        if (obj.hideFlags != HideFlags.None) {
          continue;
        }

        if (obj.name.StartsWith(ID_PREFIX)) {
          AssertOrThrow.True(!result.ContainsKey(obj.name),
              "ID collision: duplicate object ID: " + obj.name + " (case insensitive)");
          result[obj.name] = obj;
        }
      }
      return result;
    }

    /// <summary>
    /// Looks up a unique object by its ID. This locates the object regardless of whether it's
    /// active or not. Throws an exception if the object can't be found.
    /// </summary>
    /// <param name="id">The ID of the object to locate.</param>
    /// <returns>The object</returns>
    public static GameObject ObjectById(string id) {
      AssertOrThrow.True(id.StartsWith(ID_PREFIX),
          "Can't look up an ID that doesn't start with the ID prefix: " + id);
      lock (cacheLock) {
        if (cache == null) {
          cache = CreateCache();
        }
        GameObject result;
        if (!cache.TryGetValue(id, out result)) {
          throw new Exception("Object not found: " + id + ". Make sure it's statically in the scene.");
        }
        return result;
      }
    }

    /// <summary>
    /// Similar to LookUpById but returns the component of the given type of the resulting GameObject.
    /// Aborts and throws an exception if the object does not exist or if it does not have the requested
    /// component.
    /// </summary>
    /// <typeparam name="T">The type of component to query</typeparam>
    /// <param name="id">The ID of the object to search.</param>
    /// <returns>The requested component.</returns>
    public static T ComponentById<T>(string id) {
      GameObject obj = ObjectById(id);
      T comp = obj.GetComponent<T>();
      AssertOrThrow.NotNull(comp, "Object " + id + " does not have a component of type " + typeof(T).Name);
      return comp;
    }
  }
}
