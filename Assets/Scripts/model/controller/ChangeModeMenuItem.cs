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

using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.app;

namespace com.google.apps.peltzer.client.model.controller
{

    /// <summary>
    ///   SelectableMenuItem that can be attached to a palette to change the current mode.
    /// </summary>
    public class ChangeModeMenuItem : SelectableMenuItem
    {
        // Animation constants.
        private static readonly float SCALE_CHANGE_DURATION = 0.25f;
        private static readonly float POSITION_CHANGE_DURATION = 0.2f;

        // Where the toolhead should be, relative to the controller.
        private readonly Vector3 TARGET_LOCAL_POSITION_VIVE = new Vector3(0.0004f, -0.0068f, -0.0037f);
        private readonly Vector3 TARGET_LOCAL_POSITION_STEAM_RIFT_RIGHT = new Vector3(-.00404f, -.03399f, -.03312f);
        private readonly Vector3 TARGET_LOCAL_ROTATION_STEAM_RIFT_RIGHT = new Vector3(39.194f, -4.73f, -3.063f);
        private readonly Vector3 TARGET_LOCAL_POSITION_STEAM_RIFT_LEFT = new Vector3(-.0025f, -0.0325f, -.034f);
        private readonly Vector3 TARGET_LOCAL_ROTATION_STEAM_RIFT_LEFT = new Vector3(39.259f, -6.693f, -5.973f);

        private readonly Vector3 TARGET_LOCAL_POSITION_OCULUS_RIGHT = new Vector3(.00031f, -.00835f, .0362f);
        private readonly Vector3 TARGET_LOCAL_ROTATION_OCULUS_RIGHT = new Vector3(-5.056f, .547f, 7.56f);
        private readonly Vector3 TARGET_LOCAL_POSITION_OCULUS_LEFT = new Vector3(0.0015f, -0.0099f, 0.0338f);
        private readonly Vector3 TARGET_LOCAL_ROTATION_OCULUS_LEFT = new Vector3(-5.9f, -2.18f, 5.889f);

        // The mode this item will change to.
        public ControllerMode mode;

        // Animation details.
        private Vector3? targetPosition = null;
        private Vector3? targetScale = null;
        private Vector3? targetRotation = null;
        private float timeStartedLerping;
        private float currentLerpDuration;

        public override void ApplyMenuOptions(PeltzerMain main)
        {
            // Additionally, pass a reference to the gameobject for animation.
            main.peltzerController.ChangeMode(mode, gameObject);
            main.audioLibrary.PlayClip(main.audioLibrary.selectToolSound);
        }

        private void Update()
        {
            if (targetScale.HasValue)
            {
                // Calculate progress through the animation. Values >1 may be meaningless, so this should be checked
                // for below rather than blindly passed through.
                float pctDone = (Time.time - timeStartedLerping) / SCALE_CHANGE_DURATION;

                if (pctDone >= 1)
                {
                    // If we're done, immediately set the scale.
                    transform.localScale = targetScale.Value;
                    targetScale = null;
                }
                else
                {
                    // If we're not done, lerp towards the target scale.
                    transform.localScale =
                      Vector3.Lerp(gameObject.transform.localScale, targetScale.Value, pctDone);
                }
            }

            if (targetPosition.HasValue)
            {
                // Calculate progress through the animation. Values >1 may be meaningless, so this should be checked
                // for below rather than blindly passed through.
                float pctDone = (Time.time - timeStartedLerping) / POSITION_CHANGE_DURATION;

                if (pctDone >= 1)
                {
                    // If we're done, immediately set the final position and activate any animations.
                    transform.localPosition = targetPosition.Value;
                    targetPosition = null;
                    ToolHeadAnimationEnd();
                }
                else
                {
                    // If we're not done, lerp towards the target position.
                    transform.localPosition =
                      Vector3.Lerp(gameObject.transform.localPosition, targetPosition.Value, pctDone);
                }
            }

            if (targetRotation.HasValue)
            {
                // Calculate progress through the animation. Values >1 may be meaningless, so this should be checked
                // for below rather than blindly passed through.
                float pctDone = (Time.time - timeStartedLerping) / POSITION_CHANGE_DURATION;

                if (pctDone >= 1)
                {
                    // If we're done, immediately set the final rotation and activate any animations.
                    transform.localRotation = Quaternion.Euler(targetRotation.Value);
                    targetRotation = null;
                }
                else
                {
                    // If we're not done, lerp towards the target rotation.
                    transform.localRotation =
                      Quaternion.Lerp(gameObject.transform.localRotation, Quaternion.Euler(targetRotation.Value), pctDone);
                }
            }
        }

        /// <summary>
        ///   Moves this toolhead smoothly from the palette to the controller.
        /// </summary>
        public void BringToController()
        {
            // Abandon the scale animation if we're activating a move animation.
            if (targetScale.HasValue)
            {
                transform.localScale = targetScale.Value;
                targetScale = null;
            }

            timeStartedLerping = Time.time;
            if (Config.Instance.VrHardware == VrHardware.Vive)
            {
                targetPosition = TARGET_LOCAL_POSITION_VIVE;
            }
            else if (Config.Instance.VrHardware == VrHardware.Rift)
            {
                if (Config.Instance.sdkMode == SdkMode.SteamVR)
                {
                    if (PeltzerMain.Instance.peltzerControllerInRightHand)
                    {
                        targetPosition = TARGET_LOCAL_POSITION_STEAM_RIFT_RIGHT;
                        targetRotation = TARGET_LOCAL_ROTATION_STEAM_RIFT_RIGHT;
                    }
                    else
                    {
                        targetPosition = TARGET_LOCAL_POSITION_STEAM_RIFT_LEFT;
                        targetRotation = TARGET_LOCAL_ROTATION_STEAM_RIFT_LEFT;
                    }
                }
                else
                {
                    if (PeltzerMain.Instance.peltzerControllerInRightHand)
                    {
                        targetPosition = TARGET_LOCAL_POSITION_OCULUS_RIGHT;
                        targetRotation = TARGET_LOCAL_ROTATION_OCULUS_RIGHT;
                    }
                    else
                    {
                        targetPosition = TARGET_LOCAL_POSITION_OCULUS_LEFT;
                        targetRotation = TARGET_LOCAL_ROTATION_OCULUS_LEFT;
                    }
                }
            }
        }

        /// <summary>
        ///   Scale up this toolhead to make it appear from thin air. Used when a toolhead is re-added to the palette.
        /// </summary>
        public void ScaleFromNothing(Vector3 finalScale)
        {
            transform.localScale = Vector3.zero;
            targetScale = finalScale;
            timeStartedLerping = Time.time;
        }

        /// <summary>
        ///   Activates animations for the toolhead once it is attached to the controller.
        /// </summary>
        private void ToolHeadAnimationEnd()
        {
            switch (mode)
            {
                case ControllerMode.extrude:
                case ControllerMode.reshape:
                case ControllerMode.subdivideFace:
                case ControllerMode.subdivideMesh:
                    GetComponent<ModifyToolheadAnimation>().Activate();
                    break;
                case ControllerMode.move:
                    GetComponent<GrabToolheadAnimation>().Activate();
                    break;
                case ControllerMode.paintFace:
                case ControllerMode.paintMesh:
                    GetComponent<PaintToolheadAnimation>().Activate();
                    break;
            }
        }
    }
}
