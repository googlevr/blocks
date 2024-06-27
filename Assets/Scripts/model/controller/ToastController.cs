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

namespace com.google.apps.peltzer.client.model.controller
{
    public class ToastController : MonoBehaviour
    {
        private static float TOTAL_TOAST_TIME_S = 5f;
        private static float TOAST_APEX_Z_LOCATION = .2f;
        private static float TOAST_VELOCITY = .1f;

        private Image background;
        private Text toastText;
        private float toastStart;
        private bool toastActive = false;

        void Start()
        {
            background = transform.Find("Background").GetComponent<Image>();
            Color bgColor = Color.white;
            bgColor.a = 0f;
            background.color = bgColor;
            toastText = transform.Find("ToastText").GetComponent<Text>();
        }

        void Update()
        {
            if (!toastActive)
            {
                return;
            }

            float currentTime = Time.time;
            float toastTime = currentTime - toastStart;
            if (toastTime < 1f)
            {
                Vector3 currPos = gameObject.transform.localPosition;
                currPos = new Vector3(currPos.x, currPos.y,
                    Mathf.Min(currPos.z + TOAST_VELOCITY * Time.deltaTime, TOAST_APEX_Z_LOCATION));
                gameObject.transform.localPosition = currPos;

                Color currBackgroundColor = background.color;
                currBackgroundColor.a = Mathf.Min(currBackgroundColor.a + Time.deltaTime, 1f);
                background.color = currBackgroundColor;
            }
            if (toastTime > 4f)
            {
                Vector3 currPos = gameObject.transform.localPosition;
                currPos = new Vector3(currPos.x, currPos.y, Mathf.Max(currPos.z - TOAST_VELOCITY * Time.deltaTime, .1f));
                gameObject.transform.localPosition = currPos;

                Color currBackgroundColor = background.color;
                currBackgroundColor.a = Mathf.Max(currBackgroundColor.a - Time.deltaTime, 0f);
                background.color = currBackgroundColor;
            }
            if (toastTime > TOTAL_TOAST_TIME_S)
            {
                toastActive = false;
                toastText.text = "";
                background.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        public void ShowToastMessage(string message)
        {
            toastText.text = message;
            toastStart = Time.time;
            toastActive = true;
        }
    }
}