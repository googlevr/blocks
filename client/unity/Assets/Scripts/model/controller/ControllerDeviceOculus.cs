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
  class ControllerDeviceOculus : ControllerDevice {
    private const float LIGHT_TRIGGER_PULL_THRESHOLD = 0.01f;
    // The most-recent thumbstick location.
    private Vector2 currentPad = Vector2.zero;

    // Which controller is this (Touch Left, Touch Right)?
    public OVRInput.Controller controllerType = OVRInput.Controller.None;

    // An Oculus controller's validity is determined in OculusHandTrackingManager.
    private bool isValid;
    private bool wasValidOnLastUpdate;
    public bool IsTrackedObjectValid { get { return isValid && OVRManager.hasVrFocus; } set { isValid = value; } }

    // We must manually track velocity in the Oculus SDK.
    private Transform transform;
    private Vector3 worldPositionOnLastUpdate;
    private Vector3 velocity;

    // We must manually track button releases in the Oculus SDK.
    private bool triggerPressed;
    private bool gripPressed;
    private bool secondaryButtonPressed;
    private bool applicationButtonPressed;
    private bool touchpadPressed;
    private bool triggerHalfPressed;
    private bool triggerWasPressedOnLastUpdate;
    private bool gripWasPressedOnLastUpdate;
    private bool secondaryButtonWasPressedOnLastUpdate;
    private bool applicationButtonWasPressedOnLastUpdate;
    private bool touchpadWasPressedOnLastUpdate;
    private bool triggerWasHalfPressedOnLastUpdate;

    // Haptics.
    private OVRHapticsClip rumbleHapticsClip;
    private AudioClip rumbleClip;

    // Constructor, taking in a transform such that it can be regularly updated.
    public ControllerDeviceOculus(Transform transform) {
      this.transform = transform;
      if (rumbleClip != null) {
        rumbleHapticsClip = new OVRHapticsClip(rumbleClip);
      }
    }

    // Update loop (to be called manually, this is not a MonoBehavior).
    public void Update() {
      if (!isValid) {
        // In an invalid state, nothing is pressed.
        triggerPressed = false;
        gripPressed = false;
        secondaryButtonPressed = false;
        applicationButtonPressed = false;
        touchpadPressed = false;
        velocity = Vector3.zero;
        currentPad = Vector2.zero;

        // Return before calculating releases, and without updating any 'previous state' variables.
        return;
      }

      // Update the latest thumbstick location, if possible.
      currentPad = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controllerType);

      // Update velocity only when we have two subsequent valid updates.
      if (wasValidOnLastUpdate) {
        velocity = (transform.position - worldPositionOnLastUpdate) / Time.deltaTime;
      } else {
        velocity = Vector3.zero;
      }

      // Update 'previous state' variables.
      triggerWasPressedOnLastUpdate = triggerPressed;
      gripWasPressedOnLastUpdate = gripPressed;
      secondaryButtonWasPressedOnLastUpdate = secondaryButtonPressed;
      applicationButtonWasPressedOnLastUpdate = applicationButtonPressed;
      touchpadWasPressedOnLastUpdate = touchpadPressed;
      worldPositionOnLastUpdate = transform.position;
      wasValidOnLastUpdate = isValid;
      triggerWasHalfPressedOnLastUpdate = triggerHalfPressed;


      // Find which buttons are currently pressed.
      triggerPressed = IsPressedInternal(ButtonId.Trigger);
      gripPressed = IsPressedInternal(ButtonId.Grip);
      secondaryButtonPressed = IsPressedInternal(ButtonId.SecondaryButton);
      applicationButtonPressed = IsPressedInternal(ButtonId.ApplicationMenu);
      touchpadPressed = IsPressedInternal(ButtonId.Touchpad);
      triggerHalfPressed = IsTriggerHalfPressedInternal();
    }

    // A mapping from ButtonId to OvrInput Button.
    private static bool OvrButtonFromButtonId(ButtonId buttonId, out OVRInput.Button ovrButton) {
      switch (buttonId) {
        case ButtonId.ApplicationMenu:
          ovrButton = OVRInput.Button.One;
          return true;
        case ButtonId.Touchpad:
          ovrButton = OVRInput.Button.PrimaryThumbstick;
          return true;
        case ButtonId.Trigger:
          ovrButton = OVRInput.Button.PrimaryIndexTrigger;
          return true;
        case ButtonId.Grip:
          ovrButton = OVRInput.Button.PrimaryHandTrigger;
          return true;
        case ButtonId.SecondaryButton:
          ovrButton = OVRInput.Button.Two;
          return true;
      }

      ovrButton = OVRInput.Button.Any;
      return false;
    }

    // A mapping from ButtonId to OvrInput Touch.
    private static bool OvrTouchFromButtonId(ButtonId buttonId, out OVRInput.Touch ovrTouch) {
      switch (buttonId) {
        case ButtonId.ApplicationMenu:
          ovrTouch = OVRInput.Touch.One;
          return true;
        case ButtonId.Touchpad:
          ovrTouch = OVRInput.Touch.PrimaryThumbstick;
          return true;
        case ButtonId.Trigger:
          ovrTouch = OVRInput.Touch.PrimaryIndexTrigger;
          return true;
        case ButtonId.SecondaryButton:
          ovrTouch = OVRInput.Touch.Two;
          return true;
      }

      ovrTouch = OVRInput.Touch.Any;
      return false;
    }

    private bool IsPressedInternal(ButtonId buttonId) {
      if (!isValid) return false;

      OVRInput.Button ovrButton;
      if (!OvrButtonFromButtonId(buttonId, out ovrButton)) return false;

      // The Touch thumbstick is considered 'pressed' is it is in one of the far quadrants, or if it is in the center
      // and has actually been depressed. This allows users to simply flick the thumbstick to choose an option, rather
      // than having to move and press-in the thumbstick, which is tiresome.
      if (buttonId == ButtonId.Touchpad) {
        TouchpadLocation touchpadLocation = GetTouchpadLocation();
        return (touchpadLocation == TouchpadLocation.CENTER && OVRInput.Get(ovrButton, controllerType)) ||
          (touchpadLocation != TouchpadLocation.CENTER && touchpadLocation != TouchpadLocation.NONE);
      } else {
        return OVRInput.Get(ovrButton, controllerType);
      }
    }

    private bool IsTriggerHalfPressedInternal() {
      if (!isValid) return false;

      // Only record as half pressed if the trigger is not pressed.
      return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) >= LIGHT_TRIGGER_PULL_THRESHOLD
        && !triggerPressed;
    }

    // Interface method implementations begin.

    public Vector3 GetVelocity() {
      return velocity;
    }

    public bool IsPressed(ButtonId buttonId) {
      if (!isValid) return false;

      switch (buttonId) {
        case ButtonId.ApplicationMenu:
          return applicationButtonPressed;
        case ButtonId.Touchpad:
          return touchpadPressed;
        case ButtonId.Trigger:
          return triggerPressed;
        case ButtonId.Grip:
          return gripPressed;
        case ButtonId.SecondaryButton:
          return secondaryButtonPressed;
      }

      return false;
    }

    public bool IsTriggerHalfPressed() {
      if (!isValid) return false;
      return triggerHalfPressed;
    }

    public bool WasTriggerJustReleasedFromHalfPress() {
      if (!isValid) return false;
      return !triggerHalfPressed && !triggerPressed && triggerWasHalfPressedOnLastUpdate;
    }

    public bool WasJustPressed(ButtonId buttonId) {
      if (!isValid) return false;

      switch (buttonId) {
        case ButtonId.ApplicationMenu:
          return applicationButtonPressed && !applicationButtonWasPressedOnLastUpdate;
        case ButtonId.Touchpad:
          return touchpadPressed && !touchpadWasPressedOnLastUpdate;
        case ButtonId.Trigger:
          return triggerPressed && !triggerWasPressedOnLastUpdate;
        case ButtonId.Grip:
          return gripPressed && !gripWasPressedOnLastUpdate;
        case ButtonId.SecondaryButton:
          return secondaryButtonPressed && !secondaryButtonWasPressedOnLastUpdate;
      }

      return false;
    }

    public bool WasJustReleased(ButtonId buttonId) {
      if (!isValid) return false;

      switch (buttonId) {
        case ButtonId.ApplicationMenu:
          return !applicationButtonPressed && applicationButtonWasPressedOnLastUpdate;
        case ButtonId.Touchpad:
          return !touchpadPressed && touchpadWasPressedOnLastUpdate;
        case ButtonId.Trigger:
          return !triggerPressed && triggerWasPressedOnLastUpdate;
        case ButtonId.Grip:
          return !gripPressed && gripWasPressedOnLastUpdate;
        case ButtonId.SecondaryButton:
          return !secondaryButtonPressed && secondaryButtonWasPressedOnLastUpdate;
      }

      return false;
    }

    public bool IsTouched(ButtonId buttonId) {
      if (!isValid) return false;
      OVRInput.Touch ovrTouch;
      if (!OvrTouchFromButtonId(buttonId, out ovrTouch)) return false;
      return OVRInput.Get(ovrTouch, controllerType);
    }

    public Vector2 GetDirectionalAxis() {
      return currentPad;
    }

    public TouchpadLocation GetTouchpadLocation() {
      return TouchpadLocationHelper.GetTouchpadLocation(currentPad);
    }

    public Vector2 GetTriggerScale() {
      return new Vector2(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controllerType), 0);
    }

    public void TriggerHapticPulse(ushort durationMicroSec = 500) {
      float length = durationMicroSec / 1000000f;
      var channel = controllerType == OVRInput.Controller.LTouch ? OVRHaptics.LeftChannel : OVRHaptics.RightChannel;
      if (rumbleHapticsClip != null) {
        int count = (int)(length / rumbleClip.length);
        channel.Preempt(rumbleHapticsClip);
        for (int i = 1; i < count; i++) {
          channel.Queue(rumbleHapticsClip);
        }
      }
    }
  }
}
