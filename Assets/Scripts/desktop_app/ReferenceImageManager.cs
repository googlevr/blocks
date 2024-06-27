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

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools;

namespace com.google.apps.peltzer.client.desktop_app {
  /// <summary>
  /// Manages the reference images in the scene.
  /// </summary>
  public class ReferenceImageManager : MonoBehaviour {
    /// <summary>
    /// Next free reference image ID to assign.
    /// </summary>
    private int nextId = 0;

    /// <summary>
    /// Reference images currently in the scene. Keyed by ID.
    /// </summary>
    private Dictionary<int, MoveableReferenceImage> referenceImages = new Dictionary<int, MoveableReferenceImage>();

    private Queue<AddReferenceImageCommand> pendingReferenceImageCommands = new Queue<AddReferenceImageCommand>();

    public void Update() {
      if (PeltzerMain.Instance.restrictionManager.insertingReferenceImagesAllowed) {
        // If we have reference images waiting to be added and we aren't currently in the middle of inserting one we
        // will add one.
        if (pendingReferenceImageCommands.Count > 0 && !HasGrabbedReferenceImage()) {
          // Change to the "grab" tool so the user can click to place the new reference image in the scene.
          if (PeltzerMain.Instance.peltzerController.mode != ControllerMode.move) {
            PeltzerMain.Instance.peltzerController
              .ChangeMode(ControllerMode.move, ObjectFinder.ObjectById("ID_ToolGrab"));
          }

          // Do this as an AddReferenceImageCommmand so the user can undo this operation.
          PeltzerMain.Instance.GetModel().ApplyCommand(pendingReferenceImageCommands.Dequeue());
        }
      }
    }

    /// <summary>
    /// Creates a new reference image. It will be initially stuck to the controller until the user presses
    /// the trigger button to place it on the scene. If there is already a reference image being dragged
    /// by the controller, it will be replaced by the new one.
    /// </summary>
    /// <param name="previewImagePath"></param>
    public void InsertNewReferenceImage(string previewImagePath) {
      WWW www = new WWW("file:///" + System.Uri.EscapeUriString(previewImagePath));
      Texture2D texture = new Texture2D(500, 500);
      www.LoadImageIntoTexture(texture);

      MoveableReferenceImage.SetupParams setupParams = new MoveableReferenceImage.SetupParams();
      setupParams.attachToController = true;
      // Have the image start attached to the controller and right ahead of it.
      setupParams.positionModelSpace = PeltzerMain.Instance.peltzerController.LastPositionModel +
        PeltzerMain.Instance.peltzerController.LastRotationModel * Vector3.forward * MoveableObject.HOVER_DISTANCE;
      setupParams.rotationModelSpace = PeltzerMain.Instance.peltzerController.LastRotationModel;
      setupParams.scaleModelSpace = Vector3.one * 0.5f;
      setupParams.texture = texture;
      setupParams.refImageId = nextId++;
      setupParams.initialInsertion = true;

      pendingReferenceImageCommands.Enqueue(new AddReferenceImageCommand(setupParams));
    }

    /// <summary>
    /// Creates a reference image with the specified parameters.
    /// </summary>
    /// <param name="setupParams"></param>
    public void CreateReferenceImage(MoveableReferenceImage.SetupParams setupParams) {
      AssertOrThrow.True(!referenceImages.ContainsKey(setupParams.refImageId),
        "Duplicate reference image ID: " + setupParams.refImageId);
      GameObject imageObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
      imageObject.GetComponent<MeshCollider>().enabled = false;
      referenceImages[setupParams.refImageId] = imageObject.AddComponent<MoveableReferenceImage>();
      referenceImages[setupParams.refImageId].Setup(setupParams);
    }

    /// <summary>
    /// Deletes the given reference image.
    /// </summary>
    /// <param name="refImageId"></param>
    public void DeleteReferenceImage(int refImageId) {
      MoveableReferenceImage refImage;
      if (referenceImages.TryGetValue(refImageId, out refImage)) {
        referenceImages.Remove(refImageId);
        refImage.Destroy();
      }
    }

    /// <summary>
    /// Returns whether or not there is a grabbed reference image. There will be a grabbed image if the user is
    /// in the process of moving one around, either as a result of grabbing it directly, or because they have
    /// just inserted one and haven't placed it in the scene yet.
    /// </summary>
    /// <returns>True if there is a grabbed reference image, false if not.</returns>
    public bool HasGrabbedReferenceImage() {
      foreach (MoveableReferenceImage refImage in referenceImages.Values) {
        if (refImage.grabbed) return true;
      }
      return false;
    }

    private void DeleteAllGrabbedReferenceImages() {
      List<int> ids = new List<int>(referenceImages.Keys);
      foreach (int id in ids) {
        if (referenceImages[id].grabbed) {
          DeleteReferenceImage(id);
        }
      }
    }
  }
}
