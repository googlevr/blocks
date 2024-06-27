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

using System.Collections.Generic;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools;

namespace com.google.apps.peltzer.client.tutorial {
  /// <summary>
  /// IceCream tutorial. It teaches the user to insert primitives, move, and copy objects.
  /// </summary>
  public class IceCreamTutorial : Tutorial {
    private const string POLY_MODEL_RESOURCE_PATH = "Tutorial/IceCreamTutorial";
    private const int SCOOP_PLACEHOLDER_MESH_ID = 10000;
    private const int CHERRY_PLACEHOLDER_MESH_ID = 10001;
    private const int SCOOP_SCALE_DELTA = 5;  // the scale of the sphere for a scoop.
    private const int CHERRY_SCALE_DELTA = 0;  // the expected scale of the sphere for the cherry.
    private const float HAPTIC_PULSE_STRENGTH = 0.15f;
    private const float HAPTIC_PULSE_DURATION = 0.15f;
    private const float HAPTIC_PULSE_FREQUENCY = 30f;
    private const float SUCCESS_SOUND_MINIMUM_PITCH = 0.9f;
    private const float SUCCESS_SOUND_MAXIMUM_PITCH = 1.1f;
    private const float BUZZ_INTERVAL_DURATION = 1f;

    // Placeholder meshes: used to indicate where things should end up.
    private MMesh scoopPlaceHolder;
    private MMesh cherryPlaceHolder;

    // IDs of the inserted and cloned cherries.
    private int newCherryMeshId;
    private int clonedCherryMeshId;

    private static float hapticNotifyTime = 0f;

    // The pitch of the next success sound, and by how much to increment it.
    private float nextSuccessSoundPitch;
    private float successSoundPitchIncrement;

    public void PlaySuccessSound(bool playMinPitch = false) {
      float currentPitch = playMinPitch ? SUCCESS_SOUND_MINIMUM_PITCH : nextSuccessSoundPitch;

      AudioLibrary audioLibrary = PeltzerMain.Instance.audioLibrary;
      audioLibrary.PlayClip(audioLibrary.successSound, currentPitch);
      
      if (!playMinPitch) {
        nextSuccessSoundPitch += successSoundPitchIncrement;
      }
    }

  /// <summary>
  /// Prepares the tutorial.
  /// </summary>
  public override void OnPrepare() {
      PeltzerMain main = PeltzerMain.Instance;

      // Default the pitch correctly.
      nextSuccessSoundPitch = SUCCESS_SOUND_MINIMUM_PITCH;

      // Start with the shape tool.
      main.peltzerController.mode = ControllerMode.insertVolume;
      // Only allow the shape tool for now.
      main.restrictionManager.SetOnlyAllowedControllerMode(ControllerMode.insertVolume);
      // Allow shape selection.
      main.restrictionManager.shapesMenuAllowed = false;
      // Do not allow throw to delete objects.
      main.restrictionManager.throwAwayAllowed = false;
      // Do not allow save or new operations.
      main.restrictionManager.menuActionsAllowed = false;
      // Allow tutorial menu actions.
      main.restrictionManager.tutorialMenuActionsAllowed = true;
      // Do not allow changing colors.
      main.restrictionManager.changingColorsAllowed = false;
      main.restrictionManager.SetOnlyAllowedColor(-2);
      // Do not allow switching to the Poly menu.
      main.restrictionManager.menuSwitchAllowed = false;
      // Do not allow undo/redo until the multiselect step.
      main.restrictionManager.undoRedoAllowed = false;
      // Do not allow volume filling during insertion.
      main.restrictionManager.volumeFillingAllowed = false;
      main.restrictionManager.showingWorldBoundingBoxAllowed = false;

      // Do not allow snapping.
      main.restrictionManager.snappingAllowed = false;
      // Do not allow mesh movement.
      main.restrictionManager.movingMeshesAllowed = false;
      // Do not allow copying.
      main.restrictionManager.copyingAllowed = false;
      // Increase the selection radius for the whole tutorial.
      main.restrictionManager.increasedMultiSelectRadiusAllowed = true;
      main.restrictionManager.SetTouchpadAllowed(TouchpadLocation.NONE);

      // Allow touchpad highlighting/greying out.
      main.restrictionManager.SetTouchpadHighlightingAllowed(true);
      // Grey out everything to start.
      main.attentionCaller.GreyOutAll();
      main.attentionCaller.Recolor(AttentionCaller.Element.SIREN);
      main.attentionCaller.Recolor(AttentionCaller.Element.TUTORIAL_BUTTON);

      // Don't let toolheads on the palette change color once we've greyed out all the tools.
      main.restrictionManager.toolheadColorChangeAllowed = false;
      successSoundPitchIncrement = (SUCCESS_SOUND_MAXIMUM_PITCH - SUCCESS_SOUND_MINIMUM_PITCH) / steps.Count;

      PeltzerMain.Instance.GetFloatingMessage().ResetProgressBar();
      PeltzerMain.Instance.GetFloatingMessage().PositionBillboard();
      PeltzerMain.Instance.GetFloatingMessage().Show("Let's learn the basics!", TextPosition.CENTER);
      PeltzerMain.Instance.GetFloatingMessage().ShowHeader("");
      PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();

      PeltzerMain.Instance.GetFloatingMessage().ShowProgressBar(/*show*/ false);
    }

    /// <summary>
    /// Tutorial step where the user gets introduced to the tutorial.
    /// </summary>
    private class IntroductionStep : ITutorialStep {
      float endTime;
      float startMessageTime;
      float introTime = 1.5f;
      float startMessageDuration = 0.1f;
      IceCreamTutorial tutorial;

      public IntroductionStep(IceCreamTutorial tutorial) {
        this.tutorial = tutorial;
      }

      public void OnPrepare() {
        endTime = Time.time + introTime;
        startMessageTime = Time.time + startMessageDuration;
      }

      public bool OnCommand(Command command) {
        // No commands allowed in this step.
        return false;
      }

      public bool OnValidate() {
        if (endTime != 0f && Time.time > endTime) {
          return true;
        }

        if (startMessageTime != 0f && Time.time > startMessageTime) {
          PeltzerMain.Instance.GetFloatingMessage().Show("Who doesn't like\nice cream?", TextPosition.CENTER);
          // Load the broken ice cream scene (one ice cream with missing scoop & cherry).
          PeltzerMain.Instance.tutorialManager.LoadAndAlignTutorialModel(
            POLY_MODEL_RESOURCE_PATH,
            -PeltzerMain.Instance.GetFloatingMessage().billboard.transform.forward,
            PeltzerMain.Instance.GetFloatingMessage().billboard.transform.position);

          // Find the key objects we will need (objects the user will interact with and placeholders for
          // correct positions).
          tutorial.scoopPlaceHolder = PeltzerMain.Instance.model.GetMesh(SCOOP_PLACEHOLDER_MESH_ID);
          tutorial.cherryPlaceHolder = PeltzerMain.Instance.model.GetMesh(CHERRY_PLACEHOLDER_MESH_ID);

          // Set the material for the placeholder meshes.
          PeltzerMain.Instance.model.ChangeAllFaceProperties(SCOOP_PLACEHOLDER_MESH_ID,
            new FaceProperties(MaterialRegistry.PINK_WIREFRAME_ID));
          PeltzerMain.Instance.model.ChangeAllFaceProperties(CHERRY_PLACEHOLDER_MESH_ID,
            new FaceProperties(MaterialRegistry.PINK_WIREFRAME_ID));

          // Hide the placeholders for now (we'll bring them back later).
          PeltzerMain.Instance.model.HideMeshForTestOrTutorial(SCOOP_PLACEHOLDER_MESH_ID);
          PeltzerMain.Instance.model.HideMeshForTestOrTutorial(CHERRY_PLACEHOLDER_MESH_ID);

          // Set the colour of the shape tool to 'white'
          PeltzerMain.Instance.peltzerController.currentMaterial = MaterialRegistry.WHITE_ID;
          PeltzerMain.Instance.peltzerController.ChangeToolColor();

          startMessageTime = 0f;
        }

        return false;
      }

      public void ResetState() {
        startMessageTime = 0f;
        endTime = 0f;
      }

      public void OnFinish() {
        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to manipulate world-space.
    /// </summary>
    private class ZoomAndMoveStep : ITutorialStep {
      float nextMessageTime;
      float nextMessageDuration = 3.0f;
      float timeFirstStartedMoving = 0;
      float timeStoppedMoving = 0;
      bool wasMovingLastFrame = false;
      bool isShowingMoveZoomGIF = false;

      private IceCreamTutorial iceCreamTutorial;

      public ZoomAndMoveStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      public void OnPrepare() {
        // Show instructions.
        // TODO This instruction should be rewritten.
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("Moving");
        PeltzerMain.Instance.GetFloatingMessage().Show("Move the cone closer to you", TextPosition.FULL_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("MOVE");
        PeltzerMain.Instance.GetFloatingMessage().ShowProgressBar(/*show*/ true);
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PALETTE_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_GRIP_RIGHT);
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PALETTE_GRIP_RIGHT);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PALETTE_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_GRIP_RIGHT);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PALETTE_GRIP_RIGHT);

        nextMessageTime = Time.time + nextMessageDuration;
      }

      public bool OnCommand(Command command) {
        // No commands allowed in this step.
        return false;
      }

      public bool OnValidate() {
        // We'll wait for 3 seconds after the user starts moving, or until they've stopped zooming for 0.75 seconds.
        if ((timeStoppedMoving != 0 && Time.time - timeStoppedMoving > 0.75f)
          || (timeFirstStartedMoving != 0 && Time.time - timeFirstStartedMoving > 3f)) {
          PeltzerMain.Instance.GetFloatingMessage().ShowHeader("");
          PeltzerMain.Instance.GetFloatingMessage().Show("Looking good", TextPosition.CENTER_NO_TITLE, /*play confetti*/ true);
          PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
          return true;
        }

        if (PeltzerMain.Instance.Zoomer.moving) {
          if (timeFirstStartedMoving == 0) {
            timeFirstStartedMoving = Time.time;
          }
          wasMovingLastFrame = true;
        } else {
          if (wasMovingLastFrame) {
            timeStoppedMoving = Time.time;
          }
          wasMovingLastFrame = false;
        }

        return false;
      }

      public void ResetState() {
        isShowingMoveZoomGIF = false;
        timeFirstStartedMoving = 0;
        timeStoppedMoving = 0;
        wasMovingLastFrame = false;
      }

      public void OnFinish() {
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PALETTE_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_GRIP_RIGHT);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PALETTE_GRIP_RIGHT);

        // Hide the placeholder.
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PALETTE_GRIP_LEFT);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_GRIP_RIGHT);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PALETTE_GRIP_RIGHT);
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("Inserting");
        PeltzerMain.Instance.GetFloatingMessage().Show("How about another scoop?", TextPosition.CENTER);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        iceCreamTutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();

        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to select the sphere primitive.
    /// </summary>
    private class SelectSpherePrimitiveStep : ITutorialStep {
      private IceCreamTutorial tutorial;
      private float nextBuzzTime;

      public SelectSpherePrimitiveStep(IceCreamTutorial tutorial) {
        this.tutorial = tutorial;
      }

      public void OnPrepare() {
        // Set the default scale of the shape to be quite big (for the first scoop).
        PeltzerMain.Instance.GetVolumeInserter().SetScaleTo(SCOOP_SCALE_DELTA);
        // Move the volumeInserter over to the cylinder.
        PeltzerMain.Instance.peltzerController.shapesMenu
          .SetShapeMenuItem((int)Primitives.Shape.CYLINDER, /* showMenu */ false);

        PeltzerMain main = PeltzerMain.Instance;
        // Allow the touchpad's left button.
        main.restrictionManager.touchpadLeftAllowed = true;
        // Recolor the touchpad's left button.
        main.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_THUMBSTICK);
        main.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);
        main.attentionCaller.Recolor(
          main.peltzerController.controllerGeometry.volumeInserterOverlay.GetComponent<Overlay>().leftIcon);
        // Call attention to the touchpad's left button.
        main.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_THUMBSTICK);
        main.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);
        PeltzerMain.Instance.restrictionManager.shapesMenuAllowed = true;

        // Look at me.
        main.peltzerController.LookAtMe();
        nextBuzzTime = Time.time + BUZZ_INTERVAL_DURATION;

        // Disallow scaling.
        main.restrictionManager.scaleOnVolumeInsertionAllowed = false;
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show("Switch to a sphere shape", TextPosition.FULL_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("SELECT_SPHERE");
      }

      public bool OnCommand(Command command) {
        // No commands are allowed in this step (no model mutations).
        return false;
      }

      public bool OnValidate() {
        if (PeltzerMain.Instance.peltzerController.shapesMenu.CurrentItemId == (int)Primitives.Shape.CYLINDER
          && Time.time > nextBuzzTime) {
          PeltzerMain.Instance.peltzerController.LookAtMe();
          nextBuzzTime = Time.time + BUZZ_INTERVAL_DURATION;
        }

        // This step is done when the user selects the sphere primitive.
        return PeltzerMain.Instance.peltzerController.shapesMenu.CurrentItemId == (int)Primitives.Shape.SPHERE;
      }

      public void ResetState() {
        // Nothing to reset.
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        PeltzerMain.Instance.restrictionManager.shapesMenuAllowed = false;
        PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed = false;
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_THUMBSTICK);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);
        PeltzerMain.Instance.attentionCaller.GreyOut(
          PeltzerMain.Instance.peltzerController.controllerGeometry.volumeInserterOverlay.GetComponent<Overlay>().leftIcon);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_THUMBSTICK);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);
        // Congratulate the user on this outstanding victory.
        PeltzerMain.Instance.GetFloatingMessage().Show("Nice\nThat's the one", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        tutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to place an additional scoop.
    /// </summary>
    private class PlaceScoopStep : ITutorialStep {
      private const float DISTANCE_TOLERANCE = 0.025f;
      private IceCreamTutorial tutorial;
      private bool spherePlaced;
      private bool completed = false;

      public PlaceScoopStep(IceCreamTutorial tutorial) {
        this.tutorial = tutorial;
      }

      public void OnPrepare() {
        // Allow volume insertion so the user can place the body.
        PeltzerMain.Instance.restrictionManager.volumeInsertionAllowed = true;
        // Show the body placeholder to guide the user.
        PeltzerMain.Instance.model.UnhideMeshForTestOrTutorial(SCOOP_PLACEHOLDER_MESH_ID);
        // Recolor the volumeInserter toolhead since the user is going to use it to place the sphere.
        PeltzerMain.Instance.attentionCaller.Recolor(ControllerMode.insertVolume);
        // Call attention to trigger.
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show("Pile it on top", TextPosition.FULL_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("INSERT_SPHERE");
      }

      public bool OnCommand(Command command) {
        // Hack to allow us to delete the wireframe.
        if (completed && command is DeleteMeshCommand) {
          return true;
        }

        if (spherePlaced || !(command is AddMeshCommand)) {
          // Only adding meshes is allowed, and only while the body has not yet been placed.
          return false;
        }
        // Check if the mesh the user wants to add is at the correct position.
        float dist = Vector3.Distance(((AddMeshCommand)command).GetMeshClone().offset,
          tutorial.scoopPlaceHolder.offset);
        if (dist > DISTANCE_TOLERANCE) {
          PeltzerMain.Instance.GetFloatingMessage()
            .Show("Place it in the right spot", TextPosition.FULL_SIDE);
          return false;
        }
        spherePlaced = true;
        PeltzerMain.Instance.GetFloatingMessage().Show("That's better", TextPosition.FULL_SIDE, true);
        return true;
      }

      public bool OnValidate() {
        // This step is done when the user has placed the body at the right place.
        return spherePlaced;
      }
      
      public void ResetState() {
        spherePlaced = false;
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.restrictionManager.volumeInsertionAllowed = false;
        // Stop showing the placeholder mesh.
        PeltzerMain.Instance.model.HideMeshForTestOrTutorial(SCOOP_PLACEHOLDER_MESH_ID);
        completed = true;
        PeltzerMain.Instance.model.ApplyCommand(new DeleteMeshCommand(SCOOP_PLACEHOLDER_MESH_ID));
        PeltzerMain.Instance.GetFloatingMessage().Show("Now for the cherry\non top", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        tutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to scale down the sphere for a cherry.
    /// </summary>
    private class ScaleSphereStep : ITutorialStep {
      private bool encouragementShown;
      private float nextBuzzTime;

      private IceCreamTutorial iceCreamTutorial;

      public ScaleSphereStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      public void OnPrepare() {
        PeltzerMain main = PeltzerMain.Instance;
        // Allow primitive scaling, but nothing else.
        main.restrictionManager.scaleOnVolumeInsertionAllowed = true;
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show("That's too big\nSize it down", TextPosition.FULL_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("SMALLER_SPHERE");
        // Allow the touchpad's down button.
        main.restrictionManager.touchpadDownAllowed = true;
        main.restrictionManager.touchpadUpAllowed = false;
        // Call attention to the touchpad's up button.
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_THUMBSTICK);
        main.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TOUCHPAD_DOWN);
        PeltzerMain.Instance.attentionCaller.Recolor(
          PeltzerMain.Instance.peltzerController.controllerGeometry.volumeInserterOverlay.GetComponent<Overlay>().downIcon);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_THUMBSTICK);
        main.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TOUCHPAD_DOWN);

        // Look at me.
        main.peltzerController.LookAtMe();
      }

      public bool OnCommand(Command command) {
        // No commands are allowed in this step (no model mutations).
        return false;
      }

      public bool OnValidate() {
        if (PeltzerMain.Instance.GetVolumeInserter().scaleDelta == SCOOP_SCALE_DELTA
          && Time.time > nextBuzzTime) {
          PeltzerMain.Instance.peltzerController.LookAtMe();
          nextBuzzTime = Time.time + BUZZ_INTERVAL_DURATION;
        }

        if (!encouragementShown && PeltzerMain.Instance.GetVolumeInserter().scaleDelta < SCOOP_SCALE_DELTA) {
          PeltzerMain.Instance.GetFloatingMessage().Show("Keep on going", TextPosition.FULL_SIDE);
          encouragementShown = true;
        }
        // TODO add : You're almost there!

        // This step is done when the user has the right size selected.
        // We allow <= because if the user overshoots it with the continuous press, we don't care.
        // (the tutorial will still work).
        return PeltzerMain.Instance.GetVolumeInserter().scaleDelta <= CHERRY_SCALE_DELTA;
      }
      
      public void ResetState() {
        encouragementShown = false;
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        PeltzerMain.Instance.restrictionManager.scaleOnVolumeInsertionAllowed = false;
        PeltzerMain.Instance.restrictionManager.touchpadDownAllowed = false;
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_THUMBSTICK);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TOUCHPAD_DOWN);
        PeltzerMain.Instance.attentionCaller.GreyOut(
          PeltzerMain.Instance.peltzerController.controllerGeometry.volumeInserterOverlay.GetComponent<Overlay>().downIcon);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_THUMBSTICK);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TOUCHPAD_DOWN);
        PeltzerMain.Instance.GetFloatingMessage().Show("Perfect", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        iceCreamTutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to place an cherry.
    /// </summary>
    private class PlaceCherryStep : ITutorialStep {
      private const float DISTANCE_TOLERANCE = 0.03f;
      private IceCreamTutorial tutorial;
      private bool cherryPlaced;

      public PlaceCherryStep(IceCreamTutorial tutorial) {
        this.tutorial = tutorial;
      }

      public void OnPrepare() {
        // Allow volume insertion so the user can place the body.
        PeltzerMain.Instance.restrictionManager.volumeInsertionAllowed = true;
        // Show the body placeholder to guide the user.
        PeltzerMain.Instance.model.UnhideMeshForTestOrTutorial(CHERRY_PLACEHOLDER_MESH_ID);

        // Call attention to trigger.
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show("Place the cherry on top", TextPosition.FULL_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("INSERT_ANOTHER_SPHERE");
      }

      public bool OnCommand(Command command) {
        // Hack to allow deleting the wireframe.
        if (cherryPlaced && command is DeleteMeshCommand) {
          return true;
        }

        if (cherryPlaced || !(command is AddMeshCommand)) {
          // Only adding meshes is allowed, and only while the body has not yet been placed.
          return false;
        }
        // Check if the mesh the user wants to add is at the correct position.
        float dist = Vector3.Distance(((AddMeshCommand)command).GetMeshClone().offset,
          tutorial.cherryPlaceHolder.offset);
        if (dist > DISTANCE_TOLERANCE) {
          PeltzerMain.Instance.GetFloatingMessage().Show("Try again", TextPosition.FULL_SIDE);
          return false;
        }
        cherryPlaced = true;
        PeltzerMain.Instance.GetFloatingMessage().Show("", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        tutorial.newCherryMeshId = ((AddMeshCommand)command).GetMeshId();
        return true;
      }

      public bool OnValidate() {
        // This step is done when the user has placed the cherry at the right place.
        return cherryPlaced;
      }
      
      public void ResetState() {
        cherryPlaced = false;
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        PeltzerMain.Instance.attentionCaller.GreyOut(ControllerMode.insertVolume);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.restrictionManager.volumeInsertionAllowed = false;
        // Stop showing the placeholder mesh.
        PeltzerMain.Instance.model.HideMeshForTestOrTutorial(CHERRY_PLACEHOLDER_MESH_ID);
        PeltzerMain.Instance.model.ApplyCommand(new DeleteMeshCommand(CHERRY_PLACEHOLDER_MESH_ID));
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("");
        PeltzerMain.Instance.GetFloatingMessage()
          .Show("Let's paint it <color=#CD0000>RED</color>", TextPosition.CENTER_NO_TITLE);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        tutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    /// Tutorial step where the user is asked to switch to the paint tool.
    /// </summary>
    private class SwitchToPaintToolStep : ITutorialStep {
      private IceCreamTutorial iceCreamTutorial;
      private float nextBuzzTime;

      public SwitchToPaintToolStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      public void OnPrepare() {
        // Only allow volume insertion (current tool) and the paint tool.
        PeltzerMain.Instance.restrictionManager.SetAllowedControllerModes(
            new List<ControllerMode>() { ControllerMode.insertVolume, ControllerMode.paintMesh });
        // Call attention to trigger.
        PeltzerMain.Instance.attentionCaller.Recolor(ControllerMode.paintMesh);
        PeltzerMain.Instance.attentionCaller.StartGlowing(ControllerMode.paintMesh);
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("Painting");
        PeltzerMain.Instance.GetFloatingMessage().Show("Pick up the paint brush", TextPosition.BOTTOM);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("CHOOSE_PAINTBRUSH");
        // Set all the colors to be allowed so that they aren't greyed out. However changingColors is still disabled
        // so the user can't select them. We just want them to be colorful for the ripple animation.
        PeltzerMain.Instance.attentionCaller.RecolorAllColorSwatches();

        // Look at me.
        PeltzerMain.Instance.paletteController.LookAtMe();
      }

      public bool OnCommand(Command command) {
        // No model mutations for now, thank you very much.
        return false;
      }

      public bool OnValidate() {
        if (Time.time > nextBuzzTime) {
          PeltzerMain.Instance.paletteController.LookAtMe();
          nextBuzzTime = Time.time + BUZZ_INTERVAL_DURATION;
        }

        // This step is done when the user has selected the paint tool.
        return PeltzerMain.Instance.peltzerController.mode == ControllerMode.paintMesh;
      }

      public void ResetState() {
        // Nothing to reset.
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        // Only the paint tool is allowed from now on (so the user can't switch to a different one).
        PeltzerMain.Instance.restrictionManager.SetOnlyAllowedControllerMode(ControllerMode.paintMesh);
        // To not distract from the ripple animation, delay the confetti effect until the next step.
        iceCreamTutorial.PlaySuccessSound();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();
        PeltzerMain.Instance.attentionCaller.StopGlowing(ControllerMode.paintMesh);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
      }
    }

    /// <summary>
    /// Tutorial step where the user is asked to switch to the paint tool.
    /// </summary>
    private class SelectRedColorStep : ITutorialStep {
      private int previousMaterial;
      private float startRippleTime;
      private float rippleDuration = 2.5f;
      private ChangeMaterialMenuItem[] allColourSwatches;

      private IceCreamTutorial iceCreamTutorial;

      public SelectRedColorStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      public void OnPrepare() {
        allColourSwatches = PeltzerMain.Instance.paletteController.transform.GetComponentsInChildren<ChangeMaterialMenuItem>(true);

        // Allow color selection for this step.
        PeltzerMain.Instance.restrictionManager.SetOnlyAllowedColor(8);
        PeltzerMain.Instance.attentionCaller.GreyOutAllColorSwatches();
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.RED_PAINT_SWATCH);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.RED_PAINT_SWATCH);
        PeltzerMain.Instance.restrictionManager.changingColorsAllowed = true;
        // Disallow controller commands and mesh selection.
        PeltzerMain.Instance.restrictionManager.SetAllowedControllerModes(null);
        PeltzerMain.Instance.restrictionManager.onlySelectableMeshIdForTutorial = -1;
        // Call attention to trigger.
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        // Make note of the currently selected material to be able to detect when a new
        // color is chosen.
        previousMaterial = PeltzerMain.Instance.peltzerController.currentMaterial;

        PeltzerMain.Instance.GetPainter().StartFullRipple();
        startRippleTime = Time.time + rippleDuration;

        // Show instructions, and play confetti effect this step.
        PeltzerMain.Instance.GetFloatingMessage().Show("Flip the palette to select <color=#CD0000><b>RED</b></color>", TextPosition.BOTTOM);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("CHOOSE_COLOR");
      }

      public bool OnCommand(Command command) {
        // No model mutations for now, thank you very much.
        return false;
      }

      public bool OnValidate() {
        if (Time.time > startRippleTime) {
          PeltzerMain.Instance.GetPainter().StartFullRipple();
          startRippleTime = Time.time + rippleDuration;
        }

        // If the user selects a color other than red (or red orange), display an error message.
        if (PeltzerMain.Instance.peltzerController.currentMaterial != MaterialRegistry.RED_ID &&
          PeltzerMain.Instance.peltzerController.currentMaterial != MaterialRegistry.DEEP_ORANGE_ID &&
          PeltzerMain.Instance.peltzerController.currentMaterial != previousMaterial) {
          PeltzerMain.Instance.GetFloatingMessage().Show("That's not <color=#CD0000><b>RED</b></color>", TextPosition.BOTTOM);
          return false;
        }

        // This step is done when the user has selected the red colour.
        return PeltzerMain.Instance.peltzerController.currentMaterial == MaterialRegistry.RED_ID
          || PeltzerMain.Instance.peltzerController.currentMaterial == MaterialRegistry.DEEP_ORANGE_ID;
      }
      
      public void ResetState() {
        // Nothing to reset.
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        // Only the paint tool is allowed from now on (so the user can't switch to a different one).
        PeltzerMain.Instance.restrictionManager.SetOnlyAllowedControllerMode(ControllerMode.paintMesh);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.restrictionManager.changingColorsAllowed = false;
        PeltzerMain.Instance.restrictionManager.SetOnlyAllowedColor(-2);
        PeltzerMain.Instance.attentionCaller.GreyOutAllColorSwatches();
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.RED_PAINT_SWATCH);
        PeltzerMain.Instance.GetFloatingMessage().Show("Nice", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        iceCreamTutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to paint the newly-inserted cherry.
    /// </summary>
    private class PaintCherryStep : ITutorialStep {
      private IceCreamTutorial tutorial;
      private bool cherryPainted;

      public PaintCherryStep(IceCreamTutorial tutorial) {
        this.tutorial = tutorial;
      }

      public void OnPrepare() {
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show("Paint the cherry <color=#CD0000><b>RED</b></color>", TextPosition.HALF_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("PAINT_COLOR");
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartMeshGlowing(tutorial.newCherryMeshId);
        PeltzerMain.Instance.restrictionManager.onlySelectableMeshIdForTutorial = tutorial.newCherryMeshId;
      }

      public bool OnCommand(Command command) {
        if (cherryPainted) {
          return false;
        }
        ChangeFacePropertiesCommand paintCommand = null;

        if (command is ChangeFacePropertiesCommand) {
          // If there is a single command, check it paints the cherry.
          paintCommand = (ChangeFacePropertiesCommand)command;
          if (paintCommand.GetMeshId() != tutorial.newCherryMeshId) {
            return false;
          }
        } else if (command is CompositeCommand) {
          // Else if there is a list of commands, check that at least one of them paints the cherry.
          List<Command> compositeCommandEntries = ((CompositeCommand)command).GetCommands();
          foreach (Command compositeCommandEntry in compositeCommandEntries) {
            if (compositeCommandEntry is ChangeFacePropertiesCommand) {
              paintCommand = (ChangeFacePropertiesCommand)compositeCommandEntry;
              if (paintCommand.GetMeshId() != tutorial.newCherryMeshId) {
                paintCommand = null;
                continue;
              }
            }
          }
          if (paintCommand == null) {
            return false;
          }
        }

        cherryPainted = true;
        return true;
      }

      public bool OnValidate() {
        // Make sure the new cherry is always glowing to call attention to it.
        PeltzerMain.Instance.attentionCaller.MakeSureMeshIsGlowing(tutorial.newCherryMeshId);
        return cherryPainted;
      }
      
      public void ResetState() {
        cherryPainted = false;
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        // Hide the placeholder.
        PeltzerMain.Instance.attentionCaller.GreyOut(ControllerMode.paintMesh);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopMeshGlowing(tutorial.newCherryMeshId);
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("");
        PeltzerMain.Instance.GetFloatingMessage()
          .Show("Your friend wants one\nLet's make a copy", TextPosition.CENTER_NO_TITLE, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        tutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    /// Tutorial step where the user is asked to switch to the grab tool.
    /// </summary>
    private class SwitchToGrabToolStep : ITutorialStep {
      private IceCreamTutorial iceCreamTutorial;
      private float nextBuzzTime;

      public SwitchToGrabToolStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      public void OnPrepare() {
        // Only allow paint (current tool) and the move tool.
        PeltzerMain.Instance.restrictionManager.SetAllowedControllerModes(
            new List<ControllerMode>() { ControllerMode.paintMesh, ControllerMode.move });
        // Call attention to the trigger.
        PeltzerMain.Instance.attentionCaller.Recolor(ControllerMode.move);
        PeltzerMain.Instance.attentionCaller.StartGlowing(ControllerMode.move);
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("Copying");
        PeltzerMain.Instance.GetFloatingMessage().Show("Choose the grab tool", TextPosition.BOTTOM);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("CHOOSE_GRAB");

        // Look at me.
        PeltzerMain.Instance.paletteController.LookAtMe();
      }

      public bool OnCommand(Command command) {
        // No model mutations for now, thank you very much.
        return false;
      }

      public bool OnValidate() {
        if (Time.time > nextBuzzTime) {
          PeltzerMain.Instance.paletteController.LookAtMe();
          nextBuzzTime = Time.time + BUZZ_INTERVAL_DURATION;
        }

        // This step is done when the user has selected the move tool.
        return PeltzerMain.Instance.peltzerController.mode == ControllerMode.move;
      }

      public void ResetState() {
        // Nothing to reset.
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.paletteController.YouDidIt();

        // Only the move tool is allowed from now on (so the user can't switch to a different one).
        PeltzerMain.Instance.restrictionManager.SetOnlyAllowedControllerMode(ControllerMode.move);
        PeltzerMain.Instance.GetFloatingMessage().Show("First select everything", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        iceCreamTutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();
        PeltzerMain.Instance.attentionCaller.StopGlowing(ControllerMode.move);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to multi-select the everything (the entire cone).
    /// </summary>
    private class MultiSelectEverythingStep : ITutorialStep {
      private static string DEFAULT_INSTRUCTION =
        "Click then drag through shapes";
      bool triggerDown;
      int numMeshes;
      bool userTriedAndFailed;

      private IceCreamTutorial iceCreamTutorial;

      public MultiSelectEverythingStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      public void OnPrepare() {
        PeltzerMain main = PeltzerMain.Instance;
        numMeshes = main.model.GetNumberOfMeshes();
        // Call attention to the trigger.
        main.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        main.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);

        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show(DEFAULT_INSTRUCTION, TextPosition.HALF_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("MULTISELECT");
        PeltzerMain.Instance.restrictionManager.onlySelectableMeshIdForTutorial = null;
        PeltzerMain.Instance.controllerMain.ControllerActionHandler += OnControllerEvent;
        triggerDown = false;
      }

      private void OnControllerEvent(object sender, ControllerEventArgs args) {
        if (args.ControllerType == ControllerType.PELTZER
          && args.ButtonId == ButtonId.Trigger
          && args.Action == ButtonAction.DOWN) {
          triggerDown = true;
        }

        if (args.ControllerType == ControllerType.PELTZER
          && args.ButtonId == ButtonId.Trigger
          && args.Action == ButtonAction.UP) {
          triggerDown = false;
        }
      }

      public bool OnCommand(Command command) {
        // No commands are allowed in this step (no model mutations).
        return false;
      }

      public bool OnValidate() {
        Selector selector = PeltzerMain.Instance.GetSelector();
        List<int> selectedMeshIds = new List<int>(selector.selectedMeshes);

        if (selector.isMultiSelecting) {
          if (selectedMeshIds.Count == numMeshes) {
            // Success!  Stop them from being able to deselect.
            PeltzerMain.Instance.restrictionManager.deselectAllowed = false;
            return true;
          } else if (selectedMeshIds.Count > 0) {
            // They didn't get everything yet but are still multi-selecting.
          }
        } else if (selectedMeshIds.Count > 0){
          // They've stopped multi-selecting. If the user doesn't have all the ice cream cone clear the selection.
          selector.ClearState();
          // Play error sound.
          // Play error haptics.
          PeltzerMain.Instance.GetFloatingMessage().Show("Missed some\nTry again", TextPosition.HALF_SIDE);
          userTriedAndFailed = true;
        }

        if (triggerDown) {
          PeltzerMain.Instance.GetFloatingMessage().Show("Wave through everything", TextPosition.HALF_SIDE);
          userTriedAndFailed = false;
        } else if (!userTriedAndFailed){
          PeltzerMain.Instance.GetFloatingMessage().Show(DEFAULT_INSTRUCTION, TextPosition.HALF_SIDE);
        }

        return false;
      }
      
      public void ResetState() {
        triggerDown = false;
        userTriedAndFailed = false;
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        // Disallow undo/redo.
        PeltzerMain.Instance.restrictionManager.undoRedoAllowed = false;
        // Congratulate the user on this outstanding victory.
        PeltzerMain.Instance.GetFloatingMessage().Show("Now that everything is selected let's copy it", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        iceCreamTutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    // TODO could add a 'group' step here, then ungroup to remove the cherry.

    /// <summary>
    /// Tutorial step where the user is instructed to copy the ice cream. They can place the new cone anywhere.
    /// </summary>
    private class CopyIceCreamStep : ITutorialStep {
      private IceCreamTutorial tutorial;
      private bool copyHappened;
      private bool waitingForTriggerDown = false;
      private bool triggerHasBeenPressed = false;
      private float nextBuzzTime;

      public CopyIceCreamStep(IceCreamTutorial tutorial) {
        this.tutorial = tutorial;
      }

      private void OnControllerEvent(object sender, ControllerEventArgs args) {
        if (waitingForTriggerDown
          && args.ControllerType == ControllerType.PELTZER
          && args.ButtonId == ButtonId.Trigger
          && args.Action == ButtonAction.DOWN) {
          triggerHasBeenPressed = true;
        }
      }

      public void OnPrepare() {
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show("Copy the cone", TextPosition.FULL_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("COPY");

        PeltzerMain.Instance.restrictionManager.copyingAllowed = true;

        // Allow the touchpad's left button.
        PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed = true;
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_THUMBSTICK);
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);
        PeltzerMain.Instance.attentionCaller.Recolor(
          PeltzerMain.Instance.peltzerController.controllerGeometry.moveOverlay.GetComponent<Overlay>().leftIcon);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_THUMBSTICK);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);
        PeltzerMain.Instance.controllerMain.ControllerActionHandler += OnControllerEvent;

        // Look at me.
        PeltzerMain.Instance.peltzerController.LookAtMe();
      }

      public bool OnCommand(Command command) {
        if (copyHappened) {
          return false;
        }

        // We expect a CompositeCommand. We require that it contains at least a copy of the cherry as the minimal
        // requirement to be able to continue the tutorial (rather than requiring everything, and risking some edge
        // case blocking the tutorial.
        if (!(command is CompositeCommand)) {
          return false;
        }
        CompositeCommand compositeCommand = (CompositeCommand)command;
        foreach (Command compositeCommandEntry in compositeCommand.GetCommands()) {
          if (compositeCommandEntry is CopyMeshCommand) {
            PeltzerMain.Instance.restrictionManager.deselectAllowed = true;
            CopyMeshCommand copyMeshCommand = (CopyMeshCommand)compositeCommandEntry;
            if (copyMeshCommand.copiedFromId == tutorial.newCherryMeshId) {
              tutorial.clonedCherryMeshId = copyMeshCommand.GetCopyMeshId();
              copyHappened = true;
              return true;
            }
          }
        }
        return false;
      }

      public bool OnValidate() {
        bool copyButtonPressed = PeltzerMain.Instance.GetMover().currentMoveType == Mover.MoveType.CLONE;

        if (!copyButtonPressed && Time.time > nextBuzzTime) {
          PeltzerMain.Instance.peltzerController.LookAtMe();
          nextBuzzTime = Time.time + BUZZ_INTERVAL_DURATION;
        }

        if (copyButtonPressed && !waitingForTriggerDown) {
          // Allow the touchpad's left button.
          PeltzerMain.Instance.restrictionManager.touchpadLeftAllowed = false;
          PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_THUMBSTICK);
          PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);
          PeltzerMain.Instance.attentionCaller.GreyOut(
            PeltzerMain.Instance.peltzerController.controllerGeometry.moveOverlay.GetComponent<Overlay>().leftIcon);
          PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_THUMBSTICK);
          PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TOUCHPAD_LEFT);

          PeltzerMain.Instance.GetFloatingMessage().Show("Place it next to the original", TextPosition.FULL_SIDE);
          PeltzerMain.Instance.GetFloatingMessage().ShowGIF("INSERT_SPHERE");
          // Call attention to trigger.
          PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
          PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
          waitingForTriggerDown = true;
        }

        return copyHappened && (waitingForTriggerDown && triggerHasBeenPressed);
      }
      
      public void ResetState() {
        copyHappened = false;
        waitingForTriggerDown = false;
        triggerHasBeenPressed = false;
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();
        PeltzerMain.Instance.restrictionManager.copyingAllowed = false;
        PeltzerMain.Instance.restrictionManager.movingMeshesAllowed = false;
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("");
        PeltzerMain.Instance.GetFloatingMessage().Show("Your friend doesn't want a cherry", TextPosition.CENTER_NO_TITLE, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        tutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.attentionCaller.GreyOut(ControllerMode.move);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);

        // Stop the user from selecting anything now.
        PeltzerMain.Instance.restrictionManager.onlySelectableMeshIdForTutorial = -1;
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    /// Tutorial step where the user is asked to switch to the delete tool.
    /// </summary>
    private class SwitchToDeleteToolStep : ITutorialStep {
      private IceCreamTutorial iceCreamTutorial;
      private float nextBuzzTime;

      public SwitchToDeleteToolStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      public void OnPrepare() {
        // Only allow move (current tool) and the delete tool.
        PeltzerMain.Instance.restrictionManager.SetAllowedControllerModes(
            new List<ControllerMode>() { ControllerMode.move, ControllerMode.delete });
        PeltzerMain.Instance.attentionCaller.Recolor(ControllerMode.delete);
        PeltzerMain.Instance.attentionCaller.StartGlowing(ControllerMode.delete);
        // Call attention to trigger.
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("Erasing");
        PeltzerMain.Instance.GetFloatingMessage().Show("Grab the eraser", TextPosition.BOTTOM);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("CHOOSE_ERASER");

        // Look at me.
        PeltzerMain.Instance.paletteController.LookAtMe();
      }

      public bool OnCommand(Command command) {
        // No model mutations for now, thank you very much.
        return false;
      }

      public bool OnValidate() {
        if (Time.time > nextBuzzTime) {
          PeltzerMain.Instance.paletteController.LookAtMe();
          nextBuzzTime = Time.time + BUZZ_INTERVAL_DURATION;
        }

        // This step is done when the user has selected the delete tool.
        return PeltzerMain.Instance.peltzerController.mode == ControllerMode.delete;
      }

      public void ResetState() {
        // Nothing to reset.
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.paletteController.YouDidIt();

        // Only the delete tool is allowed from now on (so the user can't switch to a different one).
        PeltzerMain.Instance.restrictionManager.SetOnlyAllowedControllerMode(ControllerMode.delete);
        PeltzerMain.Instance.attentionCaller.StopGlowing(ControllerMode.delete);
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.GetFloatingMessage().Show("Let's get rid of it", TextPosition.CENTER, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        iceCreamTutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();
      }
    }

    /// <summary>
    /// Tutorial step where the user is instructed to delete the second cherry.
    /// </summary>
    private class DeleteSecondCherryStep : ITutorialStep {
      private IceCreamTutorial tutorial;
      private bool deletedCherry;

      public DeleteSecondCherryStep(IceCreamTutorial tutorial) {
        this.tutorial = tutorial;
      }

      public void OnPrepare() {
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().Show("Erase the\nnew cherry", TextPosition.HALF_SIDE);
        PeltzerMain.Instance.GetFloatingMessage().ShowGIF("ERASE");
        PeltzerMain.Instance.attentionCaller.Recolor(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StartMeshGlowing(tutorial.clonedCherryMeshId);
        PeltzerMain.Instance.restrictionManager.onlySelectableMeshIdForTutorial = tutorial.clonedCherryMeshId;
      }

      // Only allow a DeleteMesh command on the cherry, which is sufficient but not necessary for this
      // step.
      // We want the tutorial to complete at the point the user sees the cherry disappear, which will
      // be before the command is sent, as the delete tool waits until the trigger is released to issue commands.
      public bool OnCommand(Command command) {
        if (deletedCherry) {
          return false;
        }

        // We expect a CompositeCommand. We require that it contains at least a copy of the cherry as the minimal
        // requirement to be able to continue the tutorial (rather than requiring everything, and risking some edge
        // case blocking the tutorial.
        if (!(command is CompositeCommand)) {
          return false;
        }
        CompositeCommand compositeCommand = (CompositeCommand)command;
        foreach (Command compositeCommandEntry in compositeCommand.GetCommands()) {
          if (compositeCommandEntry is DeleteMeshCommand) {
            DeleteMeshCommand deleteMeshCommand = (DeleteMeshCommand)compositeCommandEntry;
            if (deleteMeshCommand.MeshId == tutorial.clonedCherryMeshId) {
              deletedCherry = true;
              return true;
            }
          }
        }
        return false;
      }

      public bool OnValidate() {
        if (deletedCherry || !PeltzerMain.Instance.model.HasMesh(tutorial.clonedCherryMeshId) ||
          PeltzerMain.Instance.model.MeshIsMarkedForDeletion(tutorial.clonedCherryMeshId)) {
          return true;
        }
        // Make sure the cherry stays glowing.
        PeltzerMain.Instance.attentionCaller.MakeSureMeshIsGlowing(tutorial.clonedCherryMeshId);
        return false;
      }
      
      public void ResetState() {
        deletedCherry = false;
      }

      public void OnFinish() {
        // You did it!
        PeltzerMain.Instance.peltzerController.YouDidIt();

        PeltzerMain.Instance.attentionCaller.GreyOut(ControllerMode.delete);
        // Hide the placeholder.
        PeltzerMain.Instance.attentionCaller.GreyOut(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopGlowing(AttentionCaller.Element.PELTZER_TRIGGER);
        PeltzerMain.Instance.attentionCaller.StopMeshGlowing(tutorial.clonedCherryMeshId);
        PeltzerMain.Instance.GetFloatingMessage().ShowHeader("");
        PeltzerMain.Instance.GetFloatingMessage().Show("That's better", TextPosition.CENTER_NO_TITLE, true);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
        tutorial.PlaySuccessSound();
        PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.SIREN,
          PeltzerMain.Instance.attentionCaller.defaultSirenGlow);
        PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs();
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();

        // Reset state variables.
        ResetState();
      }
    }

    /// <summary>
    ///   End of tutorial -- display a 'finished' message for a short while then time out.
    /// </summary>
    private class EndTutorialStep : ITutorialStep {
      private IceCreamTutorial iceCreamTutorial;

      public EndTutorialStep(IceCreamTutorial iceCreamTutorial) {
        this.iceCreamTutorial = iceCreamTutorial;
      }

      float startTime = 0.0f;
      float congratsTime;
      float congratsDuration = 0.1f;
      // How long we should wait on this step; very little time because the end animation is ~4
      // seconds and begins when this step is over.
      float duration = 2f;

      public void OnPrepare() {
        // Show instructions.
        PeltzerMain.Instance.GetFloatingMessage().IncrementProgressBar();
        PeltzerMain.Instance.GetFloatingMessage().Show("", TextPosition.CENTER);
        PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();

        // Re-enable menu actions.
        PeltzerMain.Instance.restrictionManager.menuActionsAllowed = true;
        startTime = Time.time;
        congratsTime = Time.time + congratsDuration;
      }

      public bool OnCommand(Command command) {
        // No commands allowed in this step.
        return false;
      }

      public bool OnValidate() {
        // This step is complete after a certain time duration.
        if (Time.time > startTime + duration) {
          return true;
        }

        if (congratsTime != 0f && Time.time > congratsTime) {
          PeltzerMain.Instance.GetFloatingMessage().Show("Congrats!\nYou've got the basics", TextPosition.CENTER_NO_TITLE);
          PeltzerMain.Instance.GetFloatingMessage().ShowProgressBar(/*show*/ false);
          PeltzerMain.Instance.GetFloatingMessage().PlayFinalConfetti();
          PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.tutorialCompletionSound);
          PeltzerMain.Instance.attentionCaller.GlowTheSiren();
          PeltzerMain.Instance.attentionCaller.CascadeGlowAllLightbulbs(/*duration*/ 10f);
          congratsTime = 0f;
        }

        return false;
      }

      public void ResetState() {
        // Nothing to reset.
      }

      public void OnFinish() {
        // Play an extra-successful sound for the final step.
        PeltzerMain.Instance.GetFloatingMessage().Show("Can't wait to see\nwhat you make!", TextPosition.CENTER_NO_TITLE);
        // No finish actions; this step times out and is succeeded by 'ExitTutorial' cleanup.
      }
    }

    public IceCreamTutorial() {
      AddStep(new IntroductionStep(this));
      AddStep(new ZoomAndMoveStep(this));
      AddStep(new SelectSpherePrimitiveStep(this));
      AddStep(new PlaceScoopStep(this));
      AddStep(new ScaleSphereStep(this));
      AddStep(new PlaceCherryStep(this));
      AddStep(new SwitchToPaintToolStep(this));
      AddStep(new SelectRedColorStep(this));
      AddStep(new PaintCherryStep(this));
      AddStep(new SwitchToGrabToolStep(this));
      AddStep(new MultiSelectEverythingStep(this));
      AddStep(new CopyIceCreamStep(this));
      AddStep(new SwitchToDeleteToolStep(this));
      AddStep(new DeleteSecondCherryStep(this));
      AddStep(new EndTutorialStep(this));
    }
  }
}
