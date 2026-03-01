// MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace PackageUrl.Tests.TestAssets;

public class PurlTestData : DataAttribute
{
    private static readonly JsonSerializer s_serializer = new JsonSerializer();
    private static readonly Dictionary<string, IReadOnlyCollection<ITheoryDataRow>> s_assetsStore =
        new Dictionary<string, IReadOnlyCollection<ITheoryDataRow>>();
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

    public override bool SupportsDiscoveryEnumeration() => true;

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker
    )
    {
        if (s_assetsStore.ContainsKey(_filePath))
        {
            return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(s_assetsStore[_filePath]);
        }

        using (var streamReader = new StreamReader(_filePath))
        {
            var reader = new JsonTextReader(streamReader);
            var data = s_serializer.Deserialize<PurlTestData[]>(reader);
            s_assetsStore[_filePath] = data.Select(x => (ITheoryDataRow)new TheoryDataRow(x))
                .ToList();
            return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(s_assetsStore[_filePath]);
        }
    }
}
