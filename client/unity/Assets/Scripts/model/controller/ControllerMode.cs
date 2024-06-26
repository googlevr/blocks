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

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   The current mode of a controller.
  ///   IMPORTANT: enum values are used as indices, so the values must be numbered sequentially from 0
  ///   (default enum value assignment).
  /// </summary>
  public enum ControllerMode {
    /// <summary>
    ///   Mode for inserting new primitives and volumes.
    /// </summary>
    insertVolume,
    /// <summary>
    ///   Mode for inserting strokes.
    /// </summary>
    insertStroke,
    /// <summary>
    ///   Mode for moving mesh components.
    /// </summary>
    reshape,
    /// <summary>
    ///   Mode for extruding faces.
    /// </summary>
    extrude,
    /// <summary>
    /// Mode for subdividing faces.
    /// </summary>
    subdivideFace,
    /// <summary>
    /// Mode for subdividing entire meshes.
    /// </summary>
    subdivideMesh,
    /// <summary>
    /// Mode for deleting meshes.
    /// </summary>
    delete,
    /// <summary>
    /// Mode for moving meshes.
    /// </summary>
    move,
    /// <summary>
    /// Mode for painting meshes.
    /// </summary>
    paintMesh,
    /// <summary>
    /// Mode for painting faces.
    /// </summary>
    paintFace,
    /// <summary>
    /// Mode for selecting paint color from existing objects.
    /// </summary>
    paintDropper,
    /// <summary>
    /// Mode for deleting meshes via subtraction (csg).
    /// </summary>
    subtract,
    /// <summary>
    /// Mode for deleting edges.
    /// </summary>
    deletePart,
  }
}
