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

namespace com.google.apps.peltzer.client.model.main {
  /// <summary>
  ///   Manages the transformation between "world space" and "model space".
  ///
  ///   Model space is the space in which the meshes are represented. The world space is the Unity coordinate system.
  ///   By manipulating the transformation between model and world space, the user can pan/rotate/zoom the model
  ///   to make editing easier. The transform, however, is NOT part of the model, it's just a viewing convenience
  ///   (think of it like the scrollbar position and zoom level of a 2D document -- they just influence how you
  ///   view the document, they are not part of the content itself).
  ///
  /// </summary>
  public class WorldSpace {
    private const float MAX_SCALE = 4.0f;
    private const float MIN_SCALE = 0.25f;
    private const float DEFAULT_SCALE = 0.8f;
    private float _scale;
    private Vector3 _offset;
    private Quaternion _rotation = Quaternion.identity;
    private Matrix4x4 _modelToWorld;
    private Matrix4x4 _worldToModel;
    private bool isLimited;
    public Bounds bounds { get; private set; }
    public float scale { get { return _scale; } set { _scale = LimitScale(value); RecalcTransform(); } }
    public Quaternion rotation { get { return _rotation; } set { _rotation = value; RecalcTransform(); } }
    public Vector3 offset { get { return _offset; } set { _offset = value; RecalcTransform(); } }
    public Matrix4x4 modelToWorld { get { return _modelToWorld; } private set { _modelToWorld = value; } }
    public Matrix4x4 worldToModel { get { return _worldToModel; } private set { _worldToModel = value; } }

    public WorldSpace(Bounds bounds, bool isLimited = true) {
      this.bounds = bounds;
      this.isLimited = isLimited;
      SetToDefault();
    }

    public void SetToDefault() {
      scale = DEFAULT_SCALE;
      offset = Vector3.zero;
      _rotation = Quaternion.identity;
    }

    public Vector3 WorldToModel(Vector3 pos) {
      return _worldToModel.MultiplyPoint(pos);
    }

    public Vector3 ModelToWorld(Vector3 pos) {
      return _modelToWorld.MultiplyPoint(pos);
    }

    public Vector3 ModelVectorToWorld(Vector3 vec) {
      return _modelToWorld.MultiplyVector(vec);
    }

    public Vector3 WorldVectorToModel(Vector3 vec) {
      return _worldToModel.MultiplyVector(vec);
    }

    public Quaternion ModelOrientationToWorld(Quaternion orient) {
      return rotation * orient;
    }

    public Quaternion WorldOrientationToModel(Quaternion orient) {
      return Quaternion.Inverse(rotation) * orient;
    }

    public void ResetScale() {
      scale = 1.0f;
    }

    public void ResetRotation() {
      _rotation = Quaternion.identity;
    }

    private void RecalcTransform() {
      // TODO(31747542): Non identity rotations break things that use _offset and _scale directly instead
      // of using the matrices.
      _modelToWorld = Matrix4x4.TRS(_offset, _rotation, Vector3.one * _scale);
      worldToModel = _modelToWorld.inverse;
    }

    private float LimitScale(float scale) {
      return isLimited ? Mathf.Min(MAX_SCALE, Mathf.Max(scale, MIN_SCALE)) : scale;
    }
  }
}
