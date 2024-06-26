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

namespace com.google.apps.peltzer.client.model.render {
  public class MaterialAndColor {
    // This is necessary in order to change to a different version of the material - ie, when we need to use a preview
    // shader instead of the regular shader. This allows us to look up the correct replacement material and properly
    // handle exception materials such as Glass and Gem.  Ideally at some point we retain references in classes to
    // only matId and look up MaterialAndColor on demand in order to avoid sync bugs.  Until we can refactor to that,
    // material and color should instead be treated as canonical and matId used only when change-of-shader is required
    // for effects.
    public int matId;
    public Material material;
    // This material is to handle an edge case when a material needs two passes which need to be in different spots of
    // the renderqueue (Glass, at the moment).  We should abstract this away into something more elegant later.
    public Material material2;
    public Color32 color;

    public MaterialAndColor(Material material, int id) {
      this.material = material;
      this.color = new Color32(255, 255, 255, 255);
      this.matId = id;
    }

    public MaterialAndColor(Material material, Color32 color, int id) {
      this.material = material;
      this.color = color;
      this.matId = id;
    }
    
    public MaterialAndColor(Material material, Material material2, Color32 color, int id) {
      this.material = material;
      this.material2 = material2;
      this.color = color;
      this.matId = id;
    }

    public MaterialAndColor Clone() {
      if (material2 != null) {
        return new MaterialAndColor(new Material(material), new Material(material2), color, matId);
      }
      else {
        return new MaterialAndColor(new Material(material), color, matId);
      }
    }
  }
}