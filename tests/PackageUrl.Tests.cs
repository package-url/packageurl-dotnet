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

using Newtonsoft.Json;
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
        
        [Theory]
        [InlineData("{\"Scheme\":\"pkg\",\"Type\":\"npm\",\"Namespace\":null,\"Name\":\"foo\"," +
                    "\"Version\":\"1.2.3\",\"Qualifiers\":null,\"Subpath\":null}",
            "pkg:npm/foo@1.2.3")]
        public void TestJsonSerialization(string serializedPurl, string packageUrlStr)
        {
            PackageURL purl = new PackageURL(packageUrlStr);
            string jsonPurl = purl.ToJson();

            Assert.Equal(serializedPurl, jsonPurl);
        }
        
        [Theory]
        [InlineData("{\"Scheme\":\"pkg\",\"Type\":\"npm\",\"Namespace\":null,\"Name\":\"foo\"," +
                    "\"Version\":\"1.2.3\",\"Qualifiers\":null,\"Subpath\":null}",
            "pkg:npm/foo@1.2.3")]
        public void TestJsonDeserialization(string data, string packageUrlStr)
        {
            PackageURL expectedPurl = new PackageURL(packageUrlStr);
            PackageURL deserializedPurl = PackageURL.FromJson(data);

            Assert.Equal(expectedPurl.ToString(), deserializedPurl.ToString());
            Assert.Equal("pkg", deserializedPurl.Scheme);
            Assert.Equal(expectedPurl.Type, deserializedPurl.Type);
            Assert.Equal(expectedPurl.Namespace, deserializedPurl.Namespace);
            Assert.Equal(expectedPurl.Name, deserializedPurl.Name);
            Assert.Equal(expectedPurl.Version, deserializedPurl.Version);
            Assert.Equal(expectedPurl.Subpath, deserializedPurl.Subpath);
            if (expectedPurl.Qualifiers != null)
            {
                Assert.NotNull(deserializedPurl.Qualifiers);
            }
        }
    }
}
