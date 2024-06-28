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
using UnityEngine;

namespace com.google.apps.peltzer.client.model.controller
{

    /// <summary>
    ///   EnvironmentMenuItem that can be attached to an object that will trigger an environment change.
    /// </summary>
    public class EnvironmentMenuItem : SelectableMenuItem
    {
        public EnvironmentThemeManager.EnvironmentTheme theme;
        private GameObject selectedBorder;

        public void Start()
        {
            selectedBorder = transform.Find("Selected").gameObject;
            PeltzerMain.Instance.environmentThemeManager.EnvironmentThemeActionHandler += EnvironmentThemeActionHandler;
            // We persist the last chosen environment in user prefs and set that during setup,
            // however at that point this component has not started and thusly
            // will not respond to the EnvironmentThemeActionHandler event. So we check here.
            if (PeltzerMain.Instance.environmentThemeManager.currentTheme == theme)
            {
                selectedBorder.SetActive(true);
            }
        }

        public override void ApplyMenuOptions(PeltzerMain main)
        {
            if (main.environmentThemeManager != null)
            {
                main.SetEnvironmentTheme(theme);
                main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
            }
        }

        public void EnvironmentThemeActionHandler(object sender, EnvironmentThemeManager.EnvironmentTheme selectedTheme)
        {
            selectedBorder.SetActive(selectedTheme == theme);
        }
    }
}