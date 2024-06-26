using UnityEngine;
using System.Collections;

public class GlTF_Scale : GlTF_Vector3 {
  public GlTF_Scale() {
    items = new float[] { 1f, 1f, 1f };
  }
  public GlTF_Scale(Vector3 v) {
    items = new float[] { v.x, v.y, v.z };
  }
  public override void Write() {
    Indent(); jsonWriter.Write("\"scale\": [ ");
    WriteVals();
    jsonWriter.Write("]");
  }
}
