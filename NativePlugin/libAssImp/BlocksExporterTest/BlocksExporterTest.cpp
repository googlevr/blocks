#include <iostream>
#include <string>
#include <random>
#include "DllExports.h"

void dummylog(const char * logLine) {
	std::cout << logLine << std::endl;
}

void EmitModel(std::string filename, int matId);

void generatedTest();

void main() {
	SetDebugFunction(&dummylog);
	std::cout << "Hello, world." << std::endl;
	EmitModel("test_model.fbx", 8);
	//EmitModel("Glass.dae", 24);

	//generatedTest();
	Debug("Done");
	//EmitModel("Gem.dae", 25);
	std::string temp;
	//std::cin >> temp;
	//return;

	Vector3 a;
	Vector3 b;
	int results[1000];
	/*
	int spaceId = AllocSpatialPartitioner(a, b);

	std::default_random_engine generator;
	std::uniform_real_distribution<float> distribution(0, 10);
	for (int i = 0; i < 100; i++) {
		Vector3 a, b;
		a.x = distribution(generator);
		a.y = distribution(generator);
		a.z = distribution(generator);
		b.x = distribution(generator);
		b.y = distribution(generator);
		b.z = distribution(generator);
		//std::cout << a.to_string() << std::endl;
		SpatialPartitionerAddItem(spaceId, i, a, b);
	}
	int results[1000];
	int results2[1000];

	for (int i = 0; i < 100; i++) {
		Vector3 a, b;
		
		a.x = distribution(generator);
		a.y = distribution(generator);
		a.z = distribution(generator);
		b.x = distribution(generator);
		b.y = distribution(generator);
		b.z = distribution(generator);
		std::cout << "Testing a bounding box " << a.to_string() << " with extents " << b.to_string() << std::endl;
		int resultsA = SpatialPartitionerIntersectedBy(spaceId, a, b, results, 1000);
		int resultsB = SpatialPartitionerIntersectedByOrig(spaceId, a, b, results2, 1000);
		if (resultsA != resultsB) {
			std::cout << "Number of results from vectorized test " << resultsA << " didn't match the number of the results from standard test" << resultsB << std::endl;
		}
		std::cout << " got " << resultsA << " intersections " << std::endl;
		for (int j = 0; j < resultsA; j++) {
			if (results[j] != results2[j]) {
				std::cout << "Vectorized test returned " << results[j] << " in a slot where standard test returned " << results2[j] << std::endl;
			}
		}
	}
	*/
	int spaceId2 = AllocSpatialPartitioner(a, b);
	Vector3 targetCenter;
	targetCenter.x = 0;
	targetCenter.y = 0;
	targetCenter.z = 0;
	Vector3 targetExtents;
	targetExtents.x = 0.5;
	targetExtents.y = 0.5;
	targetExtents.z = 0.5;
	SpatialPartitionerAddItem(spaceId2, 1, targetCenter, targetExtents);
	int resultCount = SpatialPartitionerIntersectedBy(spaceId2, targetCenter, targetExtents, results, 1000);
	std::cout << "Collision test found " << resultCount << " collisions" << std::endl;
	targetCenter.x = 1.25;
	targetCenter.y = 1.25;
	targetCenter.z = 1.25;

	resultCount = SpatialPartitionerIntersectedBy(spaceId2, targetCenter, targetExtents, results, 1000);
	std::cout << "Collision test found " << resultCount << " collisions" << std::endl;

	Debug("Done");
	//EmitModel("Gem.dae", 25);
	temp;
	std::cin >> temp;

}

void generatedTest() {
	// Paste output from debug dll here to locally debug sequences that cause errors in the app.
}

void EmitModel(std::string filename, int matId) {
	//StartExport();

	//CreateTexturedCube();

//	Vector3* vertArray = new Vector3[24];
//	Color32* colorArray = new Color32[24];
//
//	Vector3* vertices = new Vector3[8];
//	vertices[0] = Vector3(-50, 0, 50);
//	vertices[1] = Vector3(50, 0, 50);
//	vertices[2] = Vector3(50, 100, 50);
//	vertices[3] = Vector3(-50, 100, 50);
//	vertices[4] = Vector3(-50, 0, -50);
//	vertices[5] = Vector3(50, 0, -50);
//	vertices[6] = Vector3(50, 100, -50);
//	vertices[7] = Vector3(-50, 100, -50);
///*
//	vertArray[0] = Vector3(1.0, -1.0, -1.0);
//	vertArray[1] = Vector3(1.0, 1.0, -1.0);
//	vertArray[2] = Vector3(1.0, 1.0, 1.0);
//	vertArray[3] = Vector3(1.0, -1.0, 1.0);
//
//	vertArray[7] = Vector3(-1.0, -1.0, -1.0);
//	vertArray[6] = Vector3(-1.0, 1.0, -1.0);
//	vertArray[5] = Vector3(-1.0, 1.0, 1.0);
//	vertArray[4] = Vector3(-1.0, -1.0, 1.0);
//
//	vertArray[8] = Vector3(-1.0, -1.0, -1.0);
//	vertArray[9] = Vector3(1.0, -1.0, -1.0);
//	vertArray[10] = Vector3(1.0, -1.0, 1.0);
//	vertArray[11] = Vector3(-1.0, -1.0, 1.0);
//
//	vertArray[15] = Vector3(-1.0, 1.0, -1.0);
//	vertArray[14] = Vector3(1.0, 1.0, -1.0);
//	vertArray[13] = Vector3(1.0, 1.0, 1.0);
//	vertArray[12] = Vector3(-1.0, 1.0, 1.0);
//
//	vertArray[16] = Vector3(-1.0, -1.0, 1.0);
//	vertArray[17] = Vector3(1.0, -1.0, 1.0);
//	vertArray[18] = Vector3(1.0, 1.0, 1.0);
//	vertArray[19] = Vector3(-1.0, 1.0, 1.0);
//
//	vertArray[23] = Vector3(-1.0, -1.0, -1.0);
//	vertArray[22] = Vector3(1.0, -1.0, -1.0);
//	vertArray[21] = Vector3(1.0, 1.0, -1.0);
//	vertArray[20] = Vector3(-1.0, 1.0, -1.0);*/
//
//	int* triangles = new int[36];
//
//	for (int i = 0; i < 36; i += 6) {
//		triangles[i] = 4 * i;
//		triangles[i + 1] = 4 * i + 1;
//		triangles[i + 2] = 4 * i + 2;
//
//		triangles[i + 3] = 4 * i;
//		triangles[i + 4] = 4 * i + 2;
//		triangles[i + 5] = 4 * i + 3;
//	}
//
//	////AddMesh(ma/*tId,
//	//	vertArray,
//	//	colorArray,
//	//	triangles,
//	//	24,
//	//	36);*/
///*
//	AddMesh(matId,
//		vertices,
//		colorArray,
//		triangles,
//		8,
//		36);*/
//
	//FinishExport();

	return;
}


