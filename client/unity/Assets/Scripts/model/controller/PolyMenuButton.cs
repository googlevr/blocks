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

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   A button on the Poly menu. Specific actions are set in sub-classes, this class handles hover/click UI state.
  /// </summary>
  public class PolyMenuButton : SelectableMenuItem {
    private readonly float BUMPED_Y_SCALE_FACTOR = 1.25f;
    private readonly float HOVERED_Y_SCALE_FACTOR = 2f;

    private readonly float BUMP_DURATION = 0.1f;
    private readonly float HOVER_DURATION = 0.25f;

    private float defaultYPosition;
    private float defaultYScale;

    private float hoveredYPosition;
    private float hoveredYScale;

    private float bumpedYPosition;
    private float bumpedYScale;

    private bool isBumping = false;
    public bool isHovered = false;
    private Vector3? targetPosition = null;
    private Vector3? targetScale = null;
    private float timeStartedLerping;
    private float currentLerpDuration;

    public void Start() {
      defaultYPosition = transform.localPosition.y;
      defaultYScale = transform.localScale.y;

      hoveredYScale = defaultYScale * HOVERED_Y_SCALE_FACTOR;
      hoveredYPosition = (hoveredYScale - defaultYScale) / 2.0f;

      bumpedYScale = defaultYScale * BUMPED_Y_SCALE_FACTOR;
      bumpedYPosition = (bumpedYScale - defaultYScale) / 2.0f;
    }

    /// <summary>
    ///   Lerps towards a target position and scale over BUMP_DURATION. If we are bumping, then when the 'bumped'
    ///   positions and scales are reached, reverts to the 'hovered' positions and scales.
    /// </summary>
    void Update() {
      if (targetPosition == null || targetScale == null) {
        return;
      }

      float pctDone = (Time.time - timeStartedLerping) / currentLerpDuration;

      if (pctDone >= 1) {
        // If we're done, immediately set the position and scale.
        gameObject.transform.localPosition = targetPosition.Value;
        gameObject.transform.localScale = targetScale.Value;
        if (isBumping) {
          // If we're done and we were bumping down, revert to the expected position.
          isBumping = false;
          SetToDefaultPositionAndScale();
        } else {
          // If we're done and were not bumping down, then there's no more lerping to do.
          targetPosition = null;
          targetScale = null;
        }
      } else {
        // If we're not done, lerp towards the target position and scale.
        gameObject.transform.localPosition =
          Vector3.Lerp(gameObject.transform.localPosition, targetPosition.Value, pctDone);
        gameObject.transform.localScale =
          Vector3.Lerp(gameObject.transform.localScale, targetScale.Value, pctDone);
      }
    }

    /// <summary>
    ///   Briefly 'bump' the menu item to a middle state, before returning it to its default state.
    ///   Used to visually indicate to the user that a click was received.
    /// </summary>
    internal void StartBump() {
      isBumping = true;
      StartLerp(bumpedYPosition, bumpedYScale, BUMP_DURATION);
    }

    /// <summary>
    ///   Allows an external source to set whether this swatch is being hovered.
    /// </summary>
    public void SetHovered(bool isHovered) {
      if (this.isHovered == isHovered) {
        return;
      }
      this.isHovered = isHovered;
      SetToDefaultPositionAndScale();
    }

    /// <summary>
    ///   Sets the position and scale to their default (non-bump-influenced) states.
    /// </summary>
    private void SetToDefaultPositionAndScale() {
      float yPosition = isHovered ? hoveredYPosition : defaultYPosition;
      float yScale = isHovered ? hoveredYScale : defaultYScale;
      StartLerp(yPosition, yScale, HOVER_DURATION);
    }

    /// <summary>
    ///   Lerps the position and scale to given states over a given duration.
    /// </summary>
    private void StartLerp(float yPosition, float yScale, float duration) {
      targetPosition = new Vector3(
        gameObject.transform.localPosition.x, yPosition, gameObject.transform.localPosition.z);
      targetScale = new Vector3(
        gameObject.transform.localScale.x, yScale, gameObject.transform.localScale.z);
      currentLerpDuration = duration;
      timeStartedLerping = Time.time;
    }
  }
}
