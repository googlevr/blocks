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

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.desktop_app {
  public class PreviewController : MonoBehaviour {
    class OpenFileDialogAndCreatePreview : BackgroundWork {
      private const string DIALOG_TITLE = "Choose a Reference Image";

      private readonly PreviewController previewController;

      private bool userCancelled;

      public OpenFileDialogAndCreatePreview(PreviewController controller) {
        previewController = controller;
      }

      public void BackgroundWork() {
#if UNITY_STANDALONE_WIN
        string selectedPath;
        if (Win32FileDialog.ShowWin32FileDialog(DIALOG_TITLE,
            Win32FileDialog.FilterType.IMAGE_FILES, out selectedPath)) {
          previewController.previewImagePath = selectedPath;
          previewController.loadNewPreviewImage = true;
        } else {
          userCancelled = true;
        }
#else
        Debug.LogError("Open file dialog not available in this platform.");
#endif
      }

      public void PostWork() {
        if (userCancelled) {
          previewController.ChangeMenuPrompt(showClickToInsert: true);
        }
      }
    }

    bool loadNewPreviewImage = false;
    string previewImagePath = null;

    void Update() {
      if (loadNewPreviewImage && previewImagePath != null) {
        PeltzerMain.Instance.referenceImageManager.InsertNewReferenceImage(previewImagePath);
        loadNewPreviewImage = false;
        previewImagePath = null;
        ChangeMenuPrompt(showClickToInsert: true);
      }
    }

    public void SelectPreviewImage() {
      BackgroundWork openDialog = new OpenFileDialogAndCreatePreview(this);
      PeltzerMain.Instance.DoFilePickerBackgroundWork(openDialog);
      ChangeMenuPrompt(showClickToInsert: false);
    }

    /// <summary>
    ///   Switches the menu prompt between telling a user to click to insert a reference image, or to
    ///   take off their headset to complete adding one.
    /// </summary>
    private void ChangeMenuPrompt(bool showClickToInsert) {
      ObjectFinder.ObjectById("ID_add_ref_image").SetActive(showClickToInsert);
      ObjectFinder.ObjectById("ID_take_off_headset_for_ref_image").SetActive(!showClickToInsert);
    }
  }
}
