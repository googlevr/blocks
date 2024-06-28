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

using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.controller
{
    public class OculusHandTrackingManager : MonoBehaviour
    {
        // These are set to null to remove warning log.
        public Transform leftTransform = null;
        public Transform rightTransform = null;

        // This class tries to mimic the tracking done in SteamVR's SteamVR_TrackedObject, which updates
        // poses in response to the event "new_poses". This event is sent in
        // SteamVR_UpdatePoses.OnPreCull(). But OnPreCull() is only available to components attached to
        // the camera, which this class is not. So this public OnPreCull() is called exactly once
        // from the OculusVideoRendering.OnPreCull() which is attached to the camera.
        void Update()
        {
            // Adding in additional checks to make sure for each controller instead of a single check for
            // both so the player will know if either controller is having problems.
            bool touchControllersConnected =
                (OVRInput.GetConnectedControllers() & OVRInput.Controller.Touch)
                == OVRInput.Controller.Touch;
            bool leftTouchValid = false;
            if (OVRInput.GetControllerOrientationTracked(OVRInput.Controller.LTouch) ||
                OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch) &&
                touchControllersConnected)
            {
                leftTouchValid = true;
            }

            bool rightTouchValid = false;
            if (OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch) ||
                OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch) &&
                touchControllersConnected)
            {
                rightTouchValid = true;
            }

            PeltzerMain.Instance.paletteController.controller.IsTrackedObjectValid = leftTouchValid;
            PeltzerMain.Instance.peltzerController.controller.IsTrackedObjectValid = rightTouchValid;

            leftTransform.localRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
            rightTransform.localRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

            leftTransform.localPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            rightTransform.localPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        }
    }
}