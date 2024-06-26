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

using UnityEngine;
using System.Collections;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Haptic feedback implementations for the Peltzer app.
  ///     * Holds a reference to associated SteamVR_Controller.Device.
  ///     * Abstracts complex vibration pattern on device.
  /// </summary>
  public class HapticFeedback : MonoBehaviour {
    /// <summary>
    ///   Placeholder enum for various feedback types. As feedback becomes
    ///   better characterized via ongoing testing to more meaningful names,
    ///   these will be updated e.g. CAUTION, CONFIRMATION, etc.
    /// </summary>
    public enum HapticFeedbackType {
      FEEDBACK_1,
      FEEDBACK_2,
      FEEDBACK_3,
      FEEDBACK_4,
      FEEDBACK_5,
      FEEDBACK_6,
      FEEDBACK_7,
      FEEDBACK_8,
      FEEDBACK_9,
      FEEDBACK_10,
      FEEDBACK_11,
      FEEDBACK_12,
      FEEDBACK_13,
      FEEDBACK_14,
      FEEDBACK_15,
      FEEDBACK_16,
      FEEDBACK_17,
      FEEDBACK_18
    }

    // Device instance.
    public ControllerDevice controller;
    private bool startVibration = false;

    //length is how long the vibration should go for
    //strength is vibration strength from 0-1
    IEnumerator LongVibration(float length, float strength) {
      for (float i = 0; i < length; i += Time.deltaTime) {
        if (controller != null) {
          controller.TriggerHapticPulse((ushort)Mathf.Lerp(0, 3999, strength));
        }
        yield return null;
      }
    }

    //vibrationCount is how many vibrations
    //vibrationLength is how long each vibration should go for
    //gapLength is how long to wait between vibrations
    //strength is vibration strength from 0-1
    IEnumerator LongVibration(int vibrationCount, float[] vibrationLength, float gapLength, float[] strength) {
      for (int i = 0; i < vibrationCount; i++) {
        if (i != 0) yield return new WaitForSeconds(gapLength);
        yield return StartCoroutine(LongVibration(vibrationLength[i], Mathf.Clamp01(strength[i])));
      }
    }

    /// <summary>
    ///   Signal A
    ///     * Atomic Signals from which larger patterns are constructed.
    /// </summary>
    IEnumerator SignalA(float length, float strength) {
      float[] l = { length };
      float[] s = { strength };
      StartCoroutine(LongVibration(1, l, 0, s));
      yield return null;
    }

    /// <summary>
    ///   Signal B
    ///     * Atomic Signals from which larger patterns are constructed.
    /// </summary>
    IEnumerator SignalB(float length, float strength) {
      for (float i = 0; i < length; i += Time.deltaTime) {
        ushort s = (ushort)Mathf.Lerp(0, 3999, Mathf.Lerp(0, strength, (i / length)));
        controller.TriggerHapticPulse(s);
        yield return null;
      }
    }

    /// <summary>
    ///   Signal C
    ///     * Atomic Signals from which larger patterns are constructed.
    /// </summary>
    IEnumerator SignalC(float length, float strength) {
      for (float i = length; i > 0; i -= Time.deltaTime) {
        ushort s = (ushort)Mathf.Lerp(0, 3999, Mathf.Lerp(0, strength, (i / length)));
        controller.TriggerHapticPulse(s);
        yield return null;
      }
    }

    /// <summary>
    ///   Signal D
    ///     * Atomic Signals from which larger patterns are constructed.
    /// </summary>
    IEnumerator SignalD(float length, float strength) {
      for (float i = 0; i < length; i += Time.deltaTime) {
        ushort s = (ushort)Mathf.Lerp(0, 3999, Mathf.Lerp(0, strength, (i / length) * (i / length)));
        controller.TriggerHapticPulse(s);
        yield return null;
      }
    }

    /// <summary>
    ///   Signal E
    ///     * Atomic Signals from which larger patterns are constructed.
    /// </summary>
    IEnumerator SignalE(float length, float strength) {
      for (float i = length; i > 0; i -= Time.deltaTime) {
        ushort s = (ushort)Mathf.Lerp(0, 3999, Mathf.Lerp(0, strength, (i / length) * (i / length)));
        controller.TriggerHapticPulse(s);
        yield return null;
      }
    }

    /// <summary>
    ///   Calls patterns in sequence.
    /// </summary>
    private static IEnumerator Sequence(params IEnumerator[] sequence) {
      for (int i = 0; i < sequence.Length; ++i) {
        while (sequence[i].MoveNext())
          yield return sequence[i].Current;
      }
    }

    /// <summary>
    ///   Pattern 7
    ///     * Pattern constructed from Signal B -> Signal B.
    /// </summary>
    private void Pattern_7(float length, float strength) {
      StartCoroutine(Sequence(SignalB(length, strength), SignalB(length, strength)));
    }

    /// <summary>
    ///   Pattern 8
    ///     * Pattern constructed from Signal C.
    /// </summary>
    private void Pattern_8(float length, float strength) {
      StartCoroutine(SignalC(length, strength));
    }

    /// <summary>
    ///   Pattern 9
    ///     * Pattern constructed from Signal C -> Signal C.
    /// </summary>
    private void Pattern_9(float length, float strength) {
      StartCoroutine(Sequence(SignalC(length, strength), SignalC(length, strength)));
    }

    /// <summary>
    ///   Pattern 10
    ///     * Pattern constructed from Signal B -> Signal C.
    /// </summary>
    private void Pattern_10(float length, float strength) {
      StartCoroutine(Sequence(SignalB(length, strength), SignalC(length, strength)));
    }

    /// <summary>
    ///   Pattern 11
    ///     * Pattern constructed from Signal C -> Signal B.
    /// </summary>
    private void Pattern_11(float length, float strength) {
      StartCoroutine(Sequence(SignalC(length, strength), SignalB(length, strength)));
    }

    /// <summary>
    ///   Pattern 12
    ///     * Pattern constructed from Signal A -> Signal A.
    /// </summary>
    private void Pattern_12(float length, float strength) {
      StartCoroutine(Sequence(SignalA(length, strength / 2), SignalA(length, strength)));
    }

    /// <summary>
    ///   Pattern 13
    ///     * Pattern constructed from Signal A -> Signal A.
    /// </summary>
    private void Pattern_13(float length, float strength) {
      StartCoroutine(Sequence(SignalA(length, strength), SignalA(length, strength / 2)));
    }

    /// <summary>
    ///   Stops vibration of undetermined length.
    /// </summary>
    private void StopVibration() {
      startVibration = false;
    }

    /// <summary>
    ///   Plays back the specified the feedback type at the given length and strength specified.
    /// </summary>
    /// <param name="feedbackType">The feedback pattern type.</param>
    /// <param name="length">Duration of feedback in seconds.</param>
    /// <param name="strength">Strength value normaled between 0 and 1.</param>
    public void PlayHapticFeedback(HapticFeedbackType feedbackType, float length, float strength) {
      if (controller == null || !gameObject.activeInHierarchy) return;

      switch (feedbackType) {
        case HapticFeedbackType.FEEDBACK_1:
          Feedback1(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_2:
          Feedback2(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_3:
          Feedback3(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_4:
          Feedback4(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_5:
          Feedback5(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_6:
          Feedback6(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_7:
          Feedback7(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_8:
          Feedback8(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_9:
          Feedback9(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_10:
          Feedback10(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_11:
          Feedback11(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_12:
          Feedback12(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_13:
          Feedback13(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_14:
          Feedback14(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_15:
          Feedback15(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_16:
          Feedback16(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_17:
          Feedback17(length, strength);
          break;
        case HapticFeedbackType.FEEDBACK_18:
          Feedback18(length, strength);
          break;
        default:
          break;
      }
    }

    /// <summary>
    ///   Feedback 1
    /// </summary>  
    private void Feedback1(float length, float strength) {
      StartCoroutine(SignalA(length, strength));
    }

    /// <summary>
    ///   Feedback 2
    /// </summary> 
    private void Feedback2(float length, float strength) {
      StartCoroutine(SignalB(length, strength));
    }

    /// <summary>
    ///   Feedback 3
    /// </summary> 
    private void Feedback3(float length, float strength) {
      StartCoroutine(SignalC(length, strength));
    }

    /// <summary>
    ///   Feedback 4
    /// </summary> 
    private void Feedback4(float length, float strength) {
      StartCoroutine(SignalD(length, strength));
    }

    /// <summary>
    ///   Feedback 5
    /// </summary> 
    private void Feedback5(float length, float strength) {
      StartCoroutine(SignalE(length, strength));
    }

    /// <summary>
    ///   Feedback 6
    /// </summary> 
    private void Feedback6(float length, float strength) {
      StartCoroutine(SignalA(length, strength));
    }

    /// <summary>
    ///   Feedback 7
    /// </summary> 
    private void Feedback7(float length, float strength) {
      float[] _l = { length, length };
      float[] _s = { strength, strength };
      StartCoroutine(LongVibration(2, _l, .05f, _s));
    }

    /// <summary>
    ///   Feedback 8
    /// </summary> 
    private void Feedback8(float length, float strength) {
      float[] _l = { length, length, length };
      float[] _s = { strength, strength, strength };
      StartCoroutine(LongVibration(3, _l, .05f, _s));
    }

    /// <summary>
    ///   Feedback 9
    /// </summary> 
    private void Feedback9(float length, float strength) {
      float[] _l = { length * 2, length };
      float[] _s = { strength, strength };
      StartCoroutine(LongVibration(2, _l, .05f, _s));
    }

    /// <summary>
    ///   Feedback 10
    /// </summary> 
    private void Feedback10(float length, float strength) {
      float[] _l = { length, length * 2 };
      float[] _s = { strength, strength };
      StartCoroutine(LongVibration(2, _l, .05f, _s));
    }

    /// <summary>
    ///   Feedback 11
    /// </summary> 
    private void Feedback11(float length, float strength) {
      StartCoroutine(SignalB(length, strength));
    }

    /// <summary>
    ///   Feedback 12
    /// </summary> 
    private void Feedback12(float length, float strength) {
      Pattern_7(length, strength);
    }

    /// <summary>
    ///   Feedback 13
    /// </summary> 
    private void Feedback13(float length, float strength) {
      Pattern_8(length, strength);
    }

    /// <summary>
    ///   Feedback 14
    /// </summary> 
    private void Feedback14(float length, float strength) {
      Pattern_9(length, strength);
    }

    /// <summary>
    ///   Feedback 15
    /// </summary> 
    private void Feedback15(float length, float strength) {
      Pattern_10(length, strength);
    }

    /// <summary>
    ///   Feedback 16
    /// </summary> 
    private void Feedback16(float length, float strength) {
      Pattern_11(length, strength);
    }

    /// <summary>
    ///   Feedback 17
    /// </summary> 
    private void Feedback17(float length, float strength) {
      Pattern_12(length, strength);
    }

    /// <summary>
    ///   Feedback 18
    /// </summary> 
    private void Feedback18(float length, float strength) {
      Pattern_13(length, strength);
    }
  }
}
