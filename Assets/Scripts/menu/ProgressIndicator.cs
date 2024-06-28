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

using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using UnityEngine.UI;

namespace com.google.apps.peltzer.client.menu
{
    /// <summary>
    /// Widget that gives visual feedback about the progress of a long operation.
    /// We can show two states: working and done, and animate appropriately between them.
    /// </summary>
    public class ProgressIndicator : MonoBehaviour
    {
        /// <summary>
        /// Minimum apparent duration of work. To avoid visual jank, if the operation finishes sooner than this,
        /// we continue pretending to be working (showing the progress indicator) until this minimum time elapses.
        /// </summary>
        private const float MIN_APPARENT_WORK_DURATION_SECONDS = 2.0f;

        /// <summary>
        /// How long we linger in the "finished" state, showing the "done" animation. After this time elapses,
        /// we hide the progress indicator.
        /// </summary>
        private const float FINISHED_STATE_DURATION_SECONDS = 1.0f;

        /// <summary>
        /// Duration of the transition animations between states.
        /// </summary>
        private const float STATE_TRANSITION_ANIM_DURATION_SECONDS = 0.3f;

        /// <summary>
        /// Starting scale for the state transition animation. We animate objects starting from this scale and going
        /// to their natural scale (1.0f).
        /// </summary>
        private const float STATE_TRANSITION_ANIM_INIT_SCALE = 0.02f;

        /// <summary>
        /// Percentage of original scale to scale up to during animation.
        /// </summary>
        private const float STATE_TRANSITION_ANIM_SCALE_INCREASE = .5f;

        /// <summary>
        /// The time in seconds we spend scaling down the progress indicator on completion.
        /// </summary>
        private const float SCALE_OUT_ANIMATION_DURATION = 0.2f;

        /// <summary>
        /// Position of the progress indicator when using the Oculus.
        /// </summary>
        private static readonly Vector3 ROOT_POSITION_OCULUS = new Vector3(0f, 0f, 0.0808f);


        private Color blueBlock = new Color(5f / 255f, 144f / 255f, 179f / 255f, 1.0f);
        private Color yellowBlock = new Color(250f / 255f, 175f / 255f, 9f / 255f, 1.0f);
        private Color redBlock = new Color(255f / 255f, 24f / 255f, 4f / 255f, 1.0f);
        private Color greenBlock = new Color(39f / 255f, 192f / 255f, 72f / 255f, 1.0f);

        /// <summary>
        /// Represents which state we are in.
        /// </summary>
        private enum State
        {
            // Not showing progress indicator. This is the initial state.
            NOT_SHOWING,
            // Showing the "working" indeterminate spinner.
            WORKING,
            // In the PENDING_SUCCESS and PENDING_ERROR states, we are ready to go to either the success or error
            // state, but waiting a bit so it's not too abrupt (this happens if the operation finishes too quickly,
            // in which case we want to keep the indicator around on the screen for a bit).
            PENDING_SUCCESS,
            PENDING_ERROR,
            // Showing the success state. Automatically transitions to NOT_SHOWING after the appropriate time passes.
            SUCCESS,
            // Showing the error state. Automatically transitions to NOT_SHOWING after the appropriate time passes.
            ERROR,
            // In the pending not showing state we know we don't want to show the progress indicator anymore and are
            // going to fade it out.
            PENDING_NOT_SHOWING
        }

        private State state = State.NOT_SHOWING;

        /// <summary>
        /// Root object of the progress indicator hierarchy.
        /// </summary>
        private GameObject rootObject;

        /// <summary>
        /// Visual indicator of the "working" state, which we show when an operation is in progress.
        /// </summary>
        private GameObject workingIndicator;

        private GameObject redCube;
        private GameObject yellowCube;
        private GameObject blueCube;


        /// <summary>
        /// Text mesh containing the text we are currently displaying.
        /// </summary>
        private TextMesh progressText;

        /// <summary>
        /// Time at which the operation started (transitioned to WORKING state).
        /// </summary>
        private float operationStartTime;

        /// <summary>
        /// Time at which the operation finished (transitioned to FINISHED state).
        /// </summary>
        private float operationFinishTime;

        /// <summary>
        /// The text to update the text mesh with when we transition to FINISHED.
        /// Only used in the PENDING_* states.
        /// </summary>
        private string pendingText;

        /// <summary>
        /// The scale of the root object of the progress indicator hierarchy before any scaling animations
        /// are applied to it.
        /// </summary>
        private Vector3 rootObjectScaleAtStart;

        /// <summary>
        /// The previous state before our current state.
        /// </summary>
        private State lastState;

        private void Start()
        {
            rootObject = ObjectFinder.ObjectById("ID_ProgressIndicatorPanel");
            workingIndicator = ObjectFinder.ObjectById("ID_BlocksProgressLooper");
            redCube = ObjectFinder.ObjectById("ID_LooperCubeRed");
            yellowCube = ObjectFinder.ObjectById("ID_LooperCubeYellow");
            blueCube = ObjectFinder.ObjectById("ID_LooperCubeBlue");

            progressText = ObjectFinder.ComponentById<TextMesh>("ID_ProgressText");
            rootObject.SetActive(true);
            InstanceMats();
            rootObject.SetActive(false);
            rootObjectScaleAtStart = rootObject.transform.localScale;
            ChangeState(State.NOT_SHOWING);

            if (Config.Instance.sdkMode == SdkMode.Oculus)
            {
                rootObject.transform.localPosition = ROOT_POSITION_OCULUS;
            }
        }

        /// <summary>
        /// Starts showing the indicator with the given message.
        /// Caller should call FinishOperation when the operation is done.
        /// </summary>
        /// <param name="message"></param>
        public void StartOperation(string message)
        {
            progressText.text = message;
            operationStartTime = Time.time;
            // Force hide the menu hint.
            PeltzerMain.Instance.menuHint.ChangeState(MenuHint.State.INACTIVE);
            ChangeState(State.WORKING);
        }

        /// <summary>
        /// Transitions the indicator into the "done" state, showing the given new message.
        /// The indicator will automatically disappear after a few seconds.
        /// </summary>
        /// <param name="success">Whether the operation finished successfully.</param>
        /// <param name="message">The message to show.</param>
        public void FinishOperation(bool success, string message)
        {
            if (Time.time - operationStartTime < MIN_APPARENT_WORK_DURATION_SECONDS)
            {
                // It's too soon after we started the operation, so can't finish yet because it would look janky
                // (the progress message would just pop for a brief time). So we will keep on pretending that we're
                // still working for a bit, just for visual polish purposes.
                pendingText = message;
                ChangeState(success ? State.PENDING_SUCCESS : State.PENDING_ERROR);
            }
            else
            {
                // We've been showing the progress indicator for long enough that we can just finish immediately
                // if it is an error. However we still need to go into a "pending" success state to make sure the
                // save preview is ready.
                progressText.text = message;
                ChangeState(success ? State.PENDING_SUCCESS : State.ERROR);
            }
        }

        private void Update()
        {
            // Auto-advance the states at the right times.
            float timeSinceStart = Time.time - operationStartTime;
            float timeSinceFinish = Time.time - operationFinishTime;
            if (state == State.PENDING_SUCCESS
              && timeSinceStart > MIN_APPARENT_WORK_DURATION_SECONDS
              && PeltzerMain.Instance.savePreview.state == SavePreview.State.WAITING)
            {
                // PENDING_SUCCESS -> SUCCESS.
                progressText.text = pendingText;
                ChangeState(State.SUCCESS);
            }
            else if (state == State.PENDING_ERROR && timeSinceStart > MIN_APPARENT_WORK_DURATION_SECONDS)
            {
                // PENDING_ERROR -> ERROR.
                progressText.text = pendingText;
                ChangeState(State.ERROR);
            }
            else if (state == State.ERROR && timeSinceFinish > FINISHED_STATE_DURATION_SECONDS)
            {
                // (SUCCESS | ERROR) -> NOT_SHOWING
                ChangeState(State.PENDING_NOT_SHOWING);
            }
            else if (state == State.PENDING_NOT_SHOWING)
            {
                float pctDone = (Time.time - operationStartTime) / SCALE_OUT_ANIMATION_DURATION;

                if (pctDone > 1)
                {
                    // If the last state was successful we want to show the saved preview after the progress
                    // indicator scales down.
                    if (lastState == State.SUCCESS)
                    {
                        PeltzerMain.Instance.savePreview.ChangeState(SavePreview.State.SCALE_ANIMATING);
                    }
                    ChangeState(State.NOT_SHOWING);
                }
                else
                {
                    rootObject.transform.localScale = Vector3.Lerp(rootObjectScaleAtStart, Vector3.zero, pctDone);
                }
            }
        }

        private void InstanceMats()
        {
            MeshRenderer redRenderer = redCube.GetComponent<MeshRenderer>();
            redRenderer.material = new Material(redRenderer.material);
            MeshRenderer yellowRenderer = yellowCube.GetComponent<MeshRenderer>();
            yellowRenderer.material = new Material(yellowRenderer.material);
            MeshRenderer blueRenderer = blueCube.GetComponent<MeshRenderer>();
            blueRenderer.material = new Material(blueRenderer.material);
        }

        private void SetAllColors(Color color)
        {
            MeshRenderer redRenderer = redCube.GetComponent<MeshRenderer>();
            MeshRenderer yellowRenderer = yellowCube.GetComponent<MeshRenderer>();
            MeshRenderer blueRenderer = blueCube.GetComponent<MeshRenderer>();

            redRenderer.material.color = color;
            yellowRenderer.material.color = color;
            blueRenderer.material.color = color;
        }

        private void ResetAnimColors()
        {
            MeshRenderer redRenderer = redCube.GetComponent<MeshRenderer>();
            MeshRenderer yellowRenderer = yellowCube.GetComponent<MeshRenderer>();
            MeshRenderer blueRenderer = blueCube.GetComponent<MeshRenderer>();

            redRenderer.material.color = redBlock;
            yellowRenderer.material.color = yellowBlock;
            blueRenderer.material.color = blueBlock;
        }

        /// <summary>
        /// Changes state and does the necessary maintenance tasks to enter the new state.
        /// </summary>
        /// <param name="newState">The new state to enter.</param>
        private void ChangeState(State newState)
        {
            lastState = state;
            AudioLibrary audioLibrary = PeltzerMain.Instance.audioLibrary;

            state = newState;
            rootObject.SetActive(newState != State.NOT_SHOWING);

            switch (state)
            {
                case State.PENDING_NOT_SHOWING:
                    operationStartTime = Time.time;
                    break;
                case State.NOT_SHOWING:
                    rootObject.transform.localScale = rootObjectScaleAtStart;
                    break;
                case State.WORKING:
                case State.PENDING_SUCCESS:
                case State.PENDING_ERROR:
                    workingIndicator.SetActive(true);
                    if (lastState == State.NOT_SHOWING && newState != State.NOT_SHOWING)
                    {
                        ResetAnimColors();
                    }
                    // "Working" indicator should be visible; "done" indicator should be invisible.
                    if (newState == State.WORKING)
                    {
                        operationStartTime = Time.time;
                    }
                    rootObject.SetActive(true);
                    break;
                case State.SUCCESS:
                    // Clear the text so that it doesn't scale down.
                    progressText.text = "";
                    audioLibrary.PlayClip(audioLibrary.saveSound);
                    ChangeState(State.PENDING_NOT_SHOWING);
                    break;
                case State.ERROR:
                    // Both indicators ("working" and "success/error") are simultaneously visible, for animation purposes.
                    // After the transition animation, we hide the working indicator and leave only the success/error
                    // indicator.
                    operationFinishTime = Time.time;
                    rootObject.SetActive(true);
                    workingIndicator.SetActive(true);
                    SetAllColors(redBlock);
                    audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.errorSound);
                    break;
                default:
                    throw new System.Exception("Invalid state " + state);
            }
        }
    }
}
