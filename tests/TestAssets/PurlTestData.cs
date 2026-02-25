// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Xunit.Sdk;

namespace PackageUrl.Tests.TestAssets
{
    public class PurlTestData : DataAttribute
    {
        private static readonly JsonSerializer s_serializer = new JsonSerializer();
        private static readonly Dictionary<string, IEnumerable<object[]>> s_assetsStore = new Dictionary<string, IEnumerable<object[]>>();
        private readonly string _filePath;

        public string Description;

        public string Purl;

        [JsonProperty("canonical_purl")]
        public string CanonicalPurl;

        public string Type;

        public string Namespace;

        public string Name;

        public string Version;

        public SortedDictionary<string, string> Qualifiers;

        public string Subpath;

        [JsonProperty("is_invalid")]
        public bool IsInvalid;

        public PurlTestData(string filePath)
        {
            _filePath = filePath;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (s_assetsStore.ContainsKey(_filePath))
            {
                return s_assetsStore[_filePath];
            }

            using (var streamReader = new StreamReader(_filePath))
            {
                var reader = new JsonTextReader(streamReader);
                var data = s_serializer.Deserialize<PurlTestData[]>(reader);
                s_assetsStore[_filePath] = data.Select(x => new object[] { x });
                return s_assetsStore[_filePath];
            }
        }
    }
}
