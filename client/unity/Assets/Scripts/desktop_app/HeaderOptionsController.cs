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
using System.Collections;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.desktop_app {
  public class HeaderOptionsController : MonoBehaviour {

    private Rect localBounds;

    void Start() {
      HoverableButton signOut = transform.Find("SignOutOption").gameObject.AddComponent<HoverableButton>();
      signOut.SetOnClickAction(() => {
        PeltzerMain.Instance.InvokeMenuAction(MenuAction.SIGN_OUT);
      });
    }

    void LateUpdate() {
      if (Input.GetMouseButtonDown(0) && !localBounds.Contains(Input.mousePosition)) {
        Close();
      }
    }

    void OnEnable() {
      Vector2 size = GetComponent<RectTransform>().sizeDelta;
      Vector2 position = GetComponent<RectTransform>().position;
      localBounds = new Rect(position.x - size.x / 2.0f, position.y - size.y / 2.0f, size.x, size.y);
    }

    public void Open() {
      gameObject.SetActive(true);
    }

    public void Close() {
      gameObject.SetActive(false);
    }
  }
}