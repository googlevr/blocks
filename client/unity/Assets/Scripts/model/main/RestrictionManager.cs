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

using com.google.apps.peltzer.client.model.controller;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.main {
  /// <summary>
  /// Manages the restrictions to use the app.
  /// Restrictions are set when the app is running in a special mode, such as the tutorial mode.
  /// </summary>
  public class RestrictionManager {
    /// <summary>
    /// Indicates whether or not volume insertion is allowed.
    /// </summary>
    public bool volumeInsertionAllowed { get; set; }

    /// <summary>
    /// Indicates whether or not filling during volume insertion is allowed.
    /// </summary>
    public bool volumeFillingAllowed { get; set; }

    /// <summary>
    /// Indicates whether or not the user is allowed use the shapes menu (for primitive selection) in
    /// the volume inserter.
    /// </summary>
    public bool shapesMenuAllowed { get; set; }

    /// <summary>
    /// Indicates whether or not the user is allowed to scale the primitive up/down in the volume inserter.
    /// </summary>
    public bool scaleOnVolumeInsertionAllowed { get; set; }

    /// <summary>
    /// Indicates whether the user is allowed to use the palette.
    /// </summary>
    public bool paletteAllowed { get; set; }

    /// <summary>
    /// Indicates whether the menu actions (grid mode, save, new, etc) are allowed.
    /// </summary>
    public bool menuActionsAllowed { get; set; }

    /// <summary>
    /// Indicates whether the menu actions related to tutorial are allowed.
    /// </summary>
    public bool tutorialMenuActionsAllowed { get; set; }

    /// <summary>
    /// Indicates whether switching between the Poly menu and tools menu is allowed.
    /// </summary>
    public bool menuSwitchAllowed { get; set; }

    /// <summary>
    /// Indicates whether undo/redo are allowed.
    /// </summary>
    public bool undoRedoAllowed { get; set; }

    /// <summary>
    /// Indicates whether or not manipulating the model-world transform is allowed (zooming/panning/rotating).
    /// </summary>
    public bool changeWorldTransformAllowed { get; set; }

    /// <summary>
    /// Indicates whether tooltips are supposed to appear or not.
    /// </summary>
    private bool _tooltipsAllowed;
    public bool tooltipsAllowed {
      get { return _tooltipsAllowed; }
      set {
        _tooltipsAllowed = value;
        if (!_tooltipsAllowed) {
          PeltzerMain.Instance.paletteController.HideTooltips();
          PeltzerMain.Instance.peltzerController.HideTooltips();
        }
      }
    }

    /// <summary>
    /// Indicates whether reference images should be shown.
    /// </summary>
    public bool insertingReferenceImagesAllowed { get; set; }

    /// <summary>
    /// Indicates whether changing colours via the colour palette is allowed.
    /// </summary>
    public bool changingColorsAllowed { get; set; }

    /// <summary>
    /// Indicates whether throwing objects away to delete them is allowed.
    /// </summary>
    public bool throwAwayAllowed { get; set; }

    /// <summary>
    /// Whether the touchpad can be modified to highligh and glow. When this is true we need to hide the standard
    /// touchpad object and replace it with a quad segmented one so we can isolate the highlight.
    /// </summary>
    public bool touchpadHighlightingAllowed { get; set; }

    /// <summary>
    /// Whether toolheads should change color when a new color is selected.
    /// </summary>
    public bool toolheadColorChangeAllowed { get; set; }

    /// <summary>
    /// Whether the user is allowed to move meshes.
    /// </summary>
    public bool movingMeshesAllowed { get; set; }

    /// <summary>
    /// Whether the world bounding box should be shown.
    /// </summary>
    public bool showingWorldBoundingBoxAllowed { get; set; }

    /// </summary>
    /// Whether the user is allowed to snap.
    /// </summary>
    public bool snappingAllowed { get; set; }

    /// <summary>
    /// Whether the user is allowed to copy.
    /// </summary>
    public bool copyingAllowed { get; set; }

    /// <summary>
    /// Whether the user is given a bigger selection radius.
    /// </summary>
    public bool increasedMultiSelectRadiusAllowed { get; set; }

    /// <summary>
    /// Whether the user is allowed to deselect.
    /// </summary>
    public bool deselectAllowed { get; set; }

    public bool touchpadUpAllowed { get; set; }
    public bool touchpadDownAllowed { get; set; }
    public bool touchpadRightAllowed { get; set; }
    public bool touchpadLeftAllowed { get; set; }
    /// <summary>
    /// Whether controllerMain will send out controller events.
    /// </summary>
    public bool controllerEventsAllowed { get; set; }


    /// <summary>
    /// Indicates whether the user is allowed to use each controller mode or not.
    /// Using an array instead of a dictionary for faster lookups.
    /// </summary>
    private bool[] controllerModeAllowed = new bool[Enum.GetValues(typeof(ControllerMode)).Length];

    /// <summary>
    /// In the tutorial, we only wish one specific mesh ID to be selectable.
    /// </summary>
    public int? onlySelectableMeshIdForTutorial;

    /// <summary>
    /// In the tutorial, we only allow the user to select one color.
    /// </summary>
    private int onlyAllowedMaterialId;

    /// <summary>
    /// Creates a new RestrictionManager. By default no restrictions are in place. The restriction manager sets bool
    /// restrictions that are enforced throughout all of the scripts and tools in the project.
    /// </summary>
    public RestrictionManager() {
      // Everything is allowed by default.
      AllowAll();
    }

    /// <summary>
    /// Returns whether or not the given controller mode is currently allowed.
    /// </summary>
    /// <param name="mode">The controller mode</param>
    /// <returns>True if allowed (user can use it), false if not allowed (use can't use it).</returns>
    public bool IsControllerModeAllowed(ControllerMode mode) {
      return controllerModeAllowed[(int)mode];
    }

    /// <summary>
    /// Returns whether a materialId is allowed to be selected.
    /// </summary>
    /// <param name="materialId"></param>
    /// <returns></returns>
    public bool IsColorAllowed(int materialId) {
      // -2 means no colors allowed at all.
      if (onlyAllowedMaterialId == -2) {
        return false;
      }

      // Return true if the material is the allowed material or if -1 which means all materials are allowed.
      return (materialId == onlyAllowedMaterialId || onlyAllowedMaterialId == -1);
    }

    public void SetOnlyAllowedColor(int materialId) {
      if (onlyAllowedMaterialId == materialId) return;
      onlyAllowedMaterialId = materialId;
    }

    /// <summary>
    /// Sets the allowed controller modes. All other modes will be disallowed.
    /// </summary>
    /// <param name="modes">The allowed modes. Can be null to mean no modes are allowed.</param>
    public void SetAllowedControllerModes(IEnumerable<ControllerMode> modes) {
      for (int i = 0; i < controllerModeAllowed.Length; i++) {
        controllerModeAllowed[i] = false;
      }
      if (modes == null) {
        return;
      }
      foreach (ControllerMode mode in modes) {
        controllerModeAllowed[(int)mode] = true;
      }
    }

    /// <summary>
    /// (Syntactic sugar) Allows only one controller mode.
    /// </summary>
    /// <param name="mode">The only mode to allow.</param>
    public void SetOnlyAllowedControllerMode(ControllerMode mode) {
      for (int i = 0; i < controllerModeAllowed.Length; i++) {
        controllerModeAllowed[i] = ((int)mode == i);
      }
    }

    public void SetTouchpadHighlightingAllowed(bool isAllowed) {
      // The segmented touchpad may not be available for the Rift, which uses a thumbstick.
      if (PeltzerMain.Instance.peltzerController.controllerGeometry.segmentedTouchpad == null) return;

      PeltzerMain.Instance.peltzerController.controllerGeometry.touchpad.SetActive(!isAllowed);
      PeltzerMain.Instance.paletteController.controllerGeometry.touchpad.SetActive(!isAllowed);
      PeltzerMain.Instance.peltzerController.controllerGeometry.segmentedTouchpad.SetActive(isAllowed);
      PeltzerMain.Instance.paletteController.controllerGeometry.segmentedTouchpad.SetActive(isAllowed);

      touchpadHighlightingAllowed = isAllowed;
    }

    public void SetTouchpadAllowed (TouchpadLocation touchPad) {
      touchpadUpAllowed = touchPad == TouchpadLocation.TOP;
      touchpadDownAllowed = touchPad == TouchpadLocation.BOTTOM;
      touchpadRightAllowed = touchPad == TouchpadLocation.RIGHT;
      touchpadLeftAllowed = touchPad == TouchpadLocation.LEFT;
    }

    /// <summary>
    /// Unrestricts everything, returning to the default unrestricted mode.
    /// </summary>
    public void AllowAll() {
      Reset(/* allowAll */ true);
    }

    /// <summary>
    /// Restricts everything.
    /// </summary>
    public void ForbidAll() {
      Reset(/* allowAll */ false);
    }

    /// <summary>
    /// Resets all restrictions to either the allowed state or the restricted state.
    /// </summary>
    private void Reset(bool allow) {
      for (int i = 0; i < controllerModeAllowed.Length; i++) {
        controllerModeAllowed[i] = allow;
      }
      volumeInsertionAllowed = allow;
      volumeFillingAllowed = allow;
      shapesMenuAllowed = allow;
      scaleOnVolumeInsertionAllowed = allow;
      paletteAllowed = allow;
      menuActionsAllowed = allow;
      tooltipsAllowed = allow;
      undoRedoAllowed = allow;
      changeWorldTransformAllowed = allow;
      insertingReferenceImagesAllowed = allow;
      changingColorsAllowed = allow;
      touchpadUpAllowed = allow;
      touchpadDownAllowed = allow;
      touchpadRightAllowed = allow;
      touchpadLeftAllowed = allow;
      controllerEventsAllowed = allow;
      toolheadColorChangeAllowed = allow;
      showingWorldBoundingBoxAllowed = allow;
      snappingAllowed = allow;
      movingMeshesAllowed = allow;
      copyingAllowed = allow;
      tutorialMenuActionsAllowed = allow;
      deselectAllowed = allow;
      // Allowing a larger selection radius is the non-standard state.
      increasedMultiSelectRadiusAllowed = !allow;

      // -1 means all are allowed, -2 mean none.
      if (allow) {
        SetOnlyAllowedColor(-1);
      } else {
        SetOnlyAllowedColor(-2);
      }

      // Highlighting is the non-standard state.
      SetTouchpadHighlightingAllowed(!allow);

      throwAwayAllowed = allow;
      menuSwitchAllowed = allow;
      if (allow) {
        onlySelectableMeshIdForTutorial = null;
      }
    }
  }
}
