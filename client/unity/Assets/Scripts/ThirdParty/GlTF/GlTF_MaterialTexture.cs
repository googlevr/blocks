using UnityEngine;
using System.Collections;

public class GlTF_MaterialTexture : GlTF_ColorOrTexture {
  public GlTF_MaterialTexture(string n, GlTF_Texture t) {
    name = n; texture = t;
  }
  public GlTF_Texture texture;
  public override void Write() {
    Indent(); jsonWriter.Write("\"" + name + "\": \"" + texture.name + "\"");
  }
}
