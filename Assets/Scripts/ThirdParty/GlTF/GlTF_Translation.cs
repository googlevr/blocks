using UnityEngine;
using System.Collections;

public class GlTF_Translation : GlTF_Vector3 {
  public GlTF_Translation(Vector3 v) {
    items = new float[] { v.x, v.y, v.z };
  }
  public override void Write() {
    Indent(); jsonWriter.Write("\"translation\": [ ");
    WriteVals();
    jsonWriter.Write("]");
  }
}
