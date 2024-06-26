#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
// TODO(ineula): This code is currently unreferenced. We need to remove dependencies on UnityEditor
// before we can use it, or else we should discard this code.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

public class GlTF_Animation : GlTF_Writer {
  public List<GlTF_Channel> channels = new List<GlTF_Channel>();
  public int count;
  public GlTF_Parameters parameters;
  public List<GlTF_AnimSampler> animSamplers = new List<GlTF_AnimSampler>();
  private bool gotTranslation = false;
  private bool gotRotation = false;
  private bool gotScale = false;

  public GlTF_Animation(string n) {
    name = n;
    parameters = new GlTF_Parameters(n);
  }

  public void Populate(AnimationClip c) {
    // TODO: Switch to AnimationUtility.GetCurveBindings(c), address FIXTHIS ids below.
    // look at each curve
    // if position, rotation, scale detected for first time
    //  create channel, sampler, param for it
    //  populate this curve into proper component
    AnimationClipCurveData[] curveDatas = null;
    // The following is omitted to prevent compiler warnings about obsolete code.
#if false
    curveDatas = AnimationUtility.GetAllCurves(c, true);
#endif
    if (curveDatas != null)
      count = curveDatas[0].curve.keys.Length;
    for (int i = 0; i < curveDatas.Length; i++) {
      string propName = curveDatas[i].propertyName;
      if (propName.Contains("m_LocalPosition")) {
        if (!gotTranslation) {
          gotTranslation = true;
          GlTF_AnimSampler s = new GlTF_AnimSampler(name + "_AnimSampler", "translation");
          GlTF_Channel ch = new GlTF_Channel("translation", s);
          GlTF_Target target = new GlTF_Target();
          target.id = "FIXTHIS";
          target.path = "translation";
          ch.target = target;
          channels.Add(ch);
          animSamplers.Add(s);
        }
      }
      if (propName.Contains("m_LocalRotation")) {
        if (!gotRotation) {
          gotRotation = true;
          GlTF_AnimSampler s = new GlTF_AnimSampler(name + "_RotationSampler", "rotation");
          GlTF_Channel ch = new GlTF_Channel("rotation", s);
          GlTF_Target target = new GlTF_Target();
          target.id = "FIXTHIS";
          target.path = "rotation";
          ch.target = target;
          channels.Add(ch);
          animSamplers.Add(s);
        }
      }
      if (propName.Contains("m_LocalScale")) {
        if (!gotScale) {
          gotScale = true;
          GlTF_AnimSampler s = new GlTF_AnimSampler(name + "_ScaleSampler", "scale");
          GlTF_Channel ch = new GlTF_Channel("scale", s);
          GlTF_Target target = new GlTF_Target();
          target.id = "FIXTHIS";
          target.path = "scale";
          ch.target = target;
          channels.Add(ch);
          animSamplers.Add(s);
        }
      }
      parameters.Populate(curveDatas[i]);
      //			Type propType = curveDatas[i].type;
    }
  }

  public override void Write() {
    Indent(); jsonWriter.Write("\"" + name + "\": {\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"channels\": [\n");
    foreach (GlTF_Channel c in channels) {
      CommaNL();
      c.Write();
    }
    jsonWriter.WriteLine();
    Indent(); jsonWriter.Write("]");
    CommaNL();

    Indent(); jsonWriter.Write("\"count\": " + count + ",\n");

    parameters.Write();
    CommaNL();

    Indent(); jsonWriter.Write("\"samplers\": {\n");
    IndentIn();
    foreach (GlTF_AnimSampler s in animSamplers) {
      CommaNL();
      s.Write();
    }
    IndentOut();
    jsonWriter.WriteLine();
    Indent(); jsonWriter.Write("}\n");

    IndentOut();
    Indent(); jsonWriter.Write("}");
  }
}
#endif
