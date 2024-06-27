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

namespace com.google.apps.peltzer.client.model.util
{
    [TestFixture]
    // Tests for DisjointSet.
    public class DisjointSetTest
    {
        [Test]
        public void TestAdd()
        {
            DisjointSet<int> disjointSet = new DisjointSet<int>();
            Assert.False(disjointSet.Contains(123));
            Assert.False(disjointSet.Contains(0));
            Assert.False(disjointSet.Contains(42));
            disjointSet.Add(42);
            Assert.True(disjointSet.Contains(42));
            disjointSet.Add(600673);
            disjointSet.Add(600673);  // redundant.
            Assert.True(disjointSet.Contains(600673));

            // All elements should be in the same set as themselves, if they are in the structure.
            Assert.True(disjointSet.AreInSameSet(42, 42));
            Assert.True(disjointSet.AreInSameSet(600673, 600673));

            // But not if the are not in the structure.
            Assert.False(disjointSet.AreInSameSet(391393, 391393));
        }

        [Test]
        public void TestJoin()
        {
            DisjointSet<int> disjointSet = new DisjointSet<int>();
            disjointSet.Add(100);
            disjointSet.Add(200);
            disjointSet.Add(300);
            disjointSet.Add(400);

            // Should be 4 disjoint sets.
            Assert.False(disjointSet.AreInSameSet(100, 200));
            Assert.False(disjointSet.AreInSameSet(100, 300));
            Assert.False(disjointSet.AreInSameSet(100, 400));
            Assert.False(disjointSet.AreInSameSet(200, 300));
            Assert.False(disjointSet.AreInSameSet(200, 400));
            Assert.False(disjointSet.AreInSameSet(300, 400));
            Assert.False(disjointSet.AreInSameSet(100, 4193193));  // non-members also allowed.
            Assert.False(disjointSet.AreInSameSet(-13192, 200));  // non-members also allowed.

            // Now join 100 with 200.
            disjointSet.Join(100, 200);

            // Now one set should be { 100, 200 }, and 300 and 400 are in separate sets.
            Assert.True(disjointSet.AreInSameSet(100, 200));
            Assert.False(disjointSet.AreInSameSet(100, 300));
            Assert.False(disjointSet.AreInSameSet(200, 300));
            Assert.False(disjointSet.AreInSameSet(300, 400));

            // Now join 300 with 400.
            // The new situation is { 100, 200 } and { 300, 400 }.
            disjointSet.Join(300, 400);
            Assert.True(disjointSet.AreInSameSet(100, 200));
            Assert.True(disjointSet.AreInSameSet(400, 300));
            Assert.False(disjointSet.AreInSameSet(200, 300));
            Assert.False(disjointSet.AreInSameSet(100, 400));

            // Now finally join 200 with 300, bringing everyone together in one big happy set:
            // { 100, 200, 300, 400 }.
            disjointSet.Join(200, 300);
            Assert.True(disjointSet.AreInSameSet(100, 200));
            Assert.True(disjointSet.AreInSameSet(100, 300));
            Assert.True(disjointSet.AreInSameSet(100, 400));
            Assert.True(disjointSet.AreInSameSet(200, 300));
            Assert.True(disjointSet.AreInSameSet(200, 400));
            Assert.True(disjointSet.AreInSameSet(300, 400));
        }
    }
}