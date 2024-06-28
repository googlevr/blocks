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

using com.google.apps.peltzer.client.app;
using UnityEngine;

/// <summary>
///   This class acts as a pseudo GIF, providing a simple mechanic for looping frame based animation.
/// </summary>
public class GIFDisplay : MonoBehaviour
{
    /// <summary>
    ///   The speed of the animation specified as the number of frames per second.
    /// </summary>
    public int framesPerSecond = 2;
    /// <summary>
    ///   Arrays of Textures representing the frames of the animation.
    /// </summary>
    public Texture[] frames;
    public Texture[] riftFrames;

    /// <summary>
    ///   Determines which frame to play based on frames per second in a loop.
    /// </summary>
    void Update()
    {
        Texture[] framesToUse = Config.Instance.VrHardware == VrHardware.Vive ? frames : riftFrames;

        if (frames.Length == 0)
        {
            Debug.Log("no frames!");
            return;
        }
        int idx = Mathf.FloorToInt((Time.time * framesPerSecond) % framesToUse.Length);
        gameObject.transform.GetComponent<Renderer>().material.mainTexture = framesToUse[idx];
    }
}
