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

namespace com.google.apps.peltzer.client.guides
{
    /// <summary>
    ///  Rope rendering utility class for debugging or developing.
    ///  
    ///  NOTE: Should not be used in production since it uses game objects to render.
    /// </summary>
    public class Rope
    {
        private static readonly float WIDTH = 0.002f;
        private readonly Color START_COLOR = Color.red;
        private readonly Color END_COLOR = Color.red;

        GameObject go;
        LineRenderer lineRenderer;

        public Rope()
        {
            go = new GameObject("rope");

            lineRenderer = go.AddComponent<LineRenderer>();
            lineRenderer.startWidth = WIDTH;
            lineRenderer.endWidth = WIDTH;

            lineRenderer.startColor = START_COLOR;
            lineRenderer.endColor = END_COLOR;
        }

        public void UpdatePosition(Vector3 sourceWorldSpace, Vector3 targetWorldSpace)
        {
            lineRenderer.SetPosition(0, sourceWorldSpace);
            lineRenderer.SetPosition(1, targetWorldSpace);
        }

        public void Hide()
        {
            go.SetActive(false);
        }

        public void Unhide()
        {
            go.SetActive(true);
        }

        public void Destroy()
        {
            GameObject.Destroy(go);
        }
    }
}