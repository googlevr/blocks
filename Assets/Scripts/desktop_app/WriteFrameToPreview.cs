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

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    /// This is attached to the camera which renders previews. It controls setting the proper
    /// render texture and active render texture before rendering, and writing the rendered
    /// frame to a texture post rendering.
    /// </summary>
    public class WriteFrameToPreview : MonoBehaviour
    {
        private Camera previewCam;
        private RenderTexture renderTexture;
        private Image previewImage;

        public void Setup(RenderTexture renderTexture, Image previewImage)
        {
            this.renderTexture = renderTexture;
            this.previewImage = previewImage;
        }

        void Awake()
        {
            previewCam = gameObject.GetComponent<Camera>();
        }

        void OnPreRender()
        {
            if (renderTexture != null && previewImage != null)
            {
                previewCam.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
            }
        }

        void OnPostRender()
        {
            if (renderTexture != null && previewImage != null)
            {
                if (previewImage.sprite != null && previewImage.sprite.texture != null)
                {
                    Texture2D previewTex = previewImage.sprite.texture;
                    previewTex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    previewTex.Apply();
                }
                else
                {
                    Texture2D previewTex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                    previewTex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    previewTex.Apply();
                    previewImage.sprite = Sprite.Create(previewTex, new Rect(0, 0, renderTexture.width, renderTexture.height), new Vector2(.5f, .5f));
                }
            }
        }
    }
}