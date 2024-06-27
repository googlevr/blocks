using UnityEngine;
using System.Collections;

public class GlTF_FloatArray : GlTF_Writer {
  public float[] items;
  public int minItems = 0;
  public int maxItems = 0;

  public GlTF_FloatArray() {
  }
  public GlTF_FloatArray(string n) {
    name = n;
  }

  public override void Write() {
    if (name.Length > 0) {
      Indent(); jsonWriter.Write("\"" + name + "\": [");
    }
    WriteVals();
    if (name.Length > 0) {
      jsonWriter.Write("]");
    }
  }

  public virtual void WriteVals() {
    for (int i = 0; i < maxItems; i++) {
      if (i > 0)
        jsonWriter.Write(", ");
      jsonWriter.Write(items[i].ToString());
    }
  }
}
