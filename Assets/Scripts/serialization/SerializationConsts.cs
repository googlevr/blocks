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
using System.Linq;
using System.Text;

namespace com.google.apps.peltzer.client.serialization
{
    public static class SerializationConsts
    {
        // Chunk labels:
        public const int CHUNK_PELTZER = 100;
        // Basic mesh data.
        public const int CHUNK_MMESH = 101;
        // Remix IDs (optional chunk).
        public const int CHUNK_MMESH_EXT_REMIX_IDS = 102;
        // Recommended rotation of model on the Poly Menu (optional chunk).
        public const int CHUNK_PELTZER_EXT_MODEL_ROTATION = 103;
        // Note: when adding additional mesh chunks, name them CHUNK_MMESH_EXT_* and
        // describe what new fields they contain.

        // Maximum allowed counts for repeated fields (for sanity checking).
        public const int MAX_MESHES_PER_FILE = 100000;
        public const int MAX_MATERIALS_PER_FILE = 1024;
        public const int MAX_VERTICES_PER_MESH = 500000;
        public const int MAX_FACES_PER_MESH = 100000;
        public const int MAX_VERTICES_PER_FACE = 256;
        public const int MAX_HOLES_PER_FACE = 256;
        public const int MAX_VERTICES_PER_HOLE = 256;
        public const int MAX_REMIX_IDS_PER_MMESH = 256;
    }
}
