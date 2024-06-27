#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
// TODO(ineula): This code is currently unreferenced. We need to remove dependencies on UnityEditor
// before we can use it, or else we should discard this code.
using UnityEngine;
using System.Collections;
using UnityEditor;

public class GlTF_Parameters : GlTF_Writer {
  public GlTF_Accessor timeAccessor;
  public GlTF_Accessor translationAccessor;
  public GlTF_Accessor rotationAccessor;
  public GlTF_Accessor scaleAccessor;

  // seems like a bad place for this
  private float[] times;// = new float[curve.keys.Length];
  private Vector3[] positions;
  private Vector3[] scales;
  private Vector4[] rotations;
  private bool px, py, pz;
  private bool sx, sy, sz;
  private bool rx, ry, rz, rw;

  public GlTF_Parameters(string n) {
    name = n;
  }

  public void Populate(AnimationClipCurveData curveData) {
    string propName = curveData.propertyName;
    if (times == null) // allocate one array of times, assumes all channels have same number of keys
    {
      timeAccessor = new GlTF_Accessor(name + "TimeAccessor", GlTF_Accessor.Type.SCALAR, GlTF_Accessor.ComponentType.FLOAT);
      timeAccessor.bufferView = GlTF_Writer.floatBufferView;
      GlTF_Writer.accessors.Add(timeAccessor);
      times = new float[curveData.curve.keys.Length];
      for (int i = 0; i < curveData.curve.keys.Length; i++)
        times[i] = curveData.curve.keys[i].time;
      timeAccessor.PopulateHalfFloat(times);
    }

    if (propName.Contains("m_LocalPosition")) {
      if (positions == null) {
        translationAccessor = new GlTF_Accessor(name + "TranslationAccessor", GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT);
        translationAccessor.bufferView = GlTF_Writer.vec3BufferView;
        GlTF_Writer.accessors.Add(translationAccessor);
        positions = new Vector3[curveData.curve.keys.Length];
      }

      if (propName.Contains(".x")) {
        px = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          positions[i].x = curveData.curve.keys[i].value;
      } else if (propName.Contains(".y")) {
        py = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          positions[i].y = curveData.curve.keys[i].value;
      } else if (propName.Contains(".z")) {
        pz = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          positions[i].z = curveData.curve.keys[i].value;
      }
      if (px && py && pz)
        translationAccessor.Populate(positions, convertToGL: true, convertToMeters:true);
    }

    if (propName.Contains("m_LocalScale")) {
      if (scales == null) {
        scaleAccessor = new GlTF_Accessor(name + "ScaleAccessor", GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT);
        scaleAccessor.bufferView = GlTF_Writer.vec3BufferView;
        GlTF_Writer.accessors.Add(scaleAccessor);
        scales = new Vector3[curveData.curve.keys.Length];
      }

      if (propName.Contains(".x")) {
        sx = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          scales[i].x = curveData.curve.keys[i].value;
      } else if (propName.Contains(".y")) {
        sy = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          scales[i].y = curveData.curve.keys[i].value;
      } else if (propName.Contains(".z")) {
        sz = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          scales[i].z = curveData.curve.keys[i].value;
      }
      if (sx && sy && sz)
        scaleAccessor.Populate(scales, convertToGL: false, convertToMeters: false);
    }

    if (propName.Contains("m_LocalRotation")) {
      if (rotations == null) {
        rotationAccessor = new GlTF_Accessor(name + "RotationAccessor", GlTF_Accessor.Type.VEC4, GlTF_Accessor.ComponentType.FLOAT);
        rotationAccessor.bufferView = GlTF_Writer.vec4BufferView;
        GlTF_Writer.accessors.Add(rotationAccessor);
        rotations = new Vector4[curveData.curve.keys.Length];
      }

      if (propName.Contains(".x")) {
        rx = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          rotations[i].x = curveData.curve.keys[i].value;
      } else if (propName.Contains(".y")) {
        ry = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          rotations[i].y = curveData.curve.keys[i].value;
      } else if (propName.Contains(".z")) {
        rz = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          rotations[i].z = curveData.curve.keys[i].value;
      } else if (propName.Contains(".w")) {
        rw = true;
        for (int i = 0; i < curveData.curve.keys.Length; i++)
          rotations[i].w = curveData.curve.keys[i].value;
      }
      if (rx && ry && rz && rw)
        rotationAccessor.Populate(scales, convertToGL: false, convertToMeters: false);
    }
  }

  public override void Write() {
    Indent(); jsonWriter.Write("\"" + "parameters" + "\": {\n");
    IndentIn();
    if (times != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"" + "TIME" + "\": \"" + timeAccessor.name + "\"");
    }
    if (rotations != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"" + "rotation" + "\": \"" + rotationAccessor.name + "\"");
    }
    if (scales != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"" + "scale" + "\": \"" + scaleAccessor.name + "\"");
    }
    if (positions != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"" + "translation" + "\": \"" + translationAccessor.name + "\"");
    }
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("}");
  }

  /*
	public Dictionary<string, string> parms = new Dictionary<string,string>();

	public void AddPararm (string key, string val)
	{
		parms.Add (key, val);
	}

	public override void Write()
	{
		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
		IndentIn();
		foreach (KeyValuePair<string,string> p in parms)
		{
			CommaNL();
			Indent();	jsonWriter.Write ("\"" + p.Key + "\": \"" + p.Value +"\"");
		}
		jsonWriter.WriteLine();

		IndentOut();
		Indent();		jsonWriter.Write ("}");
	}
*/
}
#endif
