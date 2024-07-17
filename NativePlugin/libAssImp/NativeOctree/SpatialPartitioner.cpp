#include "SpatialPartitioner.h"
#include <vector>
#include <iostream>
#include <fstream>
#include <xmmintrin.h> //SSE
#include <emmintrin.h> //SSE2

SpatialPartitioner::SpatialPartitioner() {

};

bool intersects(AABB &volume0, AABB &volume1) {
	/* This is an implementation of AABB intersection using SSE/SSE2 SIMD instructions.
	This is the non-SIMD code for what it is implementing:
	if (volume0.max.x < volume1.min.x || volume0.min.x > volume1.max.x) return 0;
	if (volume0.max.y < volume1.min.y || volume0.min.y > volume1.max.y) return 0;
	if (volume0.max.z < volume1.min.z || volume0.min.z > volume1.max.z) return 0;
	return 1;*/
	__m128 temp = _mm_cmplt_ps(volume0.vecmax, volume1.vecmin);
	__m128 temp2 = _mm_cmpgt_ps(volume0.vecmin, volume1.vecmax);
	temp = _mm_or_ps(temp, temp2);
	return (_mm_movemask_ps(temp) & 7) <= 0;

}

bool intersectsOrig(AABB &volume0, AABB &volume1) {
	for (int i = 0; i < 3; i++) {
		if (volume0.vecmax.m128_f32[i] < volume1.vecmin.m128_f32[i] || volume0.vecmin.m128_f32[i] > volume1.vecmax.m128_f32[i]) return 0;
	}
	return 1;

}

int contains(AABB &volume0, AABB &volume1) {
	// SSE implementation of the below scalar check.
	// Checks whether the volume1 AABB is completely contained within the volume0 AABB.
	/*if (volume0.min.x > volume1.min.x || volume0.max.x < volume1.max.x) return 0;
	if (volume0.min.y > volume1.min.y || volume0.max.y < volume1.max.y) return 0;
	if (volume0.min.z > volume1.min.z || volume0.max.z < volume1.max.z) return 0;*/
	__m128 temp = _mm_cmpgt_ps(volume0.vecmin, volume1.vecmin);
	__m128 temp2 = _mm_cmplt_ps(volume0.vecmax, volume1.vecmax);
	temp = _mm_or_ps(temp, temp2);
	return (_mm_movemask_ps(temp) & 7) <= 0;
}

/// Adds an item as itemId with the specified bounds.
void SpatialPartitioner::AddItem(int itemId, Vector3 &itemBoundsCenter, Vector3 &itemBoundsSize) {
	idToIndex[itemId] = (int)elementVector.size();
	elementVector.push_back(AABB(itemId, itemBoundsCenter, itemBoundsSize));
};

/// Updates an item with the specified id.
void SpatialPartitioner::UpdateItem(int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsSize) {
	int index = idToIndex[itemId];
	elementVector[index] = AABB(itemId, itemBoundsCenter, itemBoundsSize);
};

/// Extracts the integer id from the component of the float vector that its bits are stored in.
/// Implemented via magic.
int32_t IdFromAABB(AABB& box) {
	__m128 temp = _mm_shuffle_ps(box.vecmax, box.vecmax, _MM_SHUFFLE(3, 3, 3, 3));
	return _mm_cvtsi128_si32(_mm_castps_si128(temp));
}

/// Removes an item with the specified id.
void SpatialPartitioner::RemoveItem(int itemId) {
	if (elementVector.size() > 1) {
		size_t index = idToIndex[itemId];
		if (index == elementVector.size() - 1) {
			idToIndex.erase(itemId);
			elementVector.pop_back();
			return;
		}
		int lastId = IdFromAABB(elementVector[elementVector.size() - 1]);
		elementVector[index] = elementVector[elementVector.size() - 1];
		elementVector.pop_back();
		idToIndex.erase(itemId);
		idToIndex[lastId] = index;
	}
	else {
		elementVector.clear();
		idToIndex.clear();
	}
};

/// Tests whether the AABB defined by testCenter and testExtents fully contains any elements, and returns them in the supplied array
/// which must already be allocated.
int SpatialPartitioner::ContainedBy(Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
	int id = -1;
	AABB testAABB = AABB(id, testCenter, testExtents);
	int curNumResults = 0;
	for (auto it = elementVector.begin(); it != elementVector.end(); ++it) {
		if (contains(testAABB, *it)) {
			returnArray[curNumResults] = IdFromAABB(*it);
			curNumResults++;
			if (curNumResults >= returnArrayMaxSize) return curNumResults;
		}
	}
	return curNumResults;
};

int isectStarted = 0;
/// Tests whether the AABB defined by testCenter and testExtents intersects any elements, and returns them in the supplied array
/// which must already be allocated.
int SpatialPartitioner::IntersectedBy(Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
	int id = -1;

	AABB testAABB = AABB(id, testCenter, testExtents);
	int curNumResults = 0;
	for (auto it = elementVector.begin(); it != elementVector.end(); ++it) {
		if (intersects(testAABB, *it)) {
			int isectId = IdFromAABB(*it);
			returnArray[curNumResults] = isectId;
			curNumResults++;
			if (curNumResults >= returnArrayMaxSize) return curNumResults;
		}
	}
	return curNumResults;
};

/// Tests whether the AABB defined by testCenter and testExtents intersects any elements, and returns them in the supplied array
/// which must already be allocated.
int SpatialPartitioner::IntersectedByOrig(Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize) {
	int id = -1;
	AABB testAABB = AABB(id, testCenter, testExtents);
	int curNumResults = 0;
	for (auto it = elementVector.begin(); it != elementVector.end(); ++it) {
		if (intersectsOrig(testAABB, *it)) {
			int isectid = IdFromAABB(*it);
			returnArray[curNumResults] = isectid;
			curNumResults++;
			if (curNumResults >= returnArrayMaxSize) return curNumResults;
		}
	}
	return curNumResults;
};

/// Checks whether this partitioner contains an item with the supplied handle.
bool SpatialPartitioner::HasItem(int itemHandle) {
	return idToIndex.find(itemHandle) != idToIndex.end();
};