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

namespace com.google.apps.peltzer.client.model.controller
{

    /// <summary>
    ///   SelectableMenuItem that can be attached to items on the palette menu that are empty space.
    ///   This will allow us to hide the toolhead when hovering over nonbutton portions of the menu.
    /// </summary>
    public class EmptyMenuItem : SelectableMenuItem
    {
        public override void ApplyMenuOptions(PeltzerMain main)
        {

        }
    }
}