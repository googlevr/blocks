#include "libAssImp/DllExports.h"
#include "FBXSupport/FBXSupport.h"
#include "SpatialPartitioner.h"

#include <unordered_map>
#include <memory>

static std::unordered_map<int, SpatialPartitioner> SpatialPartitionerMap = {};
static int nextSpatialPartitionerId = 0;

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT int AllocSpatialPartitioner(Vector3 center, Vector3 size) {
	//Debug("In AllocSpatialPartitioner");
	int id = nextSpatialPartitionerId++;
	SpatialPartitionerMap[id] = SpatialPartitioner();
	return id;
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerAddItem(int SpatialPartitionerHandle, int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsSize) {
	//Debug("In SpatialPartitionerAddItem");
	SpatialPartitionerMap[SpatialPartitionerHandle].AddItem(itemId, itemBoundsCenter, itemBoundsSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerUpdateItem(int SpatialPartitionerHandle, int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsSize) {
	//Debug("In SpatialPartitionerUpdateItem");
	SpatialPartitionerMap[SpatialPartitionerHandle].UpdateItem(itemId, itemBoundsCenter, itemBoundsSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerRemoveItem(int SpatialPartitionerHandle, int itemId) {
	//Debug("In SpatialPartitionerRemoveItem");
	SpatialPartitionerMap[SpatialPartitionerHandle].RemoveItem(itemId);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT int SpatialPartitionerContainedBy(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
	return SpatialPartitionerMap[SpatialPartitionerHandle].ContainedBy(testCenter, testExtents, returnArray, returnArrayMaxSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT int SpatialPartitionerIntersectedBy(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
	//Debug("In SpatialPartitionerIntersectedBy");
	return SpatialPartitionerMap[SpatialPartitionerHandle].IntersectedBy(testCenter, testExtents, returnArray, returnArrayMaxSize);
};

/// Allocates an SpatialPartitioner and returns a handle.
BLOCKSEXPORT void SpatialPartitionerHasItem(int SpatialPartitionerHandle, int itemHandle) {
	SpatialPartitionerMap[SpatialPartitionerHandle].HasItem(itemHandle);
};