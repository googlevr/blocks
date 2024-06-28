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
using System.Linq;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.tools;

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    /// A reference image is an image that's on the scene to help the user create their model. The user creates these
    /// by clicking the Add Reference Image button on the desktop app. A reference image exists in MODEL SPACE, so it's
    /// "moves" with the model when the user pans/zooms/rotates.
    ///
    /// The reference image can be moved, scaled and deleted like a Poly mesh, even though it isn't really Poly mesh.
    ///
    /// The image is NOT part of the model and doesn't get saved with it. It only exists for reference during the
    /// session.
    /// </summary>
    public class MoveableReferenceImage : MoveableObject
    {
        public static readonly string REFERENCE_IMAGE_NAME_PREFIX = "Reference Image ";

        /// <summary>
        /// Parameters indicating how to create a reference image.
        /// </summary>
        public struct SetupParams
        {
            /// <summary>
            /// ID of the reference image.
            /// </summary>
            public int refImageId;
            /// <summary>
            /// Texture that represents the reference image.
            /// </summary>
            public Texture2D texture;
            /// <summary>
            /// If true, the image moves with the controller (starts in the grabbed state)
            /// until the user clicks the trigger to place it down.
            /// </summary>
            public bool attachToController;
            /// <summary>
            /// Position of the image in model space.
            /// </summary>
            public Vector3 positionModelSpace;
            /// <summary>
            /// Rotation of the image in model space.
            /// </summary>
            public Quaternion rotationModelSpace;
            /// <summary>
            /// Scale of the image in model space.
            /// </summary>
            public Vector3 scaleModelSpace;
            /// <summary>
            /// Whether this is the initial insertion of the image (and hence undo should reattach it to the controller)
            /// </summary>
            public bool initialInsertion;
        }

        /// <summary>
        /// Minimum scale of the reference image.
        /// </summary>
        private const float SCALE_MIN = 0.2f;

        /// <summary>
        /// Maximum scale of the reference image.
        /// </summary>
        private const float SCALE_MAX = 4.0f;

        /// <summary>
        /// Scale increment (by how much the scale changes at every click).
        /// </summary>
        private const float SCALE_INCREMENT = 0.1f;

        /// <summary>
        /// Texture that represents the image.
        /// </summary>
        public Texture2D referenceImageTexture { get; set; }

        /// <summary>
        /// ID of the reference image. Reference images have unique IDs.
        /// </summary>
        public int referenceImageId { get; set; }

        private bool initialInsertion;

        public void Setup(SetupParams setupParams)
        {
            base.Setup();
            referenceImageTexture = setupParams.texture;
            referenceImageId = setupParams.refImageId;
            float halfAspect = (setupParams.texture.width / (float)setupParams.texture.height) * .5f;
            initialInsertion = setupParams.initialInsertion;
            gameObject.name = REFERENCE_IMAGE_NAME_PREFIX + setupParams.refImageId;

            mesh = GetComponent<MeshFilter>().mesh;
            // Unity has a somewhat odd default vertex order, so this code is:
            //    * Resizing the quad so it has the same aspect ratio as the image.
            //    * Reordering the vertices so that they are clockwise and can be used in our mesh math.
            mesh.SetVertices(new List<Vector3>() {
          new Vector3(-halfAspect, -.5f),
          new Vector3(-halfAspect, .5f),
          new Vector3(halfAspect, .5f),
          new Vector3(halfAspect, -.5f)
        });
            mesh.SetUVs(0, new List<Vector2>() {
          new Vector2(0, 0),
          new Vector2(0, 1.0f),
          new Vector2(1.0f, 1.0f),
          new Vector2(1.0f, 0)
        });
            // Add both forward-facing and backward-facing triangles so the reference image can be seen from both sides.
            mesh.SetTriangles(new int[] {
        // Front-facing triangles:
        0, 1, 2,
        0, 2, 3,
        // Back-facing triangles:
        2, 1, 0,
        3, 2, 0}, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            // Prepare the material that we will use to render the reference image. We clone the default
            // Unity material and set the shader to Unlit/Texture.
            material = new Material(gameObject.GetComponent<MeshRenderer>().material);
            material.shader = Shader.Find("Unlit/UnlitTransparentWithColor");
            material.mainTexture = setupParams.texture;

            // We won't be using the default MeshRenderer component. We'll handle rendering on our own.
            GameObject.Destroy(gameObject.GetComponent<MeshRenderer>());

            if (setupParams.attachToController)
            {
                // Have the image start attached to the controller and right ahead of it.
                positionModelSpace = PeltzerMain.Instance.peltzerController.LastPositionModel +
                  PeltzerMain.Instance.peltzerController.LastRotationModel * Vector3.forward * HOVER_DISTANCE;
                rotationModelSpace = PeltzerMain.Instance.peltzerController.LastRotationModel;
            }
            else
            {
                positionModelSpace = setupParams.positionModelSpace;
                rotationModelSpace = setupParams.rotationModelSpace;
            }
            transform.position = PeltzerMain.Instance.worldSpace.ModelToWorld(positionModelSpace);
            transform.rotation = PeltzerMain.Instance.worldSpace.ModelOrientationToWorld(rotationModelSpace);

            scaleModelSpace = setupParams.scaleModelSpace;

            RecalculateVerticesAndNormal();

            if (setupParams.attachToController)
            {
                Grab();
            }
        }

        internal override void Delete()
        {
            base.Delete();

            SetupParams setupParams = new SetupParams();
            setupParams.positionModelSpace = positionModelSpace;
            setupParams.rotationModelSpace = rotationModelSpace;
            setupParams.scaleModelSpace = scaleModelSpace;
            setupParams.texture = referenceImageTexture;
            setupParams.refImageId = referenceImageId;
            PeltzerMain.Instance.GetModel().ApplyCommand(new DeleteReferenceImageCommand(setupParams));
        }

        internal override void Release()
        {
            base.Release();

            // Force an update to get the latest position and rotation.
            UpdatePosition();

            SetupParams oldParams = new SetupParams();
            oldParams.positionModelSpace = positionAtStartOfMove;
            oldParams.rotationModelSpace = rotationAtStartOfMove;
            oldParams.scaleModelSpace = scaleAtStartOfMove;
            oldParams.texture = referenceImageTexture;
            oldParams.refImageId = referenceImageId;

            SetupParams newParams = oldParams;
            newParams.positionModelSpace = positionModelSpace;
            newParams.rotationModelSpace = rotationModelSpace;
            newParams.scaleModelSpace = scaleModelSpace;

            oldParams.initialInsertion = initialInsertion;
            // Delete the old image and add a new one with the updated position/rotation/scale.
            base.Delete();
            PeltzerMain.Instance.GetModel().ApplyCommand(new CompositeCommand(new List<Command>() {
        new DeleteReferenceImageCommand(oldParams),
        new AddReferenceImageCommand(newParams)
      }));
        }

        internal override void Scale(bool scaleUp)
        {
            Vector3 oldScale = scaleModelSpace;
            float increment = scaleUp ? SCALE_INCREMENT : -SCALE_INCREMENT;
            float newScaleFactor = Mathf.Clamp(scaleModelSpace.x + increment, SCALE_MIN, SCALE_MAX);
            scaleModelSpace = Vector3.one * newScaleFactor;
            RecalculateVerticesAndNormal();

            // If the image is grabbed, we will apply the scale at the end of the move, so we don't need to worry about
            // that here. However, if it's not grabbed, we have to apply the command right now.
            if (!grabbed)
            {
                SetupParams oldParams = new SetupParams();
                oldParams.positionModelSpace = positionModelSpace;
                oldParams.rotationModelSpace = rotationModelSpace;
                oldParams.scaleModelSpace = oldScale;
                oldParams.texture = referenceImageTexture;
                oldParams.refImageId = referenceImageId;

                SetupParams newParams = oldParams;
                newParams.scaleModelSpace = scaleModelSpace;

                base.Delete();
                PeltzerMain.Instance.GetModel().ApplyCommand(new CompositeCommand(new List<Command>() {
          new DeleteReferenceImageCommand(oldParams), new AddReferenceImageCommand(newParams)
        }));
            }
        }
    }
}