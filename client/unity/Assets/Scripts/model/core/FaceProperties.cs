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
  ///   Properties for Faces.  A value-type.
  /// </summary>
  public struct FaceProperties {
    public int materialId { get; private set; }

    public FaceProperties(int materialId) {
      this.materialId = materialId;
    }
  }
}
