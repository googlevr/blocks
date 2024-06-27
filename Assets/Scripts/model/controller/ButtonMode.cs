﻿// Copyright 2020 The Blocks Authors
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

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   The current mode of a button.
    ///   IMPORTANT: enum values are used as indices, so the values must be numbered sequentially from 0
    ///   (default enum value assignment).
    /// </summary>
    public enum ButtonMode
    {
        /// <summary>
        ///   Default
        /// </summary>
        NONE,
        /// <summary>
        ///  The button is active and clickable.
        /// </summary>
        ACTIVE,
        /// <summary>
        ///  The button is inactive and not clickable.
        /// </summary>
        INACTIVE,
        /// <summary>
        ///  The button is active but not yet clickable.
        /// </summary>
        WAITING
    }
}
