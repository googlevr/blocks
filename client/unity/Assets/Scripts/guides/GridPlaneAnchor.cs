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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.tools.utils;
using TMPro;

/// <summary>
///   This class is responsible for managing a world space grid plane that is locked to an axis of moment.
///   A grid plane consistes of the visual grid with a child "anchor". A grid plane anchor is the interaction
///   point for the grid plane to which the anchor is nested.
/// </summary>
public class GridPlaneAnchor : MonoBehaviour {

  /// <summary>
  ///   Axis type to specify to which axis the plane belongs.
  /// </summary>
  public enum AxisType {
    X = 0,
    Y = 1,
    Z = 2
  }

  // Immutable marks the object as a source for generating new instances and are not moved themselves.
  public bool isImmutable = true;
  public AxisType axisType;
  /// <summary>
  ///   Holds a lookup of the currently cloned planes, the root GameObject of a "GridPlane" to which the anchor is parented,
  ///   which is used for de-duping along a particular axis type. This dictionary is populated for any source grid planes, i.e. a plane that spawns another plane.
  /// </summary>
  public Dictionary<float, GameObject> planeClones;

  private bool isSetup = false;
  private bool isHovered = false;
  private bool isHeld = false;
  private bool isGrabFrameUpdate = false;
  private bool isReleaseFrameUpdate = false;

  private Material planeMaterial;
  private Material anchorMaterial;
  private TextMeshPro label;
  private GridPlaneAnchor rootGridPlaneAnchor;

  void Start() {
    Setup();
  }

  private void Setup() {
    PeltzerMain.Instance.peltzerController.PeltzerControllerActionHandler += ControllerEventHandler;
    planeMaterial = transform.parent.GetComponent<Renderer>().material;
    label = transform.parent.Find("Label").gameObject.GetComponent<TextMeshPro>();
    if (isImmutable) {
      planeClones = new Dictionary<float, GameObject>();
    }
    isSetup = true;
  }

  void Update() {
    if (!isSetup) return;
    
    isHovered = IsCollidedWithSelector();
    
    if (!isHovered && isImmutable) {
      isHeld = false;
    }

    // Remove the reference.
    if (isGrabFrameUpdate && !isImmutable) {
      isGrabFrameUpdate = false;
      if (rootGridPlaneAnchor.planeClones.ContainsKey(transform.parent.localPosition[(int)axisType])) {
        rootGridPlaneAnchor.planeClones.Remove(transform.parent.localPosition[(int)axisType]);
      }
    }

    if (isHeld && !isImmutable) {
      // Set zoomer flag to show world bounds while held.
      PeltzerMain.Instance.Zoomer.isManipulatingGridPlane = true;

      // Snap grid plane to grid.
      Vector3 newLSPos = transform.parent.parent.parent
        .InverseTransformPoint(PeltzerMain.Instance.worldSpace
          .ModelToWorld(PeltzerMain.Instance.GetSelector().selectorPosition));

      switch (axisType) {
        case AxisType.X:
          newLSPos = new Vector3(newLSPos.x,
            transform.parent.transform.localPosition.y,
            transform.parent.transform.localPosition.z);
          break;
        case AxisType.Y:
          newLSPos = new Vector3(transform.parent.transform.localPosition.x,
            newLSPos.y,
            transform.parent.transform.localPosition.z);
          break;
        case AxisType.Z:
          newLSPos = new Vector3(transform.parent.transform.localPosition.x,
            transform.parent.transform.localPosition.y,
            newLSPos.z);
          break;
      }

      // Clamp precision @ .3
      newLSPos = new Vector3(Mathf.RoundToInt(newLSPos.x * 1000.000f) / 1000.000f,
        Mathf.RoundToInt(newLSPos.y * 1000.000f) / 1000.000f,
        Mathf.RoundToInt(newLSPos.z * 1000.000f) / 1000.000f);

      transform.parent.localPosition = newLSPos;

      // Display value.
      SetLabel(newLSPos[(int)axisType].ToString("F3") + " bu");
    } else if (isHovered && !isImmutable) {
      // Display value.
      SetLabel(transform.parent.localPosition[(int)axisType].ToString("F3") + " bu");
    } else {
      // Clear label.
      SetLabel("");
    }

    // Pass material to shader// Get world position of selector position.
    Vector4 selectorWorldPosition = PeltzerMain.Instance.worldSpace
      .ModelToWorld(PeltzerMain.Instance.peltzerController.LastPositionModel);
    selectorWorldPosition.w = PeltzerMain.Instance.peltzerController.isBlockMode ? 0 : 1;
    planeMaterial.SetVector("_SelectorPosition", selectorWorldPosition);

    // check if it is overlapping
    if(isReleaseFrameUpdate && !isImmutable) {
      isReleaseFrameUpdate = false;
      if (IsOverlappingPlaneOnSameAxis()) {
        DestroyGridPlane();
      } else {
        rootGridPlaneAnchor.planeClones.Add(transform.parent.localPosition[(int)axisType], transform.parent.gameObject);
      }
    }
  }

  /// <summary>
  ///   Set the label text from position information.
  /// </summary>
  /// <param name="text">The string to to display in the label.</param>
  private void SetLabel(string text) {
    label.gameObject.transform.LookAt(PeltzerMain.Instance.hmd.transform);
    label.text = text;
  }


  /// <summary>
  ///   Creates a new grid plane along a coordinate axis.
  /// </summary>
  private void SpawnNewGridPlane() {
    // Clone and set transform.
    GameObject planeClone = GameObject.Instantiate(transform.parent).gameObject;
    planeClone.transform.Find("GridAnchor").GetComponent<GridPlaneAnchor>().rootGridPlaneAnchor = this;
    GridPlaneAnchor gridPlaneAnchor = planeClone.transform.Find("GridAnchor").GetComponent<GridPlaneAnchor>();
    gridPlaneAnchor.isImmutable = false;
    gridPlaneAnchor.isHeld = true;
    gridPlaneAnchor.isHovered = true;
    planeClone.transform.parent = transform.parent.parent;
    planeClone.transform.localEulerAngles = transform.parent.transform.localEulerAngles;
    planeClone.transform.position = transform.parent.transform.position;
    planeClone.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
    gridPlaneAnchor.Setup();

    // Reset the interactions on this plane.
    if (isImmutable) {
      isHeld = false;
      isHovered = false;
    }
  }

  // Handle mode selection when appropriate.
  private void ControllerEventHandler(object sender, ControllerEventArgs args) {
    if (IsGrabbingAnchor(args)) {
      // Disable multiselection.
      PeltzerMain.Instance.GetSelector().EndMultiSelection();
      
      // As we use the eraser on spawned clones to provide a "delete" method, we exclude
      // any delete tool (eraser) from spawning to provide clarity.
      if (isImmutable && PeltzerMain.Instance.peltzerController.mode != ControllerMode.delete) {
        SpawnNewGridPlane();
      } else if (!isImmutable) {
        isHeld = true;
        // if tool is eraser, delete this plane via parent.
        if (PeltzerMain.Instance.peltzerController.mode == ControllerMode.delete) {
          DestroyGridPlane();
          return;
        }

        // Set grab check semaphore for next update.
        // This semaphore is used to check for duplicate planes at same level.
        isGrabFrameUpdate = true;
      }
    } else if(IsReleasingAnchor(args)) {
      isHeld = false;
      PeltzerMain.Instance.Zoomer.isManipulatingGridPlane = false;

      // If plane is set outside of world space, destroy it.
      if(!isImmutable &&
        (Mathf.Abs(transform.parent.localPosition[(int)axisType]) >= 0.5f || IsOverlappingPlaneOnSameAxis())) {
        DestroyGridPlane();
        return;
      }

      // Set release check semaphore for next update.
      // This semaphore is used to check for duplicate planes at same level.
      isReleaseFrameUpdate = true;
    }
  }

  /// <summary>
  ///   Destroys this instance of a grid plane.
  /// </summary>
  private void DestroyGridPlane() {
    isHeld = false;
    isHovered = false;
    PeltzerMain.Instance.peltzerController.PeltzerControllerActionHandler -= ControllerEventHandler;
    DestroyImmediate(transform.parent.gameObject);
  }

  /// <summary>
  ///   Determines if a planes position is currently the same as another of the same axis alignment.
  /// </summary>
  private bool IsOverlappingPlaneOnSameAxis() {
    if(rootGridPlaneAnchor.planeClones.ContainsKey(transform.parent.localPosition[(int)axisType])) {
      return true;
    }
    return false;
  }

  /// <summary>
  ///   Determines if the selector is colliding with this transform.
  /// </summary>
  private bool IsCollidedWithSelector() {
    float dist = Vector3.Distance(PeltzerMain.Instance.worldSpace
        .ModelToWorld(PeltzerMain.Instance.GetSelector().selectorPosition), transform.position);
    return dist <= transform.localScale.x/2f * PeltzerMain.Instance.worldSpace.scale;
  }

  /// <summary>
  ///   Determines if this anchor is being grabbed.
  /// </summary>
  /// <param name="args">Controller event information</param>
  private bool IsGrabbingAnchor(ControllerEventArgs args) {
    return isHovered 
      && args.ControllerType == ControllerType.PELTZER
      && args.ButtonId == ButtonId.Trigger
      && args.Action == ButtonAction.DOWN;
  }

  /// <summary>
  ///   Determines if the anchor is being released.
  /// </summary>
  /// <param name="args">Controller event informatin</param>
  private bool IsReleasingAnchor(ControllerEventArgs args) {
    return isHeld 
      && args.ControllerType == ControllerType.PELTZER
      && args.ButtonId == ButtonId.Trigger
      && args.Action == ButtonAction.UP;
  }
}