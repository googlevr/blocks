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

#define STEAMVRBUILD
using UnityEngine;

using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Controller SDK logic for Steam. Supports both Vive and Rift+Touch hardware.
  /// </summary>
#if STEAMVRBUILD
  public class ControllerDeviceSteam : ControllerDevice {
    // With latest update to SteamVR (12/9/16), Oculus Touch B/Y are now supported. The plugin doesn't have
    // ButtonMask added to support this.
    public const ulong ButtonMask_Button01 = (1ul << (int)Valve.VR.EVRButtonId.k_EButton_A);
    public const ulong ButtonMask_Button02 = (1ul << (int)Valve.VR.EVRButtonId.k_EButton_ApplicationMenu);
    private const float LIGHT_TRIGGER_PULL_THRESHOLD = 0.01f;

    // The TrackedObject for this controller, and its index.
    private readonly SteamVR_TrackedObject trackedObject;
    private int Index {
      get { return (int)trackedObject.index; }
    }

    // The current and previous state of the controller.
    private Valve.VR.VRControllerState_t currentState;
    private Valve.VR.VRControllerState_t previousState;
    // The current and previous most-recent non-zero touchpad locations.
    private Vector2 currentPad;
    private Vector2 previousPad;

    // Cache button presses for efficiency.
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

    // A Steam controller's validity is always determined by its TrackedObject's validity.
    private bool isValid;
    public bool IsTrackedObjectValid {
      get { return trackedObject.isValid; }
      set { Debug.Assert(value == trackedObject.isValid); }
    }

    // Constructor, taking in a transform such that it can find the SteamVRTrackedObject.
    public ControllerDeviceSteam(Transform transform) {
      trackedObject = Config.Instance.VrHardware == VrHardware.Rift ?
          transform.GetComponent<SteamVR_TrackedObject>() :
          transform.GetComponent<SteamVR_TrackedObject>();
      currentState = new Valve.VR.VRControllerState_t();
      previousState = new Valve.VR.VRControllerState_t();
    }

    // Update loop (to be called manually, this is not a MonoBehavior).
    public void Update() {
      // Get current and previous controller state.
      SteamVR steamVR = SteamVR.instance;
      if (steamVR != null) {
        previousState = currentState;
        steamVR.hmd.GetControllerState((uint)Index, ref currentState,
          (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Valve.VR.VRControllerState_t)));
      }

      // Update the latest touchpad location, if possible.
      previousPad = currentPad;
      if (Index != (int)SteamVR_TrackedObject.EIndex.None) {
        var currentPadV = currentState.rAxis0;
        currentPad = new Vector2(currentPadV.x, currentPadV.y);
      } else {
        currentPad = Vector2.zero;
      }

      // Update 'previous state' variables.
      triggerWasPressedOnLastUpdate = triggerPressed;
      gripWasPressedOnLastUpdate = gripPressed;
      secondaryButtonWasPressedOnLastUpdate = secondaryButtonPressed;
      applicationButtonWasPressedOnLastUpdate = applicationButtonPressed;
      touchpadWasPressedOnLastUpdate = touchpadPressed;
      triggerWasHalfPressedOnLastUpdate = triggerHalfPressed;

      // Find which buttons are currently pressed.
      triggerPressed = ButtonIsPressedInternal(ButtonId.Trigger);
      gripPressed = ButtonIsPressedInternal(ButtonId.Grip);
      secondaryButtonPressed = ButtonIsPressedInternal(ButtonId.SecondaryButton);
      applicationButtonPressed = ButtonIsPressedInternal(ButtonId.ApplicationMenu);
      touchpadPressed = ButtonIsPressedInternal(ButtonId.Touchpad);
      triggerHalfPressed = IsTriggerHalfPressedInternal();
    }

    // A mapping from ButtonId to SteamVR ButtonMask.
    private static ulong GetMaskFromButtonId(ButtonId buttonId) {
      ulong mask = 0;
      switch (buttonId) {
        case ButtonId.ApplicationMenu:
          mask = Config.Instance.VrHardware == VrHardware.Rift
            ? ButtonMask_Button01
            : SteamVR_Controller.ButtonMask.ApplicationMenu;
          break;
        case ButtonId.Touchpad:
          mask = SteamVR_Controller.ButtonMask.Touchpad;
          break;
        case ButtonId.Trigger:
          mask = SteamVR_Controller.ButtonMask.Trigger;
          break;
        case ButtonId.Grip:
          mask = SteamVR_Controller.ButtonMask.Grip;
          break;
        case ButtonId.SecondaryButton:
          mask = ButtonMask_Button02;
          break;
      }
      return mask;
    }

    // The trigger scale for the previous update, to aid with detecting trigger-released using our custom threshold.
    private Vector2 GetPreviousTriggerScale() {
      return new Vector2(previousState.rAxis1.x, previousState.rAxis1.y);
    }

    // Interface method implementations begin.
    
    public Vector3 GetVelocity() {
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return Vector3.zero; }
      SteamVR_Controller.Device controller = SteamVR_Controller.Input((int)trackedObject.index);
      return controller.velocity;
    }

    public bool IsTriggerHalfPressedInternal() {
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return false; }
      SteamVR_Controller.Device controller = SteamVR_Controller.Input((int)trackedObject.index);
      // Only record as half pressed if the trigger is not pressed.
      return controller.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x >= LIGHT_TRIGGER_PULL_THRESHOLD
        && !triggerPressed;
    }

    public bool IsPressed(ButtonId buttonId) {
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return false; }

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
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return false; }
      return triggerHalfPressed;
    }

    public bool WasTriggerJustReleasedFromHalfPress() {
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return false; }
      return !triggerHalfPressed && !triggerPressed && triggerWasHalfPressedOnLastUpdate;
    }

    public bool WasJustPressed(ButtonId buttonId) {
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return false; }

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
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return false; }

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

    private bool ButtonIsPressedInternal(ButtonId buttonId) {
      if (buttonId == ButtonId.Trigger && GetTriggerScale().x <= PeltzerMain.TRIGGER_THRESHOLD) {
        return false;
      }

      ulong mask = GetMaskFromButtonId(buttonId);

      // The Touch thumbstick is considered 'pressed' is it is in one of the far quadrants, or if it is in the center
      // and has actually been depressed. This allows users to simply flick the thumbstick to choose an option, rather
      // than having to move and press-in the thumbstick, which is tiresome.
      if (Config.Instance.VrHardware == VrHardware.Rift && buttonId == ButtonId.Touchpad) {
        TouchpadLocation touchpadLocation =
          TouchpadLocationHelper.GetTouchpadLocation(currentPad);
        if (touchpadLocation == TouchpadLocation.CENTER) {
          return (currentState.ulButtonPressed & mask) != 0;
        } else if (touchpadLocation == TouchpadLocation.NONE) {
          return false;
        } else {
          return true;
        }
      }

      return (currentState.ulButtonPressed & mask) != 0;
    }

    public bool IsTouched(ButtonId buttonId) {
      if (Index == (int)SteamVR_TrackedObject.EIndex.None) { return false; }

      ulong mask = GetMaskFromButtonId(buttonId);
      return (currentState.ulButtonTouched & mask) != 0;
    }

    public Vector2 GetDirectionalAxis() {
      return currentPad;
    }

    public TouchpadLocation GetTouchpadLocation() {
      return TouchpadLocationHelper.GetTouchpadLocation(currentPad);
    }

    public Vector2 GetTriggerScale() {
      return new Vector2(currentState.rAxis1.x, currentState.rAxis1.y);
    }

    public void TriggerHapticPulse(ushort durationMicroSec = 500) {
      SteamVR steamVR = SteamVR.instance;
      if (steamVR == null) { return; }
        // Steam accepts a parameter for indicating the axis of the haptic pulse but 0 is the only
        // one implemented now and this concept doesn't exist for Oculus controllers so it's being
        // hardcoded to (uint)0 here.
        steamVR.hmd.TriggerHapticPulse((uint)Index, (uint)0, (char)durationMicroSec);
    }
  }
#endif
}
