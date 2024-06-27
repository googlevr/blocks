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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.zandria;
using System.Collections.Generic;

namespace com.google.apps.peltzer.client.model.controller {

  /// <summary>
  ///   SelectableMenuItem that can be attached to a palette to change the current mode.
  /// </summary>
  public class SelectZandriaCreationMenuItem : SelectableMenuItem {
    public List<MMesh> meshes;

    public override void ApplyMenuOptions(PeltzerMain main) {
      // Uncomment to re-enable 'quick grab' if desired.
      PeltzerMain.Instance.GetPolyMenuMain().InvokeDetailsMenuAction(menu.PolyMenuMain.DetailsMenuAction.IMPORT);
    }
  }
}
