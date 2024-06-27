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
using System.Collections;
using UnityEngine.EventSystems;
using System;

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    /// This class handles the hovering and selection of individual items in the "more options" menu.
    /// </summary>
    public class MyCreationMoreButtonOptionHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private GameObject hover;
        private Action clickAction;

        void Start()
        {
            EventSystem eventSystem = EventSystem.current;
            hover = transform.Find("hover").gameObject;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (clickAction != null)
            {
                clickAction();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hover.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hover.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
        }

        public void OnPointerUp(PointerEventData eventData)
        {
        }

        public void SetClickAction(Action clickAction)
        {
            this.clickAction = clickAction;
        }

        public void RemoveHover()
        {
            hover.SetActive(false);
        }
    }
}
