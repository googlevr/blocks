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

using NUnit.Framework;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.controller
{

    [TestFixture]
    // Tests for TouchpadLocation.
    public class TouchpadLocationTest
    {
        [Test]
        public void TestBasicQuadrants()
        {
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.RIGHT.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(.5f, 0))));
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.TOP.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(0, .5f))));
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.LEFT.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(-.5f, 0))));
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.BOTTOM.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(0, -.5f))));
        }

        [Test]
        public void TestCenter()
        {
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.CENTER.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(0, 0))));
        }

        [Test]
        public void TestXYCombinations()
        {
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.TOP.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(.1f, .5f))));
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.BOTTOM.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(.1f, -.5f))));
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.BOTTOM.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(-.1f, -.5f))));
            NUnit.Framework.Assert.IsTrue(
              TouchpadLocation.LEFT.Equals(TouchpadLocationHelper.GetTouchpadLocation(new Vector2(-.5f, 0))));
        }
    }
}
