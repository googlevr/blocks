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

using System;
using System.Collections.Generic;
using com.google.apps.peltzer.client.alignment;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using Valve.VR;
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.model.render
{
    /// <summary>
    /// UX Effect which renders mesh snap axes.
    /// </summary>
    class ContinuousMeshSnapEffect
    {
        List<FaceKey> highlightedFaces = new List<FaceKey>();

        /// <summary>
        /// Constructs the effect, Initialize must still be called before the effect starts to take place.
        /// </summary>
        /// <param name="snapTarget">The MMesh id of the target mesh to play the shader on.</param>
        public ContinuousMeshSnapEffect()
        {
        }

        public void Finish()
        {
            HighlightUtils highlightUtils = PeltzerMain.Instance.highlightUtils;
            foreach (FaceKey highlightedFace in highlightedFaces)
            {
                highlightUtils.TurnOff(highlightedFace);
            }
        }

        public void UpdateFromSnapSpace(MeshSnapSpace meshSnapSpace)
        {
            MMesh targetMesh = PeltzerMain.Instance.GetModel().GetMesh(meshSnapSpace.targetMeshId);
            HighlightUtils highlightUtils = PeltzerMain.Instance.highlightUtils;

            foreach (Face targetFace in targetMesh.GetFaces())
            {
                FaceKey faceKey = new core.FaceKey(targetMesh.id, targetFace.id);
                highlightUtils.TurnOn(faceKey);
                highlightUtils.SetFaceStyleToSelect(faceKey, targetMesh.offset);

                highlightedFaces.Add(faceKey);
            }
        }
    }
}
