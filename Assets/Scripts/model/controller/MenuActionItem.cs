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

namespace com.google.apps.peltzer.client.model.controller {

  /// <summary>
  ///   SelectableMenuItem that can be attached to items on the palette file menu.
  /// </summary>
  public class MenuActionItem : PolyMenuButton {
    public MenuAction action;
    /// <summary>
    /// Returns whether or not action is currently allowed by the restriction manager.
    /// </summary>
    /// <returns>Whether or not action is allowed.</returns>
    internal bool ActionIsAllowed() {
      return (PeltzerMain.Instance.restrictionManager.tutorialMenuActionsAllowed
        && PeltzerMain.TUTORIAL_MENU_ACTIONS.Contains(action))
        || PeltzerMain.Instance.restrictionManager.menuActionsAllowed;
    }

    public override void ApplyMenuOptions(PeltzerMain main) {
      if (!ActionIsAllowed()) return;

      main.InvokeMenuAction(action);
      main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
      StartBump();
    }
  }
}