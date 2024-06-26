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

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Abstract wrapper for controller SDKs, to be implemented for Steam and Oculus.
  /// </summary>
  public interface ControllerDevice {
    // Note that this is not a MonoBehavior, its Update must be called from one.
    void Update();

    // Get/Set whether this controller is in a valid state (if not, none of the methods below are meaningful).
    bool IsTrackedObjectValid { get; set; }

    // Get the current velocity of this controller.
    Vector3 GetVelocity();
    
    // Check whether a given button is pressed, or has just been pressed/released.
    bool IsPressed(ButtonId buttonId);
    bool WasJustPressed(ButtonId buttonId);
    bool WasJustReleased(ButtonId buttonId);

    // Check whether the trigger is half pressed or was just released from a half press.
    bool IsTriggerHalfPressed();
    bool WasTriggerJustReleasedFromHalfPress();

    // Check whether a given button is touched.
    // Only valid where the given button supports capacative inputs in the hardware being used.
    bool IsTouched(ButtonId buttonId);

    // Get the position of the user's thumb on the touchpad (Vive) or stick (Rift), as a vector or a TouchpadLocation.
    Vector2 GetDirectionalAxis();
    TouchpadLocation GetTouchpadLocation();

    // Get the position of the trigger.
    Vector2 GetTriggerScale();

    // Trigger a haptic pulse of the given duration. 
    void TriggerHapticPulse(ushort durationMicroSec = 500);
  }
}