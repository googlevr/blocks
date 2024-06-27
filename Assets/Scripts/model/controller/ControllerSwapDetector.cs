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
using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using UnityEngine.UI;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Detects and handles the "controller bump" gesture to switch the left and right controllers.
  /// </summary>
  public class ControllerSwapDetector : MonoBehaviour {
    /// <summary>
    /// Minimum angle between controllers to allow swap.
    /// </summary>
    private const float OPPOSED_ANGLE_THRESHOLD = 170.0f;

    /// <summary>
    /// Minimum angle between controllers to allow swap when using Oculus SDK.
    /// </summary>
    private const float OPPOSED_ANGLE_THRESHOLD_OCULUS = 90f;

    /// <summary>
    /// Maximum distance between controllers to allow swap.
    /// </summary>
    private const float MAX_DISTANCE_FOR_SWAP = 0.4f;

    /// <summary>
    /// Maximum distance between controllers to allow swap when using Oculus SDK.
    /// </summary>
    private const float MAX_DISTANCE_FOR_SWAP_OCULUS = 0.04f;

    /// <summary>
    /// Minimum magnitude of acceleration to consider controller to have bumped.
    /// </summary>
    private const float BUMP_ACCELERATION_THRESHOLD = 10.0f;

    /// <summary>
    /// Maximum velocity of controller to consider controller to have bumped
    /// (a bump happens when there is a sufficiently large acceleration and a
    /// sufficiently small velocity, indicating that the controller has just stopped).
    /// </summary>
    private const float BUMP_VELOCITY_THRESHOLD = 0.5f;

    /// <summary>
    /// Duration for which a bump remains active. This exists because controllers can detect
    /// the bump at different times due to sensor imprecision, so we need the bump to remain
    /// "valid" for a while in order to know that both controllers bumped.
    /// </summary>
    private const float BUMP_DURATION = 0.2f;

    /// <summary>
    /// Minimum time between successive controller swaps.
    /// </summary>
    private const float MIN_TIME_BETWEEN_SWAPS = 2.0f;

    /// <summary>
    /// Angle to adjust the Oculus controllers' forwards by when measuring if they are facing away
    /// from each other.
    /// </summary>
    private static readonly Quaternion ADJUST_ANGLE_OCULUS = Quaternion.Euler(-45, 0, 0);

    /// <summary>
    /// Whether setup is done or not.
    /// </summary>
    private bool setupDone;

    /// <summary>
    /// We can't swap controllers until both are available: this tracks if we're waiting for a swap.
    /// </summary>
    private bool waitingToSwapControllers;

    /// <summary>
    /// Peltzer controller.
    /// </summary>
    private PeltzerController peltzerController;

    /// <summary>
    /// Palette controller.
    /// </summary>
    private PaletteController paletteController;

    /// <summary>
    /// Time until which we do not detect the controller swap gesture.
    /// </summary>
    private float snoozeUntil;

    /// <summary>
    /// Information about the position/velocity/acceleration of a particular controller.
    /// </summary>
    private class ControllerInfo {
      public Vector3 position;
      public Vector3 velocity;
      public Vector3 acceleration;
      public float lastBumpTime;

      public ControllerInfo(Vector3 initialPosition) {
        position = initialPosition;
      }

      public void UpdatePosition(Vector3 newPosition) {
        Vector3 newVelocity = (newPosition - position) / Time.deltaTime;
        acceleration = (newVelocity - velocity) / Time.deltaTime;
        velocity = newVelocity;
        position = newPosition;

        if (acceleration.magnitude > BUMP_ACCELERATION_THRESHOLD && velocity.magnitude < BUMP_VELOCITY_THRESHOLD) {
          lastBumpTime = Time.time;
        }
      }

      public bool BumpedRecently() {
        return Time.time - lastBumpTime < BUMP_DURATION;
      }
    }
    private ControllerInfo[] controllers = new ControllerInfo[2];

    /// <summary>
    /// Initial setup. Must be called once when initializing.
    /// </summary>
    public void Setup() {
      peltzerController = PeltzerMain.Instance.peltzerController;
      paletteController = PeltzerMain.Instance.paletteController;
      controllers[0] = new ControllerInfo(peltzerController.gameObject.transform.position);
      controllers[1] = new ControllerInfo(paletteController.gameObject.transform.position);
      setupDone = true;
    }

    private void Update() {
      if (!setupDone) return;

      if (waitingToSwapControllers) {
        TrySwappingControllers();
        return;
      }

      controllers[0].UpdatePosition(peltzerController.transform.position);
      controllers[1].UpdatePosition(paletteController.transform.position);

      // If we're snoozing, just chill.
      if (Time.time < snoozeUntil) return;

      // If the controllers are appropriately positioned and oriented, and both have recently bumped,
      // then the user has performed the "swap controller" gesture.
      if (AreControllersOpposed() && AreControllersCloseEnough() && DidControllersBump()) {
        // Swap controllers
        TrySwappingControllers();
        snoozeUntil = Time.time + MIN_TIME_BETWEEN_SWAPS;
      }
    }

    /// <summary>
    /// Returns whether or not the controllers are facing opposite directions.
    /// </summary>
    private bool AreControllersOpposed() {
      if (Config.Instance.sdkMode == SdkMode.Oculus) {
        return Vector3.Angle(ADJUST_ANGLE_OCULUS * peltzerController.transform.forward,
          ADJUST_ANGLE_OCULUS * paletteController.transform.forward) > OPPOSED_ANGLE_THRESHOLD_OCULUS;
      }
      return Vector3.Angle(peltzerController.transform.forward, paletteController.transform.forward) >
        OPPOSED_ANGLE_THRESHOLD;
    }

    /// <summary>
    /// Returns whether or not the controllers are close enough to swap.
    /// </summary>
    private bool AreControllersCloseEnough() {
      if (Config.Instance.sdkMode == SdkMode.Oculus) {
        return Vector3.Distance(peltzerController.controllerGeometry.handleBase.transform.position,
            paletteController.controllerGeometry.handleBase.transform.position) <
          MAX_DISTANCE_FOR_SWAP_OCULUS;
      }
      return Vector3.Distance(peltzerController.transform.position, paletteController.transform.position) <
        MAX_DISTANCE_FOR_SWAP;
    }

    /// <summary>
    /// Returns whether or not both controllers have recently bumped.
    /// </summary>
    private bool DidControllersBump() {
      return controllers[0].BumpedRecently() && controllers[1].BumpedRecently();
    }

    /// <summary>
    /// Swaps the controllers if both are available, else queues the swap until both are available.
    /// </summary>
    public void TrySwappingControllers() {
      waitingToSwapControllers = true;

      if (!PeltzerController.AcquireIfNecessary(ref PeltzerMain.Instance.peltzerController)
        || !PaletteController.AcquireIfNecessary(ref PeltzerMain.Instance.paletteController))
        return;

      bool userIsNowRightHanded = !PeltzerMain.Instance.peltzerControllerInRightHand;
      PeltzerMain.Instance.peltzerControllerInRightHand = userIsNowRightHanded;

      // Record the user's preference for future sessions if the user is using Rift.
      if (Config.Instance.VrHardware == VrHardware.Rift) {
        PlayerPrefs.SetString(PeltzerMain.LEFT_HANDED_KEY, userIsNowRightHanded ? "false" : "true");

        // Update the menu text & icon.
        ObjectFinder.ObjectById("ID_user_is_right_handed").SetActive(userIsNowRightHanded);
        ObjectFinder.ObjectById("ID_user_is_left_handed").SetActive(!userIsNowRightHanded);
      }

      // Disable any active tooltips so they will not get stuck on the wrong controller.
      PeltzerMain.Instance.peltzerController.HideTooltips();
      PeltzerMain.Instance.paletteController.HideTooltips();

      // Set the application button and secondary button to be inactive on both controllers by default.
      // If a tool wants it active it will set it itself.
      PeltzerMain.Instance.peltzerController.SetApplicationButtonOverlay(ButtonMode.INACTIVE);
      PeltzerMain.Instance.peltzerController.SetSecondaryButtonOverlay(/*active*/ false);
      PeltzerMain.Instance.paletteController.SetApplicationButtonOverlay(/*active*/ false);
      PeltzerMain.Instance.paletteController.SetSecondaryButtonOverlay(/*active*/ false);

      // Flip the Grab tool so it matches the current dominant hand.
      GameObject grabTool = ObjectFinder.ObjectById("ID_ToolGrab");
      float grabToolScaleX = grabTool.transform.localScale.x;
      grabTool.transform.localScale = new Vector3(
        grabToolScaleX * -1,
        grabTool.transform.localScale.y,
        grabTool.transform.localScale.z);

      if (Config.Instance.sdkMode == SdkMode.SteamVR) {
#if STEAMVRBUILD
        SteamVR_TrackedObject peltzerTrackedObj = peltzerController.GetComponent<SteamVR_TrackedObject>();
        SteamVR_TrackedObject paletteTrackedObj = paletteController.GetComponent<SteamVR_TrackedObject>();
        SteamVR_TrackedObject.EIndex tmp = peltzerTrackedObj.index;
        peltzerTrackedObj.index = paletteTrackedObj.index;
        paletteTrackedObj.index = tmp;
#endif
      } else if (Config.Instance.sdkMode == SdkMode.Oculus) {
        ControllerDeviceOculus peltzerControllerDeviceOculus = (ControllerDeviceOculus)peltzerController.controller;
        ControllerDeviceOculus paletteControllerDeviceOculus = (ControllerDeviceOculus)paletteController.controller;
        if (peltzerControllerDeviceOculus.controllerType == OVRInput.Controller.LTouch) {
          peltzerControllerDeviceOculus.controllerType = OVRInput.Controller.RTouch;
          paletteControllerDeviceOculus.controllerType = OVRInput.Controller.LTouch;
        } else { 
          peltzerControllerDeviceOculus.controllerType = OVRInput.Controller.LTouch;
          paletteControllerDeviceOculus.controllerType = OVRInput.Controller.RTouch;
        }
       
        Transform temp = Config.Instance.oculusHandTrackingManager.leftTransform;
        Config.Instance.oculusHandTrackingManager.leftTransform = Config.Instance.oculusHandTrackingManager.rightTransform;
        Config.Instance.oculusHandTrackingManager.rightTransform = temp;
      }

      // For the Rift, we need to swap-back the controller geometry, such that the physical appearance of the 
      // controllers doesn't change.
      if (Config.Instance.VrHardware == VrHardware.Rift) {
        if (Config.Instance.sdkMode == SdkMode.SteamVR) {
          paletteController.controllerGeometry.gameObject.transform.SetParent(peltzerController.steamRiftHolder.transform, /* worldPositionStays */ false);
          peltzerController.controllerGeometry.gameObject.transform.SetParent(paletteController.steamRiftHolder.transform, /* worldPositionStays */ false);
        } else {
          paletteController.controllerGeometry.gameObject.transform.SetParent(peltzerController.oculusRiftHolder.transform, /* worldPositionStays */ false);
          peltzerController.controllerGeometry.gameObject.transform.SetParent(paletteController.oculusRiftHolder.transform, /* worldPositionStays */ false);
        }

        ControllerGeometry oldPaletteControllerGeometry = paletteController.controllerGeometry;
        paletteController.controllerGeometry = peltzerController.controllerGeometry;
        peltzerController.controllerGeometry = oldPaletteControllerGeometry;

        paletteController.controllerGeometry.baseControllerAnimation.SetControllerDevice(paletteController.controller);
        peltzerController.controllerGeometry.baseControllerAnimation.SetControllerDevice(peltzerController.controller);

        peltzerController.ResetTouchpadOverlay();
        paletteController.ResetTouchpadOverlay();

        peltzerController.BringAttachedToolheadToController();

        PeltzerMain.Instance.ResolveControllerHandedness();
      }
      waitingToSwapControllers = false;
    }
  }
}
