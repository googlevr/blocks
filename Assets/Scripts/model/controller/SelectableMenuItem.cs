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

namespace com.google.apps.peltzer.client.model.controller
{
    public class SelectableMenuItem : MonoBehaviour
    {
        // Name to display when this menu item is hovered. Set in Unity editor.
        public string hoverName;
        public bool isActive = true;

        /// <summary>
        ///   Called whenever a SelectableMenuItem is touched.
        /// </summary>
        /// <param name="main"></param>
        public virtual void ApplyMenuOptions(PeltzerMain main)
        {

        }
    }
}
