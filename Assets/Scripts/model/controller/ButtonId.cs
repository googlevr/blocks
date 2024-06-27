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

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   Descriptor for a button action.
    /// </summary>
    public enum ButtonId
    {
        /// <summary>
        ///   For now, these are duplicates of Valve.VR.EVRButtonId.
        /// </summary>
        //k_EButton_System = 0,            // Unused.
        ApplicationMenu = 1,
        Grip = 2,
        //k_EButton_DPad_Left = 3,         // Unused.
        //k_EButton_DPad_Up = 4,           // Unused.
        //k_EButton_DPad_Right = 5,        // Unused.
        //k_EButton_DPad_Down = 6,         // Unused.
        SecondaryButton = 7,               // The second button on the Oculus Touch controller (B/Y)
                                           //k_EButton_ProximitySensor = 31,  // Unused.
                                           //k_EButton_Axis0 = 32,            // Unused.
                                           //k_EButton_Axis1 = 33,            // Unused.
                                           //k_EButton_Axis2 = 34,            // Unused.
                                           //k_EButton_Axis3 = 35,            // Unused.
                                           //k_EButton_Axis4 = 36,            // Unused.
        Touchpad = 32,
        Trigger = 33,
        //k_EButton_Dashboard_Back = 2,    // Unused.
        //k_EButton_Max = 64,              // Unused.
    }
}
