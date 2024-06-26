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
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {
  
  public class MeshWithMaterialRenderer : MonoBehaviour {
    
     // Layer settings, for cameras.
    public static readonly int DEFAULT_LAYER = 10; // PolyAssets -- won't show up in thumbnails
    public static readonly int NO_SHADOWS_LAYER = 9; // NoShadowsLayer -- won't cast a shadow

    public List<MeshWithMaterial> meshes;
    public WorldSpace worldSpace;
    
    protected bool overrideWithPreviewShader = false;

    public void UsePreviewShader(bool usePreview) {
      overrideWithPreviewShader = usePreview;
    }
    
    private SmoothMoves smoother;
 
    public bool IgnoreWorldScale;
    public bool IgnoreWorldRotation;
    /// <summary>
    /// For entities that exist in world space, not model space.
    /// </summary>
    public bool UseGameObjectPosition;
    
    public float fade = 0.3f;

    // Which Layer this mesh will be drawn to.
    public int Layer = DEFAULT_LAYER;
    
    public void Init(WorldSpace worldSpace) {
      this.worldSpace = worldSpace;
      smoother = new SmoothMoves(worldSpace, Vector3.zero, Quaternion.identity);
    }
    
    // All rendering needs to be done at the very end of the frame after all the state changes to the models have been
    // made.
    void LateUpdate() {
      // If along the GameObject's hierarchy a parent is set to inactive
      // these will return null and logically we don't need to render. We cannot simply check activeSelf
      // as it may still return true.
      if (worldSpace == null || transform == null) return;

      // For positioning, we calculate our position using supplied model coordinates and WorldSpace, and then transform
      // that by our parent transform.
      // Usually we'll be in one of two cases:
      //   1. Parent transform is identity, and we're positioning purely via worldSpace and supplied coords.
      //   2. worldSpace and supplied coords are identity (or contain only scale) and we're positioned by the parent.
      //
      // It is also possible to use both in conjunction in order to position within a parent's frame of reference
      // but at present that isn't used anywhere.
      Vector3 pos = UseGameObjectPosition ? gameObject.transform.position : smoother.GetDisplayPositionInWorldSpace();
      Quaternion orientation = IgnoreWorldRotation ? gameObject.transform.rotation : smoother.GetDisplayOrientationInWorldSpace();
      Vector3 scale = IgnoreWorldScale ? smoother.GetScale() 
        :  smoother.GetScale() * worldSpace.scale;

      Matrix4x4 matrix;
      if (UseGameObjectPosition) {
        matrix = Matrix4x4.TRS(pos, orientation, scale) ;
      } else {
        matrix = transform.localToWorldMatrix * Matrix4x4.TRS(pos, orientation, scale);
      }
      Render(matrix);
    }
    
    public void Render(Matrix4x4 transformMatrix) {
      foreach (MeshWithMaterial meshWithMaterial in meshes) {
        Material renderMat = meshWithMaterial.materialAndColor.material;
        if (overrideWithPreviewShader && meshWithMaterial.materialAndColor.matId != MaterialRegistry.GLASS_ID) {
          renderMat = MaterialRegistry.GetPreviewOfMaterialById(meshWithMaterial.materialAndColor.matId).material;
          renderMat.SetFloat("_MultiplicitiveAlpha", fade);
          Graphics.DrawMesh(meshWithMaterial.mesh, transformMatrix, renderMat, Layer);
        }
        else {
          Graphics.DrawMesh(meshWithMaterial.mesh, transformMatrix, renderMat, Layer);
          if (meshWithMaterial.materialAndColor.material2 != null) {
            Graphics.DrawMesh(meshWithMaterial.mesh, transformMatrix, meshWithMaterial.materialAndColor.material2, Layer);
          }
        }

      }
    }
    
    /// <summary>
    ///   Overrides the material of all meshes in this renderer with the given MaterialAndColor.
    /// </summary>
    public void OverrideWithNewMaterial(MaterialAndColor newMaterialAndColor) {
      List<MeshWithMaterial> newMeshes = new List<MeshWithMaterial>(meshes.Count);
      foreach (MeshWithMaterial mwm in meshes) {
        // Override the vertex colours.
        Color32[] colors = new Color32[mwm.mesh.vertexCount];
        for (int i = 0; i < colors.Length; i++) {
          colors[i] = newMaterialAndColor.color;
        }
        mwm.mesh.colors32 = colors;

        newMeshes.Add(new MeshWithMaterial(mwm.mesh, newMaterialAndColor));
      }
      meshes = newMeshes;
    }

    /// <summary>
    ///   As above, taking a material by ID.
    /// </summary>
    /// <param name="newMaterialId"></param>
    public void OverrideWithNewMaterial(int newMaterialId) {
      OverrideWithNewMaterial(MaterialRegistry.GetMaterialAndColorById(newMaterialId));
    }

    
    void Update() {
      smoother.UpdateDisplayPosition();
    }
    
     public Vector3 positionModelSpace {
      get { return smoother.GetPositionInModelSpace(); }
    }

    /// <summary>
    /// Returns the position in world space. Note that if this IS NOT affected by smoothing. Smoothing is a purely
    /// visual effect and does not alter the object's position.
    /// </summary>
    /// <returns></returns>
    public Vector3 GetPositionInWorldSpace() {
      return transform.localToWorldMatrix * smoother.GetPositionInWorldSpace();
    }

    public Vector3 GetPositionInModelSpace() {
      return smoother.GetPositionInModelSpace();
    }

    /// <summary>
    /// Returns the orientation in model space.
    /// </summary>
    public Quaternion GetOrientationInModelSpace() {
      return smoother.GetOrientationInModelSpace();
    }

    /// <summary>
    /// Sets the position in model space, optionally with smoothing.
    /// </summary>
    /// <param name="newPositionModelSpace">The new position in model space.</param>
    /// <param name="smooth">True if a smoothing effect is desired. Note that smoothing is a purely visual effect.
    /// The actual position is instantaneously updated regardless of smoothing.</param>
    public void SetPositionModelSpace(Vector3 newPositionModelSpace, bool smooth = false) {
      smoother.SetPositionModelSpace(newPositionModelSpace, smooth);
    }

    /// <summary>
    /// Sets the position in world space, optionally with smoothing.
    /// </summary>
    /// <param name="newPositionModelSpace">The new position in model space.</param>
    /// <param name="smooth">True if a smoothing effect is desired. Note that smoothing is a purely visual effect.
    /// The actual position is instantaneously updated regardless of smoothing.</param>
    public void SetPositionWorldSpace(Vector3 newPositionWorldSpace, bool smooth = false) {
      // Coords relative to parent node
      Vector3 parentCoords = transform.worldToLocalMatrix* newPositionWorldSpace;
      smoother.SetPositionWorldSpace(parentCoords, smooth);
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
      smoother.SetPositionWithDisplayOverrideModelSpace(newPositionModelSpace, newDisplayPositionModelSpace);
    }

    /// <summary>
    /// Sets the orientation in model space, optionally with smoothing.
    /// </summary>
    /// <param name="newOrientationModelSpace">The new orientation in model space.</param>
    /// <param name="smooth">True if a smoothing effect is desired. Note that smoothing is a purely visual effect.
    /// The actual orientation is instantaneously updated regardless of smoothing.</param>
    public void SetOrientationModelSpace(Quaternion newOrientationModelSpace, bool smooth = false) {
      smoother.SetOrientationModelSpace(newOrientationModelSpace, smooth);
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
      smoother.SetOrientationWithDisplayOverrideModelSpace(newOrientationModelSpace, newDisplayOrientationModelSpace, 
        smooth);
    }

    /// <summary>
    /// Resets the local transform to identity.
    /// </summary>
    public void ResetTransform() {
      SetOrientationModelSpace(Quaternion.identity);
      SetPositionModelSpace(Vector3.zero);
    }

    /// <summary>
    /// Animates this object's displayed position from the given position to the current one.
    /// </summary>
    /// <param name="oldPosModelSpace">Old position, in the model space.</param>
    public void AnimatePositionFrom(Vector3 oldPosModelSpace) {
      smoother.AnimatePositionFrom(oldPosModelSpace);
    }

    /// <summary>
    /// Animates this object's displayed scale from the given scale to the default scale (1.0f).
    /// </summary>
    /// <param name="fromScale">Old scale factor.</param>
    public void AnimateScaleFrom(float fromScale) {
      smoother.AnimateScaleFrom(fromScale);
    }

    /// <summary>
    ///   Sets this MeshWithMaterialRenderer up as a copy of another MeshWithMaterialRenderer.
    /// </summary>
    /// <param name="other">The other MeshWithMaterialRenderer</param>
    public void SetupAsCopyOf(MeshWithMaterialRenderer other) {
      meshes = new List<MeshWithMaterial>(other.meshes);
      worldSpace = other.worldSpace;
      smoother = new SmoothMoves(other.smoother);
    }

    public Vector3 GetCurrentAnimatedScale() {
      return smoother.GetScale();
    }
    
  }
}