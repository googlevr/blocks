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
    ///   SelectableMenuItem that can be attached to a palette to change the current page of the PolyMenu.
    ///   It is expected that these are configured in the Unity Editor with local:
    ///   - y position of DEFAULT_Y_POSITION
    ///   - y scale of DEFAULT_Y_SCALE
    /// </summary>
    public class SelectablePolyMenuItem : PolyMenuButton
    {
        public int polyMenuIndex;

        public override void ApplyMenuOptions(PeltzerMain main)
        {
            main.GetPolyMenuMain().ApplyMenuChange(polyMenuIndex);
            main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);

            // Bump down slightly and back up to its position, to provide a visual indication that
            // the user's click was registered.
            StartBump();
        }
    }
}
