#define _CRT_SECURE_NO_WARNINGS
#include "DllExports.h"
#include "FBXSupport.h"
#include "NativeOctree\SpatialPartitioner.h"
#include <unordered_map>
#include <memory>
#include <iostream>
#include <fstream>

static FuncPtr _Debug;

void SetDebugFunction_Internal(FuncPtr fp)
{
	_Debug = fp;
	_Debug("Debug function");
}

BLOCKSEXPORT void Debug(const char * logline) {
	_Debug(logline);
}

BLOCKSEXPORT void SetDebugFunction(FuncPtr fp) {
	SetDebugFunction_Internal(fp);
}


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

static std::unordered_map<int, SpatialPartitioner> SpatialPartitionerMap = {};
static int nextSpatialPartitionerId = 0;

#ifdef BLOCKS_DEBUG
void InitCommandLog(int handle);

int WriteVector3Setup(int handle, Vector3 vec);

int WriteArrayTargetSetup(int handle, int size);

int WriteIntSetup(int handle, int val);

void WriteCommand(int handle, std::string name, int arg0);

void WriteCommand(int handle, std::string name, int arg0, int arg1);

void WriteCommand(int handle, std::string name, int arg0, int arg1, int arg2);

void WriteCommand(int handle, std::string name, int arg0, int arg1, int arg2, int arg3);
#endif

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT int AllocSpatialPartitioner(Vector3 center, Vector3 size) {
	//Debug("In AllocSpatialPartitioner");
	int id = nextSpatialPartitionerId++;
#ifdef BLOCKS_DEBUG
	int arg0 = WriteVector3Setup(id, center);
	int arg1 = WriteVector3Setup(id, size);
	InitCommandLog(id);
#endif // BLOCKS_DEBUG
	SpatialPartitionerMap[id] = SpatialPartitioner();
	return id;
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerAddItem(int SpatialPartitionerHandle, int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsSize) {
#ifdef BLOCKS_DEBUG
	int arg0 = WriteIntSetup(SpatialPartitionerHandle, itemId);
	int arg1 = WriteVector3Setup(SpatialPartitionerHandle, itemBoundsCenter);
	int arg2 = WriteVector3Setup(SpatialPartitionerHandle, itemBoundsSize);
	WriteCommand(SpatialPartitionerHandle, "AddItem", arg0, arg1, arg2);
#endif // BLOCKS_DEBUG
	SpatialPartitionerMap[SpatialPartitionerHandle].AddItem(itemId, itemBoundsCenter, itemBoundsSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerUpdateItem(int SpatialPartitionerHandle, int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsSize) {
#ifdef BLOCKS_DEBUG
	int arg0 = WriteIntSetup(SpatialPartitionerHandle, itemId);
	int arg1 = WriteVector3Setup(SpatialPartitionerHandle, itemBoundsCenter);
	int arg2 = WriteVector3Setup(SpatialPartitionerHandle, itemBoundsSize);
	WriteCommand(SpatialPartitionerHandle, "UpdateItem", arg0, arg1, arg2);
#endif // BLOCKS_DEBUG
	SpatialPartitionerMap[SpatialPartitionerHandle].UpdateItem(itemId, itemBoundsCenter, itemBoundsSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerRemoveItem(int SpatialPartitionerHandle, int itemId) {
#ifdef BLOCKS_DEBUG
	int arg0 = WriteIntSetup(SpatialPartitionerHandle, itemId);
	WriteCommand(SpatialPartitionerHandle, "RemoveItem", arg0);
#endif // BLOCKS_DEBUG
	SpatialPartitionerMap[SpatialPartitionerHandle].RemoveItem(itemId);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT int SpatialPartitionerContainedBy(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
#ifdef BLOCKS_DEBUG
	int arg0 = WriteVector3Setup(SpatialPartitionerHandle, testCenter);
	int arg1 = WriteVector3Setup(SpatialPartitionerHandle, testExtents);
	int arg2 = WriteArrayTargetSetup(SpatialPartitionerHandle, returnArrayMaxSize);
	int arg3 = WriteIntSetup(SpatialPartitionerHandle, returnArrayMaxSize);
	WriteCommand(SpatialPartitionerHandle, "ContainedBy", arg0, arg1, arg2, arg3);
#endif // BLOCKS_DEBUG
	return SpatialPartitionerMap[SpatialPartitionerHandle].ContainedBy(testCenter, testExtents, returnArray, returnArrayMaxSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT int SpatialPartitionerIntersectedBy(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
#ifdef BLOCKS_DEBUG
	int arg0 = WriteVector3Setup(SpatialPartitionerHandle, testCenter);
	int arg1 = WriteVector3Setup(SpatialPartitionerHandle, testExtents);
	int arg2 = WriteArrayTargetSetup(SpatialPartitionerHandle, returnArrayMaxSize);
	int arg3 = WriteIntSetup(SpatialPartitionerHandle, returnArrayMaxSize);
	WriteCommand(SpatialPartitionerHandle, "IntersectedBy", arg0, arg1, arg2, arg3);
#endif // BLOCKS_DEBUG
	return SpatialPartitionerMap[SpatialPartitionerHandle].IntersectedBy(testCenter, testExtents, returnArray, returnArrayMaxSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT int SpatialPartitionerIntersectedByOrig(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
	//Debug("In SpatialPartitionerIntersectedBy");
	return SpatialPartitionerMap[SpatialPartitionerHandle].IntersectedByOrig(testCenter, testExtents, returnArray, returnArrayMaxSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerHasItem(int SpatialPartitionerHandle, int itemHandle) {
#ifdef BLOCKS_DEBUG
	int arg0 = WriteIntSetup(SpatialPartitionerHandle, itemHandle);
	WriteCommand(SpatialPartitionerHandle, "HasItem", arg0);
#endif // BLOCKS_DEBUG
	SpatialPartitionerMap[SpatialPartitionerHandle].HasItem(itemHandle);
};

#ifdef BLOCKS_DEBUG
int varNum = 0;
int started = 0;

void InitCommandLog(int handle) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	if (started == 0) {
		outfile.open(stream.str(), std::ofstream::out | std::ofstream::trunc);
		started++;
	}
	else {
		outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	}
	std::stringstream stream2;
	stream2 << "a" << handle;
	std::stringstream stream3;
	stream3 << "b" << handle;
	outfile << "Vector3 " << stream2.str() << ", " << stream3.str() << ";";
	outfile << "int handle" << handle << " = AllocSpatialPartitioner(" << stream2.str() << " , " << stream3.str() << ");";
	outfile.close();
}

int WriteVector3Setup(int handle, Vector3 vec) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	std::stringstream varName;
	varName << "var" << varNum++;
	outfile << "Vector3 " << varName.str() << "; " << varName.str() << ".x = " << vec.x << "; " <<
		varName.str() << ".y = " << vec.y << "; " << varName.str() << ".z =  " << vec.z << ";" << std::endl;
	outfile.close();
	return varNum - 1;
};

int WriteArrayTargetSetup(int handle, int size) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	std::stringstream varName;
	varName << "var" << varNum++;
	outfile << "int* " << varName.str() << " = new int[" << size << "];" << std::endl;
	outfile.close();
	return varNum - 1;
};

int WriteIntSetup(int handle, int val) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	std::stringstream varName;
	varName << "var" << varNum++;
	outfile << "int " << varName.str() << " = " << val << ";" << std::endl;
	outfile.close();
	return varNum - 1;
};

void WriteCommand(int handle, std::string name, int arg0) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	std::stringstream handleVal;
	handleVal << "handle" << handle;
	outfile << "SpatialPartitioner" << name << "(" << handleVal.str() << ", var" << arg0 << ");" << std::endl;
	outfile.close();
};

void WriteCommand(int handle, std::string name, int arg0, int arg1) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	std::stringstream handleVal;
	handleVal << "handle" << handle;
	outfile << "SpatialPartitioner" << name << "(" << handleVal.str() << ", var" << arg0 << ", var" << arg1 << ");" << std::endl;
	outfile.close();
};

void WriteCommand(int handle, std::string name, int arg0, int arg1, int arg2) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	std::stringstream handleVal;
	handleVal << "handle" << handle;
	outfile << "SpatialPartitioner" << name << "(" << handleVal.str() << ", var" << arg0 << ", var" << arg1 << ", var" << arg2 <<");" << std::endl;
	outfile.close();
};

void WriteCommand(int handle, std::string name, int arg0, int arg1, int arg2, int arg3) {
	std::ofstream outfile;
	std::stringstream stream;
	stream << "commandlog.txt";
	outfile.open(stream.str(), std::ofstream::out | std::ofstream::app);
	std::stringstream handleVal;
	handleVal << "handle" << handle;
	outfile << "SpatialPartitioner" << name << "(" << handleVal.str() << ", var" << arg0 << ", var" << arg1 << ", var" << arg2 << ", var" << arg3 << ");" << std::endl;
	outfile.close();
};
#endif