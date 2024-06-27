// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Overlay : MonoBehaviour {
  public GameObject up;
  public GameObject down;
  public GameObject left;
  public GameObject right;
  public GameObject center;
  public GameObject on;
  public GameObject off;

  public SpriteRenderer upIcon;
  public SpriteRenderer downIcon;
  public SpriteRenderer leftIcon;
  public SpriteRenderer rightIcon;
  public SpriteRenderer centerIcon;
  public SpriteRenderer onIcon;
  public SpriteRenderer offIcon;

  public SpriteRenderer[] icons;
}
