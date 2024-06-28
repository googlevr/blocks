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
// limitations under the License.using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// This class primarily exists to provide an editor level interface for assigning materials for various effects
/// other than adding them directly to PeltzerMain.
/// </summary>
public class MaterialLibrary : MonoBehaviour
{
    public float paperCraftNoiseScale = 500f;
    public float paperCraftNoiseIntensity = 0.2f;

    public Material baseMaterial;
    public Material highlightMaterial;
    public Material highlightMaterial2;
    public Material highlightSilhouetteMaterial;
    public Material gemMaterial;
    public Material glassMaterial;
    public Material glassSpecMaterial;
    public Material subtractMaterial;
    public Material copyMaterial;
    public Material snapEffectMaterial;
    public Material meshInsertEffectMaterial;
    public Material meshSelectMaterial;
    public Material gridMaterial;
    public Material pointHighlightMaterial;
    public Material pointInactiveMaterial;
    public Material edgeHighlightMaterial;
    public Material edgeInactiveMaterial;
    public Material faceHighlightMaterial;
    public Material facePaintMaterial;
    public Material faceExtrudeMaterial;
    public Material selectMaterial;

    public void Start()
    {
        Shader.SetGlobalFloat("_NoiseScale", paperCraftNoiseScale);
        Shader.SetGlobalFloat("_NoiseIntensity", paperCraftNoiseIntensity);
    }
}
