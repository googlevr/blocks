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
using System;
using System.Collections;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.export
{
    /// <summary>
    ///   A camera script that auto-positions itself with a good view of a model, takes a picture of the model,
    ///   and returns it as the bytes of a PNG file.
    /// </summary>
    public class AutoThumbnailCamera : MonoBehaviour
    {
        // Multiplier for the distance to move the camera.
        private const float DISTANCE_SCALER = 2.5f;

        // Field of view of the camera (a too-large value might prevent small models from showing up).
        private const float FIELD_OF_VIEW = 60.0f;

        // Background color for the thumbnail image.
        private static readonly Color THUMBNAIL_BACKGROUND_COLOR = new Color(0.93f, 0.93f, 0.93f);

        private Camera thumbnailCamera;

        /// <summary>
        ///   Turn off the camera by default.
        /// </summary>
        void Start()
        {
            thumbnailCamera = GetComponent<Camera>();
            thumbnailCamera.fieldOfView = FIELD_OF_VIEW;
            thumbnailCamera.clearFlags = CameraClearFlags.SolidColor;
            thumbnailCamera.backgroundColor = THUMBNAIL_BACKGROUND_COLOR;
            thumbnailCamera.gameObject.SetActive(false);
        }

        /// <summary>
        ///   Positions ThumbnailCamera to get an appropriate view of the model.
        /// </summary>
        void PositionCamera()
        {
            // Reposition camera to view the complete bounding box of the model.
            Bounds modelBounds = PeltzerMain.Instance.model.FindBoundsOfAllMeshes();
            modelBounds.center = PeltzerMain.Instance.worldSpace.ModelToWorld(modelBounds.center);
            modelBounds.size = PeltzerMain.Instance.worldSpace.scale * modelBounds.size;

            float distance = Mathf.Max(modelBounds.size.x, modelBounds.size.y, modelBounds.size.z);
            distance /= (2.0f * Mathf.Tan(0.5f * thumbnailCamera.fieldOfView * Mathf.Deg2Rad));
            thumbnailCamera.transform.position = modelBounds.center - distance * Vector3.forward * DISTANCE_SCALER;

            // Look towards the center of the model.
            Vector3 relativePos = modelBounds.center - transform.position;
            Quaternion rotation = Quaternion.LookRotation(relativePos);
            thumbnailCamera.transform.rotation = rotation;
        }

        /// <summary>
        ///   Takes a screenshot at the end of the current frame (such that everything on LateUpdate has been drawn), 
        ///   and then calls a callback with the PNG bytes of the screenshot/
        /// </summary>
        public IEnumerator TakeScreenShot(Action<byte[]> callback)
        {
            // Wait.
            yield return new WaitForEndOfFrame();

            // Disable the environment and terrain so we don't get them on the screenshot.
            GameObject envObj = ObjectFinder.ObjectById("ID_Environment");
            GameObject terrain = ObjectFinder.ObjectById("ID_TerrainLift");
            envObj.SetActive(false);
            terrain.SetActive(false);

            // Activate the camera, and render to a texture. Zandria requires a 576x432px image per bug.
            thumbnailCamera.gameObject.SetActive(true);
            PositionCamera();

            RenderTexture renderTexture = new RenderTexture(576, 432, 24);
            thumbnailCamera.targetTexture = renderTexture;

            // Placeholder to save and then later restore whatever is the current active RenderTexture. This is
            // necessary to make sure the view of the thumbnailCamera is rendered and not main camera, which is
            // what RenderTexture.active defaults to.
            RenderTexture activeRender = RenderTexture.active;
            RenderTexture.active = thumbnailCamera.targetTexture;
            thumbnailCamera.Render();

            // Save to an image.
            Texture2D imageOverview = new Texture2D(thumbnailCamera.targetTexture.width, thumbnailCamera.targetTexture.height, TextureFormat.RGB24, false);
            imageOverview.ReadPixels(new Rect(0, 0, thumbnailCamera.targetTexture.width, thumbnailCamera.targetTexture.height), 0, 0);
            imageOverview.Apply();
            byte[] bytes = imageOverview.EncodeToPNG();

            // Deactivate the camera again.
            thumbnailCamera.gameObject.SetActive(false);
            RenderTexture.active = activeRender;

            // Re-enable the environment and terrain.
            envObj.SetActive(true);

            // Encode texture into PNG and callback.
            callback(bytes);
        }
    }
}
