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

namespace com.google.apps.peltzer.client.model.core
{

    /// <summary>
    ///   A Command that is a composite of several other Commands.
    /// </summary>
    public class CompositeCommand : Command
    {
        public const string COMMAND_NAME = "composite";

        private readonly List<Command> commands;

        public CompositeCommand(List<Command> commands)
        {
            this.commands = commands;
        }

        public void ApplyToModel(Model model)
        {
            foreach (Command command in commands)
            {
                command.ApplyToModel(model);
            }
        }

        public Command GetUndoCommand(Model model)
        {
            List<Command> undoCommands = new List<Command>(commands.Count);
            foreach (Command command in commands)
            {
                undoCommands.Add(command.GetUndoCommand(model));
            }
            // Undo should be applied in reverse order.
            undoCommands.Reverse();
            return new CompositeCommand(undoCommands);
        }

        // Visible for testing.
        public List<Command> GetCommands()
        {
            return commands;
        }
    }
}
