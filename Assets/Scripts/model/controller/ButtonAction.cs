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
    public enum ButtonAction
    {
        /// <summary>
        ///   Null state.
        /// </summary>
        NONE,

        /// <summary>
        ///   Button was pressed.
        /// </summary>
        DOWN,

        /// <summary>
        ///   Button was unpressed.
        /// </summary>
        UP,

        /// <summary>
        ///   Touchpad is grazed.
        /// </summary>
        TOUCHPAD,

        /// <summary>
        ///   Primarily in service of the trigger button so that
        ///   an abstraction can be made to indicate a "light" trigger down.
        /// </summary>
        LIGHT_DOWN,

        /// <summary>
        ///   Primarily in service of the trigger button so that
        ///   an abstraction can be made  to indicate a "light" trigger up.
        /// </summary>
        LIGHT_UP
    }
}
