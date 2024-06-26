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

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  /// A command consists of making a copy of an existing mesh.
  /// </summary>
  public class CopyMeshCommand : CompositeCommand {
    internal readonly int copiedFromId;
    internal readonly MMesh copy;

    public CopyMeshCommand(int copiedFromId, MMesh copy) : base(new List<Command>() {
      new AddMeshCommand(copy)
    }) {
      this.copiedFromId = copiedFromId;
      this.copy = copy;
    }

    public int GetCopyMeshId() {
      return copy.id;
    }
  }
}
