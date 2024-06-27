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
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.controller
{

    /// <summary>
    ///   SelectableMenuItem that can be attached to a palette to change the current material. This represents one
    ///   colour swatch (square) on the colour-picker palette.
    ///   It is expected that these are configured in the Unity Editor with local:
    ///   - y position of DEFAULT_Y_POSITION
    ///   - y scale of DEFAULT_Y_SCALE
    /// </summary>
    public class ChangeMaterialMenuItem : SelectableMenuItem
    {
        public int materialId;

        private readonly float BUMPED_Y_POSITION = 0.001f;
        private readonly float BUMPED_Y_SCALE = 0.0025f;

        private readonly float HOVERED_Y_POSITION = -0.0025f;
        private readonly float HOVERED_Y_SCALE = 0.01f;

        private readonly float BUMP_DURATION = 0.1f;
        private readonly float HOVER_DURATION = 0.25f;
        private readonly float RIPPLE_DURATION = 0.3f; // How long it takes to ripple out, or ripple in, a swatch.
        private readonly float MAX_RIPPLE_HOLD_DURATION = 0.23f; // How long a swatch sticks out after rippling out.

        // Ripple animations.
        private bool isRipplingOut;

        private struct RippleParams
        {
            public Vector3 localPosition;
            public Vector3 localScale;
            public float initialDelay;
            public float reverseDelay;

            public RippleParams(Vector3 localPosition, Vector3 localScale, float initialDelay, float reverseDelay)
            {
                this.localPosition = localPosition;
                this.localScale = localScale;
                this.initialDelay = initialDelay;
                this.reverseDelay = reverseDelay;
            }
        }

        private RippleParams currentRippleParams;
        private Vector3 defaultLocalPosition;
        private Vector3 defaultLocalScale;
        private bool isBumping = false;
        public bool isHovered = false;
        private Vector3? targetPosition = null;
        private Vector3? targetScale = null;
        private float timeStartedLerping;
        private float currentLerpDuration;

        void Start()
        {
            defaultLocalPosition = transform.localPosition;
            defaultLocalScale = transform.localScale;
        }

        private RippleParams GetRippleParams()
        {
            Vector3 rippleLocalPosition = defaultLocalPosition;
            Vector3 rippleLocalScale = defaultLocalScale;
            float initialRippleDelay = 0;
            float reverseRippleDelay = 0;

            // Materials on the 'top' of the palette always animate up.
            if (materialId <= 5)
            {
                rippleLocalPosition += new Vector3(0f, 0f, 0.01f);
                rippleLocalScale += new Vector3(0f, 0f, 0.02f);

                // Specify the delays based on the user's handedness.
                if (PeltzerMain.Instance.peltzerController.handedness == Handedness.RIGHT)
                {
                    initialRippleDelay = materialId * 0.05f;
                    reverseRippleDelay = (5 - materialId) * 0.05f;
                }
                else
                {
                    initialRippleDelay = (5 - materialId) * 0.05f;
                    reverseRippleDelay = materialId * 0.05f;
                }
            }

            if (PeltzerMain.Instance.peltzerController.handedness == Handedness.RIGHT)
            {
                // Materials on the 'right' of the palette animate out to the right if the user is right-handed.
                if (materialId % 6 == 0)
                {
                    rippleLocalPosition += new Vector3(0.01f, 0f, 0f);
                    rippleLocalScale += new Vector3(0.02f, 0f, 0f);
                    int row = materialId / 6;
                    initialRippleDelay = row * 0.05f;
                    // Acting as if there are 6 rows, because there are 6 columns.
                    reverseRippleDelay = (6 - row) * 0.05f;
                }
            }
            else
            {
                // Materials on the 'left' of the palette animate out to the left if the user is left-handed.
                if ((materialId + 1) % 6 == 0)
                {
                    rippleLocalPosition += new Vector3(-0.01f, 0f, 0f);
                    rippleLocalScale += new Vector3(0.02f, 0f, 0f);
                    int row = materialId / 6;
                    initialRippleDelay = row * 0.05f;
                    // Acting as if there are 6 rows, because there are 6 columns.
                    reverseRippleDelay = (6 - row) * 0.05f;
                }
            }

            return new RippleParams(rippleLocalPosition, rippleLocalScale, initialRippleDelay, reverseRippleDelay);
        }

        /// <summary>
        ///   Lerps towards a target position and scale over BUMP_DURATION. If we are bumping, then when the 'bumped'
        ///   positions & scales are reached, reverts to the 'hovered' positions and scales.
        /// </summary>
        void Update()
        {
            if (targetPosition == null || targetScale == null)
            {
                return;
            }

            float timeDiff = Time.time - timeStartedLerping;
            if (timeDiff < 0)
            {
                // This implies we are rippling, but have not yet reached the delay threshold. 
                // We mess with timeStartedLerping to get the delay effect, is why this happens.
                return;
            }

            float pctDone = timeDiff / currentLerpDuration;

            if (pctDone >= 1)
            {
                // If we're done, immediately set the position and scale.
                gameObject.transform.localPosition = targetPosition.Value;
                gameObject.transform.localScale = targetScale.Value;
                if (isBumping)
                {
                    // If we're done and we were bumping down, revert to the expected position.
                    isBumping = false;
                    SetToDefaultPositionAndScale(HOVER_DURATION);
                }
                else if (isRipplingOut)
                {
                    // If we're done and we were rippling out, then revert to the default position.
                    isRipplingOut = false;
                    SetToDefaultPositionAndScale(RIPPLE_DURATION, MAX_RIPPLE_HOLD_DURATION - currentRippleParams.reverseDelay);
                }
                else
                {
                    // If we're done and were not bumping down, then there's no more lerping to do.
                    targetPosition = null;
                    targetScale = null;
                }
            }
            else
            {
                pctDone = Mathf.SmoothStep(0f, 1f, pctDone);

                // If we're not done, lerp towards the target position and scale.
                gameObject.transform.localPosition =
                  Vector3.Lerp(gameObject.transform.localPosition, targetPosition.Value, pctDone);
                gameObject.transform.localScale =
                  Vector3.Lerp(gameObject.transform.localScale, targetScale.Value, pctDone);
            }
        }

        public override void ApplyMenuOptions(PeltzerMain main)
        {
            // Return if you aren't allowed to change colors or you aren't allowed this color.
            if (!PeltzerMain.Instance.restrictionManager.changingColorsAllowed
              || !PeltzerMain.Instance.restrictionManager.IsColorAllowed(materialId))
            {
                return;
            }
            main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);

            // When clicked, we first update the material being used in Poly.
            main.peltzerController.currentMaterial = materialId;
            main.HasEverChangedColor = true;
            main.peltzerController.ChangeToolColor();

            // And we then 'bump' the swatch down slightly and back up to its position, to provide a visual indication that
            // the user's click was registered.
            StartBump();

            // If the user changes colour, but was using a non-insertion tool, switch to the paintbrush.
            ControllerMode currentMode = PeltzerMain.Instance.peltzerController.mode;
            if (currentMode != ControllerMode.paintFace
              && currentMode != ControllerMode.paintMesh
              && currentMode != ControllerMode.insertVolume
              && currentMode != ControllerMode.insertStroke)
            {
                main.peltzerController.ChangeMode(ControllerMode.paintMesh, ObjectFinder.ObjectById("ID_ToolPaint"));
            }
        }


        /// <summary>
        ///   Briefly 'bump' the menu item to a middle state, before returning it to its default state.
        ///   Used to visually indicate to the user that a click was received.
        /// </summary>
        public void StartBump()
        {
            isBumping = true;
            StartLerp(
              new Vector3(gameObject.transform.localPosition.x, BUMPED_Y_POSITION, gameObject.transform.localPosition.z),
              new Vector3(gameObject.transform.localScale.x, BUMPED_Y_SCALE, gameObject.transform.localScale.z),
              BUMP_DURATION);
        }

        public void StartRipple()
        {
            isRipplingOut = true;
            currentRippleParams = GetRippleParams();
            StartLerp(currentRippleParams.localPosition, currentRippleParams.localScale,
              RIPPLE_DURATION, currentRippleParams.initialDelay);
        }

        /// <summary>
        ///   Allows an external source to set whether this swatch is being hovered.
        /// </summary>
        public void SetHovered(bool isHovered)
        {
            if (this.isHovered == isHovered)
            {
                return;
            }
            this.isHovered = isHovered;
            SetToDefaultPositionAndScale(HOVER_DURATION);
        }

        /// <summary>
        ///   Sets the position and scale to their default (non-bump-influenced) states.
        /// </summary>
        private void SetToDefaultPositionAndScale(float duration, float delay = 0)
        {
            if (isHovered)
            {
                StartLerp(
                  new Vector3(defaultLocalPosition.x, HOVERED_Y_POSITION, defaultLocalPosition.z),
                  new Vector3(defaultLocalScale.x, HOVERED_Y_SCALE, defaultLocalScale.z),
                  duration);
            }
            else
            {
                StartLerp(defaultLocalPosition, defaultLocalScale, duration, delay);
            }
        }

        /// <summary>
        ///   Lerps the position and scale to given states over a given duration with a given delay.
        /// </summary>
        private void StartLerp(Vector3 position, Vector3 scale, float duration, float delay = 0)
        {
            targetPosition = position;
            targetScale = scale;
            currentLerpDuration = duration;
            timeStartedLerping = Time.time + delay;
        }
    }
}
