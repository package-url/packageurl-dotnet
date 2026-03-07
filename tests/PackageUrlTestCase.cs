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

using System.Text.Json;
using Xunit.Sdk;

namespace PackageUrl.Tests;

/// <summary>
/// A single test case from the purl-spec test suite (schema v0.1).
/// </summary>
public sealed class PackageUrlTestCase : IXunitSerializable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Description { get; set; } = "";
    public string TestGroup { get; set; } = "";
    public string TestType { get; set; } = "";
    public bool ExpectedFailure { get; set; }
    public string? ExpectedFailureReason { get; set; }

    /// <summary>Input purl string (for parse/roundtrip tests).</summary>
    public string? InputPurl { get; set; }

    /// <summary>Input components (for build tests).</summary>
    public PackageUrlComponents? InputComponents { get; set; }

    /// <summary>Expected canonical purl string (for build/roundtrip tests).</summary>
    public string? ExpectedPurl { get; set; }

    /// <summary>Expected parsed components (for parse tests).</summary>
    public PackageUrlComponents? ExpectedComponents { get; set; }

    public PackageUrlTestCase() { }

    public override string ToString() => Description;

    public void Deserialize(IXunitSerializationInfo info)
    {
        Description = info.GetValue<string>(nameof(Description)) ?? "";
        TestGroup = info.GetValue<string>(nameof(TestGroup)) ?? "";
        TestType = info.GetValue<string>(nameof(TestType)) ?? "";
        ExpectedFailure = info.GetValue<bool>(nameof(ExpectedFailure));
        ExpectedFailureReason = info.GetValue<string?>(nameof(ExpectedFailureReason));
        InputPurl = info.GetValue<string?>(nameof(InputPurl)) ?? "";
        ExpectedPurl = info.GetValue<string?>(nameof(ExpectedPurl)) ?? "";

        var inputJson = info.GetValue<string?>("InputComponentsJson");
        if (inputJson != null)
            InputComponents = JsonSerializer.Deserialize<PackageUrlComponents>(
                inputJson,
                s_jsonOptions
            )!;

        var expectedJson = info.GetValue<string?>("ExpectedComponentsJson");
        if (expectedJson != null)
            ExpectedComponents = JsonSerializer.Deserialize<PackageUrlComponents>(
                expectedJson,
                s_jsonOptions
            )!;
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(Description), Description);
        info.AddValue(nameof(TestGroup), TestGroup);
        info.AddValue(nameof(TestType), TestType);
        info.AddValue(nameof(ExpectedFailure), ExpectedFailure);
        info.AddValue(nameof(ExpectedFailureReason), ExpectedFailureReason);
        info.AddValue(nameof(InputPurl), InputPurl);
        info.AddValue(nameof(ExpectedPurl), ExpectedPurl);
        info.AddValue(
            "InputComponentsJson",
            InputComponents != null
                ? JsonSerializer.Serialize(InputComponents, s_jsonOptions)
                : null
        );
        info.AddValue(
            "ExpectedComponentsJson",
            ExpectedComponents != null
                ? JsonSerializer.Serialize(ExpectedComponents, s_jsonOptions)
                : null
        );
    }
}
