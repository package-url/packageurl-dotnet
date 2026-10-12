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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xunit;

namespace PackageUrl.Tests;

/// <summary>
/// Edge case tests for PURL type, qualifier, namespace, subpath, and name validation.
/// </summary>
public class PackageUrlTests
{
    [Theory]
    [InlineData("pkg:some+type/name")]
    [InlineData("pkg:some,type/name")]
    public void TestTypeWithInvalidCharactersThrows(string purl)
    {
        Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(purl));
    }

    [Theory]
    [InlineData("pkg:valid-type/name")]
    [InlineData("pkg:valid.type/name")]
    public void TestTypeWithValidCharactersParses(string purl)
    {
        var parsed = new PackageURL(purl);
        Assert.NotNull(parsed.Type);
    }

    [Theory]
    [InlineData("pkg:npm/foo@1.0?123BAD!!!=value")]
    [InlineData("pkg:npm/foo@1.0?=value")]
    [InlineData("pkg:npm/foo@1.0?-key=value")]
    [InlineData("pkg:npm/foo@1.0?_key=value")]
    public void TestInvalidQualifierKeyThrows(string purl)
    {
        Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(purl));
    }

    [Fact]
    public void TestQualifierKeysAreLowercased()
    {
        var purl = new PackageURL("pkg:npm/foo@1.0?Arch=i386");
        Assert.True(purl.Qualifiers!.ContainsKey("arch"));
        Assert.False(purl.Qualifiers!.ContainsKey("Arch"));
    }

    [Fact]
    public void TestQualifierValueContainingEquals()
    {
        var purl = new PackageURL("pkg:npm/foo@1.0?key=val%3Due");
        Assert.Equal("val=ue", purl.Qualifiers!["key"]);
    }

    [Fact]
    public void TestEmptyQualifierValueIsDiscarded()
    {
        var purl = new PackageURL("pkg:npm/foo@1.0?empty=&arch=i386");
        Assert.False(purl.Qualifiers!.ContainsKey("empty"));
        Assert.Equal("i386", purl.Qualifiers!["arch"]);
    }

    [Fact]
    public void TestDuplicateQualifierKeyThrows()
    {
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("pkg:npm/foo@1.0?arch=i386&arch=amd64")
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestQualifiersCannotBeMutated(bool parse)
    {
        var purl = parse
            ? new PackageURL("pkg:generic/a?arch=x64")
            : new PackageURL(
                "generic", null, "a", null,
                new SortedDictionary<string, string> { ["arch"] = "x64" }, null
            );
        var qualifiers = Assert.IsAssignableFrom<IDictionary<string, string>>(purl.Qualifiers);

        Assert.Throws<NotSupportedException>(() => qualifiers["arch"] = "arm64");
        Assert.Throws<NotSupportedException>(() => qualifiers.Add("invalid key", "value"));
        Assert.Throws<NotSupportedException>(() => qualifiers.Remove("arch"));
        Assert.Throws<NotSupportedException>(() => qualifiers.Clear());
        Assert.Equal(new PackageURL("pkg:generic/a?arch=x64"), purl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestQualifierInputIsCopied(bool initiallyEmpty)
    {
        var input = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!initiallyEmpty)
        {
            input.Add("arch", "x64");
        }
        var purl = new PackageURL("generic", null, "a", null, input, null);
        var expectedText = purl.ToString();
        var expected = new PackageURL(expectedText);
        var hash = purl.GetHashCode();
        var set = new HashSet<PackageURL> { purl };

        input["arch"] = "arm64";

        Assert.Equal(expectedText, purl.ToString());
        Assert.Equal(hash, purl.GetHashCode());
        Assert.Equal(expected, purl);
        Assert.Contains(expected, set);
    }

    [Fact]
    public void TestQualifiersCanBeReusedToConstructAnIndependentPurl()
    {
        var source = new PackageURL("pkg:generic/a?arch=x64");
        var rebuilt = new PackageURL("generic", null, "a", null, source.Qualifiers, null);

        Assert.Equal(source, rebuilt);
        Assert.Equal(source.GetHashCode(), rebuilt.GetHashCode());
        Assert.NotSame(source.Qualifiers, rebuilt.Qualifiers);
    }

    [Theory]
    [InlineData("pkg:generic/a")]
    [InlineData("pkg:generic/a?")]
    [InlineData("pkg:generic/a?unused=")]
    [InlineData("pkg:generic/a?first=&second=")]
    public void TestEmptyQualifiersAreAbsent(string input)
    {
        var expected = new PackageURL("pkg:generic/a");
        var purl = new PackageURL(input);
        var built = new PackageURL(
            "generic", null, "a", null, new SortedDictionary<string, string>(), null
        );
        var discarded = new PackageURL(
            "generic", null, "a", null,
            new SortedDictionary<string, string> { ["unused"] = "" }, null
        );

        foreach (var value in new[] { purl, built, discarded })
        {
            Assert.Null(value.Qualifiers);
            Assert.Equal(expected.ToString(), value.ToString());
            Assert.Equal(expected, value);
            Assert.Equal(expected.GetHashCode(), value.GetHashCode());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("th-TH")]
    public void TestQualifierIdentityIsCultureIndependent(string culture)
    {
        const string canonical = "pkg:generic/a?a-b=1&a.b=2&a_b=3&ab=4";
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var expected = new PackageURL(canonical);
            var input = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["a-b"] = "1",
                ["a.b"] = "2",
                ["a_b"] = "3",
                ["ab"] = "4",
            };
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var parsed = new PackageURL("pkg:generic/a?ab=4&a_b=3&a.b=2&a-b=1");
            var built = new PackageURL("generic", null, "a", null, input, null);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            foreach (var purl in new[] { parsed, built })
            {
                Assert.Equal(canonical, purl.ToString());
                Assert.Equal(input, purl.Qualifiers);
                Assert.Equal(expected, purl);
                Assert.Equal(expected.GetHashCode(), purl.GetHashCode());
                Assert.Equal("1", purl.Qualifiers!["a-b"]);
                Assert.Equal("2", purl.Qualifiers["a.b"]);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TestCaseNormalizedDuplicateQualifierKeysThrow()
    {
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("pkg:generic/a?Arch=x64&arch=arm64")
        );
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL(
                "generic", null, "a", null,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Arch"] = "x64",
                    ["arch"] = "arm64",
                }, null
            )
        );
    }

    [Theory]
    [InlineData(null, "JSONStream", "pkg:npm/JSONStream@1.3.5")]
    [InlineData("@Scope", "JSONStream", "pkg:npm/%40Scope/JSONStream@1.3.5")]
    [InlineData("@Scope", "jsonstream", "pkg:npm/%40Scope/jsonstream@1.3.5")]
    public void TestNpmIdentifierCaseIsPreserved(string? ns, string name, string canonical)
    {
        var parsed = new PackageURL(canonical.Replace("pkg:npm", "pkg:NPM"));
        var built = new PackageURL("NPM", ns, name, "1.3.5", null, null);
        var lowercased = new PackageURL(
            "npm", ns?.ToLowerInvariant(), name.ToLowerInvariant(), "1.3.5", null, null
        );

        foreach (var purl in new[] { parsed, built })
        {
            Assert.Equal(ns, purl.Namespace);
            Assert.Equal(name, purl.Name);
            Assert.Equal(canonical, purl.ToString());
            Assert.NotEqual(lowercased, purl);
        }
        Assert.Equal(parsed, built);
        Assert.Equal(parsed.GetHashCode(), built.GetHashCode());
    }

    [Fact]
    public void TestNpmShortConstructorPreservesNameCase()
    {
        var purl = new PackageURL("NPM", "JSONStream");

        Assert.Equal("pkg:npm/JSONStream", purl.ToString());
        Assert.Equal(new PackageURL("pkg:npm/JSONStream"), purl);
    }

    [Theory]
    [InlineData("%", false)]
    [InlineData("%2", false)]
    [InlineData("%GG", false)]
    [InlineData("%FF", true)]
    [InlineData("%C3", true)]
    [InlineData("%C0%AF", true)]
    [InlineData("%ED%A0%80", true)]
    [InlineData("%F4%90%80%80", true)]
    [InlineData("%80", true)]
    [InlineData("%C3x%A9", true)]
    public void TestInvalidPercentEncodingThrows(string encoded, bool invalidUtf8)
    {
        foreach (var input in PurlsWithComponent(encoded))
        {
            var error = Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(input));
            if (invalidUtf8)
            {
                Assert.IsType<DecoderFallbackException>(error.InnerException);
            }
        }
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void TestInvalidUnicodeIsRejectedDuringConstruction(int surrogate)
    {
        var invalid = new string((char)surrogate, 1);
        foreach (var input in PurlsWithComponent(invalid))
        {
            var error = Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(input));
            Assert.IsType<EncoderFallbackException>(error.InnerException);
        }

        Action[] constructors =
        [
            () => new PackageURL("generic", invalid, "a", null, null, null),
            () => new PackageURL("generic", invalid),
            () => new PackageURL("generic", null, "a", invalid, null, null),
            () => new PackageURL(
                "generic", null, "a", null,
                new SortedDictionary<string, string> { ["key"] = invalid }, null
            ),
            () => new PackageURL("generic", null, "a", null, null, invalid),
        ];
        foreach (var construct in constructors)
        {
            var error = Assert.Throws<MalformedPackageUrlException>(construct);
            Assert.IsType<EncoderFallbackException>(error.InnerException);
        }
    }

    [Theory]
    [InlineData("\u00E9", "%C3%A9")]
    [InlineData("\U0001F600", "%F0%9F%98%80")]
    [InlineData("\uFFFD", "%EF%BF%BD")]
    [InlineData("+", "%2B")]
    [InlineData("%GG", "%25GG")]
    [InlineData("a%2Fb", "a%252Fb")]
    [InlineData("@?#", "%40%3F%23")]
    public void TestValidPercentEncodingRoundTrips(string decoded, string encoded)
    {
        var canonical = $"pkg:generic/{encoded}/{encoded}@{encoded}?key={encoded}#{encoded}";
        var parsed = new PackageURL(canonical);
        var built = new PackageURL(
            "generic", decoded, decoded, decoded,
            new SortedDictionary<string, string> { ["key"] = decoded }, decoded
        );

        Assert.Equal(decoded, parsed.Namespace);
        Assert.Equal(decoded, parsed.Name);
        Assert.Equal(decoded, parsed.Version);
        Assert.Equal(decoded, parsed.Qualifiers!["key"]);
        Assert.Equal(decoded, parsed.Subpath);
        Assert.Equal(canonical, parsed.ToString());
        Assert.Equal(canonical, built.ToString());
        Assert.Equal(built, parsed);
        Assert.Equal(built.GetHashCode(), parsed.GetHashCode());
    }

    [Theory]
    [InlineData("\u00E9")]
    [InlineData("\U0001F600")]
    [InlineData("\uFFFD")]
    [InlineData("+")]
    public void TestRawUnicodeAndPlusArePreserved(string name)
    {
        var parsed = new PackageURL("pkg:generic/" + name);
        var rebuilt = new PackageURL(parsed.ToString());

        Assert.Equal(name, parsed.Name);
        Assert.Equal(name, rebuilt.Name);
        Assert.Equal(parsed, rebuilt);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(257)]
    public void TestPercentDecodingAcrossBufferThresholds(int encodedLength)
    {
        var prefix = new string('a', encodedLength - 12);
        var input = "pkg:generic/" + prefix + "%F0%9F%98%80";
        var parsed = new PackageURL(input);

        Assert.Equal(prefix + "\U0001F600", parsed.Name);
        Assert.Equal(input, parsed.ToString());
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("pkg:generic/" + new string('a', encodedLength - 3) + "%FF")
        );
    }

    private static string[] PurlsWithComponent(string value) =>
    [
        $"pkg:generic/{value}/a",
        $"pkg:generic/a{value}",
        $"pkg:generic/a@{value}",
        $"pkg:generic/a?key={value}",
        $"pkg:generic/a?ignored{value}",
        $"pkg:generic/a#{value}",
    ];

    [Theory]
    [InlineData("src%2Flib")]
    [InlineData("src%2flib")]
    [InlineData("%2Fetc")]
    [InlineData("a/%2F/b")]
    public void TestSubpathEncodedSlashesThrow(string subpath)
    {
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("pkg:generic/a#" + subpath)
        );
    }

    [Fact]
    public void TestSubpathIsDecodedOnlyOnce()
    {
        const string canonical = "pkg:generic/a#src%252Flib";
        var parsed = new PackageURL(canonical);
        var built = new PackageURL("generic", null, "a", null, null, "src%2Flib");

        Assert.Equal("src%2Flib", parsed.Subpath);
        Assert.Equal(canonical, parsed.ToString());
        Assert.Equal(canonical, built.ToString());
        Assert.Equal(parsed, built);
    }

    [Fact]
    public void TestSubpathUnicodeSegmentsRoundTrip()
    {
        const string canonical = "pkg:generic/a#src/%E6%96%87/%F0%9F%98%80";
        var parsed = new PackageURL("pkg:generic/a#//src/%E6%96%87/%F0%9F%98%80//");
        var built = new PackageURL("generic", null, "a", null, null, "src/\u6587/\U0001F600");

        Assert.Equal("src/\u6587/\U0001F600", parsed.Subpath);
        Assert.Equal(canonical, parsed.ToString());
        Assert.Equal(parsed, built);
        Assert.Equal(parsed, new PackageURL(parsed.ToString()));
    }

    [Theory]
    [InlineData("path/%2e/file")]
    [InlineData("path/%2E%2e/file")]
    public void TestSubpathEncodedDotSegmentsThrow(string subpath)
    {
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("pkg:generic/a#" + subpath)
        );
    }

    [Fact]
    public void TestEncodedSlashesRemainValidInNamesAndQualifierValues()
    {
        const string canonical = "pkg:generic/a%2Fb?path=src%2Flib";
        var purl = new PackageURL(canonical);

        Assert.Equal("a/b", purl.Name);
        Assert.Equal("src/lib", purl.Qualifiers!["path"]);
        Assert.Equal(canonical, purl.ToString());
    }

    [Theory]
    [InlineData("path/./file")]
    [InlineData("path/../file")]
    [InlineData(".")]
    [InlineData("..")]
    public void TestSubpathWithDotSegmentsThrows(string subpath)
    {
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("npm", null, "foo", "1.0", null, subpath)
        );
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("pkg:npm/foo@1.0#" + subpath)
        );
    }

    [Theory]
    [InlineData("path//file", "path/file")]
    [InlineData("/path/file/", "path/file")]
    [InlineData("///path///file///", "path/file")]
    public void TestSubpathEmptySegmentsAreNormalized(string subpath, string expected)
    {
        var purl = new PackageURL("npm", null, "foo", "1.0", null, subpath);
        var parsed = new PackageURL("pkg:npm/foo@1.0#" + subpath);

        Assert.Equal(expected, purl.Subpath);
        Assert.Equal(expected, parsed.Subpath);
        Assert.Equal(purl, parsed);
    }

    [Theory]
    [InlineData("org//pkg")]
    public void TestNamespaceWithEmptySegmentThrows(string ns)
    {
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("npm", ns, "foo", "1.0", null, null)
        );
    }

    [Theory]
    [InlineData("/org/pkg", "org/pkg")]
    [InlineData("org/pkg/", "org/pkg")]
    public void TestNamespaceLeadingTrailingSlashesAreStripped(string ns, string expected)
    {
        var purl = new PackageURL("npm", ns, "foo", "1.0", null, null);
        Assert.Equal(expected, purl.Namespace);
    }

    [Fact]
    public void TestEmptyNameThrows()
    {
        Assert.Throws<MalformedPackageUrlException>(() =>
            new PackageURL("npm", null, "", "1.0", null, null)
        );
    }
}
