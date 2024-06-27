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

using System.IO;
using UnityEngine;
using UnityEngine.Video;

using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.controller {

  /// <summary>
  ///   SelectableMenuItem that can be attached to items on the palette file menu.
  /// </summary>
  public class VideoActionItem : MenuActionItem {
    // The video viewer will appear to the side of the button that was pressed to begin the video.
    public static Vector3 VIDEO_VIEWER_OFFSET_LEFT_HANDED = new Vector3(1.9f, 0, 2.4f);
    public static Vector3 VIDEO_VIEWER_OFFSET_RIGHT_HANDED = new Vector3(1.9f, 0, -2.4f);

    // The filename of the video this MenuActionItem will play.
    public string videoFilename;

    /// <summary>
    ///   When this button is clicked, if it is currently enabled/allowed, then we make a noise, animate the button
    ///   and begin playing the video.
    /// </summary>
    public override void ApplyMenuOptions(PeltzerMain main) {
      if (!ActionIsAllowed()) return;

      PlayVideo();
      main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
      StartBump();
    }


    /// <summary>
    ///   Begins playing the video specified in videoFilename.
    ///   
    ///   There is exactly one video viewer in the scene. It will be displayed if hidden, and any previous video will
    ///   be immediately switched out in favour of this video. The video viewer will be placed in the scene near to
    ///   the button represented by this script's transform, but may later be moved with the grab tool.
    ///   
    ///   Videos are streamed from the local StreamingAssets folder rather than pre-buffered, due to performance issues
    ///   on Windows in the current Unity Video Preparation path.
    /// </summary>
    private void PlayVideo() {
      // Find the video viewer and set it active.
      GameObject videoViewer = PeltzerMain.Instance.GetVideoViewer();
      videoViewer.SetActive(true);

      // Offset the video viewer from the button, placing it nicely by the menu.
      Vector3 change = PeltzerMain.Instance.peltzerController.handedness == Handedness.RIGHT ?
        VIDEO_VIEWER_OFFSET_RIGHT_HANDED :
        VIDEO_VIEWER_OFFSET_LEFT_HANDED;
      Vector3 newLocalPos = transform.localPosition + change;
      videoViewer.transform.position = transform.TransformPoint(newLocalPos);
      videoViewer.transform.rotation = transform.rotation * Quaternion.Euler(0, 180, 0);
      VideoPlayer player = videoViewer.GetComponent<VideoPlayer>();

      // Stop any existing video that was playing.
      if (player.isPlaying) {
        player.Stop();
      }

      // Prepare this video. We use streaming rather than pre-buffering for performance reasons. Quoth Unity:
      //   "A fix to make preparation in another thread is ongoing, we'll make it available as soon as possible."
      // https://forum.unity.com/threads/videoplayer-seturl-and-videoplayer-prepare-very-expensive.465128/
      player.url = Path.Combine(Application.streamingAssetsPath, Path.Combine("Videos", videoFilename));

      // Set up the audio for this video. A little ugly but I can't find a better way.
      player.controlledAudioTrackCount = 1;
      AudioSource audioSource = GetComponent<AudioSource>();
      if (audioSource == null) {
        audioSource = gameObject.AddComponent<AudioSource>();
      }
      player.SetTargetAudioSource(0, audioSource);

      // Stream this video.
      player.Play();
    }
  }
}