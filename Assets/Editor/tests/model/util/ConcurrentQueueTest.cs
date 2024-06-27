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
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.util
{
    [TestFixture]
    // Tests for ConcurrentQueue.
    public class ConcurrentQueueTest
    {
        private const string MAIN = "Main";
        private const string BACKGROUND = "Background";
        private Thread backgroundThread;
        private bool running = true;
        private ConcurrentQueue<string> forBackground = new ConcurrentQueue<string>();
        private ConcurrentQueue<WorkInfo> forMainThread = new ConcurrentQueue<WorkInfo>();

        public void SetupThread()
        {
            if (Thread.CurrentThread.Name == null || !Thread.CurrentThread.Name.Equals(MAIN))
            {
                Thread.CurrentThread.Name = MAIN;
            }
            backgroundThread = new Thread(BackgroundThread);
            backgroundThread.Name = BACKGROUND;
            backgroundThread.Start();
        }

        public void StopThread()
        {
            running = false;
            if (backgroundThread != null)
            {
                backgroundThread.Abort();
            }
        }

        [Test]
        public void TestBasics()
        {
            ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
            queue.Enqueue("foo");
            queue.Enqueue("bar");

            string fromQueue;
            NUnit.Framework.Assert.True(queue.Dequeue(out fromQueue));
            NUnit.Framework.Assert.AreEqual("foo", fromQueue);

            NUnit.Framework.Assert.True(queue.WaitAndDequeue(10, out fromQueue));
            NUnit.Framework.Assert.AreEqual("bar", fromQueue);

            // Queue should now be empty.
            NUnit.Framework.Assert.False(queue.Dequeue(out fromQueue));

            float start = Time.realtimeSinceStartup;
            NUnit.Framework.Assert.False(queue.WaitAndDequeue(20, out fromQueue));
            NUnit.Framework.Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, 0.015f,
              "Should have waited at least approximately 20ms.");
        }

        [Test]
        public void TestThreaded()
        {
            try
            {
                SetupThread();

                forBackground.Enqueue("foo");
                forBackground.Enqueue("bar");
                Thread.Sleep(100);
                forBackground.Enqueue("baz");

                // Make sure work was done in right order on right thread.
                WorkInfo info;
                NUnit.Framework.Assert.True(forMainThread.WaitAndDequeue(/*  wait time */ 1000, out info));
                NUnit.Framework.Assert.AreEqual("foo", info.workName);
                NUnit.Framework.Assert.AreEqual(BACKGROUND, info.threadName);

                NUnit.Framework.Assert.True(forMainThread.WaitAndDequeue(/*  wait time */ 1000, out info));
                NUnit.Framework.Assert.AreEqual("bar", info.workName);
                NUnit.Framework.Assert.AreEqual(BACKGROUND, info.threadName);

                NUnit.Framework.Assert.True(forMainThread.WaitAndDequeue(/*  wait time */ 1000, out info));
                NUnit.Framework.Assert.AreEqual("baz", info.workName);
                NUnit.Framework.Assert.AreEqual(BACKGROUND, info.threadName);
                NUnit.Framework.Assert.Greater(info.tryCount, 5,
                  "Should have had to try several times while waiting");
            }
            finally
            {
                StopThread();
            }
        }

        void BackgroundThread()
        {
            int tries = 0;
            while (running)
            {
                string workName;
                if (forBackground.WaitAndDequeue(/*  wait time */ 10, out workName))
                {
                    WorkInfo info = new WorkInfo();
                    info.workName = workName;
                    info.tryCount = tries;
                    info.threadName = Thread.CurrentThread.Name;
                    tries = 0;
                    forMainThread.Enqueue(info);
                }
                else
                {
                    tries++;
                }
            }
        }

        class WorkInfo
        {
            public string workName;
            public string threadName;
            public int tryCount;
        }
    }
}
