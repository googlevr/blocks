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
using com.google.apps.peltzer.client.tools.utils;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {
  public class SmoothScale {
    public Vector3 scaleFactor;
    // If true, we are doing a scaling animation.
    public bool scaleAnimActive;
    // If scaleAnimActive == true, this indicates the initial scale we are animating from.
    public float scaleAnimStartScale;
    // If scaleAnimActive == true, this indicates the time when the scaling animation began (as in Time.time).
    public float scaleAnimStartTime;
    // If scaleAnimActive == true, this indicates the duration of the scale animation.
    public float scaleAnimDuration;
    // Duration of the scale animation, in seconds.
    private const float SCALE_ANIM_DURATION = 0.10f;

    public SmoothScale() {
      scaleFactor = Vector3.one;
      scaleAnimActive = false;
      scaleAnimStartScale = 1f;
      scaleAnimStartTime = 0f;
      scaleAnimDuration = 0f;
    }

    public SmoothScale(SmoothScale source) {
      scaleFactor = source.scaleFactor;
      scaleAnimActive = source.scaleAnimActive;
      scaleAnimStartScale = source.scaleAnimStartScale;
      scaleAnimStartTime = source.scaleAnimStartTime;
      scaleAnimDuration = source.scaleAnimDuration;
    }

    public void Update() {
      scaleFactor = Vector3.one;
      if (scaleAnimActive) {
        scaleFactor = GetCurrentAnimatedScale();
        scaleAnimActive = scaleAnimActive && (Time.time - scaleAnimStartTime < scaleAnimDuration);
      }
    }

    public Vector3 GetScale() {
      return scaleFactor;
    }
    
    /// <summary>
    /// Animates this object's displayed scale from the given scale to the default scale (1.0f).
    /// </summary>
    /// <param name="fromScale">Old scale factor.</param>
    public void AnimateScaleFrom(float fromScale) {
      scaleAnimActive = true;
      scaleAnimStartScale = fromScale;
      scaleAnimDuration = SCALE_ANIM_DURATION;
      scaleAnimStartTime = Time.time;
    }

    /// <summary>
    /// Returns the current animated scale, that is, the scale at which this object is currently being displayed
    /// for animation purposes.
    /// </summary>
    /// <returns>The current animated scale.</returns>
    private Vector3 GetCurrentAnimatedScale() {
      if (!scaleAnimActive) return Vector3.one;
      float elapsed = Time.time - scaleAnimStartTime;
      float factor = scaleAnimStartScale + Mathf.Sqrt(Mathf.Clamp01(elapsed / scaleAnimDuration)) *
        (1.0f - scaleAnimStartScale);
      return factor * Vector3.one;
    }
  }
  
  public class SmoothMoves {
    // Smoothing settings:

    // How long, in seconds, it takes for the displayed position to catch up with the real position.
    private const float DISPLAY_CATCH_UP_TIME = 0.05f;
    // Minimum speed at which displayed position catches up with the real position.
    private const float MIN_CATCH_UP_SPEED = 0.1f;
    // How close the displayed position has to be to the real position for us to end the animation.
    private const float DIST_TO_TARGET_EPSILON = 0.001f;
    
    private Vector3 _positionModelSpace;
    /// <summary>
    /// Used to disable linear interpolation of display position. Primary use case is when a user of
    /// MeshWithMaterialRenderer is handling a transform on its own where linear interpolation would result
    /// in incorrect output - rotating the mesh by its parent transform, for example.
    /// </summary>
    bool preventDisplayPositionUpdate = false;
    
    // Optional position in model space.  For things that are not parented to game objects (like selections)
    // it makes more sense to specify their location in model space.
    // Setting this position will result in a hard update (no smoothing). If smoothing is desired, use the
    // SetPositionModelSpace method and indicate your desire to have the motion smoothed.
    public Vector3 positionModelSpace {
      get {
        return _positionModelSpace;
      }
      set {
        // Setting this property is a hard update (not smoothed).
        SetPositionModelSpace(value, false /* smooth */);
      }
    }

    private Quaternion _orientationModelSpace;

    // Optional orientation in model space.
    // Setting is private to enforce use of SetOrientationModelSpace as it makes intent clearer.
    public Quaternion orientationModelSpace {
      get {
        return _orientationModelSpace;
      }
      private set {
        // Setting this property is a hard update (not smoothed).
        SetOrientationModelSpace(value, false /* smooth */);
      }
    }

    // For smooth animation, this is the position at which we are currently displaying this object.
    // We update this each frame to chase after the real position.
    private Vector3? displayPositionModelSpace;

    // A wrapper for model space orientation that manages spherical linear interpolation of orientation changes.
    // This is updated every frame.
    private Slerpee displayOrientationWrapperModelSpace;

    private SmoothScale smoothScale;

    private WorldSpace worldSpace;

    public SmoothMoves(WorldSpace worldSpace, Vector3 startingPositionModel, Quaternion startingOrientationModel) {
      this.worldSpace = worldSpace;
      positionModelSpace = startingPositionModel;
      orientationModelSpace = startingOrientationModel;
      smoothScale = new SmoothScale();
    }

    public SmoothMoves(SmoothMoves source) {
      this.worldSpace = source.worldSpace;
      this.positionModelSpace = source.positionModelSpace;
      this.orientationModelSpace = source.orientationModelSpace;
      this.smoothScale = new SmoothScale(source.smoothScale);
    }
    
     public void UpdateDisplayPosition() {
      if (null == positionModelSpace) return;
      if (preventDisplayPositionUpdate) return;
      if (null == displayPositionModelSpace) {
        // If we don't have a display position yet, use the actual position.
        displayPositionModelSpace = positionModelSpace;
        return;
      }
      // Update the display position smoothly.
      Vector3 targetPos = positionModelSpace;
      Vector3 curPos = displayPositionModelSpace.Value;
      float distToTarget = Vector3.Distance(targetPos, curPos);
      // Calculate the speed we have to move at in order to hit the target in DISPLAY_CATCH_UP_TIME.
      // But don't go any slower than MIN_CATCH_UP_SPEED.
      float speed = Mathf.Max(distToTarget / DISPLAY_CATCH_UP_TIME, MIN_CATCH_UP_SPEED);
      // Displacement is how far we could move on this frame, given the computed speed.
      float displacement = speed * Time.deltaTime;
      if (displacement >= distToTarget) {
        // Arrived at target.
        curPos = targetPos;
      } else {
        // Didn't arrive at target yet.
        // Update curPos to go towards targetPos at the given speed.
        curPos += displacement * (targetPos - curPos).normalized;
      }
      displayPositionModelSpace = curPos;

      smoothScale.Update(); 
    }

    public Vector3 GetScale() {
      return smoothScale.GetScale();
    }

    public Vector3 GetDisplayPositionInWorldSpace() {
      return (displayPositionModelSpace != null) ?
        worldSpace.ModelToWorld(displayPositionModelSpace.Value) :
        GetPositionInWorldSpace();
    }
    


    /// <summary>
    /// Returns the position in world space. Note that if this IS NOT affected by smoothing. Smoothing is a purely
    /// visual effect and does not alter the object's position.
    /// </summary>
    /// <returns></returns>
    public Vector3 GetPositionInWorldSpace() {
     return worldSpace.ModelToWorld(positionModelSpace);
    }

    public Vector3 GetPositionInModelSpace() {
      return positionModelSpace;
    }
    
    /// <summary>
    /// Returns the display orientation in world space.
    /// </summary>
    public Quaternion GetDisplayOrientationInWorldSpace() {
      if (displayOrientationWrapperModelSpace != null) {
        // Retrieve the current slerped orientation...
        Quaternion modelOrientation = displayOrientationWrapperModelSpace.UpdateAndGetCurrentOrientation();
        // and transform it into world space.
        return worldSpace.ModelOrientationToWorld(modelOrientation);
      } else {
        // If we don't have a display orientation, just return the actual orientation in world space.
        return GetOrientationInWorldSpace();
      }
    }

    /// <summary>
    /// Returns the orientation in world space.
    /// </summary>
    public Quaternion GetOrientationInWorldSpace() {
      return worldSpace.ModelOrientationToWorld(orientationModelSpace);
    }

    /// <summary>
    /// Returns the orientation in model space.
    /// </summary>
    public Quaternion GetOrientationInModelSpace() {
      return orientationModelSpace;
    }

    /// <summary>
    /// Sets the position in model space, optionally with smoothing.
    /// </summary>
    /// <param name="newPositionModelSpace">The new position in model space.</param>
    /// <param name="smooth">True if a smoothing effect is desired. Note that smoothing is a purely visual effect.
    /// The actual position is instantaneously updated regardless of smoothing.</param>
    public void SetPositionModelSpace(Vector3 newPositionModelSpace, bool smooth = false) {
      _positionModelSpace = newPositionModelSpace;

      if (!smooth) {
        // Immediately update the display position as well.
        displayPositionModelSpace = newPositionModelSpace;
      }
      preventDisplayPositionUpdate = false;
    }

    /// <summary>
    /// Sets the position in world space, optionally with smoothing.
    /// </summary>
    /// <param name="newPositionModelSpace">The new position in model space.</param>
    /// <param name="smooth">True if a smoothing effect is desired. Note that smoothing is a purely visual effect.
    /// The actual position is instantaneously updated regardless of smoothing.</param>
    public void SetPositionWorldSpace(Vector3 newPositionModelSpace, bool smooth = false) {
      _positionModelSpace = worldSpace.WorldToModel(newPositionModelSpace);

      if (!smooth) {
        // Immediately update the display position as well.
        displayPositionModelSpace = _positionModelSpace;
      }
      preventDisplayPositionUpdate = false;
    }
    
    /// <summary>
    /// Sets the position in model space, overriding smoothing with an override display position. This should primarily
    /// be used when an external tool is handling smoothing (smoothing a parent rotation, for example) where lerping
    /// position would result in an incorrect display position.
    /// Positions will not be linearly interpolated until SetPositionModelSpace is called again.
    /// </summary>
    /// <param name="newPositionModelSpace">The new position in model space.</param>
    /// <param name="newDisplayPositionModelSpace">The override position to display the mesh at.</param>
    public void SetPositionWithDisplayOverrideModelSpace(Vector3 newPositionModelSpace,
      Vector3 newDisplayPositionModelSpace) {
      _positionModelSpace = newPositionModelSpace;
      displayPositionModelSpace = newDisplayPositionModelSpace;
      preventDisplayPositionUpdate = true;
    }

    /// <summary>
    /// Sets the orientation in model space, optionally with smoothing.
    /// </summary>
    /// <param name="newOrientationModelSpace">The new orientation in model space.</param>
    /// <param name="smooth">True if a smoothing effect is desired. Note that smoothing is a purely visual effect.
    /// The actual orientation is instantaneously updated regardless of smoothing.</param>
    public void SetOrientationModelSpace(Quaternion newOrientationModelSpace, bool smooth = false) {
      _orientationModelSpace = newOrientationModelSpace;
      
      // Detect bad rotations when in editor, and log.
#if UNITY_EDITOR
      if (!util.Math3d.QuaternionIsValidRotation(newOrientationModelSpace)) {
        Debug.Log("Bad orientation in set orientation: " + newOrientationModelSpace);
      }
#endif

      // Create Slerpee for this mesh to manage interpolation if it doesn't exist.
      if (displayOrientationWrapperModelSpace == null) {
        displayOrientationWrapperModelSpace = new Slerpee(newOrientationModelSpace);
      }
      if (!smooth) {
        // Immediately update the display position as well.
        displayOrientationWrapperModelSpace.UpdateOrientationInstantly(newOrientationModelSpace);
      } else {
        // Otherwise set the target orientation for the slerp.
        displayOrientationWrapperModelSpace.StartOrUpdateSlerp(newOrientationModelSpace);
      }
    }

    public void SetOrientationWorldSpace(Quaternion newOrientationWorldSpace, bool smooth = false) {
      SetOrientationModelSpace(worldSpace.WorldOrientationToModel(newOrientationWorldSpace), smooth);
    }

    /// <summary>
    /// Sets the orientation in model space, with a display override (for when a tool is managing its own smoothing,
    /// ie, when the smoothing is being done on a parent transform).
    /// </summary>
    /// <param name="newOrientationModelSpace">The new orientation in model space.</param>
    /// <param name="newDisplayOrientationModelSpace">The orientation to display.</param>
    /// <param name="smooth">Whether to smooth transitions to and from the display orientation.
    /// This option is here primarily to smooth a transition into an override mode.</param>
    public void SetOrientationWithDisplayOverrideModelSpace(Quaternion newOrientationModelSpace,
      Quaternion newDisplayOrientationModelSpace, bool smooth) {
      _orientationModelSpace = newOrientationModelSpace;
      if (newOrientationModelSpace == null) {
        return;
      }

      // Detect bad rotations when in editor, and log.
#if UNITY_EDITOR
      if (!util.Math3d.QuaternionIsValidRotation(newOrientationModelSpace)) {
        Debug.Log("Bad orientation in set orientation: " + newOrientationModelSpace);
      }
#endif

      // Create Slerpee for this mesh to manage interpolation if it doesn't exist.
      if (displayOrientationWrapperModelSpace == null) {
        displayOrientationWrapperModelSpace = new Slerpee(newDisplayOrientationModelSpace);
      }
      if (smooth) {
        displayOrientationWrapperModelSpace.StartOrUpdateSlerp(newDisplayOrientationModelSpace);
      } else {
        displayOrientationWrapperModelSpace.UpdateOrientationInstantly(newDisplayOrientationModelSpace);
      }
    }

    /// <summary>
    /// Animates this object's displayed position from the given position to the current one.
    /// </summary>
    /// <param name="oldPosModelSpace">Old position, in the model space.</param>
    public void AnimatePositionFrom(Vector3 oldPosModelSpace) {
      displayPositionModelSpace = oldPosModelSpace;
    }

    /// <summary>
    /// Animates this object's displayed scale from the given scale to the default scale (1.0f).
    /// </summary>
    /// <param name="fromScale">Old scale factor.</param>
    public void AnimateScaleFrom(float fromScale) {
      smoothScale.AnimateScaleFrom(fromScale);
    }
   
  }
}