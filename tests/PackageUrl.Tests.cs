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

using PackageUrl.Tests.TestAssets;
using Xunit;

namespace PackageUrl.Tests
{
    /// <summary>
    /// Test cases for PackageURL parsing.
    /// </summary>
    /// <remarks>
    /// Original test cases retrieved from: https://raw.githubusercontent.com/package-url/purl-spec/master/test-suite-data.json
    /// </remarks>
    public class PackageURLTest
    {
        [Theory]
        [PurlTestData("TestAssets/test-suite-data.json")]
        public void TestConstructorParsing(PurlTestData data)
        {
            if (data.IsInvalid)
            {
                Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(data.Purl));
                return;
            }

            PackageURL purl = new PackageURL(data.Purl);
            Assert.Equal(data.CanonicalPurl, purl.ToString());
            Assert.Equal("pkg", purl.Scheme);
            Assert.Equal(data.Type, purl.Type);
            Assert.Equal(data.Namespace, purl.Namespace);
            Assert.Equal(data.Name, purl.Name);
            Assert.Equal(data.Version, purl.Version);
            Assert.Equal(data.Subpath, purl.Subpath);
            if (data.Qualifiers != null)
            {
                Assert.NotNull(purl.Qualifiers);
            }
        }

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
            Assert.True(purl.Qualifiers.ContainsKey("arch"));
            Assert.False(purl.Qualifiers.ContainsKey("Arch"));
        }

        [Fact]
        public void TestQualifierValueContainingEquals()
        {
            var purl = new PackageURL("pkg:npm/foo@1.0?key=val%3Due");
            Assert.Equal("val=ue", purl.Qualifiers["key"]);
        }

        [Fact]
        public void TestEmptyQualifierValueIsDiscarded()
        {
            var purl = new PackageURL("pkg:npm/foo@1.0?empty=&arch=i386");
            Assert.False(purl.Qualifiers.ContainsKey("empty"));
            Assert.Equal("i386", purl.Qualifiers["arch"]);
        }

        [Fact]
        public void TestDuplicateQualifierKeyThrows()
        {
            Assert.Throws<MalformedPackageUrlException>(() =>
                new PackageURL("pkg:npm/foo@1.0?arch=i386&arch=amd64")
            );
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
        }

        [Theory]
        [InlineData("path//file", "path/file")]
        [InlineData("/path/file/", "path/file")]
        [InlineData("///path///file///", "path/file")]
        public void TestSubpathEmptySegmentsAreNormalized(string subpath, string expected)
        {
            var purl = new PackageURL("npm", null, "foo", "1.0", null, subpath);
            Assert.Equal(expected, purl.Subpath);
        }

        [Theory]
        [InlineData("org//pkg")]
        [InlineData("/org/pkg")]
        [InlineData("org/pkg/")]
        public void TestNamespaceWithEmptySegmentThrows(string ns)
        {
            Assert.Throws<MalformedPackageUrlException>(() =>
                new PackageURL("npm", ns, "foo", "1.0", null, null)
            );
        }

        [Fact]
        public void TestEmptyNameThrows()
        {
            Assert.Throws<MalformedPackageUrlException>(() =>
                new PackageURL("npm", null, "", "1.0", null, null)
            );
        }

        [Theory]
        [PurlTestData("TestAssets/test-suite-data.json")]
        public void TestConstructorParameters(PurlTestData data)
        {
            if (data.IsInvalid)
            {
                Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(data.Purl));
                return;
            }

            PackageURL purl = new PackageURL(data.Type, data.Namespace, data.Name, data.Version, data.Qualifiers, data.Subpath);

            Assert.Equal(data.CanonicalPurl, purl.ToString());
            Assert.Equal("pkg", purl.Scheme);
            Assert.Equal(data.Type, purl.Type);
            Assert.Equal(data.Namespace, purl.Namespace);
            Assert.Equal(data.Name, purl.Name);
            Assert.Equal(data.Version, purl.Version);
            Assert.Equal(data.Subpath, purl.Subpath);
            if (data.Qualifiers != null)
            {
                Assert.NotNull(purl.Qualifiers);
            }
        }
    }
}
