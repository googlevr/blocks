using UnityEngine;
using System.Collections;

public class GlTF_DirectionalLight : GlTF_Light {
  public override void Write() {
    color.Write();
  }
}
