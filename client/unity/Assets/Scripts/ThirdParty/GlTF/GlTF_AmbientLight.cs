using UnityEngine;
using System.Collections;

public class GlTF_AmbientLight : GlTF_Light {
  public override void Write() {
    color.Write();
  }
}
