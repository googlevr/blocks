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
using UnityEngine;

namespace com.google.apps.peltzer.client.model.controller {
  public struct ControllerTooltip {
    public String tooltipLabel;
    public String tooltipText;
    public float textWidth;
    
    public ControllerTooltip(String label, String text, float textWidth) {
      tooltipLabel = label;
      tooltipText = text;
      this.textWidth = textWidth;
    }
  }
  
  /// <summary>
  /// Manages tooltips for one UI element (to ensure multiple tooltips for the same button can't be shown
  /// at the same time).
  /// </summary>
  public class TooltipManager {
    private const float padding = 0.008f;
    
    private Dictionary<String, ControllerTooltip> tooltips;

    private GameObject root;
    private GameObject leftTip;
    private Transform leftTipL;
    private TextMesh leftTipText;
    private Transform leftBg;
    private GameObject rightTip;
    private Transform rightTipL;
    private TextMesh rightTipText;
    private Transform rightBg;
    private bool isActive = false;
    
    public TooltipManager(IEnumerable<ControllerTooltip> tooltips, GameObject root, GameObject leftTip,
      GameObject rightTip) {
      this.root = root;
      this.leftTip = leftTip;
      this.leftTipText = leftTip.GetComponentInChildren<TextMesh>();
      this.leftBg = leftTip.transform.Find("bg");
      this.leftTipL = leftTip.transform.Find("tipL");
      this.rightTip = rightTip;
      this.rightTipText = rightTip.GetComponentInChildren<TextMesh>();
      this.rightBg = rightTip.transform.Find("bg");
      this.rightTipL = rightTip.transform.Find("tipL");
      this.tooltips = new Dictionary<String, ControllerTooltip>();
      foreach (ControllerTooltip tipSpec in tooltips) {
        this.tooltips[tipSpec.tooltipLabel] = tipSpec;
      }
      TurnOff();
    }

    /// <summary>
    /// Turns off the tooltip.
    /// </summary>
    public void TurnOff() {
      root.SetActive(false);
      isActive = false;
    }

    /// <summary>
    /// Turns on the tooltip with the specified label.
    /// </summary>
    /// <param name="label"></param>
    public void TurnOn(String label) {

      float bgWidth = tooltips[label].textWidth;
      rightTipText.text = tooltips[label].tooltipText;

      Vector3 rootPos = rightTip.transform.localPosition;
      rootPos.x = bgWidth + padding;
      rightTip.transform.localPosition = rootPos;
      
      Vector3 tipLPos = rightTipL.localPosition;
      tipLPos.x = -bgWidth;
      rightTipL.localPosition = tipLPos;

      Vector3 bgScale = rightBg.localScale;
      bgScale.x = bgWidth;
      rightBg.localScale = new Vector3(tooltips[label].textWidth, rightBg.localScale.y, rightBg.localScale.z);
      
      leftTipText.text = tooltips[label].tooltipText;
      
      rootPos = leftTip.transform.localPosition;
      rootPos.x = -(bgWidth + padding);
      leftTip.transform.localPosition = rootPos;
      
      tipLPos = leftTipL.localPosition;
      tipLPos.x = bgWidth;
      leftTipL.localPosition = tipLPos;
      
      bgScale = leftBg.localScale;
      bgScale.x = bgWidth;
      leftBg.localScale = bgScale;
      
      root.SetActive(true);
      isActive = true;
    }

    /// <summary>
    /// Is a tooltip currently showing?
    /// </summary>
    /// <returns></returns>
    public bool IsActive() {
      return isActive;
    }
  }
}