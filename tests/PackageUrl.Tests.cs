// SPDX-License-Identifier: MIT

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
