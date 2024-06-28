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

using System.Collections.Generic;
using System.Linq;
using System.Threading;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using UnityEngine;
using System;

namespace com.google.apps.peltzer.client.tools
{
    /// <summary>
    /// Helper object that continually validates meshes in a background thread, indicating when they are invalid and
    /// what was the last valid state.
    ///
    /// To use this object, create an instance and call StartValidating() when you want to start validating. Then call
    /// UpdateMeshes() every frame from the UI thread to give the validator the current state of the meshes.
    /// Call StopValidating() when you want to stop validating.
    ///
    /// Between StartValidating() and StopValidating(), you can read the ValidityState property at any time to know if the validator
    /// currently thinks the meshes are valid or not, and you can call GetLastValidState to get the latest
    /// snapshot of the meshes when they were last considered to be valid.
    /// </summary>
    public class BackgroundMeshValidator
    {
        /// <summary>
        /// Worker thread that runs in the background doing validation. If this is not null, then the background thread
        /// is currently running.
        /// </summary>
        private Thread workerThread;

        private readonly Model model;

        /// <summary>
        /// Lock that guards the data in this class. Lock must be held to access most member variables
        /// (as noted below).
        /// </summary>
        private object lockObject = new object();

        /// <summary>
        /// Represents each of the possible states that we can be in.
        /// </summary>
        private enum State
        {
            // Not running.
            NOT_RUNNING,
            // Starting up.
            STARTING,
            // The background thread is hungry, waiting for juicy new data to process.
            WAITING_FOR_DATA,
            // The background thread is validating the last provided data.
            VALIDATING,
            // We want to stop. Background thread being kindly asked to quit.
            QUITTING,
        };

        /// <summary>
        /// Represents the validity of the mesh.
        /// </summary>
        public enum Validity
        {
            // Means we haven't analyzed a snapshot yet, so we don't know.
            NOT_YET_KNOWN,
            // The last snapshot we analyzed was invalid.
            INVALID,
            // The last snapshot we analyzed was valid.
            VALID,
        };

        /// <summary>
        /// The state we are currently in.
        /// GUARDED_BY(lockObject)
        /// </summary>
        private State state = State.NOT_RUNNING;

        /// <summary>
        /// Validation copies. These are guarded by the lock.
        /// GUARDED_BY(lockObject)
        /// </summary>
        private List<MMesh> validationCopies;

        /// <summary>
        /// List of all vertices that are bring manipulated (in all meshes).
        /// GUARDED_BY(lockObject)
        /// </summary>
        private HashSet<VertexKey> updatedVerts;

        /// <summary>
        /// Last valid state of the meshes (dictionary from mesh ID to MMesh).
        /// IMPORTANT: this is returned to the caller, so it's IMMUTABLE once returned.
        /// When we make a new one, we just replace this entirely by a new Dictionary.
        /// GUARDED_BY(lockObject)
        private Dictionary<int, MMesh> lastValidState;

        /// <summary>
        /// Current state of the meshes (the validity of the last snapshot we analyzed).
        /// This is volatile for efficiency (for lockless read/write). It can only be written by
        /// the WORKER thread, and can be read from any thread.
        /// </summary>
        private volatile Validity validity;

        /// <summary>
        /// Creates a BackgroundMeshValidator. This will not automatically START it (you have to do that explicitly
        /// by calling StartValidating() when you're ready).
        /// </summary>
        public BackgroundMeshValidator(Model model)
        {
            this.model = model;
        }

        /// <summary>
        /// Returns whether the validator is currently active. The validator is active between the calls to
        /// StartValidating() and StopValidating().
        /// </summary>
        public bool IsActive
        {
            get
            {
                return workerThread != null;
            }
        }

        /// <summary>
        /// Returns the current validity state of the meshes (as of the last snapshot the background thread has a
        /// chance to analyze). This may change suddenly, as it's based on a best effort by the background thread,
        /// which is processing and validating meshes asynchronously. Note that if this is called when the
        /// validator is not active, this will return the last known state.
        /// </summary>
        public Validity ValidityState
        {
            get
            {
                // validity is volatile so it can be read from any thread.
                return validity;
            }
        }

        /// <summary>
        /// Starts the validator. This will start the background thread that will do the actual work. After calling
        /// StartValidating(), you must call OfferPreviewMeshes() on every frame in order to feed data to the background thread.
        /// </summary>
        public void StartValidating()
        {
            lock (lockObject)
            {
                AssertOrThrow.True(state == State.NOT_RUNNING, "State should be State.NOT_RUNNING");
                state = State.STARTING;
            }
            validity = Validity.NOT_YET_KNOWN;
            lastValidState = new Dictionary<int, MMesh>();
            validationCopies = null;
            updatedVerts = null;

            workerThread = new Thread(new ThreadStart(WorkerThreadMain));
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        /// <summary>
        /// Stops the validator. Call this from the UI thread when you want to stop validating meshes.
        /// IMPORTANT: after this method returns, ValidityState and GetLastValidState() will return the state of the
        /// last snapshot examined in the background, which will NOT NECESSARILY be the last state fed through
        /// UpdateMeshes(). It will be the last state that the background thread had a chance to analyze. It will
        /// not "catch up" to the latest state when stopping. If this turns out to be problematic, maybe we
        /// should instead implement a queue of capacity 1 and ensure that StopValidating() will get the chance
        /// to validate the last state fed through UpdateMeshes().
        /// </summary>
        public void StopValidating()
        {
            lock (lockObject)
            {
                // If we are not running, there's nothing to do.
                if (state == State.NOT_RUNNING) return;
                // Indicate to the worker thread that we're calling it a day.
                state = State.QUITTING;
                // Wake up background thread to make it notice the state change.
                Monitor.Pulse(lockObject);
            }
            // Block and wait for worker thread to finish.
            workerThread.Join();
            workerThread = null;
            // We can update state without locking because we know the worker thread is dead (we just killed it!).
            state = State.NOT_RUNNING;
        }

        /// <summary>
        /// Call this once per frame from the UI thread to offer updated preview meshes to the validator.
        /// The validator may or may not want them, depending on its current state. Even if it doesn't
        /// want them right now, it will appreciate your politeness in offering, and do nothing.
        /// It eventually will accept them, though, when it's ready. That's why you have to call on
        /// every frame.
        /// </summary>
        /// <param name="meshes">The current state of the meshes.</param>
        /// <param name="updatedVerts">All the vertices (in all meshes) that are being manipulated.</param>
        public void UpdateMeshes(Dictionary<int, MMesh> meshes, HashSet<VertexKey> updatedVerts)
        {
            // It's an error to call this while not running.
            AssertOrThrow.True(workerThread != null,
              "Can't call UpdateMeshes when BackgroundMeshValidator is not running.");

            lock (lockObject)
            {
                if (state != State.WAITING_FOR_DATA)
                {
                    // Worker thread is busy and doesn't have an appetite for new data right now.
                    // Caller should try again later (as documented above).
                    return;
                }
                // Take a snapshot of the preview meshes so we can work offline.
                validationCopies = new List<MMesh>(meshes.Count());
                foreach (int meshId in meshes.Keys)
                {
                    validationCopies.Add(meshes[meshId].Clone());
                }
                this.updatedVerts = new HashSet<VertexKey>(updatedVerts);
                // Ok, now that we have data, we transition into the VALIDATING state.
                state = State.VALIDATING;
                // Poke the worker thread to tell it to wake up, because there's work to do.
                Monitor.Pulse(lockObject);
            }
        }

        /// <summary>
        /// Returns the last good state, that is the last state that passed mesh validation.
        /// Since validation is asynchronous, this will always be a few frames behind the current state,
        /// but shouldn't lag that far behind.
        /// </summary>
        /// <returns>The last valid state.</returns>
        public Dictionary<int, MMesh> GetLastValidState()
        {
            lock (lockObject)
            {
                // IMPLEMENTATION WARNING: since we are returning this to the caller, we must guarantee that this
                // is immutable. This is guaranteed because when we modify this, we replace it with a new Dictionary
                // rather than modify it in-place.
                return lastValidState;
            }
        }

        /// <summary>
        /// Worker thread main function.
        /// </summary>
        private void WorkerThreadMain()
        {
            // Note: yes, there is a performance penalty using a try/catch, but this runs on a background thread,
            // and it's extremely useful for us to be able to get stack traces of things that went wrong here.
            try
            {
                while (true)
                {
                    // Try to get the next set of validation copies, waiting as necessary.
                    lock (lockObject)
                    {
                        // We must always check the state right after locking, because the main thread might be
                        // signaling us to quit.
                        if (state == State.QUITTING) return;

                        // Indicate that we are sitting around waiting for new data to appear.
                        state = State.WAITING_FOR_DATA;
                        // Must use a while loop because Wait() does not guarantee to only unblock on a valid
                        // Pulse(). It can unblock spuriously at any time.
                        while (state == State.WAITING_FOR_DATA)
                        {
                            // Wait until the state changes. Monitor.Wait() temporarily releases the lock, then locks
                            // again once Monitor.Pulse() has been called.
                            Monitor.Wait(lockObject);
                        }
                        // If the main thread told us to quit, stop here.
                        if (state == State.QUITTING) return;
                        // Indicate that we are now working on validating the meshes.
                        state = State.VALIDATING;
                    }

                    // Validate the copies.
                    // When done, update last valid state, etc.
                    Dictionary<int, MMesh> currentState = new Dictionary<int, MMesh>();
                    bool allMeshesValid = true;
                    foreach (MMesh mesh in validationCopies)
                    {
                        // Get the original mesh as it is in the model (unmodified, and presumably valid).
                        MMesh originalMesh = model.GetMesh(mesh.id);
                        List<Vertex> updatedVertsForThisMesh = new List<Vertex>();
                        foreach (VertexKey v in updatedVerts)
                        {
                            if (v.meshId == mesh.id)
                            {
                                updatedVertsForThisMesh.Add(mesh.GetVertex(v.vertexId));
                            }
                        }
                        // Figure out which vertices are being updated
                        HashSet<int> updatedVertIds = new HashSet<int>(updatedVertsForThisMesh.Select(v => v.id));
                        // Fix the mutated mesh, keeping track of duplicated vertices that were not fixed.
                        DisjointSet<int> dupVerts;
                        MeshFixer.FixMutatedMesh(originalMesh, mesh, updatedVertIds, /* splitNonCoplanarFaces */ true,
                          /* mergeAdjacentCoplanarFaces */ true);

                        if (MeshValidator.IsValidMesh(mesh, updatedVertIds))
                        {
                            currentState[mesh.id] = mesh;
                        }
                        else
                        {
                            allMeshesValid = false;
                            break;
                        }
                    }

                    // Publish our findings.
                    lock (lockObject)
                    {
                        if (allMeshesValid)
                        {
                            lastValidState = currentState;
                            validity = Validity.VALID;
                        }
                        else
                        {
                            validity = Validity.INVALID;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }
    }
}