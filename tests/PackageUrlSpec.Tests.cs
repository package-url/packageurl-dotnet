// SPDX-License-Identifier: MIT
// Copyright (c) the purl authors
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace PackageUrl.Tests
{
    public record PurlTestCase(
        string Description,
        string TestType,
        object Input,
        object ExpectedOutput = null,
        bool ExpectedFailure = false,
        string TestGroup = null
    );

    public static class SpecLoader
    {
        public static List<PurlTestCase> LoadTestFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tests = new List<PurlTestCase>();

            foreach (var el in root.GetProperty("tests").EnumerateArray())
            {
                tests.Add(new PurlTestCase(
                    el.GetProperty("description").GetString(),
                    el.GetProperty("test_type").GetString(),
                    JsonElementToObject(el.GetProperty("input")),
                    el.TryGetProperty("expected_output", out var eo) ? JsonElementToObject(eo) : null,
                    el.TryGetProperty("expected_failure", out var ef) && ef.GetBoolean(),
                    el.TryGetProperty("test_group", out var tg) ? tg.GetString() : null
                ));
            }

            return tests;
        }

        public static Dictionary<string, List<PurlTestCase>> LoadSpecFiles(string directory)
        {
            var dict = new Dictionary<string, List<PurlTestCase>>();
            foreach (var file in Directory.EnumerateFiles(directory, "*-test.json"))
            {
                try
                {
                    dict[Path.GetFileName(file)] = LoadTestFile(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing {file}: {ex.Message}");
                }
            }
            return dict;
        }

        private static object JsonElementToObject(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(el.GetRawText()),
                JsonValueKind.Array => JsonSerializer.Deserialize<List<object>>(el.GetRawText()),
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
    }

    public class PackageUrlTests
    {
        private static readonly string RootDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
        private static readonly string SpecPath = Path.Combine(RootDir, "spec", "tests", "spec", "specification-test.json");
        private static readonly string SpecDir = Path.Combine(RootDir, "spec", "tests", "types");

        public static IEnumerable<object[]> ParseTests => SpecLoader.LoadTestFile(SpecPath)
            .Where(tc => tc.TestType == "parse")
            .Select(tc => new object[] { tc });

        public static IEnumerable<object[]> BuildTests => SpecLoader.LoadTestFile(SpecPath)
            .Where(tc => tc.TestType == "build")
            .Select(tc => new object[] { tc });

        public static IEnumerable<object[]> FlattenedCases => SpecLoader.LoadSpecFiles(SpecDir)
            .SelectMany(kv => kv.Value.Select(v => new object[] { kv.Key, v.Description, v }));

        [Theory(DisplayName = "Parse tests")]
        [MemberData(nameof(ParseTests))]
        public void TestParse(PurlTestCase caseData)
        {
            if (caseData.ExpectedFailure)
            {
                Assert.ThrowsAny<Exception>(() => PackageUrl.FromString(caseData.Input.ToString()));
            }
            else
            {
                var result = PackageUrl.FromString(caseData.Input.ToString());
                Assert.Equal(caseData.ExpectedOutput?.ToString(), result.ToString());
            }
        }

        [Theory(DisplayName = "Build tests")]
        [MemberData(nameof(BuildTests))]
        public void TestBuild(PurlTestCase caseData)
        {
            var input = caseData.Input as Dictionary<string, object>;
            var qualifiers = input.ContainsKey("qualifiers")
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(input["qualifiers"].ToString())
                : null;

            if (caseData.ExpectedFailure)
            {
                Assert.ThrowsAny<Exception>(() =>
                    new PackageUrl(
                        input["type"].ToString(),
                        input["name"].ToString(),
                        input.GetValueOrDefault("version")?.ToString(),
                        input.GetValueOrDefault("namespace")?.ToString(),
                        qualifiers,
                        input.GetValueOrDefault("subpath")?.ToString()
                    ).ToString());
            }
            else
            {
                var purl = new PackageUrl(
                    input["type"].ToString(),
                    input["name"].ToString(),
                    input.GetValueOrDefault("version")?.ToString(),
                    input.GetValueOrDefault("namespace")?.ToString(),
                    qualifiers,
                    input.GetValueOrDefault("subpath")?.ToString()
                );
                Assert.Equal(caseData.ExpectedOutput?.ToString(), purl.ToString());
            }
        }

        [Theory(DisplayName = "Package type case tests")]
        [MemberData(nameof(FlattenedCases))]
        public void TestPackageTypeCases(string filename, string description, PurlTestCase caseData)
        {
            if (caseData.ExpectedFailure)
                Assert.ThrowsAny<Exception>(() => RunTestCase(caseData));
            else
                RunTestCase(caseData);
        }

        private void RunTestCase(PurlTestCase caseData)
        {
            switch (caseData.TestType)
            {
                case "parse":
                    var purl = PackageUrl.FromString(caseData.Input.ToString());
                    var expected = caseData.ExpectedOutput as Dictionary<string, object>;
                    Assert.Equal(expected["type"], purl.Type);
                    Assert.Equal(expected["namespace"], purl.Namespace);
                    Assert.Equal(expected["name"], purl.Name);
                    Assert.Equal(expected["version"], purl.Version);
                    if (expected.ContainsKey("qualifiers") && expected["qualifiers"] is Dictionary<string, object> q)
                        Assert.Equal(q.ToDictionary(k => k.Key, v => v.Value.ToString()), purl.Qualifiers);
                    else
                        Assert.Empty(purl.Qualifiers);
                    Assert.Equal(expected["subpath"], purl.Subpath);
                    break;

                case "roundtrip":
                    var rt = PackageUrl.FromString(caseData.Input.ToString());
                    Assert.Equal(caseData.ExpectedOutput.ToString(), rt.ToString());
                    break;

                case "build":
                    var inp = caseData.Input as Dictionary<string, object>;
                    var pq = inp.ContainsKey("qualifiers")
                        ? JsonSerializer.Deserialize<Dictionary<string, string>>(inp["qualifiers"].ToString())
                        : null;
                    var purl2 = new PackageUrl(
                        inp["type"].ToString(),
                        inp["name"].ToString(),
                        inp.GetValueOrDefault("version")?.ToString(),
                        inp.GetValueOrDefault("namespace")?.ToString(),
                        pq,
                        inp.GetValueOrDefault("subpath")?.ToString()
                    );
                    Assert.Equal(caseData.ExpectedOutput.ToString(), purl2.ToString());
                    break;

                case "validation":
                    // Placeholder: add if you port validate_string() to C#
                    break;

                default:
                    throw new Exception($"Unknown test type: {caseData.TestType}");
            }
        }
    }
}
