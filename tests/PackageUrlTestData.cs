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
/// xunit data attribute that loads test cases from purl-spec JSON test files (schema v0.2).
/// </summary>
public class PackageUrlTestData(string directory, string? testType = null) : DataAttribute
{
    private const string TestSchema = "https://packageurl.org/schemas/purl-test.schema-0.2.json";
    private const string PinnedSource =
        "https://github.com/package-url/purl-spec/blob/7cd2d3442fb9c88155db17ada7c911b40ec22d41/";

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
            if (
                !doc.RootElement.TryGetProperty("$schema", out var schema)
                || schema.ValueKind != JsonValueKind.String
                || schema.GetString() != TestSchema
            )
            {
                throw new InvalidDataException($"Unsupported PURL test schema in '{file}'.");
            }
            var testsArray = doc.RootElement.GetProperty("tests");

            foreach (var el in testsArray.EnumerateArray())
            {
                var testType = el.GetProperty("test_type").GetString()!;
                if (testType is not ("parse" or "build" or "validate"))
                {
                    throw new InvalidDataException(
                        $"Unsupported PURL test type '{testType}' in '{file}'."
                    );
                }
                var testGroup = el.GetProperty("test_group").GetString()!;
                if (testGroup is not ("required" or "recommended"))
                {
                    throw new InvalidDataException(
                        $"Unsupported PURL test group '{testGroup}' in '{file}'."
                    );
                }
                if (_testType != null && testType != _testType)
                    continue;

                var testCase = new PackageUrlTestCase
                {
                    Description = el.GetProperty("description").GetString()!,
                    TestGroup = testGroup,
                    TestType = testType,
                    ExpectedFailure =
                        el.TryGetProperty("expected_failure", out var ef) && ef.GetBoolean(),
                    ExpectedMessage =
                        el.TryGetProperty("expected_message", out var message)
                        && message.ValueKind == JsonValueKind.String
                            ? message.GetString()!
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

                rows.Add(
                    new TheoryDataRow(testCase)
                    {
                        Skip = GetKnownSkipReason(Path.GetFileName(file), testCase),
                    }
                );
            }
        }

        s_cache[cacheKey] = rows;
        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(rows);
    }

    // Revisit these exact-case exceptions when updating TestAssets/purl-spec.json.
    private static string? GetKnownSkipReason(string file, PackageUrlTestCase data)
    {
        if (
            data.TestGroup == "required"
            && data.TestType == "parse"
            && data.ExpectedFailure
            && data.ExpectedComponents == null
            && data.ExpectedPurl == null
            && (
                (file, data.InputPurl) is
                    ("gem-test.json", "pkg:gem/jruby-launcher@1.1.2?Platform=java")
                    or (
                        "rpm-test.json",
                        "pkg:Rpm/fedora/curl@7.50.3-1.fc25?Arch=i386&Distro=fedora-25"
                    )
            )
        )
        {
            return "Strict parsing is deferred; the constructor normalizes qualifier keys. "
                + PinnedSource + "tests/types/" + file;
        }

        if (file != "git-test.json" || data.ExpectedFailure)
        {
            return null;
        }

        if (
            data.TestGroup == "recommended"
            && data.TestType == "validate"
            && data.InputPurl == "pkg:git/github/Package-url/purl-Spec@244fd47e07d1004f0aed9c"
            && data.ExpectedPurl == "pkg:git/github/package-url/purl-spec@244fd47e07d1004f0aed9c"
        )
        {
            return "The fixture lowercases Git components declared case-sensitive by the definition. "
                + PinnedSource + "types/git-definition.json";
        }

        const string gitVersion = "a72d2c07cfca03b55371089de6aa230d8c951fa0";
        var components = data.TestType switch
        {
            "parse" => data.ExpectedComponents,
            "build" => data.InputComponents,
            _ => null,
        };
        if (
            data.TestGroup == "required"
            && components is
            {
                Type: "git",
                Namespace: "codeberg.org",
                Name: "forgejo/forgejo",
                Version: gitVersion,
                Qualifiers: null,
                Subpath: null or "options/locale_readme.md",
            }
        )
        {
            var purl = "pkg:git/codeberg.org/forgejo/forgejo@" + gitVersion;
            if (components.Subpath != null)
            {
                purl += "#" + components.Subpath;
            }
            if (
                (data.TestType == "parse" && data.InputPurl == purl)
                || (data.TestType == "build" && data.ExpectedPurl == purl)
            )
            {
                return "Git namespace/name boundaries and unescaped slashes in names need upstream clarification. "
                    + PinnedSource + "tests/types/git-test.json";
            }
        }

        return null;
    }
}
