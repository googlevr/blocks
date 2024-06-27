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

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  /// Class that owns responsibility for rendering a particular mesh.  CLASSES OTHER THAN MODEL SHOULD ONLY CALL THIS
  /// ON MODEL.
  ///   /// </summary>
  public interface IMeshRenderOwner {
    /// <summary>
    /// Claim responsibility for rendering a mesh from this class.
    /// </summary>
    /// <param name="meshId">The id of the mesh being claimed</param>
    /// <returns>The id of the mesh that was claimed, or -1 for failure.</returns>
    int ClaimMesh(int meshId, IMeshRenderOwner fosterRenderer);
  }

  // Interface naming courtesy of Java.
  // This interface marks a class as the owner of the MeshRenderOwner ownership list - ie, Model.  This is the only
  // class that mesh ownership can be relinquished to.
  public interface IMeshRenderOwnerOwner {
    /// <summary>
    /// Gives responsibility for rendering a mesh to this class.  Generally, this should only be done to Model - the
    /// general dynamic being that tool classes attempt to claim ownership whenever they need a
    /// preview (which will in turn cause model to call Claim on the current owner), and then bequeath it back to Model
    /// when they are done (provided they still own the mesh.)
    /// </summary>
    /// <param name="meshId">The id of the mesh being bequeathed</param>
    /// <returns>The id of the mesh that is being bequeathed, or -1 for failure.</returns>
    void RelinquishMesh(int meshId, IMeshRenderOwner fosterRenderer);

    /// <summary>
    /// Claim responsibility for rendering a mesh from this class if and only if it is unowned by another renderer.
    /// </summary>
    /// <param name="meshId">The id of the mesh being claimed</param>
    /// <returns>The id of the mesh that was claimed, or -1 for failure.</returns>
    int ClaimMeshIfUnowned(int meshId, IMeshRenderOwner fosterRenderer);
  }
}