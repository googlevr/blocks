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

namespace com.google.apps.peltzer.client.api_clients.objectstore_client
{

    [Serializable]
    public class ObjectStoreObjectAssets
    {
        public string rootUrl;
        public string[] supportingFiles;
        public string baseFile;
    }

    [Serializable]
    public class ObjectStorePeltzerAssets
    {
        public string rootUrl;
        public string[] supportingFiles;
        public string baseFile;
    }

    [Serializable]
    public class ObjectStorePeltzerPackageAssets
    {
        public string rootUrl;
        public string[] supportingFiles;
        public string baseFile;
    }

    [Serializable]
    public class ObjectStoreObjMtlPackageAssets
    {
        public string rootUrl;
        public string[] supportingFiles;
        public string baseFile;
    }

    [Serializable]
    public class ObjectStoreObjectAssetsWrapper
    {
        public ObjectStoreObjectAssets obj;
        public ObjectStorePeltzerAssets peltzer;
        public ObjectStorePeltzerPackageAssets peltzer_package;
        public ObjectStoreObjMtlPackageAssets object_package;
    }
}