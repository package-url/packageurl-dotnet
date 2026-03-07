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
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace PackageUrl.Tests;

/// <summary>
/// xunit data attribute that loads test cases from purl-spec JSON test files (schema v0.1).
/// </summary>
public class PackageUrlTestData(string directory, string? testType = null) : DataAttribute
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Dictionary<string, IReadOnlyCollection<ITheoryDataRow>> s_cache = [];

    private readonly string _directory = directory;
    private readonly string? _testType = testType;

    public override bool SupportsDiscoveryEnumeration() => true;

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker
    )
    {
        var cacheKey = $"{_directory}|{_testType}";
        if (s_cache.TryGetValue(cacheKey, out var cached))
        {
            return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(cached);
        }

        var rows = new List<ITheoryDataRow>();

        foreach (var file in Directory.GetFiles(_directory, "*-test.json").OrderBy(f => f))
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream);
            var testsArray = doc.RootElement.GetProperty("tests");

            foreach (var el in testsArray.EnumerateArray())
            {
                var testType = el.GetProperty("test_type").GetString()!;
                if (_testType != null && testType != _testType)
                    continue;

                var testCase = new PackageUrlTestCase
                {
                    Description = el.GetProperty("description").GetString()!,
                    TestGroup = el.GetProperty("test_group").GetString()!,
                    TestType = testType,
                    ExpectedFailure =
                        el.TryGetProperty("expected_failure", out var ef) && ef.GetBoolean(),
                    ExpectedFailureReason =
                        el.TryGetProperty("expected_failure_reason", out var efr)
                        && efr.ValueKind == JsonValueKind.String
                            ? efr.GetString()!
                            : null,
                };

                var input = el.GetProperty("input");
                if (input.ValueKind == JsonValueKind.String)
                {
                    testCase.InputPurl = input.GetString()!;
                }
                else if (input.ValueKind == JsonValueKind.Object)
                {
                    testCase.InputComponents = JsonSerializer.Deserialize<PackageUrlComponents>(
                        input.GetRawText(),
                        s_jsonOptions
                    )!;
                }

                if (
                    el.TryGetProperty("expected_output", out var output)
                    && output.ValueKind != JsonValueKind.Null
                )
                {
                    if (output.ValueKind == JsonValueKind.String)
                    {
                        testCase.ExpectedPurl = output.GetString()!;
                    }
                    else if (output.ValueKind == JsonValueKind.Object)
                    {
                        testCase.ExpectedComponents =
                            JsonSerializer.Deserialize<PackageUrlComponents>(
                                output.GetRawText(),
                                s_jsonOptions
                            )!;
                    }
                }

                rows.Add(new TheoryDataRow(testCase));
            }
        }

        s_cache[cacheKey] = rows;
        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(rows);
    }
}
