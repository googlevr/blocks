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
using UnityEngine;
using System.Collections.Generic;
using TiltBrush;

/// <summary>
/// Records gifs. While active, collects frames from a camera, and when finished,
/// it creates a task to spool those frames out to a gif file.
/// </summary>
public class GifRecorder : MonoBehaviour
{

    private static readonly float RECORD_TIME_S = 30f;
    private static readonly int FPS = 33;
    private static readonly float FRAME_INTERVAL_S = 1.0f / FPS;

    private GameObject eyeCamera;
    private Camera gifRecordingCamera;
    private GameObject camObj;
    private RenderTexture gifRenderTexture;
    private GifEncodeTask task = null;
    private List<Color32[]> capturedGifFrames;
    private float intervalTimer = 0;

    private bool recordGif = false;
    private int recordFrames = 0;

    void Start()
    {
        eyeCamera = GameObject.Find("ID_Camera (eye)");
        camObj = new GameObject();
        camObj.name = "GifRecorder";
        camObj.transform.position = Vector3.zero;
        camObj.transform.rotation = Quaternion.identity;
        gifRecordingCamera = camObj.AddComponent<Camera>();
        gifRecordingCamera.nearClipPlane = .01f;
        camObj.transform.SetParent(eyeCamera.transform);
        camObj.transform.position = eyeCamera.transform.position;
        camObj.transform.rotation = eyeCamera.transform.rotation;
        gifRenderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        gifRecordingCamera.targetTexture = gifRenderTexture;
        camObj.SetActive(false);
    }

    void Update()
    {
        if (task != null && task.IsDone)
        {
            if (task.Error != null && task.Error.Length > 0)
            {
                Debug.Log("gif recording error " + task.Error);
            }
            else
            {
                Debug.Log("gif was recorded");
            }
            task = null;
        }
        if (recordGif)
        {
            if (recordFrames > 0)
            {
                intervalTimer += Time.deltaTime;
                if (intervalTimer > FRAME_INTERVAL_S)
                {
                    intervalTimer = -FRAME_INTERVAL_S;
                    RenderTexture.active = gifRenderTexture;
                    Texture2D frameTexture =
                        new Texture2D(gifRenderTexture.width, gifRenderTexture.height, TextureFormat.RGB24, false);
                    frameTexture.ReadPixels(new Rect(0, 0, gifRenderTexture.width, gifRenderTexture.height), 0, 0);
                    frameTexture.Apply();
                    RenderTexture.active = null;
                    capturedGifFrames.Add(frameTexture.GetPixels32());
                    Destroy(frameTexture);
                    recordFrames--;
                }
            }
            else
            {
                // Recording is finished
                recordGif = false;
                string filename = Application.persistentDataPath + "/poly_gif_" + Guid.NewGuid().ToString() + ".gif";
                Debug.Log("starting gif save " + filename + " with " + capturedGifFrames.Count + " frames");
                task = new GifEncodeTask(
                    capturedGifFrames, (int)(FRAME_INTERVAL_S * 1000.0f),
                    gifRenderTexture.width, gifRenderTexture.height,
                    filename,
                    1f / 8, true);
                capturedGifFrames = null;
                task.Start();
                camObj.SetActive(false);
            }
        }
    }

    public void RecordGif()
    {
        if (recordGif || task != null)
        {
            Debug.Log("Can't start new recording while current recording is active.");
            return;
        }
        recordGif = true;
        camObj.SetActive(true);
        recordFrames = (int)(FPS * RECORD_TIME_S);
        capturedGifFrames = new List<Color32[]>(recordFrames);
        Debug.Log("Starting gif record with " + recordFrames + "frames.");
    }
}
