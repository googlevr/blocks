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

using System;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using System.Collections.Generic;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  /// Subclass for a toggleable menu item that enables or disabled a feature.  Different feature changes can be
  /// specified for turning on and off the feature.  Feature changes are specified as a comma delimited string, where
  /// each entry is the string form of the feature name prepended by either a '+' or a '-' which controls whether the
  /// feature is enabled or disabled.  For example a featureStringOn of "+featureA,-featureB,+featureC" will cause
  /// featureA and featureC to be enabled when this is toggled on, and featureB to be disabled.
  /// </summary>
  public class ToggleMenuItem : MenuActionItem {
    // Whether this is initially enabled or not - if it is, featureStringOn should match the starting state of the 
    // features it modifies.
    public bool enabled;
    // The set of feature changes to apply when this is toggled on, formatted as a comma delimited list of features,
    // each prepended by either a '+' or a '-' to denote that it is being turned on or off.
    public String featureStringOn;
    // The set of feature changes to apply when this is toggled off, formatted as a comma delimited list of features,
    // each prepended by either a '+' or a '-' to denote that it is being turned on or off.
    public String featureStringOff;
    // The GameObject that displays text when this is enabled.
    public GameObject enabledText;
    // The GameObject that displays text when this is disabled.
    public GameObject disabledText;
    // Other toggle menu items that become enabled when this toggle menu is enabled.
    public List<ToggleMenuItem> enableDepedentToggles;
    // Other toggle menu items that become disabled when this toggle menu is disabled.
    public List<ToggleMenuItem> disableDependentToggles;

    public void Start() {
        enabledText.SetActive(enabled);
        disabledText.SetActive(!enabled);
      base.Start();
    }
    
    public override void ApplyMenuOptions(PeltzerMain main) {
      if (!ActionIsAllowed()) return;
      SetEnabled(!enabled);
      if (enabled) {
        Features.ToggleFeatureString(featureStringOn);
        foreach (ToggleMenuItem enableDependentToggle in enableDepedentToggles) {
          enableDependentToggle.SetEnabled(true);
        }
      } else {
        Features.ToggleFeatureString(featureStringOff);
        foreach (ToggleMenuItem disableDependentToggle in disableDependentToggles) {
          disableDependentToggle.SetEnabled(false);
        }
      }
      main.InvokeMenuAction(action);
      main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
      StartBump();
    }

    public void SetEnabled(bool newState) {
      enabled = newState;
      enabledText.SetActive(enabled);
      disabledText.SetActive(!enabled);
    }
  }
}
