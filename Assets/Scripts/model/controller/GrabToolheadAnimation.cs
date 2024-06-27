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
    ///   Animates the GrabToolhead in response to the controller's trigger value. 
    /// </summary>
    public class GrabToolheadAnimation : MonoBehaviour
    {

        PeltzerMain peltzerMain;
        private Animator animator;
        bool animationActive = false;

        void Start()
        {
            animator = GetComponentInChildren<Animator>();
            peltzerMain = FindObjectOfType<PeltzerMain>();
            animator.speed = 0;
        }

        /// <summary>
        ///   Scrubs through grabbing animation based on controller's trigger value.
        /// </summary>
        private void Update()
        {
            if (animationActive)
            {
                animator.Play("grab", -1,
                  peltzerMain.peltzerController.controller.GetTriggerScale().x * .4f + .1f);
            }
        }

        /// <summary>
        ///   Activate the animation logic by setting flag.
        /// </summary>
        public void Activate()
        {
            animationActive = true;
        }

        /// <summary>
        ///   Deactivates the animation logic by setting flag.
        /// </summary>
        public void Deactivate()
        {
            animationActive = false;
        }
    }
}