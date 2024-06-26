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

namespace com.google.apps.peltzer.client.model.util {
  /// <summary>
  ///   Runtime assertions.
  /// </summary>
  public class AssertOrThrow {

    /// <summary>
    ///   Verifies the condition is true.  Otherwise throws an Exception.
    /// </summary>
    /// <param name="condition">Condition to assert.</param>
    /// <param name="msg">Message to include with exception, if thrown.</param>
    /// <exception cref="System.Exception">
    ///   Thrown when condition is false.</exception>
    public static void True(bool condition, string msg) {
      if (!condition) {
        throw new Exception(msg);
      }
    }

    /// <summary>
    ///   Verifies the condition is false.  Otherwise throws an Exception.
    /// </summary>
    /// <param name="condition">Condition to assert.</param>
    /// <param name="msg">Message to include with exception, if thrown.</param>
    /// <exception cref="System.Exception">
    ///   Thrown when condition is true.</exception>
    public static void False(bool condition, string msg) {
      if (condition) {
        throw new Exception(msg);
      }
    }

    internal static T NotNull<T>(T val, string msg) {
      if (val == null) {
        throw new Exception(msg);
      }
      return val;
    }
  }
}
