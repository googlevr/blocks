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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using NUnit.Framework;

namespace com.google.apps.peltzer.client.model.csg
{
    [TestFixture]
    public class FaceRecomposerTest
    {

        /// <summary>
        /// For the JoinAtSegment tests, this is a diagram of the points:
        ///
        ///  1-----2-----3
        ///  |     |     |
        ///  |     |     |
        ///  4-----5-----6
        ///
        /// </summary>
        [Test]
        public void TestJoinAtSegment()
        {
            SolidVertex one = new SolidVertex(1, new Vector3(0, -1, 1), Vector3.one);
            SolidVertex two = new SolidVertex(2, new Vector3(0, 0, 1), Vector3.one);
            SolidVertex three = new SolidVertex(3, new Vector3(0, 1, 1), Vector3.one);
            SolidVertex four = new SolidVertex(4, new Vector3(0, -1, -1), Vector3.one);
            SolidVertex five = new SolidVertex(5, new Vector3(0, 0, -1), Vector3.one);
            SolidVertex six = new SolidVertex(6, new Vector3(0, 1, -1), Vector3.one);

            List<List<SolidVertex>> pieces;

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { one, two, five },
        new List<SolidVertex>() { five, two, three } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { one, two, three, five });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { two, five, one },
        new List<SolidVertex>() { five, two, three } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { two, three, five, one });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { two, one, five },
        new List<SolidVertex>() { five, three, two } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { three, two, one, five });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { two, one, four, five },
        new List<SolidVertex>() { five, six, three, two } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { six, three, two, one, four, five });

        }

        /// <summary>
        /// For the JoinAtPoint tests, this is a diagram of the vertices:
        ///
        ///         3
        ///        /|
        ///       / 2
        ///      /  |\
        ///     /   |  \
        ///    4____1____5
        ///
        /// 1 -> Shared
        /// 2 -> toInsert
        /// </summary>
        [Test]
        public void TestJoinAtPointAlt1()
        {
            SolidVertex shared = new SolidVertex(1, new Vector3(0, 0, 0), Vector3.one);
            SolidVertex toInsert = new SolidVertex(2, new Vector3(0, 1, 0), Vector3.one);
            SolidVertex three = new SolidVertex(3, new Vector3(0, 2, 0), Vector3.one);
            SolidVertex four = new SolidVertex(4, new Vector3(-1, 0, 0), Vector3.one);
            SolidVertex five = new SolidVertex(5, new Vector3(1, 0, 0), Vector3.one);

            List<List<SolidVertex>> pieces;

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, four, three },
        new List<SolidVertex>() { shared, toInsert, five } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { toInsert, five, four, three });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { three, shared, four },
        new List<SolidVertex>() { shared, toInsert, five } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { three, toInsert, five, four });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { four, three, shared },
        new List<SolidVertex>() { shared, toInsert, five } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { four, three, toInsert, five });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, four, three },
        new List<SolidVertex>() { toInsert, five, shared } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { toInsert, five, four, three });
        }

        [Test]
        public void TestJoinAtPointAlt2()
        {
            SolidVertex shared = new SolidVertex(1, new Vector3(0, 0, 0), Vector3.one);
            SolidVertex toInsert = new SolidVertex(2, new Vector3(0, 1, 0), Vector3.one);
            SolidVertex three = new SolidVertex(3, new Vector3(0, 2, 0), Vector3.one);
            SolidVertex four = new SolidVertex(4, new Vector3(-1, 0, 0), Vector3.one);
            SolidVertex five = new SolidVertex(5, new Vector3(1, 0, 0), Vector3.one);

            List<List<SolidVertex>> pieces;

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, three, four },
        new List<SolidVertex>() { toInsert, shared, five } };

            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { five, toInsert, three, four });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, three, four },
        new List<SolidVertex>() { five, toInsert, shared } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            //(1, 5, 2, 3, 4)
            CheckPoly(pieces[0], new List<SolidVertex> { five, toInsert, three, four });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { three, four, shared },
        new List<SolidVertex>() { toInsert, shared, five } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { three, four, five, toInsert });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, three, four },
        new List<SolidVertex>() { shared, five, toInsert } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { five, toInsert, three, four });
        }


        [Test]
        public void TestJoinAtPointAlt3()
        {
            SolidVertex shared = new SolidVertex(1, new Vector3(0, 0, 0), Vector3.one);
            SolidVertex toInsert = new SolidVertex(2, new Vector3(0, 1, 0), Vector3.one);
            SolidVertex three = new SolidVertex(3, new Vector3(0, 2, 0), Vector3.one);
            SolidVertex four = new SolidVertex(4, new Vector3(-1, 0, 0), Vector3.one);
            SolidVertex five = new SolidVertex(5, new Vector3(1, 0, 0), Vector3.one);

            List<List<SolidVertex>> pieces;

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, toInsert, five },
        new List<SolidVertex>() { shared, four, three } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { four, three, toInsert, five });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, toInsert, five },
        new List<SolidVertex>() { four, three, shared } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { four, three, toInsert, five });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { five, shared, toInsert },
        new List<SolidVertex>() { three, shared, four } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { five, four, three, toInsert });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { toInsert, five, shared  },
        new List<SolidVertex>() { four, three, shared } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { toInsert, five, four, three });
        }

        [Test]
        public void TestJoinAtPointAlt4()
        {
            SolidVertex shared = new SolidVertex(1, new Vector3(0, 0, 0), Vector3.one);
            SolidVertex toInsert = new SolidVertex(2, new Vector3(0, 1, 0), Vector3.one);
            SolidVertex three = new SolidVertex(3, new Vector3(0, 2, 0), Vector3.one);
            SolidVertex four = new SolidVertex(4, new Vector3(-1, 0, 0), Vector3.one);
            SolidVertex five = new SolidVertex(5, new Vector3(1, 0, 0), Vector3.one);

            List<List<SolidVertex>> pieces;

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { toInsert, shared, five },
        new List<SolidVertex>() { shared, three, four } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { toInsert, three, four, five });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { shared, five, toInsert },
        new List<SolidVertex>() { shared, three, four } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { three, four, five, toInsert });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { toInsert, shared, five },
        new List<SolidVertex>() { shared, three, four } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { toInsert, three, four, five });

            pieces = new List<List<SolidVertex>>() {
        new List<SolidVertex>() { five, toInsert, shared },
        new List<SolidVertex>() { three, four, shared } };
            pieces = FaceRecomposer.RecomposeFace(pieces);
            NUnit.Framework.Assert.AreEqual(1, pieces.Count);
            CheckPoly(pieces[0], new List<SolidVertex> { five, toInsert, three, four });
        }

        private void CheckPoly(List<SolidVertex> list1, List<SolidVertex> list2)
        {
            string failMsg = GetMessage(list1, list2);
            NUnit.Framework.Assert.AreEqual(list1.Count, list2.Count, failMsg);

            for (int i = 0; i < list1.Count; i++)
            {
                NUnit.Framework.Assert.AreEqual(list1[i].vertexId, list2[i].vertexId, failMsg);
            }
        }

        private string GetMessage(List<SolidVertex> list1, List<SolidVertex> list2)
        {
            return "Expected: " + ToStr(list1) + " but was: " + ToStr(list2);
        }

        private string ToStr(List<SolidVertex> list)
        {
            string s = "(";
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    s += ", ";
                }
                s += list[i].vertexId;
            }
            return s + ")";
        }
    }
}
