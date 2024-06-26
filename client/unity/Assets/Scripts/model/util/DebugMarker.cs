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
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.util {
  /// <summary>
  /// Debug utility to place a visible marker to indicate a position in model or world space.
  ///
  /// HOW TO USE:
  ///
  /// DebugMarker marker;
  /// private void Update() {
  ///   // Show a position in model space:
  ///   Vector3 buggyPosition = ...some buggy code generating it...;
  ///
  ///   if (!marker) marker = DebugMarker.Create();
  ///   marker.UpdateInModelSpace(buggyPosition);
  ///
  ///   // RESULT: you will see a little cube marker at the given position in model space.
  ///   // It will follow model space, so the marker will correctly reposition in world space as you rotate/scale
  ///   // the world.
  /// }
  /// </summary>
  public class DebugMarker : MonoBehaviour {
    private const float FORWARD_GIZMO_AXIS_LENGTH = 5.0f;
    private const float RIGHT_GIZMO_AXIS_LENGTH = 2.0f;
    private const float UP_GIZMO_AXIS_LENGTH = 2.0f;
    private const float GIZMO_AXIS_THICKNESS = 0.1f;
    private const float DEFAULT_MARKER_SIZE = 0.02f;  // 2 cm by default.

    private bool isInModelSpace;
    private Vector3 positionModelSpace = Vector3.zero;
    private Quaternion rotationModelSpace = Quaternion.identity;
    private Vector3 scaleModelSpace = Vector3.one * DEFAULT_MARKER_SIZE;

    /// <summary>
    /// Creates a debug marker of the given color.
    /// </summary>
    /// <param name="name">Name of the marker to show in the Unity hierarchy.</param>
    /// <param name="color">Color of the marker (defaults to green if omitted).</param>
    /// <returns>The new debug marker.</returns>
    public static DebugMarker Create(string name = "Untitled", Color? color = null) {
      DebugMarker marker = CreatePlainCube().AddComponent<DebugMarker>();
      marker.gameObject.name = "DEBUG_MARKER: " + name;
      Mesh mesh = marker.gameObject.GetComponent<MeshFilter>().mesh;
      Material baseMat = marker.gameObject.GetComponent<MeshRenderer>().material;
      Material mat = new Material(baseMat);
      mat.color = color != null ? color.Value : Color.green;
      marker.gameObject.GetComponent<MeshRenderer>().material = mat;

      // Add axis gizmos to indicate what the axes are:
      MakeAxisGizmo(Vector3.forward, FORWARD_GIZMO_AXIS_LENGTH, Color.blue, baseMat, marker.gameObject);
      MakeAxisGizmo(Vector3.right, RIGHT_GIZMO_AXIS_LENGTH, Color.red, baseMat, marker.gameObject);
      MakeAxisGizmo(Vector3.up, UP_GIZMO_AXIS_LENGTH, Color.green, baseMat, marker.gameObject);

      marker.transform.localScale = Vector3.one * DEFAULT_MARKER_SIZE;
      return marker;
    }

    /// <summary>
    /// Updates the position of the marker in world space.
    /// </summary>
    public void UpdateInWorldSpace(Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null) {
      isInModelSpace = false;
      if (position != null) gameObject.transform.position = position.Value;
      if (rotation != null) gameObject.transform.rotation = rotation.Value;
      if (scale != null) gameObject.transform.localScale = scale.Value;
    }

    /// <summary>
    /// Updates the position of the marker in model space.
    /// </summary>
    public void UpdateInModelSpace(Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null) {
      isInModelSpace = true;
      if (position != null) positionModelSpace = position.Value;
      if (rotation != null) rotationModelSpace = rotation.Value;
      if (scale != null) scaleModelSpace = scale.Value;
    }

    private void Update() {
      if (isInModelSpace) {
        gameObject.transform.position = PeltzerMain.Instance.worldSpace.ModelToWorld(positionModelSpace);
        gameObject.transform.rotation = PeltzerMain.Instance.worldSpace.ModelOrientationToWorld(rotationModelSpace);
        gameObject.transform.localScale = PeltzerMain.Instance.worldSpace.scale * scaleModelSpace;
      }
    }

    private static GameObject CreatePlainCube() {
      GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
      AssertOrThrow.NotNull(cube, "Failed to create cube primitive");
      Collider col = cube.GetComponent<Collider>();
      // We don't want a collider.
      if (col != null) Destroy(col);
      return cube;
    }

    private static GameObject MakeAxisGizmo(Vector3 axis, float length, Color color, Material baseMat,
        GameObject parent) {
      GameObject gizmo = CreatePlainCube();
      gizmo.transform.SetParent(parent.transform, /* worldPositionStays */ false);
      gizmo.transform.localRotation = Quaternion.identity;
      // Axes must start at the center and extend in their respective direction; however, since the standard
      // Unity cube is centered on the origin, we have to position it along the axis by half of its length:
      gizmo.transform.localPosition = axis * length * 0.5f;
      gizmo.transform.localScale = Vector3.one * GIZMO_AXIS_THICKNESS + axis * (length - GIZMO_AXIS_THICKNESS);
      gizmo.GetComponent<MeshRenderer>().material = new Material(baseMat);
      gizmo.GetComponent<MeshRenderer>().material.color = color;
      return gizmo;
    }
  }
}