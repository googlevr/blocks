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


//SimplePixelizer

using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
[AddComponentMenu("Image Effects/SimplePixelizer")]
public class SimplePixelizer : MonoBehaviour {	
	
	public bool colorBlending = false;
	public int pixelize = 8;
	
	//Fixed Resolution
	//Enabling fixed resolution will ignore the pixelize variable.
	//It won't ignore colorBlending
	public bool useFixedResolution = false;
	public int fixedHeight = 640;
	public int fixedWidth = 480;
	
	//Check if image effects are supported
	protected void Start() {
		if (!SystemInfo.supportsImageEffects) {
			enabled = false;
			return;
		}
	}
	
	//Downgrade the image
	void OnRenderImage (RenderTexture source, RenderTexture destination) {
		//Create the buffer
		RenderTexture buffer = null;
		
		//Set the resolution of the buffer
		if(useFixedResolution) {
			buffer = RenderTexture.GetTemporary(fixedWidth, fixedHeight, 0);
		}
		else {
			buffer = RenderTexture.GetTemporary(source.width/pixelize, source.height/pixelize, 0);
		}
		
		//Change filter mode of buffer to create the pixel effect
		buffer.filterMode = FilterMode.Point;
		
		//Change filter mode of source to disable color blending/merging
		if(!colorBlending) {
			source.filterMode = FilterMode.Point;
		}
		
		//Copy source to buffer to create the final image
		Graphics.Blit(source, buffer);	
		
		//Copy buffer to destination so it renders on screen
		Graphics.Blit(buffer, destination);
		
		//Release buffer
		RenderTexture.ReleaseTemporary(buffer);
	}
}