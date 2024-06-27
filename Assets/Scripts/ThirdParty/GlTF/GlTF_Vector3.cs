using UnityEngine;
using System.Collections;

public class GlTF_Vector3 : GlTF_FloatArray
{
    public GlTF_Vector3()
    {
        minItems = 3; maxItems = 3; items = new float[] { 0f, 0f, 0f };
    }
    public GlTF_Vector3(Vector3 v)
    {
        minItems = 3; maxItems = 3; items = new float[] { v.x, v.y, v.z };
    }
}
