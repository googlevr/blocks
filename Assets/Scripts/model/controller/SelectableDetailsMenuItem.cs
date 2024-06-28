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
using com.google.apps.peltzer.client.zandria;

namespace com.google.apps.peltzer.client.model.controller
{

    /// <summary>
    ///   SelectableDetailsMenuItem that can be attached to a creation on the PolyMenu and used to open and populate the
    ///   details section.
    /// </summary>
    public class SelectableDetailsMenuItem : SelectableMenuItem
    {
        public Creation creation;

        public override void ApplyMenuOptions(PeltzerMain main)
        {
            PeltzerMain.Instance.GetPolyMenuMain().OpenDetailsSection(creation);
            main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
        }
    }
}