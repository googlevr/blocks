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

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace com.google.apps.peltzer.client.desktop_app {
  /// <summary>
  /// A button with a background component called 'hover' that has an associated action.
  /// </summary>
  public class HoverableButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
      IPointerExitHandler, IPointerDownHandler, IPointerUpHandler {
    private GameObject hover;
    private System.Action onClick;

    void Awake() {
      hover = transform.Find("hover").gameObject;
      hover.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData) {
      if (onClick != null) {
        onClick();
        hover.SetActive(false);
      }
    }

    public void OnPointerEnter(PointerEventData eventData) {
      hover.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData) {
      hover.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData) {
    }

    public void OnPointerUp(PointerEventData eventData) {
    }

    public void SetOnClickAction(System.Action onClick) {
      this.onClick = onClick;
    }
  }
}