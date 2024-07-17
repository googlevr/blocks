#pragma once

#ifndef BLOCKSEXPORT
#define BLOCKSEXPORT __declspec(dllexport)
#endif // !BLOCKSEXPORT
#include <string>
#include "VectorTypes.h"

extern "C" {


	BLOCKSEXPORT void SetDebugFunction(FuncPtr fp);


	/// Initializes the Fbx manager and scene.
	BLOCKSEXPORT void StartExport(char* filePath);

	/// Starts a new mesh node, and updates currentMesh and currentMaterialLayer pointers.
	BLOCKSEXPORT void StartMesh(int meshId, int groupKey);

	/// Adds vertice information to the current mesh.
	BLOCKSEXPORT void AddMeshVertices(Vector3 vertices[], int numVerts);

	/// Adds a new polygon to the current mesh.
	BLOCKSEXPORT void AddFace(int matId, int vertexIndices[], int numVertices, Vector3 normal);

	// Adds a mesh with the passed vertex and triangle information; mesh MUST be triangulated.
	BLOCKSEXPORT void AddMesh(int matId,
		Vector3 vertices[],
		int triangles[],
		Vector3 normals[],
		int numVerts,
		int numTris,
		int numNormals);

	/// Responsible for calling FbxExporter.Export and saving the file, and performing necessary
	/// cleanup.
	BLOCKSEXPORT void FinishExport();


}