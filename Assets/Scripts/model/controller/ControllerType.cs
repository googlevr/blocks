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

using UnityEngine;
using System.Collections;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   Descriptor for types of controllers.
    /// </summary>
    public enum ControllerType
    {
        /// <summary>
        ///   Null state.
        /// </summary>
        NONE,

        /// <summary>
        ///   The main peltzer controller.
        /// </summary>
        PELTZER,

        /// <summary>
        ///   The palette controller.
        /// </summary>
        PALETTE
    }
}