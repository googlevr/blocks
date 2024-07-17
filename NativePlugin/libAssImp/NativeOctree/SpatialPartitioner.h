#pragma once
#include "libAssImp\VectorTypes.h"
#include <unordered_map>
#include <memory>
#include <cstdint>

struct Elem;
struct SweepSortAABB;


struct AABB {
	
	__m128 vecmax;
	__m128 vecmin;

	AABB(int32_t id, Vector3 center, Vector3 extents) {
		__m128 v_center = _mm_set_ps(reinterpret_cast<float &>(id), center.z, center.y, center.x);
		__m128 v_extents = _mm_set_ps(0, extents.z, extents.y, extents.x);
		vecmin = _mm_sub_ps(v_center, v_extents);
		vecmax = _mm_add_ps(v_center, v_extents);
	};

	AABB() {};
	AABB& operator=(const AABB& other) {
		vecmax = other.vecmax;
		vecmin = other.vecmin;
		return *this;
	}
};

class SpatialPartitioner {
public:
	SpatialPartitioner();
	~SpatialPartitioner() {};

	/// Adds an item as itemId with the specified bounds.
	void AddItem(int itemId, Vector3 &itemBoundsCenter, Vector3 &itemBoundsExtents);

	/// Updates an item with the specified id.
	void UpdateItem(int itemId, Vector3 itemBoundsCenter, Vector3 itemBoundsExtents);

	/// Removes an item with the specified id.
	void RemoveItem(int itemId);

	/// Tests whether the AABB defined by testCenter and testExtents fully contains any elements, and returns them in the supplied array
	/// which must already be allocated.
	int ContainedBy(Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize);

	/// Tests whether the AABB defined by testCenter and testExtents intersects any elements, and returns them in the supplied array
	/// which must already be allocated.
	int IntersectedBy(Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize);

	/// Tests whether the AABB defined by testCenter and testExtents intersects any elements, and returns them in the supplied array
	/// which must already be allocated.
	int IntersectedByOrig(Vector3 testCenter, Vector3 testExtents, int* returnArray, int returnArrayMaxSize);

	/// Checks whether this partitioner contains an item with the supplied handle.
	bool HasItem(int itemHandle);

private:
	std::vector<AABB> SpatialPartitioner::elementVector;
	std::unordered_map<int, int> idToIndex;
};


