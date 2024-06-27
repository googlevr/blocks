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
using com.google.apps.peltzer.client.app;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Dumb wrapper around hardware-specific logic for controller animations.
  /// </summary>
  public class BaseControllerAnimation : MonoBehaviour {
    // The physical controller responsible for input & pose.
    protected ControllerDevice controller;

    /// <summary>
    ///   The local position pivot point to use when rotating the VIVE trigger mesh.
    /// </summary>
    private readonly Vector3 TRIGGER_PIVOT_POINT_VIVE = new Vector3(0f, -0.005f, -0.05f);
    /// <summary>
    ///   The local position pivot point to use when rotating the RIFT trigger mesh.
    /// </summary>
    private readonly Vector3 TRIGGER_PIVOT_POINT_RIFT = new Vector3(0f, 0.01037f, -0.00451f);
    ///  Values used to calculate the LGripPressedLoc and RGripPressedLoc values.
    private const float GRIP_PRESS_OFFSET_VIVE = 0.0005f;
    private const float GRIP_PRESS_OFFSET_RIFT = 0.002f;
    /// <summary>
    ///   The positional y-offset for the touchpad (or any other "button") mesh when the user has pressed the button.
    /// </summary>
    private const float BTN_PRESSED_LOCATION_Y_OFFSET = 0.001f;
    /// <summary>
    ///   The positional y-offset for the touchpad (or any other "button") mesh when the user has pressed the button.
    /// </summary>
    private const float BTN_PRESSED_LOCATION_Y_OFFSET_RIFT = 0.001f;
    /// <summary>
    ///   The maximum +/- angle to lerp through rotation along X and Z axis
    ///   for touchpad orientation as mappeed to user input.
    /// </summary>
    private const float TOUCHPAD_MAX_ANGLE = 4f;
    /// <summary>
    ///   The maximum angle to lerp through for the angle of the trigger mesh as mapped to user input.
    /// </summary>
    private const float TRIGGER_MAX_ANGLE = 10f;
    /// <summary>
    ///   A scale factor used when mapping the location of the user's thumb to our custom model
    ///   which also accounts for the size of the location visual.
    /// </summary>
    private const float USER_THUMB_LOCATION_SCALE_FACTOR = 0.019f;
    /// <summary>
    ///   The positional offset for placing the thumb location highlight on the trackpad.
    /// </summary>
    private readonly Vector3 USER_THUMB_LOCATION_OFFSET = new Vector3(0f, 0.008f, 0f);
    /// <summary>
    ///   A visual indicator placed on the touchpad to show the user's thumb location on touch.
    /// </summary>
    private GameObject userThumbLocation;
    /// <summary>
    ///   A reference to the root of the touchpad GameObjects.
    /// </summary>
    public GameObject touchpads;
    /// <summary>
    ///   A reference to the default / start position of the touchpad from the related controller model.
    /// </summary>
    private Vector3 touchpadDefaultLoc;
    /// <summary>
    ///   A reference to the default / start rotation of the touchpad from the related controller model.
    /// </summary>
    private Vector3 touchpadDefaultRot;
    /// <summary>
    ///   The local position pivot point to use when rotating the trigger mesh. 
    /// </summary>
    private Vector3 triggerPivotPoint;

    /// <summary>
    ///   A reference to the the trigger mesh GameObject.
    /// </summary>
    public GameObject trigger;
    /// <summary>
    /// A reference to the default / start position of the trigger from the related controller model.
    /// </summary>
    private Vector3 triggerDefaultLoc;
    /// <summary>
    /// A reference to the default / start rotation of the trigger from the related controller model.
    /// </summary>
    private Vector3 triggerDefaultRot;

    /// <summary>
    ///   A reference to the left grip mesh GameObject.
    /// </summary>
    public GameObject LGrip;
    /// <summary>
    ///   A reference to the right grip mesh GameObject.
    /// </summary>
    public GameObject RGrip;
    /// <summary>
    ///   A rerence to the default / start position of the left grip from the related controller model
    /// </summary>
    private Vector3 LGripDefaultLoc;
    /// <summary>
    ///   A rerence to the default / start position of the right grip from the related controller model
    /// </summary>
    private Vector3 RGripDefaultLoc;
    /// <summary>
    ///   A rerence to the position when pressed of the left grip from the related controller model
    /// </summary>
    private Vector3 LGripPressedLoc;
    /// <summary>
    ///   A rerence to the position when pressed of the right grip from the related controller model
    /// </summary>
    private Vector3 RGripPressedLoc;

    /// <summary>
    ///   A reference to the App Menu button.
    /// </summary>
    public GameObject appMenuButton;
    /// <summary>
    ///   A rerence to the default / start position of the App Menu button related controller model
    /// </summary>
    private Vector3 appMenuButtonDefaultLoc;
    /// <summary>
    ///   A reference to the Secondary button (only applicable for Touch controllers)
    /// </summary>
    public GameObject secondaryButton;
    /// <summary>
    ///   Contains the overlays for the touchpad - using this for Rift touchpad effect.
    /// </summary>
    public Transform touchpadOverlay;
    /// <summary>
    ///   Touchpad icons around Rift stick.
    /// </summary>
    public GameObject touchpadIcon;
    /// <summary>
    ///   A rerence to the default / start position of the App Menu button related controller model
    /// </summary>
    /// 
    private Vector3 secondaryButtonDefaultLoc;

    void Start() {
      // Create the touchpad location visual.
      touchpadDefaultLoc = touchpads.transform.localPosition;
      touchpadDefaultRot = touchpads.transform.localEulerAngles;

      //UpdateRiftTouchPad(false);

      if (Config.Instance.VrHardware == VrHardware.Vive) {
        userThumbLocation = Instantiate(Resources.Load<GameObject>("Prefabs/userThumb"));
        userThumbLocation.transform.SetParent(touchpads.transform, /* worldPositionStays */ true);
        triggerPivotPoint = TRIGGER_PIVOT_POINT_VIVE;
      } else if (Config.Instance.VrHardware == VrHardware.Rift) {
        triggerPivotPoint = TRIGGER_PIVOT_POINT_RIFT;
      }

      // Get trigger defaults.
      triggerDefaultLoc = trigger.transform.localPosition;
      triggerDefaultRot = trigger.transform.localEulerAngles;

      // Get grip defaults.
      float gripPressOffset = Config.Instance.VrHardware == VrHardware.Vive ? GRIP_PRESS_OFFSET_VIVE : GRIP_PRESS_OFFSET_RIFT;
      if (LGrip != null) {
        LGripDefaultLoc = LGrip.transform.localPosition;
        LGripPressedLoc = new Vector3(LGripDefaultLoc.x + gripPressOffset, LGripDefaultLoc.y, LGripDefaultLoc.z);
      }
      if (RGrip != null) {
        RGripDefaultLoc = RGrip.transform.localPosition;
        RGripPressedLoc = new Vector3(RGripDefaultLoc.x - gripPressOffset, RGripDefaultLoc.y, RGripDefaultLoc.z);
      }

      // Get the reference to button defaults;
      appMenuButtonDefaultLoc = appMenuButton.transform.localPosition;
      if (secondaryButton != null) {
        secondaryButtonDefaultLoc = secondaryButton.transform.localPosition;
      }
    }

    void Update() {
      if (controller == null) {
        return;
      }
      DetectTouchpad(controller);
      DetectTrigger(controller);
      DetectGrip(controller);
      DetectAppMenu(controller);
      if (secondaryButton != null) {
        DetectSecondaryButton(controller);
      }
    }

    public void SetControllerDevice(ControllerDevice newControllerDevice) {
      controller = newControllerDevice;
    }

    /// <summary>
    ///   Detect the state of the touchpad and set the orientation and thumb locator
    ///   orientation based on current value.
    /// </summary>
    /// <param name="controller">ControllerDevice for referencing input.</param>
    private void DetectTouchpad(ControllerDevice controller) {
      // Highlight
      Vector2 loc = Vector2.zero;
      if (Config.Instance.VrHardware == VrHardware.Vive) {
        if (controller.IsTouched(ButtonId.Touchpad)) {
          userThumbLocation.SetActive(true);
          loc = controller.GetDirectionalAxis() * USER_THUMB_LOCATION_SCALE_FACTOR;
          userThumbLocation.transform.localPosition = new Vector3(
            USER_THUMB_LOCATION_OFFSET.x + loc.x,
            USER_THUMB_LOCATION_OFFSET.y,
            USER_THUMB_LOCATION_OFFSET.z + loc.y);
        } else {
          userThumbLocation.SetActive(false);
        }

        // Orientation
        if (controller.IsPressed(ButtonId.Touchpad)) {
          touchpads.transform.localPosition = new Vector3(touchpadDefaultLoc.x,
              touchpadDefaultLoc.y - BTN_PRESSED_LOCATION_Y_OFFSET, touchpadDefaultLoc.z);
          loc = controller.GetDirectionalAxis();
        }

        if (controller.WasJustReleased(ButtonId.Touchpad)) {
          touchpads.transform.localPosition = touchpadDefaultLoc;
          touchpads.transform.localEulerAngles = touchpadDefaultRot;
        }

      } else { // Oculus Touch.
        loc = controller.GetDirectionalAxis();
        touchpads.transform.localRotation = Quaternion.Euler(loc.x*20,loc.y*-20,0);
      }
    }

    /// <summary>
    ///   Detect the state of the trigger and set the orientation of the mesh based on the reported value.
    /// </summary>
    /// <param name="controller">ControllerDevice for referencing input.</param>
    private void DetectTrigger(ControllerDevice controller) {
      Vector2 triggerRotationValue = controller.GetTriggerScale() * TRIGGER_MAX_ANGLE;
      Vector3 dir = triggerDefaultLoc - triggerPivotPoint; // get point direction relative to pivot
      dir = Quaternion.Euler(triggerRotationValue) * dir; // rotate it
      trigger.transform.localPosition = dir + triggerPivotPoint; // calculate rotated point
      trigger.transform.localEulerAngles = new Vector3(
          triggerDefaultRot.x - triggerRotationValue.x,
          triggerDefaultRot.y,
          triggerDefaultRot.z);
    }

    /// <summary>
    ///   Detect the grip state and set the orientation of the mesh based on the reported value.
    /// </summary>
    /// <param name="controller">ControllerDevice for referencing input.</param>
    private void DetectGrip(ControllerDevice controller) {
      if (Config.Instance.VrHardware == VrHardware.Vive) {

        if (controller.WasJustPressed(ButtonId.Grip)) {
          if (LGrip != null) {
            LGrip.transform.localPosition = LGripPressedLoc;
          }
          if (RGrip != null) {
            RGrip.transform.localPosition = RGripPressedLoc;
          }
        } else if (controller.WasJustReleased(ButtonId.Grip)) {
          if (LGrip != null) {
            LGrip.transform.localPosition = LGripDefaultLoc;
          }
          if (RGrip != null) {
            RGrip.transform.localPosition = RGripDefaultLoc;
          }
        }
      }

      else { //Oculus Touch
        if (controller.WasJustPressed(ButtonId.Grip)) {
          if (LGrip != null) {
            LGrip.transform.localPosition = new Vector3(0, 0, -.004f);
          } 
          if (RGrip != null) {
            RGrip.transform.localPosition = new Vector3(0, 0, -0.0145f);
          }
        } else if (controller.WasJustReleased(ButtonId.Grip)) {
          if (LGrip != null) {
            LGrip.transform.localPosition = LGripDefaultLoc;
          }
          if (RGrip != null) {
            RGrip.transform.localPosition = RGripDefaultLoc;
          }
        }
      }

    }

    /// <summary>
    ///   Detect the App Menu button state and set the orientation of the mesh based on the reported value.
    /// </summary>
    /// <param name="controller">ControllerDevice for referencing input.</param>
    private void DetectAppMenu(ControllerDevice controller) {
      if (controller.WasJustPressed(ButtonId.ApplicationMenu)) {
        appMenuButton.transform.localPosition = new Vector3(appMenuButton.transform.localPosition.x,
            appMenuButton.transform.localPosition.y - BTN_PRESSED_LOCATION_Y_OFFSET,
            appMenuButton.transform.localPosition.z);
      } else if (controller.WasJustReleased(ButtonId.ApplicationMenu)) {
        appMenuButton.transform.localPosition = appMenuButtonDefaultLoc;
      }
    }

    public void UpdateTouchpadOverlay(GameObject toolhead) {
    }

    /// <summary>
    ///   Detect the secondary button state and set the orientation of the mesh based on the reported value.
    /// </summary>
    /// <param name="controller">ControllerDevice for referencing input.</param>
    private void DetectSecondaryButton(ControllerDevice controller) {
      if (controller.WasJustPressed(ButtonId.SecondaryButton)) {
        secondaryButton.transform.localPosition = new Vector3(secondaryButton.transform.localPosition.x,
            secondaryButton.transform.localPosition.y - BTN_PRESSED_LOCATION_Y_OFFSET,
            secondaryButton.transform.localPosition.z);
      } else if (controller.WasJustReleased(ButtonId.SecondaryButton)) {
        secondaryButton.transform.localPosition = secondaryButtonDefaultLoc;
      }
    }
  }
}
