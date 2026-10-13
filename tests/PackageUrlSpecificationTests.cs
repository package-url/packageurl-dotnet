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

using Xunit;

namespace PackageUrl.Tests;

/// <summary>
/// Test cases for PackageURL parsing, building, and validation with normalization.
/// </summary>
/// <remarks>
/// Source revision and paths are recorded in TestAssets/purl-spec.json.
/// Schema: https://packageurl.org/schemas/purl-test.schema-0.2.json
/// </remarks>
public class PackageUrlSpecificationTests
{
    [Theory]
    [PackageUrlTestData("TestAssets", "parse")]
    public void TestParsing(PackageUrlTestCase data)
    {
        if (data.ExpectedFailure)
        {
            Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(data.InputPurl!));
            return;
        }

        PackageURL purl = new(data.InputPurl!);
        var expected = data.ExpectedComponents!;

        Assert.Equal("pkg", purl.Scheme);
        Assert.Equal(expected.Type, purl.Type);
        Assert.Equal(expected.Namespace, purl.Namespace);
        Assert.Equal(expected.Name, purl.Name);
        Assert.Equal(expected.Version, purl.Version);
        Assert.Equal(expected.Subpath, purl.Subpath);
        if (expected.Qualifiers != null)
        {
            Assert.NotNull(purl.Qualifiers);
            Assert.Equal(expected.Qualifiers, purl.Qualifiers);
        }
        else
        {
            Assert.Null(purl.Qualifiers);
        }
    }

    [Theory]
    [PackageUrlTestData("TestAssets", "build")]
    public void TestBuilding(PackageUrlTestCase data)
    {
        var input = data.InputComponents!;

        if (data.ExpectedFailure)
        {
            Assert.Throws<MalformedPackageUrlException>(() =>
                new PackageURL(
                    input.Type!,
                    input.Namespace,
                    input.Name!,
                    input.Version,
                    input.Qualifiers,
                    input.Subpath
                )
            );
            return;
        }

        PackageURL purl = new(
            input.Type!,
            input.Namespace,
            input.Name!,
            input.Version,
            input.Qualifiers,
            input.Subpath
        );

        Assert.Equal(data.ExpectedPurl, purl.ToString());
    }

    [Theory]
    [PackageUrlTestData("TestAssets", "validate")]
    public void TestValidation(PackageUrlTestCase data)
    {
        if (data.ExpectedFailure)
        {
            Assert.Throws<MalformedPackageUrlException>(() => new PackageURL(data.InputPurl!));
            return;
        }

        PackageURL purl = new(data.InputPurl!);
        Assert.Equal(data.ExpectedPurl, purl.ToString());
    }
}
