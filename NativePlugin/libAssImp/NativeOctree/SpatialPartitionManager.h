#pragma once
#ifndef BLOCKSEXPORT
#define BLOCKSEXPORT __declspec(dllexport)
#endif // !BLOCKSEXPORT
#include "libAssImp\VectorTypes.h"

extern "C" {
	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT int AllocSpatialPartitioner(Vector3 center, Vector3 size);

	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT void SpatialPartitionerAddItem(int SpatialPartitionerHandle, int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsExtents);

	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT void SpatialPartitionerUpdateItem(int SpatialPartitionerHandle, int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsExtents);

	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT void SpatialPartitionerRemoveItem(int SpatialPartitionerHandle, int itemId);

	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT int SpatialPartitionerContainedBy(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize);

	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT int SpatialPartitionerIntersectedBy(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize);

	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT int SpatialPartitionerIntersectedByOrig(int SpatialPartitionerHandle, Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize);

	/// Allocates an SpatialPartitioner and returns a handle.
	BLOCKSEXPORT void SpatialPartitionerHasItem(int SpatialPartitionerHandle, int itemHandle);
}