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
using System.Collections;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Helper for octant locations on a controller's touchpad.
  ///   For controller selections that are on the axis, use top, bottom, left, right.
  ///   For controller selections that are diagnonal, user northeast, northwest, etc.
  /// </summary>
  public enum TouchpadLocation { NONE, TOP, BOTTOM, RIGHT, LEFT, CENTER }
    public static class TouchpadLocationHelper { 
    private static readonly float TWO_PI = Mathf.PI * 2f;
    private static readonly float PI_OVER_4 = Mathf.PI * .25f;
    private static readonly float CENTER_BUTTON_RADIUS = 0.333f;

    /// <summary>
    ///   Given an x,y co-ordinate on the touchpad, returns which quadrant
    ///   that co-ordinate maps to. Quadrants are defined as 90-degree areas
    ///   with the TOP area at -45 degrees to 45 degrees and other areas
    ///   similarly offset from the cardinal directions.
    /// </summary>
    public static TouchpadLocation GetTouchpadLocation(Vector2 position) {
      // Check for the center.
      float radius = position.magnitude;
      if (radius <= CENTER_BUTTON_RADIUS) {
        return TouchpadLocation.CENTER;
      }

      // Find the quadrant.
      float angle = Mathf.Atan2(position.x, position.y);
      if (angle < 0) {
        angle += TWO_PI;
      }
      int octant = (int)Mathf.Floor(angle / PI_OVER_4);
      switch(octant) {
        case 0:
        case 7:
          return TouchpadLocation.TOP;
        case 1:
        case 2:
          return TouchpadLocation.RIGHT;
        case 3:
        case 4:
          return TouchpadLocation.BOTTOM;
        case 5:
        case 6:
          return TouchpadLocation.LEFT;
        default:
          return TouchpadLocation.NONE;
      }
    }
  }
}
