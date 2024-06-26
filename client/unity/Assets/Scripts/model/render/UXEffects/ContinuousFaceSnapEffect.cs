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
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.model.render {
  /// <summary>
  /// UX Effect which renders guides for continuous face snapping (an outline around the target face and source face,
  /// as well as a line from the source face center to the snap point on the target face)
  /// </summary>
  class ContinuousFaceSnapEffect : UXEffectManager.UXEffect {
    private const float DEFAULT_DURATION = 1.0f;

    Vector3 basePreviewPosition;
    private Mesh previewMesh;

    private bool inSnapThreshhold = false;

    public Vector3[] snapLines = new Vector3[0];
    public Vector3[] snapNormals = new Vector3[0];
    public Vector2[] snapSelectData = new Vector2[0];
    private int[] snapLineIndices = new int[0];


    /// <summary>
    /// Constructs the effect, Initialize must still be called before the effect starts to take place.
    /// </summary>
    /// <param name="snapTarget">The MMesh id of the target mesh to play the shader on.</param>
    public ContinuousFaceSnapEffect() {
      previewMesh = new Mesh();
    }

    public override void Initialize(MeshRepresentationCache cache, MaterialLibrary materialLibrary,
      WorldSpace worldSpace) {
      base.Initialize(cache, materialLibrary.edgeHighlightMaterial, worldSpace);
    }

    public override void Render() {
      float scaleFactor = InactiveRenderer.GetEdgeScaleFactor(worldSpace);
      effectMaterial.SetFloat("_PointSphereRadius", scaleFactor);
      Graphics.DrawMesh(previewMesh,
        worldSpace.modelToWorld,
        effectMaterial,
        0); // Layer
    }

    public override void Finish() {
      Shader.SetGlobalVector("_FXPointLightColorStrength", new Vector4(0f, 0f, 0f, 0f));
      Shader.SetGlobalVector("_FXPointLightPosition", new Vector4(0f, 0f, 0f, 1f));
      UXEffectManager.GetEffectManager().EndEffect(this);
    }

    /// <summary>
    /// Updates the effect based on the supplied FaceSnapSpace.
    /// </summary>
    /// <param name="faceSnapSpace"></param>
    public void UpdateFromSnapSpace(FaceSnapSpace faceSnapSpace) {
      MMesh sourceMesh = faceSnapSpace.sourceMesh;
      MMesh targetMesh = PeltzerMain.Instance.GetModel().GetMesh(faceSnapSpace.targetFaceKey.meshId);
      Face sourceFace = sourceMesh.GetFace(faceSnapSpace.sourceFaceKey.faceId);
      Face targetFace = targetMesh.GetFace(faceSnapSpace.targetFaceKey.faceId);
      int sizeNeeded = 2 + sourceFace.vertexIds.Count * 2 + targetFace.vertexIds.Count * 2;
      if (snapLines.Length != sizeNeeded) {
        Array.Resize(ref snapLines, sizeNeeded);
        Array.Resize(ref snapLineIndices, sizeNeeded);
        Array.Resize(ref snapNormals, sizeNeeded);
        Array.Resize(ref snapSelectData, sizeNeeded);
        for (int i = 0; i < sizeNeeded; i++) {
          snapLineIndices[i] = i;
          snapNormals[i] = Vector3.up;
          snapSelectData[i] = Vector2.one;
        }
      }
      // Snap Line
      snapLines[0] = faceSnapSpace.sourceFaceCenter;
      snapLines[1] = faceSnapSpace.snapPoint;

      int curStartIndex = 2;
      Matrix4x4 xForm = Matrix4x4.TRS(faceSnapSpace.sourceMeshOffset, faceSnapSpace.sourceMeshRotation, Vector3.one);
      // Source Face
      for (int i = 0; i < sourceFace.vertexIds.Count; i++) {
        snapLines[curStartIndex + 2 * i] =
          xForm.MultiplyPoint(sourceMesh.VertexPositionInMeshCoords(sourceFace.vertexIds[i]));
        snapLines[curStartIndex + 2 * i + 1] =
          xForm.MultiplyPoint(
            sourceMesh.VertexPositionInMeshCoords(sourceFace.vertexIds[(i + 1) % sourceFace.vertexIds.Count]));
      }

      curStartIndex = curStartIndex + 2 * sourceFace.vertexIds.Count;
      // Target Face
      for (int i = 0; i < targetFace.vertexIds.Count; i++) {
        snapLines[curStartIndex + 2 * i] =
          targetMesh.VertexPositionInModelCoords(targetFace.vertexIds[i]);
        snapLines[curStartIndex + 2 * i + 1] =
          targetMesh.VertexPositionInModelCoords(targetFace.vertexIds[(i + 1) % targetFace.vertexIds.Count]);
      }
      previewMesh.Clear();
      previewMesh.vertices = snapLines;
      previewMesh.normals = snapNormals;
      previewMesh.uv = snapSelectData;
      previewMesh.SetIndices(snapLineIndices, MeshTopology.Lines, 0 /* submesh id */, false /* recalculate bounds */);
    }
  }
}
