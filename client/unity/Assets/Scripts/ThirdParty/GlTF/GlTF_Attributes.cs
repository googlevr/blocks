using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GlTF_Attributes : GlTF_Writer {
  public GlTF_Accessor normalAccessor;
  public GlTF_Accessor colorAccessor;
  public GlTF_Accessor tangentAccessor;
  public GlTF_Accessor vertexIdAccessor;
  public GlTF_Accessor positionAccessor;
  public GlTF_Accessor texCoord0Accessor;
  public GlTF_Accessor texCoord1Accessor;
  public GlTF_Accessor texCoord2Accessor;
  public GlTF_Accessor texCoord3Accessor;

  // Populate the accessor with the given UV channel on the given mesh, with the correct number of
  // elements used by the mesh (e.g. scalar/Vec2/Vec3/Vec4).
  private void PopulateUv(int channel, Mesh mesh, GlTF_Accessor accessor, bool packVertId = false) {
    if (accessor == null) {
      return;
    }
    if (channel < 0 || channel > 3) {
      throw new ArgumentException("Invalid channel");
    }

    if (packVertId && (accessor.type != GlTF_Accessor.Type.VEC4 || channel != 1)) {
      throw new ArgumentException("VertexIDs can only be packed into channel 1 with vec4 uvs");
    }

    switch (accessor.type) {
    case GlTF_Accessor.Type.SCALAR:
      List<Vector2> uvTemp = new List<Vector2>();
      mesh.GetUVs(channel, uvTemp);
      float[] uvs = new float[uvTemp.Count];
      for (int i = 0; i < uvTemp.Count; i++) {
        uvs[i] = uvTemp[i].x;
      }
      accessor.Populate(uvs);
      return;
    case GlTF_Accessor.Type.VEC2:
      // Fast path, avoid conversions.
      switch (channel) {
      case 0:
        accessor.Populate(mesh.uv, true);
        return;
      case 1:
        accessor.Populate(mesh.uv2, true);
        return;
      case 2:
        accessor.Populate(mesh.uv3, true);
        return;
      case 3:
      default:
        accessor.Populate(mesh.uv4, true);
        return;
      }
    case GlTF_Accessor.Type.VEC3:
      // This could use ListExtensions to avoid a copy, but that code lives in TiltBrush.
      List<Vector3> uvTemp3 = new List<Vector3>();
      mesh.GetUVs(channel, uvTemp3);
      accessor.Populate(uvTemp3, convertToGL: false);
      return;
    case GlTF_Accessor.Type.VEC4:
      List<Vector4> uvTemp4 = new List<Vector4>();
      mesh.GetUVs(channel, uvTemp4);
      if (packVertId) {
        for (int i = 0; i < uvTemp4.Count; i++) {
          Vector4 v = uvTemp4[i];
          v.w = i;
          uvTemp4[i] = v;
        }
      }
      accessor.Populate(uvTemp4);
      return;
    default:
      throw new ArgumentException("Unexpected accessor type");
    }
  }

  public void Populate(Mesh m) {
    positionAccessor.Populate(m.vertices, convertToGL: true, convertToMeters: true);
    if (normalAccessor != null) {
      normalAccessor.Populate(m.normals, convertToGL: true, convertToMeters: false);
    }
    if (colorAccessor != null) {
      // We assume that colors are LDR and use the more efficient colors32 format.
      //
      // TODO(ineula): Switch the glTF vertex color format to use a uint8 triple
      // or similar. This will require some changes to ThirdParty/GlTF.
      var colors = Array.ConvertAll(m.colors32, item =>
          new Vector4(item.r * 1.0f/255, item.g * 1.0f/255, item.b * 1.0f/255, item.a * 1.0f/255));
      colorAccessor.Populate(colors, convertToGL: false);
    }
    if (tangentAccessor != null) {
      tangentAccessor.Populate(m.tangents, convertToGL: true);
    }
    if (vertexIdAccessor != null) {
      float[] vertexIds = new float[m.vertexCount];
      for (float i = 0; i < m.vertexCount; i++) {
        vertexIds[(int)i] = i;
      }
      vertexIdAccessor.Populate(vertexIds);
    }

    // UVs may be 1, 2, 3 or 4 element tuples, which the following helper method resolves.
    // In the case of zero UVs, the texCoord accessor will be null and will not be populated.
    PopulateUv(0, m, texCoord0Accessor);
    PopulateUv(1, m, texCoord1Accessor, packVertId: vertexIdAccessor != null);
    PopulateUv(2, m, texCoord2Accessor);
    PopulateUv(3, m, texCoord3Accessor);
  }

  public override void Write() {
    Indent(); jsonWriter.Write("\"attributes\": {\n");
    IndentIn();
    if (positionAccessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"POSITION\": \"" + positionAccessor.name + "\"");
    }
    if (normalAccessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"NORMAL\": \"" + normalAccessor.name + "\"");
    }
    if (colorAccessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"COLOR\": \"" + colorAccessor.name + "\"");
    }
    if (tangentAccessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"TANGENT\": \"" + tangentAccessor.name + "\"");
    }
    if (vertexIdAccessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"VERTEXID\": \"" + vertexIdAccessor.name + "\"");
    }
    if (texCoord0Accessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"TEXCOORD_0\": \"" + texCoord0Accessor.name + "\"");
    }
    if (texCoord1Accessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"TEXCOORD_1\": \"" + texCoord1Accessor.name + "\"");
    }
    if (texCoord2Accessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"TEXCOORD_2\": \"" + texCoord2Accessor.name + "\"");
    }
    if (texCoord3Accessor != null) {
      CommaNL();
      Indent(); jsonWriter.Write("\"TEXCOORD_3\": \"" + texCoord3Accessor.name + "\"");
    }
    //CommaNL();
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("}");
  }

}
