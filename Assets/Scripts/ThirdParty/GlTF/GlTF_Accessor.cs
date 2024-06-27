using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GlTF_Accessor : GlTF_Writer
{
    public enum Type
    {
        SCALAR,
        VEC2,
        VEC3,
        VEC4
    }

    public enum ComponentType
    {
        USHORT = 5123,
        FLOAT = 5126
    }

    public GlTF_BufferView bufferView;//	"bufferView": "bufferView_30",
    public long byteOffset; //": 0,
    public int byteStride;// ": 12,
                          // GL enum vals ": BYTE (5120), UNSIGNED_BYTE (5121), SHORT (5122), UNSIGNED_SHORT (5123), FLOAT
                          // (5126)
    public ComponentType componentType;
    public int count;//": 2399,
    public Type type = Type.SCALAR;

    public Vector4 maxFloat;
    public Vector4 minFloat;
    public int minInt;
    public int maxInt;

    public GlTF_Accessor(string n)
    {
        name = n;
    }
    public GlTF_Accessor(string n, Type t, ComponentType c)
    {
        name = n;
        type = t;
        switch (t)
        {
            case Type.SCALAR:
                byteStride = 0;
                break;
            case Type.VEC2:
                byteStride = 8;
                break;
            case Type.VEC3:
                byteStride = 12;
                break;
            case Type.VEC4:
                byteStride = 16;
                break;
        }
        componentType = c;
    }

    public static string GetNameFromObject(Object o, string name)
    {
        return "accessor_" + name + "_" + GlTF_Writer.GetNameFromObject(o, true);
    }

    private void InitMinMaxInt()
    {
        maxInt = int.MinValue;
        minInt = int.MaxValue;
    }

    private void InitMinMaxFloat()
    {
        float min = float.MinValue;
        float max = float.MaxValue;
        maxFloat = new Vector4(min, min, min, min);
        minFloat = new Vector4(max, max, max, max);
    }

    public void PopulateUshort(int[] vs, bool flippedTriangle)
    {
        if (type != Type.SCALAR)
            throw (new System.Exception());
        byteOffset = bufferView.currentOffset;
        bufferView.PopulateUshort(vs, flippedTriangle);
        count = vs.Length;
        if (count > 0)
        {
            InitMinMaxInt();
            for (int i = 0; i < count; ++i)
            {
                minInt = Mathf.Min(vs[i], minInt);
                maxInt = Mathf.Max(vs[i], maxInt);
            }
        }
    }

    public void PopulateHalfFloat(float[] vs)
    {
        if (type != Type.SCALAR)
            throw (new System.Exception());
        byteOffset = bufferView.currentOffset;
        bufferView.PopulateHalfFloat(vs);
        count = vs.Length;
        if (count > 0)
        {
            InitMinMaxFloat();
            for (int i = 0; i < count; ++i)
            {
                minFloat.x = Mathf.Min(vs[i], minFloat.x);
                maxFloat.x = Mathf.Max(vs[i], maxFloat.x);
            }
        }
    }

    public void Populate(float[] vs)
    {
        if (type != Type.SCALAR)
            throw (new System.Exception());
        byteOffset = bufferView.currentOffset;
        bufferView.Populate(vs);
        count = vs.Length;
        if (count > 0)
        {
            InitMinMaxFloat();
            for (int i = 0; i < count; ++i)
            {
                minFloat.x = Mathf.Min(vs[i], minFloat.x);
                maxFloat.x = Mathf.Max(vs[i], maxFloat.x);
            }
        }
    }

    public void Populate(Vector2[] v2s, bool flip = false)
    {
        if (type != Type.VEC2)
            throw (new System.Exception());
        byteOffset = bufferView.currentOffset;
        count = v2s.Length;
        if (count > 0)
        {
            InitMinMaxFloat();

            if (flip)
            {
                for (int i = 0; i < v2s.Length; i++)
                {
                    bufferView.Populate(v2s[i].x);
                    float y = 1.0f - v2s[i].y;
                    bufferView.Populate(y);
                    minFloat.x = Mathf.Min(v2s[i].x, minFloat.x);
                    minFloat.y = Mathf.Min(y, minFloat.y);
                    maxFloat.x = Mathf.Max(v2s[i].x, maxFloat.x);
                    maxFloat.y = Mathf.Max(y, maxFloat.y);
                }
            }
            else
            {
                for (int i = 0; i < v2s.Length; i++)
                {
                    bufferView.Populate(v2s[i].x);
                    bufferView.Populate(v2s[i].y);
                    minFloat.x = Mathf.Min(v2s[i].x, minFloat.x);
                    minFloat.y = Mathf.Min(v2s[i].y, minFloat.y);
                    maxFloat.x = Mathf.Max(v2s[i].x, maxFloat.x);
                    maxFloat.y = Mathf.Max(v2s[i].y, maxFloat.y);
                }
            }
        }
    }

    private void PopulateVec3(IEnumerator<Vector3> v3s, bool convertToGL, bool convertToMeters)
    {
        if (type != Type.VEC3)
            throw (new System.Exception());
        byteOffset = bufferView.currentOffset;
        count = 0;

        while (v3s.MoveNext())
        {
            if (count == 0) { InitMinMaxFloat(); }
            count++;
            Vector3 v = v3s.Current;
            // Convert from DirectX/LeftHanded/(fwd=z,rt=x,up=y) -> OpenGL/RightHanded/(fwd=-z,rt=x,up=y)
            // Note that this must be kept in sync with ScriptableExporter.MakeNode(), which does a
            // similar conversion for matrices.
            if (convertToGL) { v.z *= -1; }
            if (convertToMeters) { v *= GlTFScriptableExporter.localUnitsToMeters; }
            bufferView.Populate(v.x);
            bufferView.Populate(v.y);
            bufferView.Populate(v.z);

            minFloat.x = Mathf.Min(v.x, minFloat.x);
            minFloat.y = Mathf.Min(v.y, minFloat.y);
            minFloat.z = Mathf.Min(v.z, minFloat.z);
            maxFloat.x = Mathf.Max(v.x, maxFloat.x);
            maxFloat.y = Mathf.Max(v.y, maxFloat.y);
            maxFloat.z = Mathf.Max(v.z, maxFloat.z);

        }
    }

    public void Populate(Vector3[] v3s, bool convertToGL, bool convertToMeters)
    {
        PopulateVec3(v3s.AsEnumerable().GetEnumerator(), convertToGL, convertToMeters);
    }

    public void Populate(List<Vector3> v3s, bool convertToGL)
    {
        PopulateVec3(v3s.GetEnumerator(), convertToGL, false);
    }

    private void PopulateVec4(IEnumerator<Vector4> v4s, bool convertToGL)
    {
        if (type != Type.VEC4)
            throw (new System.Exception());
        byteOffset = bufferView.currentOffset;
        count = 0;
        while (v4s.MoveNext())
        {
            if (count == 0) { InitMinMaxFloat(); }
            count++;
            Vector4 v = v4s.Current;
            // Convert from DirectX/LeftHanded/(fwd=z,rt=x,up=y) -> OpenGL/RightHanded/(fwd=-z,rt=x,up=y)
            // Note that this must be kept in sync with ScriptableExporter.MakeNode(), which does a
            // similar conversion for matrices.
            if (convertToGL) { v.z *= -1; }
            bufferView.Populate(v.x);
            bufferView.Populate(v.y);
            bufferView.Populate(v.z);
            bufferView.Populate(v.w);

            minFloat.x = Mathf.Min(v.x, minFloat.x);
            minFloat.y = Mathf.Min(v.y, minFloat.y);
            minFloat.z = Mathf.Min(v.z, minFloat.z);
            minFloat.w = Mathf.Min(v.w, minFloat.w);
            maxFloat.x = Mathf.Max(v.x, maxFloat.x);
            maxFloat.y = Mathf.Max(v.y, maxFloat.y);
            maxFloat.z = Mathf.Max(v.z, maxFloat.z);
            maxFloat.w = Mathf.Max(v.w, maxFloat.w);
        }
    }

    public void Populate(List<Vector4> v4s)
    {
        PopulateVec4(v4s.GetEnumerator(), convertToGL: false);
    }

    public void Populate(Vector4[] v4s, bool convertToGL)
    {
        PopulateVec4(v4s.AsEnumerable().GetEnumerator(), convertToGL);
    }

    private void WriteMin()
    {
        if (componentType == ComponentType.FLOAT)
        {
            switch (type)
            {
                case Type.SCALAR:
                    jsonWriter.Write(minFloat.x);
                    break;

                case Type.VEC2:
                    jsonWriter.Write(minFloat.x + ", " + minFloat.y);
                    break;

                case Type.VEC3:
                    jsonWriter.Write(minFloat.x + ", " + minFloat.y + ", " + minFloat.z);
                    break;

                case Type.VEC4:
                    jsonWriter.Write(minFloat.x + ", " + minFloat.y + ", " + minFloat.z + ", " + minFloat.w);
                    break;
            }
        }
        else if (componentType == ComponentType.USHORT)
        {
            if (type == Type.SCALAR)
            {
                jsonWriter.Write(minInt);
            }
        }
    }

    private void WriteMax()
    {
        if (componentType == ComponentType.FLOAT)
        {
            switch (type)
            {
                case Type.SCALAR:
                    jsonWriter.Write(maxFloat.x);
                    break;

                case Type.VEC2:
                    jsonWriter.Write(maxFloat.x + ", " + maxFloat.y);
                    break;

                case Type.VEC3:
                    jsonWriter.Write(maxFloat.x + ", " + maxFloat.y + ", " + maxFloat.z);
                    break;

                case Type.VEC4:
                    jsonWriter.Write(maxFloat.x + ", " + maxFloat.y + ", " + maxFloat.z + ", " + maxFloat.w);
                    break;
            }
        }
        else if (componentType == ComponentType.USHORT)
        {
            if (type == Type.SCALAR)
            {
                jsonWriter.Write(maxInt);
            }
        }
    }

    public override void Write()
    {
        Indent(); jsonWriter.Write("\"" + name + "\": {\n");
        IndentIn();
        Indent(); jsonWriter.Write("\"bufferView\": \"" + bufferView.name + "\",\n");
        Indent(); jsonWriter.Write("\"byteOffset\": " + byteOffset + ",\n");
        Indent(); jsonWriter.Write("\"byteStride\": " + byteStride + ",\n");
        Indent(); jsonWriter.Write("\"componentType\": " + (int)componentType + ",\n");
        Indent(); jsonWriter.Write("\"count\": " + count + ",\n");


        Indent(); jsonWriter.Write("\"max\": [ ");
        WriteMax();
        jsonWriter.Write(" ],\n");
        Indent(); jsonWriter.Write("\"min\": [ ");
        WriteMin();
        jsonWriter.Write(" ],\n");

        Indent(); jsonWriter.Write("\"type\": \"" + type + "\"\n");
        IndentOut();
        Indent(); jsonWriter.Write(" }");
    }
}
