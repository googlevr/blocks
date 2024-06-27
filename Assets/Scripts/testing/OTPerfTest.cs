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
using com.google.apps.peltzer.client.model.util;
using UnityEngine;
using UnityEngine.Profiling;

public class OTPerfTest : MonoBehaviour {

  // Use this for initialization
  void Start () {
    Debug.Log("OTPerfTest Start");
    RunNativeTest(100000, 1000);
  }

  void BasicTest(int numItems, int numQueries, float itemSize = 1f) {

    NativeSpatial<int> nativeSpatial = new NativeSpatial<int>();
    Random.InitState(12);
    
    // Add Test
    Bounds itemBounds = new Bounds(Vector3.one, 0.5f * Vector3.one);

    nativeSpatial.Add(1, itemBounds);
    HashSet<int> nativeOutset = new HashSet<int>();
    nativeSpatial.IntersectedBy(itemBounds, out nativeOutset);
    foreach (int id in nativeOutset) {
      Debug.Log("Collided with " + id);
    };
    Debug.Log("Finished running intersection tests");
  }
  
  void RunValidationTest(int numItems, int numQueries, float itemSize = 1f) {
    Bounds octreeBounds = new Bounds(Vector3.zero, 20f * Vector3.one);
    CollisionSystem<int> testOctree = new OctreeImpl<int>(octreeBounds);
    CollisionSystem<int> nativeSpatial = new NativeSpatial<int>();
    Random.InitState(12);
    
    // Add Test

    for (int i = 0; i < numItems; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, itemSize * Vector3.one);
      Profiler.BeginSample("OTAdd");
      testOctree.Add(i, itemBounds);
      nativeSpatial.Add(i, itemBounds);
      Profiler.EndSample();
    }
    
    Debug.Log("About to run intersection tests");
    for (int i = 0; i < numQueries; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, 1f * Vector3.one);
      HashSet<int> outSet = new HashSet<int>();
      HashSet<int> nativeOutset = new HashSet<int>();
      Profiler.BeginSample("OTIntersect");			//Debug.Log("Octree returned set of " + outSet.Count + " items");
      testOctree.IntersectedBy(itemBounds, out outSet);
      nativeSpatial.IntersectedBy(itemBounds, out nativeOutset);
      if (outSet != null) {
        //Debug.Log("Comparing nonzero results " + outSet.Count + " and " + nativeOutset.Count);
        int origSize = outSet.Count;
        outSet.IntersectWith(nativeOutset);
        if (outSet.Count != origSize) {
          Debug.Log("Results didn't match!  Octree set size: " + origSize + " native size " + nativeOutset.Count);
        }
      }
      else {
        if (nativeOutset.Count != 0) {
          Debug.Log("Native code returned more results than original");
        }
      }
      Profiler.EndSample();
    }
    Debug.Log("Finished running intersection tests");
  }

  void RunOctreeTest(int numItems, int numQueries, float itemSize = 1f) {
    Bounds octreeBounds = new Bounds(Vector3.zero, 20f * Vector3.one);
    CollisionSystem<int> testOctree = new OctreeImpl<int>(octreeBounds);
    
    Random.InitState(12);
    
    // Add Test

    for (int i = 0; i < numItems; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, itemSize * Vector3.one);
      Profiler.BeginSample("OTAdd");
        testOctree.Add(i, itemBounds);
      Profiler.EndSample();
    }
    
    for (int i = 0; i < numItems; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, itemSize * Vector3.one);
      Profiler.BeginSample("OTModify");
      testOctree.UpdateItemBounds(i, itemBounds);
      Profiler.EndSample();
    }
    
    for (int i = 0; i < numQueries; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, 0.001f * Vector3.one);
      HashSet<int> outSet = new HashSet<int>();
      Profiler.BeginSample("OTIntersect");
      testOctree.IntersectedBy(itemBounds, out outSet);
      Profiler.EndSample();
    }
    
    
  }
  
  void RunNativeTest(int numItems, int numQueries, float itemSize = 1f) {

    NativeSpatial<int> nativeSpatial = new NativeSpatial<int>();
    Random.InitState(12);
    
    
    // Add Test

    for (int i = 0; i < numItems; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, itemSize * Vector3.one);
      Profiler.BeginSample("OTAdd");
      nativeSpatial.Add(i, itemBounds);
      Profiler.EndSample();
    }
    
    for (int i = 0; i < numItems; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, itemSize * Vector3.one);
      Profiler.BeginSample("OTModify");
      nativeSpatial.UpdateItemBounds(i, itemBounds);
      Profiler.EndSample();
    }
    
    for (int i = 0; i < numQueries; i++) {
      Vector3 pos = new Vector3(Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f), Random.Range(-9.5f, 9.5f));
      Bounds itemBounds = new Bounds(pos, 0.001f * Vector3.one);
      HashSet<int> outSet = new HashSet<int>();
      Profiler.BeginSample("OTIntersect");
      nativeSpatial.IntersectedBy(itemBounds, out outSet);
      Profiler.EndSample();
    }
    
    
  }
  
  // Update is called once per frame
  void Update () {
    
  }
}
