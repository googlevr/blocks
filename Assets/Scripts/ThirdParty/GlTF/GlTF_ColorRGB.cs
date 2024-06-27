using UnityEngine;
using System.Collections;

public class GlTF_ColorRGB : GlTF_Writer
{
    private Color color;
    public GlTF_ColorRGB(string n)
    {
        name = n;
    }
    public GlTF_ColorRGB(Color c)
    {
        color = c;
    }
    public GlTF_ColorRGB(string n, Color c)
    {
        name = n; color = c;
    }
    public override void Write()
    {
        Indent();
        if (name.Length > 0)
            jsonWriter.Write("\"" + name + "\": ");
        else
            jsonWriter.Write("\"color\": [");
        jsonWriter.Write(color.r.ToString() + ", " + color.g.ToString() + ", " + color.b.ToString() + "]");
    }
}
