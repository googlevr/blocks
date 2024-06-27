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
using System;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.controller;

namespace com.google.apps.peltzer.client.menu {
  /// <summary>
  /// Represents one of the items in the startup menu.
  /// </summary>
  public class StartupMenuItem : SelectableMenuItem {
    // Set from Unity. Can be null.
    public GameObject normalObject;
    // Set from Unity. Can be null.
    public GameObject hoverObject;


    private Collider ourCollider;
    private Material ourMaterial;
    public bool hovering { get; private set; }
    public bool pointing { get; set; }

    private void Start() {
      hovering = false;
      pointing = false;
      ourCollider = gameObject.GetComponent<Collider>();
      AssertOrThrow.NotNull(ourCollider, "StartupMenuItem needs a Collider.");
     
      if (normalObject != null) {
        normalObject.SetActive(true);
      }
      if (hoverObject != null) {
        hoverObject.SetActive(false);
      }
      
    }

    private void Update() {
      if (!PeltzerMain.Instance.peltzerController) {
        // PeltzerMain hasn't initialized the controller yet, so don't do anything for now.
        return;
      }

      if (hovering !=
        (ourCollider.bounds.Contains(PeltzerMain.Instance.peltzerController.transform.position) || pointing)) {
        hovering = !hovering;
        if (hovering) {
          PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();
        }
        if (hoverObject != null) {
          hoverObject.SetActive(hovering);
        }
        if (normalObject != null) {
          normalObject.SetActive(!hovering);
        }
      }
    }
  }
}
