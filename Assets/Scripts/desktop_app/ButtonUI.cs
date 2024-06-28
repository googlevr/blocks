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
using UnityEngine.EventSystems;

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    ///   A class to manage hover behaviour for buttons in our desktop app.
    /// </summary>
    class ButtonUI : MonoBehaviour
    {
        // A hover highlight, normally comprising an image around the button and a textual tip about the button.
        private GameObject tip;

        void Start()
        {
            tip = transform.Find("Tip").gameObject;
            tip.SetActive(false);

            EventTrigger trigger = GetComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { OnPointerEnter(); });
            trigger.triggers.Add(entry);
            entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerExit;
            entry.callback.AddListener((data) => { OnPointerExit(); });
            trigger.triggers.Add(entry);
        }

        public void OnPointerEnter()
        {
            tip.SetActive(true);
        }

        public void OnPointerExit()
        {
            tip.SetActive(false);
        }
    }
}
