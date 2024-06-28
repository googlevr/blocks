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
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   Animates the ModifyToolhead in response to our event system. 
    /// </summary>
    public class ModifyToolheadAnimation : MonoBehaviour
    {

        public PeltzerMain peltzerMain;
        public ControllerMain controllerMain;
        public PeltzerController peltzerController;
        private Transform leftArm, rightArm;
        private Vector3 leftArmOpen = new Vector3(0f, -8.025001f, 0f);
        private Vector3 rightArmOpen = new Vector3(0f, 8.025001f, 0f);

        // Use this for initialization
        void Start()
        {
            leftArm = transform.Find("modifyTool_GEO/Plier_Geo/Plier_L_Geo");
            rightArm = transform.Find("modifyTool_GEO/Plier_Geo/Plier_R_Geo");
            peltzerMain = FindObjectOfType<PeltzerMain>();
            controllerMain = peltzerMain.controllerMain;
        }

        /// <summary>
        ///   An event handler that listens for controller input and delegates accordingly.
        /// </summary>
        /// <param name="sender">The sender of the controller event.</param>
        /// <param name="args">The controller event arguments.</param>
        private void ControllerEventHandler(object sender, ControllerEventArgs args)
        {
            if (args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger)
            {
                if (args.Action == ButtonAction.DOWN)
                {
                    StartAnimation();
                }
                else if (args.Action == ButtonAction.UP)
                {
                    StopAnimation();
                }
            }
        }

        /// <summary>
        ///   Activates the animation logic by attaching the event handler for input.
        /// </summary>
        public void Activate()
        {
            controllerMain.ControllerActionHandler += ControllerEventHandler;
        }

        /// <summary>
        ///   Deactivates the animation logic by removing the event handler for input.
        /// </summary>
        public void Deactivate()
        {
            controllerMain.ControllerActionHandler -= ControllerEventHandler;
        }

        /// <summary>
        ///   Entry point for actual animation which is to "close" the head grips of the tool.
        /// </summary>
        private void StartAnimation()
        {
            if (leftArm)
            {
                leftArm.localEulerAngles = rightArm.localEulerAngles = Vector3.zero;
            }
        }

        /// <summary>
        ///   Entry point for the animation which is to "open" / "relax" the head grips of the tool.
        /// </summary>
        private void StopAnimation()
        {
            if (leftArm)
            {
                leftArm.localEulerAngles = leftArmOpen;
            }
            if (rightArm)
            {
                rightArm.localEulerAngles = rightArmOpen;
            }
        }
    }
}