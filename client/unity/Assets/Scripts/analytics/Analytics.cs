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
using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.desktop_app;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace com.google.apps.peltzer.client.analytics {
  /// <summary>
  /// Collects and sends Google Analytics data.
  /// Unless the user has opted out.
  /// </summary>
  public class Analytics : MonoBehaviour {
    private const string VIVE_TRACKING_CODE = "ADD_CODE_HERE";
    private const string APP_ID = "ADD_ID_HERE";
    private const string APP_NAME = "ADD_NAME_HERE";

    private bool optedIn = true; // Default opt-in because we're Google.

    public GoogleAnalyticsV4 googleAnalytics;
    private float sessionStartTime;

    // Dropped frame state (session).
    float droppedFrameMuteEndTime = 0;
    int lastDroppedFrameBucket = -1;
    int intervalsWithDroppedFrames = 0;

    // How many times the user has hit 'save' in this session.
    private int timesSaved = 0;

    private bool hasHadException = false;
    
    // Objectstore lookup info
    private int objectStoreLookupFailures = 0;
    private int objectStoreEntryLookupFailures = 0;
    private int peltzerFileLookupFailures = 0;

    private Mutex exceptionQueueMutex = new Mutex();
    private Queue<string> uncaughtExceptionQueue;
    
    // Used to track time-in-use for each possible controller mode.
    private Dictionary<ControllerMode, System.Diagnostics.Stopwatch> toolTimers =
      new Dictionary<ControllerMode, System.Diagnostics.Stopwatch>();
    // Used to keep track of the current mode, so we can stop its timer on mode change.
    private ControllerMode? currentMode;

    // A dictionary of how many times each thing has been inserted. Predefined here as we want to have a
    // central source of truth for type names, and want to log everything, even if its count is 0.
    private Dictionary<string, int> insertTypesAndCounts = new Dictionary<string, int>() {
      { "cone", 0 }, { "cube", 0 }, { "cylinder", 0 }, { "sphere", 0 }, { "torus", 0 }, { "custom", 0 }, // Primitives.
      { "freeform_auto", 0 }, { "freeform_manual", 0 }, // The stroke tool.
    };

    // Mapping from primitive types to strings used in Google Analytics.
    public static Dictionary<Primitives.Shape, string> primitiveTypesToStrings =
      new Dictionary<Primitives.Shape, string>() {
      { Primitives.Shape.CONE, "cone" },
      { Primitives.Shape.CUBE, "cube" },
      { Primitives.Shape.CYLINDER, "cylinder" },
      { Primitives.Shape.SPHERE, "sphere" },
      { Primitives.Shape.TORUS, "torus" }
    };

    // A dictionary of how many times a user attempted an operation that failed, keyed by offending operation.
    private Dictionary<string, int> invalidOperationsAndCounts = new Dictionary<string, int>() {
      { "moveMesh", 0 }, { "flipMesh", 0 }, { "scaleNonHeldMeshes", 0 }, { "copyMeshes", 0 }, { "importModel", 0 },
      { "scaleMeshesNoneSelected", 0 }, { "scaleHeldMeshes", 0 }, { "insertVolume", 0 }, { "scaleDownVolume", 0 },
      { "scaleUpVolume", 0 }, { "switchShapeLeft", 0 }, { "switchShapeRight", 0 }, { "reshapeMesh", 0 },
      { "mutateReshapedMesh", 0 }, { "subdivideFarFromFace", 0 }, { "subdivideMesh", 0 }, { "extrudeMesh", 0 },
      { "insertStroke", 0 }, { "scaleDownStroke", 0 }, { "scaleUpStroke", 0 },
      { "saveNotLoggedIn", 0 }, { "saveLoggedIn", 0 },
    };

    private Dictionary<string, int> validOperationsAndCounts = new Dictionary<string, int>() {
      { "reshapeMesh", 0 }, { "reshapeMeshMultipleFaces", 0 }, { "reshapeMeshSingleFace", 0 },
      { "copyMeshes", 0 }, { "importModel", 0 },
      { "reshapeMeshMultipleEdges", 0 }, { "reshapeMeshSingleEdge", 0 },
      { "extrudeMesh", 0 }, { "extrudeMeshMultipleFaces", 0 }, { "extrudeMeshSingleFace", 0 },
      { "groupMeshes", 0 }, { "ungroupMeshes", 0 }, {"flipMeshes", 0 },
      { "importYourModel", 0 }, { "importFeaturedModel", 0 }, { "importLikedModel", 0 },
      { "openYourModel", 0 }, { "openFeaturedModel", 0 }, { "openLikedModel", 0 },
      { "deleteModel", 0 }, { "newModel", 0 },
      { "toggleBlockMode", 0 },
      { "saveNotLoggedIn", 0 }, { "saveLoggedIn", 0 }, { "publish", 0 },
      { "tutorialBegin", 0 }, { "tutorialComplete", 0 }, { "tutorialEarlyExit", 0 },
      { "paintMesh", 0 }, { "paintFace", 0 }, { "deleteMesh", 0 }, { "subdivideMesh", 0 }, { "insertStroke", 0 },
      { "openedPolyMenu", 0 },

      // Multi selection.
      { "multiSelect", 0 }, { "multiSelectMeshes", 0 }, { "multiSelectFaces", 0 }, { "multiSelectVertices", 0 },
      { "multiSelectEdges", 0 },

      // Snapping.
      { "usedSnapping", 0 }, { "usedSnappingExtruder", 0 }, { "usedSnappingFreeform", 0 },
      { "usedSnappingReshaper", 0 }, { "usedSnappingSubdivider", 0 }, { "usedSnappingHeldMeshes", 0 }
    };

    private bool userAuthenticated;

    public void Awake() {
      // Just set all pertinent analytics object properties at runtime since we'll likely
      // need various combinations of bundle ID, version, etc. according to our build.
      // This script must have higher priority than GoogleAnalyticsV4.
      googleAnalytics.bundleIdentifier = APP_ID;

      googleAnalytics.bundleVersion = Config.Instance.version;
      googleAnalytics.productName = APP_NAME;

      // Analytics enabled only on Vive currently.
      googleAnalytics.otherTrackingCode = VIVE_TRACKING_CODE;

      // Miscellaneous.
      googleAnalytics.sessionTimeout = -1;
      googleAnalytics.sendLaunchEvent = false;
      // We're going to handle this ourselves.
      googleAnalytics.UncaughtExceptionReporting = false;
      googleAnalytics.logLevel = GoogleAnalyticsV4.DebugMode.VERBOSE;
      exceptionQueueMutex.WaitOne();
      uncaughtExceptionQueue = new Queue<string>();
      exceptionQueueMutex.ReleaseMutex();
      Application.logMessageReceived += HandleException;
    }

    private void HandleException(string condition, string stackTrace, LogType type) {
      if (type == LogType.Exception) {
        string uncaughtExceptionStackTrace = condition + "\n" + stackTrace
          + UnityEngine.StackTraceUtility.ExtractStackTrace();
        // This gets run from a background thread, so we need a mutex here.
        exceptionQueueMutex.WaitOne();
        uncaughtExceptionQueue.Enqueue(uncaughtExceptionStackTrace);
        exceptionQueueMutex.ReleaseMutex();
      }
    }

    public void Start() {
      googleAnalytics.StartSession();
      // Ensures the start-session signal above is promptly sent.
      googleAnalytics.LogEvent(new EventHitBuilder()
        .SetEventCategory("App")
        .SetEventAction("Launch"));

      sessionStartTime = Time.time;
    }

    public void Update() {
      // The exception handler is run from a background thread so we need to mutex this.
      exceptionQueueMutex.WaitOne();
      bool first = true;
      while (uncaughtExceptionQueue.Count > 0) {
        string exceptionMessage = uncaughtExceptionQueue.Dequeue();
        if (first) {
          // We're prepending these messages to allow lexicographic sorting in analytics, and to disambiguate between
          // the order of exceptions in a frame.  This should make finding the root cause clearer.
          if (!hasHadException) {
            exceptionMessage = "AAA_FIRST_IN_APP " + exceptionMessage;
            hasHadException = true;
          }
          else {
            exceptionMessage = "AAA_FIRST_IN_FRAME " + exceptionMessage;
          }
          first = false;
        }
        googleAnalytics.LogException(exceptionMessage, true);
      }
      exceptionQueueMutex.ReleaseMutex();
    }

    /// <summary>
    ///   Allows other scripts to change the optedIn state.
    /// </summary>
    public void TogglePermissions() {
      optedIn = !optedIn;
      PlayerPrefs.SetString(PeltzerMain.DISABLE_ANALYTICS_KEY, optedIn ? "false" : "true");
      ObjectFinder.ObjectById("ID_permissions_granted").SetActive(optedIn);
      ObjectFinder.ObjectById("ID_permissions_revoked").SetActive(!optedIn);
    }

    /// <summary>
    ///   A new controller mode was selected.
    /// </summary>
    /// <param name="newMode">The new controller mode.</param>
    public void SwitchToMode(ControllerMode newMode) {
      if (currentMode.HasValue) {
        toolTimers[currentMode.Value].Stop();
      }
      if (!toolTimers.ContainsKey(newMode)) {
        toolTimers.Add(newMode, new System.Diagnostics.Stopwatch());
      }
      toolTimers[newMode].Start();
      currentMode = newMode;
    }

    /// <summary>
    ///   A mesh, of the given type, was inserted into the scene.
    /// </summary>
    public void InsertMesh(string type) {
      if (!insertTypesAndCounts.ContainsKey(type)) {
        Debug.LogError("Unexpected insert type for analytics: " + type);
        return;
      }

      insertTypesAndCounts[type]++;
    }

    /// <summary>
    /// A user performed a successful operation.
    /// </summary>
    /// <param name="type">The command that succeeded.</param>
    public void SuccessfulOperation(string command) {
      if (!validOperationsAndCounts.ContainsKey(command)) {
        Debug.LogError("Unexpected successful operation name for analytics: " + command);
        return;
      }

      validOperationsAndCounts[command]++;
    }

    /// <summary>
    /// A user attempted an operation, but it failed.
    /// </summary>
    /// <param name="type">The command that failed.</param>
    public void FailedOperation(string command) {
      if (!invalidOperationsAndCounts.ContainsKey(command)) {
        Debug.LogError("Unexpected failed operation name for analytics: " + command);
        return;
      }

      invalidOperationsAndCounts[command]++;
    }

    public void UserAuthenticated() {
      userAuthenticated = true;
    }

    /// <summary>
    ///   A frame was dropped.
    /// </summary>
    private void OnDroppedFrames() {
      // We quantize dropped frame events by 10-second buckets. This sets a relatively-high bar for quality (a single
      // dropped frame spoils a whole 10 seconds) and filters noise (e.g. dropped frame followed
      // by a few on-time frames followed by another dropped frame is perceived as all one event
      // to the user).
      var time = Time.time;
      var tenSecondBucket = (int)((time - sessionStartTime) / 10);
      if (time > droppedFrameMuteEndTime
          && tenSecondBucket > lastDroppedFrameBucket) {
        ++intervalsWithDroppedFrames;
        lastDroppedFrameBucket = tenSecondBucket;
      }
    }

    // Call this from any app event which has known issues with dropped frames so we
    // can get a cleaner signal of remaining problems.
    // Currently unused, as we wish to see all problems.
    private void MuteDroppedFramesForSeconds(float seconds) {
      droppedFrameMuteEndTime = Mathf.Max(Time.time + seconds, droppedFrameMuteEndTime);
    }

    public void OnApplicationQuit() {
      if (!optedIn) {
        return;
      }

      // Track SDK/Hardware.
      string sdkLabel =
        Config.Instance.sdkMode == SdkMode.SteamVR ? "SteamVR" :
        Config.Instance.sdkMode == SdkMode.Oculus ? "Oculus" :
        "Unset";
      googleAnalytics.LogEvent(new EventHitBuilder()
        .SetEventCategory("App")
        .SetEventAction("SDK")
        .SetEventLabel(sdkLabel)
        .SetEventValue(1));
      string hardwareLabel =
        Config.Instance.VrHardware == VrHardware.Vive ? "Vive" :
        Config.Instance.VrHardware == VrHardware.Rift ? "Rift" :
        "Other";
      googleAnalytics.LogEvent(new EventHitBuilder()
        .SetEventCategory("App")
        .SetEventAction("Hardware")
        .SetEventLabel(hardwareLabel)
        .SetEventValue(1));

      // Track insertions.
      foreach (KeyValuePair<string, int> pair in insertTypesAndCounts) {
        string type = pair.Key;
        int count = pair.Value;
        if (count == 0) {
          continue;
        }
        googleAnalytics.LogEvent(new EventHitBuilder()
          .SetEventCategory("Interaction")
          .SetEventAction("Insert")
          .SetEventLabel(type)
          .SetEventValue(count));
      }

      // Track invalid operations.
      foreach (KeyValuePair<string, int> pair in invalidOperationsAndCounts) {
        string command = pair.Key;
        int count = pair.Value;
        if (count == 0) {
          continue;
        }
        googleAnalytics.LogEvent(new EventHitBuilder()
          .SetEventCategory("Failure")
          .SetEventAction("Invalid Operation")
          .SetEventLabel(command)
          .SetEventValue(count));
      }

      // Track successful operations.
      foreach (KeyValuePair<string, int> pair in validOperationsAndCounts) {
        string command = pair.Key;
        int count = pair.Value;
        if (count == 0) {
          continue;
        }
        googleAnalytics.LogEvent(new EventHitBuilder()
          .SetEventCategory("Success")
          .SetEventAction("Valid Operation")
          .SetEventLabel(command)
          .SetEventValue(count));
      }

      // Track timing.
      foreach (KeyValuePair<ControllerMode, System.Diagnostics.Stopwatch> toolTimer in toolTimers) {
        googleAnalytics.LogTiming(new TimingHitBuilder()
                                  .SetTimingCategory("Tool")
                                  .SetTimingName(toolTimer.Key.ToString())
                                  .SetTimingInterval(toolTimer.Value.ElapsedMilliseconds));
      }

      // Manually track session length in case standard session tracking doesn't work correctly.
      googleAnalytics.LogTiming(
        "App",
        (int)((Time.time - sessionStartTime) * 1000),
        "Session length",
        /* timingLabel */ null);

      // Track saves.
      googleAnalytics.LogEvent(new EventHitBuilder()
        .SetEventCategory("Interaction")
        .SetEventAction("Save")
        .SetEventLabel("VR")
        .SetEventValue(timesSaved));
      
      googleAnalytics.LogEvent(new EventHitBuilder()
        .SetEventCategory("Exception")
        .SetEventAction("HasExperiencedException")
        .SetEventLabel("HasExperiencedException")
        .SetEventValue(hasHadException ? 1 : 0));

      // Track periods with dropped frames.
      googleAnalytics.LogEvent(new EventHitBuilder()
                               .SetEventCategory("App")
                               .SetEventAction("Ten-second buckets with dropped frames")
                               .SetEventValue(intervalsWithDroppedFrames));

      // Track if the user authenticated.
      googleAnalytics.LogEvent(new EventHitBuilder()
        .SetEventCategory("App")
        .SetEventAction("Authenticated")
        .SetEventLabel(userAuthenticated.ToString())
        .SetEventValue(1));

      googleAnalytics.StopSession();
      // Sanity check-- we should see one Quit event for each Launch.
      googleAnalytics.LogEvent(new EventHitBuilder()
        .SetEventCategory("App")
        .SetEventAction("Quit"));

    }
  }
}
