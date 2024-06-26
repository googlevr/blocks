using UnityEngine;

// A descriptive structure that allows Unity mesh vertex attributes to be inspected.
// Unfortunately, Unity provides no such interface.
public struct GlTF_VertexLayout {

  // The number of elements per-value in a UV channel.
  // e.g. a typical Vec2 UV is UvElementCount.Two.
  public enum UvElementCount {
    None,
    One,
    Two,
    Three,
    Four
  }

  public UvElementCount uv0;
  public UvElementCount uv1;
  public UvElementCount uv2;
  public UvElementCount uv3;

  public bool hasNormals;
  public bool hasTangents;
  public bool hasColors;
  public bool hasVertexIds;

  // Convert a UvElementCount to an Accessor Type.
  public static GlTF_Accessor.Type GetUvType(UvElementCount elements) {
    switch (elements) {
    case UvElementCount.One:
      return GlTF_Accessor.Type.SCALAR;
    case UvElementCount.Two:
      return GlTF_Accessor.Type.VEC2;
    case UvElementCount.Three:
      return GlTF_Accessor.Type.VEC3;
    case UvElementCount.Four:
      return GlTF_Accessor.Type.VEC4;
    default:
      throw new System.ArgumentException(
          string.Format("Invalid elements value {0}", (int)elements));
    }
  }
}