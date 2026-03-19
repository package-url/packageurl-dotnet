# packageurl-dotnet

[![Build][build-image]][build-url]
[![License][license-image]][license-url]
[![NuGet][nuget-image]][nuget-url]

A .NET parser for [Package URLs](https://ecma-tc54.github.io/ECMA-427/) (ECMA-427). Handles strings like `pkg:nuget/Newtonsoft.Json@13.0.1` -- parses them apart, builds them from components, gives you a canonical form back.

Targets .NET Standard 2.0, so it works anywhere from .NET Framework 4.6.1 through .NET 10+.

## Install

```sh
dotnet add package packageurl-dotnet
```

Or in your project file:

```xml
<PackageReference Include="packageurl-dotnet" Version="2.0.0" />
```

## Usage

Parse a PURL string:

```csharp
var purl = new PackageUrl("pkg:nuget/Newtonsoft.Json@13.0.1");

Console.WriteLine(purl.Type);      // nuget
Console.WriteLine(purl.Name);      // Newtonsoft.Json
Console.WriteLine(purl.Version);   // 13.0.1
```

Build one from parts:

```csharp
var purl = new PackageUrl(
    type: "maven",
    @namespace: "org.apache.commons",
    name: "commons-lang3",
    version: "3.14.0",
    qualifiers: null,
    subpath: null);

Console.WriteLine(purl.ToString());
// pkg:maven/org.apache.commons/commons-lang3@3.14.0
```

There's also a two-argument shorthand if you only need type and name:

```csharp
var purl = new PackageUrl("npm", "lodash");
```

## Build from source

Requires .NET SDK 10+.

```sh
dotnet pack -c Release
dotnet test -c Release ./tests
```

Or open `PackageUrl.slnx` in Visual Studio 2022+ and run tests from Test Explorer.

## License

[MIT](LICENSE)

[build-image]: https://img.shields.io/github/actions/workflow/status/package-url/packageurl-dotnet/build.yml?branch=master&style=for-the-badge
[build-url]: https://github.com/package-url/packageurl-dotnet/actions/workflows/build.yml
[license-image]: https://img.shields.io/github/license/package-url/packageurl-dotnet?style=for-the-badge
[license-url]: https://github.com/package-url/packageurl-dotnet/blob/master/LICENSE
[nuget-image]: https://img.shields.io/nuget/v/packageurl-dotnet?style=for-the-badge
[nuget-url]: https://www.nuget.org/packages/packageurl-dotnet/
