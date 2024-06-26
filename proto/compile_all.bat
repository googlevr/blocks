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

@echo off

echo This script will now compile all the .proto files into C# files.
echo Before proceeding, please read README.txt.
echo Also, make sure that:
echo    - protogen.exe is in your PATH.
echo    - protoc.exe is in your PATH.
pause

del ..\client\unity\Assets\scripts\proto\*.cs
protogen basic_types_protos.proto command_protos.proto mmesh_protos.proto peltzer_file_protos.proto
move *.cs ..\client\unity\Assets\scripts\proto

