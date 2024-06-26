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

using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Animates the PaintToolhead in response to our event system. 
  /// </summary>
  public class PaintToolheadAnimation : MonoBehaviour {

    public PeltzerMain peltzerMain;
    public ControllerMain controllerMain;
    public PeltzerController peltzerController;
    private Animator animator;
    private bool isActivated = false;
    private bool isPointed = false;
    private Vector3 lastPosition;
    private float lastVelocity = 0f;

    private readonly float MAX_VELOCITY_THRESHOLD = 3;

    private enum Direction {
      UP, DOWN, LEFT, RIGHT, NONE
    }

    // Use this for initialization
    void Start() {
      animator = transform.Find("BrushRigMesh").GetComponent<Animator>();
      peltzerMain = FindObjectOfType<PeltzerMain>();
      controllerMain = peltzerMain.controllerMain;
    }

    void Update() {
      if (isActivated) {
        float dist = Vector3.Distance(lastPosition, peltzerController.transform.position);
        float velocity = dist / Time.deltaTime;
        Vector3 direction = transform.InverseTransformDirection(peltzerController.transform.position - lastPosition);

        Direction dir = Direction.NONE;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) {
          if (direction.x < 0) {
            dir = Direction.LEFT;
          } else if (direction.x > 0) {
            dir = Direction.RIGHT;
          }
        } else if (Mathf.Abs(direction.x) < Mathf.Abs(direction.y)) {
          if (direction.y < 0) {
            dir = Direction.DOWN;
          } else if (direction.y > 0) {
            dir = Direction.UP;
          }
        }
        
        lastPosition = peltzerController.transform.position;
        
        if (animator != null) {
          switch (dir) {
            case Direction.DOWN:
              animator.Play("Up", 0, (velocity / MAX_VELOCITY_THRESHOLD > 1 ? 1 : velocity / MAX_VELOCITY_THRESHOLD));
              break;
            case Direction.UP:
              animator.Play("Down", 0, (velocity / MAX_VELOCITY_THRESHOLD > 1 ? 1 : velocity / MAX_VELOCITY_THRESHOLD));
              break;
            case Direction.LEFT:
              animator.Play("Left", 0, (velocity / MAX_VELOCITY_THRESHOLD > 1 ? 1 : velocity / MAX_VELOCITY_THRESHOLD));
              break;
            case Direction.RIGHT:
              animator.Play("Right", 0, (velocity / MAX_VELOCITY_THRESHOLD > 1 ? 1 : velocity / MAX_VELOCITY_THRESHOLD));
              break;
          }
        }
      } else {
        if (animator != null) {
          animator.SetTrigger("Release");
        }
      }
    }

    /// <summary>
    ///   An event handler that listens for controller input and delegates accordingly.
    /// </summary>
    /// <param name="sender">The sender of the controller event.</param>
    /// <param name="args">The controller event arguments.</param>
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if(args.ControllerType == ControllerType.PELTZER
        && args.ButtonId == ButtonId.Trigger) {
        if(args.Action == ButtonAction.DOWN) {
          StartAnimation();
        } else if(args.Action == ButtonAction.UP) {
          StopAnimation();
        }
      }
    }

    /// <summary>
    ///   Activates the animation logic by attaching the event handler for input.
    /// </summary>
    public void Activate() {
      isActivated = true;
      controllerMain.ControllerActionHandler += ControllerEventHandler;
    }

    /// <summary>
    ///   Deactivates the animation logic by removing the event handler for input.
    /// </summary>
    public void Deactivate() {
      isActivated = false;
      controllerMain.ControllerActionHandler -= ControllerEventHandler;
    }

    /// <summary>
    ///   Entry point for actual animation associated with the current active state of the tool.
    /// </summary>
    private void StartAnimation() {
      if (animator != null) {
        animator.SetTrigger("Active");
      }
    }

    /// <summary>
    ///   Entry point for the animation associated with the current dormant state of the tool.
    /// </summary>
    private void StopAnimation() {
      if (animator != null) {
        animator.SetTrigger("Dormant");
      }
    }

  }
}
