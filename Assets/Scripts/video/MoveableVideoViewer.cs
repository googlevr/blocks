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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools;

namespace com.google.apps.peltzer.video
{
    /// <summary>
    ///   The video viewer is a mesh with a texture that allows movies (videos) to be played. This script allows the 
    ///   viewer to be moved, and thrown with the grab tool, or 'deleted' with the delete tool, without actually
    ///   being a part of the Model. Note that we never actually delete the video viewer: exactly one viewer exists in
    ///   the scene at all times, and is hidden rather than deleted. The video viewer cannot be scaled, to avoid
    ///   distorting the movie texture.
    ///   
    ///   Operations on the video viewer are included in the undo/redo stack.
    /// </summary>
    public class MoveableVideoViewer : MoveableObject
    {
        public override void Setup()
        {
            base.Setup();

            mesh = gameObject.GetComponent<MeshFilter>().mesh;
            material = gameObject.GetComponent<MeshRenderer>().material;

            WorldSpace worldSpace = PeltzerMain.Instance.worldSpace;
            positionModelSpace = worldSpace.WorldToModel(transform.position);
            rotationModelSpace = worldSpace.WorldOrientationToModel(transform.rotation);
            RecalculateVerticesAndNormal();
        }

        internal override void Delete()
        {
            base.Delete();

            PeltzerMain.Instance.GetModel().ApplyCommand(new HideVideoViewerCommand());
        }

        internal override void Release()
        {
            base.Release();

            // Force an update to get the latest position and rotation.
            UpdatePosition();

            // Move the viewer via a command.
            Vector3 positionDelta = positionModelSpace - positionAtStartOfMove;
            Quaternion rotDelta = Quaternion.Inverse(rotationAtStartOfMove) * rotationModelSpace;
            PeltzerMain.Instance.GetModel().ApplyCommand(new MoveVideoViewerCommand(positionDelta, rotDelta));
        }
    }
}