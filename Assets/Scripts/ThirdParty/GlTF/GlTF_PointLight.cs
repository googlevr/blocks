using UnityEngine;
using System.Collections;

public class GlTF_PointLight : GlTF_Light {
  public float constantAttenuation = 1f;
  public float linearAttenuation = 0f;
  public float quadraticAttenuation = 0f;

  public GlTF_PointLight() {
    type = "point";
  }

  public override void Write() {
    color.Write();
    Indent(); jsonWriter.Write("\"constantAttentuation\": " + constantAttenuation);
    Indent(); jsonWriter.Write("\"linearAttenuation\": " + linearAttenuation);
    Indent(); jsonWriter.Write("\"quadraticAttenuation\": " + quadraticAttenuation);
    jsonWriter.Write("}");
  }
}
