using UnityEngine;
using System.Collections;

public class GlTF_Channel : GlTF_Writer {
  public GlTF_AnimSampler sampler;
  public GlTF_Target target;

  public GlTF_Channel(string ch, GlTF_AnimSampler s) {
    sampler = s;
    switch (ch) {
      case "translation":
        break;
      case "rotation":
        break;
      case "scale":
        break;
    }
  }

  public override void Write() {
    IndentIn();
    Indent(); jsonWriter.Write("{\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"sampler\": \"" + sampler.name + "\",\n");
    target.Write();
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("}");
    IndentOut();
  }
}
