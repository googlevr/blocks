#pragma once
#include <sstream>

typedef void(*FuncPtr)(const char *);

struct Vector3 {
	float x;
	float y;
	float z;

	Vector3() {
		this->x = 0.0;
		this->y = 0.0;
		this->z = 0.0;
	}

	Vector3(float x, float y, float z) {
		this->x = x;
		this->y = y;
		this->z = z;
	}

	Vector3& operator=(const Vector3& other) {
		x = other.x;
		y = other.y;
		z = other.z;
		return *this;
	}

	std::string to_string() {
		std::stringstream stream;
		stream << "<" << x << ", " << y << ", " << z << ">";
		return stream.str();
	}
};

struct Vector2 {
	float x;
	float y;

	Vector2() {
		this->x = 0.0;
		this->y = 0.0;
	}

	Vector2(float x, float y) {
		this->x = x;
		this->y = y;
	}
};

struct Color32 {
	unsigned char r;
	unsigned char g;
	unsigned char b;
	unsigned char a;

	Color32() {
		this->r = (unsigned char)255;
		this->g = (unsigned char)255;
		this->b = (unsigned char)255;
		this->a = (unsigned char)255;
	}
};

