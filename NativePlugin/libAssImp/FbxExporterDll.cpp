#define _CRT_SECURE_NO_WARNINGS
#include "FbxExporterDll.h"

#include <algorithm>
#include <map>
#include <ctime>
#include <iostream>  
#include <fstream>
#include <vector>
#include <set>
#include <fbxsdk.h>
#include "assimp\mesh.h"
#include "assimp\scene.h"
#include "assimp\Exporter.hpp"

extern "C" {
	static uint32_t rawColors[] = {
		0xBA68C8,
		0x9C27B0,
		0x673AB7,
		0x80DEEA,
		0x00BCD4,
		0x039BE5,
		0xF8BBD0,
		0xF06292,
		0xF44336,
		0x8BC34A,
		0x4CAF50,
		0x009688,
		0xFFEB3B,
		0xFF9800,
		0xFF5722,
		0xCFD8DC,
		0x78909C,
		0x455A64,
		0xFFCC88,
		0xDD9944,
		0x795548,
		0xFFFFFF,
		0x9E9E9E,
		0x1A1A1A,
	};

	static FuncPtr Debug;
	std::ofstream outfile;

	float fbxFromUnityScale = 100.0f;

	TESTDLLSORT_API void SetDebugFunction(FuncPtr fp)
	{
		Debug = fp;
		Debug("Debug function");
	}

	int nodeCount;

	// Key of the mesh group if the mesh is not in a group.
	const int MESH_GROUP_NONE = 0;

	FbxManager* manager;
	FbxScene* fbxScene;
	FbxMesh* currentMesh;
	FbxLayerElementMaterial* currentMaterialLayer;

	std::map<int, FbxNode*> groupMap;

	const char* fname;
	const int NUM_MATERIALS = 26;

	float getR(int raw) {
		return ((raw >> 16) & 255) / 255.0f;
	}

	float getG(int raw) {
		return ((raw >> 8) & 255) / 255.0f;
	}

	float getB(int raw) {
		return (raw & 255) / 255.0f;
	}

	// To preserve the correct string information when passing between managed (Unity) and
	// unmanaged (here) code, we need to make a copy of the string.
	char* MakeStringCopy(const char* string) {
		if (string == NULL) return NULL;
		char* res = (char*)malloc(strlen(string) + 1);
		strcpy(res, string);
		return res;
	}

	TESTDLLSORT_API void StartExport(char* filePath) {
		fname = MakeStringCopy(filePath);
		manager = FbxManager::Create();
		nodeCount = 0;

		fbxScene = FbxScene::Create(manager, "sceneroot");
		if (fbxScene == NULL) {
			Debug("FBX export failed; could not intialize scene");
			manager->Destroy();
			return;
		}
		return;
	};

	void CreateMaterialForMesh(FbxMesh* mesh, int matId) {
		std::srand(std::time(0));
		int randInt = std::rand();
		// Generate a unique material name, or else Unity will reuse an existing material upon import.
		// This is probably only necessary when changing the material definitions.
		std::string materialName = "material_" + std::to_string(matId) + "___" + std::to_string(randInt);
		FbxSurfacePhong* meshMaterial = FbxSurfacePhong::Create(fbxScene, materialName.c_str());

		float r = getR(rawColors[matId]);
		float g = getG(rawColors[matId]);
		float b = getB(rawColors[matId]);
		if (matId < 24) {
			meshMaterial->TransparencyFactor.Set(0.0);
			meshMaterial->Shininess.Set(0.0);
		} else {
			// Glass and gem.
			r = 0.8;
			g = 0.8;
			b = 0.8;
			meshMaterial->TransparencyFactor.Set(0.4);
			meshMaterial->Shininess.Set(0.6);
		} // TODO handle glass/gem
	    // Generate primary and secondary colors.
		meshMaterial->Emissive.Set(FbxDouble3(r, g, b));
		meshMaterial->EmissiveFactor.Set(0.0);
		meshMaterial->Ambient.Set(FbxDouble3(r, g, b));
		meshMaterial->AmbientFactor.Set(0.0);
		// Add texture for diffuse channel
		meshMaterial->Diffuse.Set(FbxDouble3(r, g, b));
		meshMaterial->DiffuseFactor.Set(1.);
		meshMaterial->ShadingModel.Set("Phong");
		meshMaterial->Specular.Set(FbxDouble3(0.0, 0, 0.0));
		meshMaterial->SpecularFactor.Set(0.0);
		FbxNode* node = mesh->GetNode();
		if (node) {
			node->AddMaterial(meshMaterial);
		}
	}

	TESTDLLSORT_API void StartMesh(int meshId, int groupKey) {
		// Create a mesh.
		currentMesh = FbxMesh::Create(manager, "mesh");
		std::string nodeName = "mesh_" + std::to_string(meshId);
		FbxNode* meshNode = FbxNode::Create(manager, nodeName.c_str());
		meshNode->SetNodeAttribute(currentMesh);
		FbxNode *rootNode = fbxScene->GetRootNode();

		if (groupKey == MESH_GROUP_NONE) {
			// Add the mesh node to the root node in the scene.
			rootNode->AddChild(meshNode);
		} else {
			// Add the mesh node to the group node it belongs to.
			if (groupMap.find(groupKey) == groupMap.end()) {
				// Create a new node for this group. 
				std::string groupName = "group_" + std::to_string(groupKey);
				groupMap[groupKey] = FbxNode::Create(manager, groupName.c_str());
				rootNode->AddChild(groupMap[groupKey]);
			}
			groupMap.at(groupKey)->AddChild(meshNode);
		}
		// Create a material layer element; each mesh has a single material corresponding to it, so the mapping mode
		// is allSame and we init the index array with a single element.
		currentMaterialLayer = currentMesh->CreateElementMaterial();
		currentMaterialLayer->SetMappingMode(FbxGeometryElement::eByPolygon);
		currentMaterialLayer->SetReferenceMode(FbxGeometryElement::eIndexToDirect);

		for (int i = 0; i < NUM_MATERIALS; i++) {
			CreateMaterialForMesh(currentMesh, i);
		}

		FbxLayer *layer0 = currentMesh->GetLayer(0);
		if (layer0 == NULL) {
			currentMesh->CreateLayer();
			layer0 = currentMesh->GetLayer(0);
		}

		// Create a normal layer. The mapping eByPolygonVertex is important to exporting sharp edges, as a mapping
		// of normals per polygon will result in soft edges that look unpleasant with a low poly aesthetic.
		FbxLayerElementNormal* lLayerElementNormal = FbxLayerElementNormal::Create(currentMesh, "normals");
		lLayerElementNormal->SetMappingMode(FbxLayerElement::eByPolygonVertex);
		lLayerElementNormal->SetReferenceMode(FbxLayerElement::eDirect);
		layer0->SetNormals(lLayerElementNormal);
	}

	TESTDLLSORT_API void AddFace(int matId, int vertexIndices[], int numVertices, Vector3 normal) {
		currentMaterialLayer->GetIndexArray().Add(matId);
		currentMesh->BeginPolygon(/*materialIndex*/ matId);
		for (int i = 0; i < numVertices; i++) {
			currentMesh->AddPolygon(vertexIndices[i]);
		}
		currentMesh->EndPolygon();

		FbxLayerElementNormal* normalsLayer = currentMesh->GetLayer(0)->GetNormals();
		for (int i = 0; i < numVertices; i++) {
			// Negate their x coordinate.
			normalsLayer->GetDirectArray().Add(FbxVector4(-normal.x, normal.y, normal.z));
		}
	}

	TESTDLLSORT_API void AddMeshVertices(Vector3 vertices[], int numVerts) {
		// Initialize the control point array of the mesh.
		currentMesh->InitControlPoints(numVerts);
		FbxVector4* lControlPoints = currentMesh->GetControlPoints();
		for (int i = 0; i < numVerts; i++) {
			// Scale each vertice and negate its x coordinate.
			lControlPoints[i] = FbxVector4(-vertices[i].x, vertices[i].y, vertices[i].z) * fbxFromUnityScale;
		}
	}

	TESTDLLSORT_API void AddMesh(int matId,
		Vector3 vertices[],
		int triangles[],
		Vector3 normals[],
		int numVerts,
		int numTris,
		int numNormals) {
		// Create a mesh.
		currentMesh = FbxMesh::Create(manager, "mesh");

		// Create a node for our mesh in the scene.
		nodeCount += 1;
		std::string nodeName = "meshNode_" + std::to_string(nodeCount);
		FbxNode* meshNode = FbxNode::Create(manager, nodeName.c_str());
		meshNode->SetNodeAttribute(currentMesh);
		// Add the mesh node to the root node in the scene.
		FbxNode *rootNode = fbxScene->GetRootNode();
		rootNode->AddChild(meshNode);
		
		// Initialize the control point array of the mesh.
		currentMesh->InitControlPoints(numVerts);
		FbxVector4* lControlPoints = currentMesh->GetControlPoints();
		for (int i = 0; i < numVerts; i++) {
			// Scale each vertice and negate its x coordinate.
			lControlPoints[i] = FbxVector4(-vertices[i].x, vertices[i].y, vertices[i].z) * fbxFromUnityScale;
		}

		currentMesh->ReservePolygonCount(numTris / 3);
		currentMesh->ReservePolygonVertexCount(numTris);
		for (int i = 0; i < numTris; i += 3) {
			currentMesh->BeginPolygon(-1);
			// Reverse the triangle winding order when exporting to FBX.
			currentMesh->AddPolygon(triangles[i + 1]);
			currentMesh->AddPolygon(triangles[i]);
			currentMesh->AddPolygon(triangles[i + 2]);
			currentMesh->EndPolygon();
		}

		// Create layer 0 for the mesh if it does not already exist.
		// This is where we will define our normals and materials.
		FbxLayer *layer0 = currentMesh->GetLayer(0);
		if (layer0 == NULL) {
			currentMesh->CreateLayer();
			layer0 = currentMesh->GetLayer(0);
		}

		// Create a material layer element; each mesh has a single material corresponding to it, so the mapping mode
		// is allSame and we init the index array with a single element.
		FbxLayerElementMaterial* lMaterialElement = FbxLayerElementMaterial::Create(currentMesh, "materials");
		lMaterialElement->SetMappingMode(FbxGeometryElement::eAllSame);
		lMaterialElement->SetReferenceMode(FbxGeometryElement::eIndexToDirect);
		lMaterialElement->GetIndexArray().Add(0);
		layer0->SetMaterials(lMaterialElement);

		// Create the material corresponding to this material ID.
		CreateMaterialForMesh(currentMesh, matId);

		//Create a normal layer.
		FbxLayerElementNormal* lLayerElementNormal = FbxLayerElementNormal::Create(currentMesh, "normals");
		lLayerElementNormal->SetMappingMode(FbxLayerElement::eByPolygon);
		lLayerElementNormal->SetReferenceMode(FbxLayerElement::eDirect);
		for (int i = 0; i < numNormals; i++) {
			// Scale the normals and negate their x coordinate.
			lLayerElementNormal->GetDirectArray().Add(FbxVector4(-normals[i].x, normals[i].y, normals[i].z) * fbxFromUnityScale);
		}
		layer0->SetNormals(lLayerElementNormal);
	}

	TESTDLLSORT_API void FinishExport() {
		// Create an IOSettings object.
		FbxIOSettings* ioSettings = FbxIOSettings::Create(manager, IOSROOT);
		manager->SetIOSettings(ioSettings);
		FbxExporter* fbxExporter = FbxExporter::Create(manager, "");

		bool fbxExportStatus = fbxExporter->Initialize(fname, -1, manager->GetIOSettings());
		if (!fbxExportStatus) {
			Debug("Export FAILED");
			printf("Call to FbxExporter::Initialize() failed.\n");
			printf("Error returned: %s\n\n", fbxExporter->GetStatus().GetErrorString());
			return;
		}
		fbxExporter->SetFileExportVersion(FBX_2014_00_COMPATIBLE);

		// Export the scene to the file.
		fbxExporter->Export(fbxScene);

		groupMap.clear();

		fbxExporter->Destroy();
		ioSettings->Destroy();
		manager->Destroy();
	};
}