#include "libAssImp\VectorTypes.h"
void SetDebugFunction_Internal(FuncPtr fp);

/// Initializes the Fbx manager and scene.
void StartExport_Internal(char* filePath);

/// Starts a new mesh node, and updates currentMesh and currentMaterialLayer pointers.
void StartMesh_Internal(int meshId, int groupKey);

/// Adds vertice information to the current mesh.
void AddMeshVertices_Internal(Vector3 vertices[], int numVerts);

/// Adds a new polygon to the current mesh.
void AddFace_Internal(int matId, int vertexIndices[], int numVertices, Vector3 normal);

// Adds a mesh with the passed vertex and triangle information; mesh MUST be triangulated.
void AddMesh_Internal(int matId,
	Vector3 vertices[],
	int triangles[],
	Vector3 normals[],
	int numVerts,
	int numTris,
	int numNormals);

/// Responsible for calling FbxExporter.Export and saving the file, and performing necessary
/// cleanup.
void FinishExport_Internal();
