﻿// Copyright 2020 The Blocks Authors
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

using UnityEngine;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.main;

/// <summary>
/// AudioSource to fade out.
/// </summary>
class Fade {
  public AudioSource source;
  public float startTime;
  public float startVolume;
  public float duration;

  public Fade(AudioSource source, float startTime, float startVolume, float duration) {
    this.source = source;
    this.startTime = startTime;
    this.startVolume = startVolume;
    this.duration = duration;
  }
}

/// <summary>
/// A library of sounds and API to play them.
/// Generates a new AudioSource for each play request, and periodically garbage collects.
/// </summary>
public class AudioLibrary : MonoBehaviour {
  // Named audio clips, each of which must be loaded in Start().
  public AudioClip alignSound;
  public AudioClip breakSound;
  public AudioClip confettiSound;
  public AudioClip copySound;
  public AudioClip decrementSound;
  public AudioClip deleteSound;
  public AudioClip errorSound;
  public AudioClip genericSelectSound;
  public AudioClip genericReleaseSound;
  public AudioClip grabMeshSound;
  public AudioClip grabMeshPartSound;
  public AudioClip groupSound;
  public AudioClip incrementSound;
  public AudioClip insertVolumeSound;
  public AudioClip menuSelectSound;
  public AudioClip modifyMeshSound;
  public AudioClip paintSound;
  public AudioClip pasteMeshSound;
  public AudioClip redoSound;
  public AudioClip releaseMeshSound;
  public AudioClip saveSound;
  public AudioClip selectToolSound;
  public AudioClip shapeMenuEndSound;
  public AudioClip snapSound;
  public AudioClip subdivideSound;
  public AudioClip startupSound;
  public AudioClip successSound;
  public AudioClip swipeLeftSound;
  public AudioClip swipeRightSound;
  public AudioClip toggleMenuSound;
  public AudioClip tutorialCompletionSound;
  public AudioClip tutorialIntroSound;
  public AudioClip tutorialMeshAnimateInSound;
  public AudioClip undoSound;
  public AudioClip ungroupSound;
  public AudioClip zoomResetSound;

  /// <summary>
  /// All AudioSources generated by play requests, to be periodically cleaned up.
  /// </summary>
  private List<AudioSource> sources = new List<AudioSource>();

  /// <summary>
  /// All AudioSources that are being faded.
  /// </summary>
  private HashSet<Fade> sourcesToFade = new HashSet<Fade>();

  /// <summary>
  /// Periodicity for cleanup, in seconds.
  /// </summary>
  private static float CLEANUP_INTERVAL = 5;
  /// <summary>
  /// Maximum number of items we'll cleanup in a tick.
  /// </summary>
  private static float CLEANUP_LIMIT = 5;
  /// <summary>
  /// Maximum duration of the fade.
  /// </summary>
  private static float FADE_DURATION = 0.5f;
  /// <summary>
  /// Timestamp of most recent cleanup.
  /// </summary>
  private float lastCleanup;
  /// <summary>
  /// Whether sounds are enabled.
  /// </summary>
  private bool soundsEnabled;

  // Leave time between repeat plays of the same clip to prevent ear-overload.
  private const float INTERVAL_BETWEEN_PLAYS = .25f;
  private Dictionary<AudioClip, float> clipsLastPlayTime = new Dictionary<AudioClip, float>();

  /// <summary>
  /// Call once to initialize and load all sounds.
  /// </summary>
  public void Setup() {
    alignSound = Resources.Load<AudioClip>("Audio/Poly_InsertPrimitiveShape_03");
    breakSound = Resources.Load<AudioClip>("Audio/Poly_ObjectBreak_04");
    confettiSound = Resources.Load<AudioClip>("Audio/Poly_ObjectBreak_10");
    copySound = Resources.Load<AudioClip>("Audio/Poly_Copy_02");
    decrementSound = Resources.Load<AudioClip>("Audio/Poly_Decrement");
    deleteSound = Resources.Load<AudioClip>("Audio/Poly_Erase_01");
    errorSound = Resources.Load<AudioClip>("Audio/Poly_InvalidOperation_01");
    groupSound = Resources.Load<AudioClip>("Audio/Poly_Group_04");
    grabMeshSound = Resources.Load<AudioClip>("Audio/Poly_GrabObject_04");
    grabMeshPartSound = Resources.Load<AudioClip>("Audio/Poly_GrabVertex_02");
    incrementSound = Resources.Load<AudioClip>("Audio/Poly_Increment");
    genericSelectSound = Resources.Load<AudioClip>("Audio/Poly_GenericSelect_07");
    genericReleaseSound = Resources.Load<AudioClip>("Audio/Poly_GenericRelease_07");
    insertVolumeSound = Resources.Load<AudioClip>("Audio/Poly_InsertVolume_08");
    menuSelectSound = Resources.Load<AudioClip>("Audio/Poly_GenericSelect_07");
    modifyMeshSound = Resources.Load<AudioClip>("Audio/Poly_Move_08");
    paintSound = Resources.Load<AudioClip>("Audio/Poly_Paint_05");
    pasteMeshSound = Resources.Load<AudioClip>("Audio/Poly_Paste_01");
    redoSound = Resources.Load<AudioClip>("Audio/Poly_Redo_02");
    releaseMeshSound = Resources.Load<AudioClip>("Audio/Poly_ReleaseObject_04");
    saveSound = Resources.Load<AudioClip>("Audio/Poly_Success_10");
    shapeMenuEndSound = Resources.Load<AudioClip>("Audio/Poly_IncrementSizeUp_01");
    selectToolSound = Resources.Load<AudioClip>("Audio/Poly_SelectToolFromPalette_01");
    snapSound = Resources.Load<AudioClip>("Audio/Poly_InsertVolume_03");
    subdivideSound = Resources.Load<AudioClip>("Audio/Poly_Subdivide_01");
    startupSound = Resources.Load<AudioClip>("Audio/Poly_IntroAnim_FullStereo");
    successSound = Resources.Load<AudioClip>("Audio/Poly_Success_05");
    swipeLeftSound = Resources.Load<AudioClip>("Audio/Poly_ChangeShapeLeft_01");
    swipeRightSound = Resources.Load<AudioClip>("Audio/Poly_ChangeShapeRight_01");
    toggleMenuSound = Resources.Load<AudioClip>("Audio/Poly_GrabObject_02");
    tutorialCompletionSound = Resources.Load<AudioClip>("Audio/Poly_Success_12");
    tutorialIntroSound = Resources.Load<AudioClip>("Audio/Poly_IntroAnim_PolyNotes_01");
    tutorialMeshAnimateInSound = Resources.Load<AudioClip>("Audio/Poly_Copy_04");
    undoSound = Resources.Load<AudioClip>("Audio/Poly_Undo_02");
    ungroupSound = Resources.Load<AudioClip>("Audio/Poly_UnGroup_04");
    zoomResetSound = Resources.Load<AudioClip>("Audio/Poly_ZoomReset_01");

    // Enable sounds on start.
    soundsEnabled = true;
  }

  /// <summary>
  /// Periodically cleans up any expired AudioSources.
  /// </summary>
  void Update() {
    HashSet<Fade> fadesToRemove = new HashSet<Fade>();
    foreach (Fade sourceToFade in sourcesToFade) {
      float fadePct = (Time.time - sourceToFade.startTime) / sourceToFade.duration;
      if (fadePct > 1.0f) {
        fadesToRemove.Add(sourceToFade);
        fadePct = 1.0f;
      }
      sourceToFade.source.volume = sourceToFade.startVolume * (1 - fadePct);
    }
    if (Time.time - lastCleanup > CLEANUP_INTERVAL) {
      lastCleanup = Time.time;
      int itemsDestroyed = 0;
      List<AudioSource> newList = new List<AudioSource>();

      // The sources in fadesToRemove will be destoyed later through the following loop.
      sourcesToFade.ExceptWith(fadesToRemove);
      foreach (AudioSource source in sources) {
        if (!source.isPlaying && itemsDestroyed++ < CLEANUP_LIMIT)
          Destroy(source);
        else
          newList.Add(source);
      }
      sources = newList;
    }
  }

  /// <summary>
  ///   Creates a new AudioSource and plays the given clip once with standard pitch.
  /// </summary>
  public void PlayClip(AudioClip clip) {
    PlayClip(clip, /* pitch */ 1.0f);
  }

  /// <summary>
  /// Creates a new AudioSource and plays the given clip once with the given pitch.
  /// </summary>
  public void PlayClip(AudioClip clip, float pitch) {
    // Don't play audio clips if the user has disabled sounds.
    if (clip == null || !soundsEnabled)
      return;

    // Don't repeat audio-clips too quickly.
    float lastPlayedTime;
    if (clipsLastPlayTime.TryGetValue(clip, out lastPlayedTime)
        && Time.time - lastPlayedTime < INTERVAL_BETWEEN_PLAYS) {
      return;
    }
    clipsLastPlayTime[clip] = Time.time;

    AudioSource source = gameObject.AddComponent<AudioSource>();
    source.clip = clip;
    source.pitch = pitch;
    source.PlayOneShot(clip);
    sources.Add(source);
  }

  /// <summary>
  /// Stops all clips of a given type from playing.
  /// </summary>
  public void StopClip(AudioClip clip) {
    // Don't bother if the user has disabled sounds.
    if (clip == null || !soundsEnabled)
      return;

    foreach (AudioSource audioSource in sources) {
      // If the given audio matches the type we are trying to stop,
      // then stop it. Note: Multiple instances of the same clip will
      // all be stopped.
      if (audioSource.isPlaying && audioSource.clip == clip) {
        audioSource.Stop();
      }
    }
  }

  /// <summary>
  /// Begins fading all clips of a given type.
  /// </summary>
  public void FadeClip(AudioClip clip) {
    // Don't bother if the user has disabled sounds.
    if (clip == null || !soundsEnabled)
      return;

    foreach (AudioSource audioSource in sources) {
      // If the given audio matches and is playing, the add it to our fading list.
      if (audioSource.isPlaying && audioSource.clip == clip) {
        sourcesToFade.Add(new Fade(audioSource, Time.time, audioSource.volume, FADE_DURATION));
      }
    }
  }

  /// <summary>
  ///   Toggles whether sounds are enabled.
  /// </summary>
  public void ToggleSounds() {
    soundsEnabled = !soundsEnabled;
    PlayerPrefs.SetString(PeltzerMain.DISABLE_SOUNDS_KEY, soundsEnabled ? "false" : "true");
    ObjectFinder.ObjectById("ID_sounds_are_on").SetActive(soundsEnabled);
    ObjectFinder.ObjectById("ID_sounds_are_off").SetActive(!soundsEnabled);
  }
}