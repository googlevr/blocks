﻿// Copyright 2020 The Blocks Authors
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
using System.Collections;

/// <summary>
/// Convenience struct containing a GameObject and its mesh.
/// </summary>
public class ObjectMesh
{
    public GameObject gameObject { get; set; }
    public Mesh mesh { get; set; }

    public ObjectMesh(GameObject gameObject, Mesh mesh)
    {
        this.gameObject = gameObject;
        this.mesh = mesh;
    }
}
