#include "FbxSupportDllInterface.h"
#include "FBXSupport.h"

BLOCKSEXPORT void StartExport(char* filePath) {
	StartExport_Internal(filePath);
};


BLOCKSEXPORT void StartMesh(int meshId, int groupKey) {
	StartMesh_Internal(meshId, groupKey);
}

BLOCKSEXPORT void AddFace(int matId, int vertexIndices[], int numVertices, Vector3 normal) {
	AddFace_Internal(matId, vertexIndices, numVertices, normal);
}

BLOCKSEXPORT void AddMeshVertices(Vector3 vertices[], int numVerts) {
	AddMeshVertices_Internal(vertices, numVerts);
}

BLOCKSEXPORT void AddMesh(int matId,
	Vector3 vertices[],
	int triangles[],
	Vector3 normals[],
	int numVerts,
	int numTris,
	int numNormals) {
	AddMesh_Internal(matId, vertices, triangles, normals, numVerts, numTris, numNormals);
}

BLOCKSEXPORT void FinishExport() {
	FinishExport_Internal();
}