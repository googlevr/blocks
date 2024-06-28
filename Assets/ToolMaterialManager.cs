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

using System.Collections;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

public class ToolMaterialManager : MonoBehaviour
{
    private const float ANIMATION_DURATION = .3f;
    // GOs using MeshRenderer
    public GameObject[] materialObjects;
    private const float DISABLE_MAX = 0.9f;

    public ControllerMode controllerMode;
    private float animationStartTime;

    public bool isDisabled;

    private bool animatingToDisable;
    private bool animatingToEnable;

    private float curPct;

    private RestrictionManager restrictionManager;
    // Use this for initialization
    void Start()
    {
        this.restrictionManager = PeltzerMain.Instance.restrictionManager;
        // Instance materials on all meshes
        foreach (GameObject go in materialObjects)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(renderer.material);
        }

        curPct = 0f;
        animatingToDisable = false;
        animatingToEnable = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!animatingToDisable && !animatingToEnable) return;
        curPct = Mathf.Min((Time.time - animationStartTime) / ANIMATION_DURATION, 1f);
        if (animatingToDisable)
        {
            SetOverridePercent(curPct);
            if (curPct >= DISABLE_MAX)
            {
                animatingToDisable = false;
            }
        }
        if (animatingToEnable)
        {
            SetOverridePercent(1f - curPct);
            if (curPct >= 1f) animatingToEnable = false;
        }
    }

    public void ChangeToEnable()
    {
        if (curPct >= 1f)
        {
            curPct = 0f;
        }

        if (!gameObject.activeInHierarchy)
        {
            SetOverridePercent(0);
            animatingToEnable = false;
            animatingToDisable = false;
        }
        else
        {
            animatingToEnable = true;
            animatingToDisable = false;
            animationStartTime = Time.time - ANIMATION_DURATION * curPct;
            isDisabled = false;
        }
    }

    public void ChangeToDisable()
    {
        if (curPct >= 1f)
        {
            curPct = 0f;
        }

        if (!gameObject.activeInHierarchy)
        {
            SetOverridePercent(DISABLE_MAX);
            animatingToEnable = false;
            animatingToDisable = false;
        }
        else
        {
            animatingToEnable = false;
            animatingToDisable = true;
            animationStartTime = Time.time - ANIMATION_DURATION * curPct;
            isDisabled = true;
        }
    }

    void SetOverridePercent(float percent)
    {
        foreach (GameObject go in materialObjects)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.material.SetFloat("_OverrideAmount", percent);
        }
    }
}
