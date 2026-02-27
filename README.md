![Build](https://github.com/package-url/packageurl-dotnet/actions/workflows/build.yml/badge.svg)
[![License][license-image]][license-url]
[![NuGet version (packageurl-dotnet)](https://img.shields.io/nuget/v/packageurl-dotnet)](https://www.nuget.org/packages/packageurl-dotnet/)

Package URL (purl) for .NET
=========

This project implements a purl parser and class for .NET. Its available as a [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) library on [NuGet.org](https://www.nuget.org/packages/packageurl-dotnet/).

Build and Test (command line)
-------------------

From root of the repository, using [dotnet-cli](https://docs.microsoft.com/en-us/dotnet/core/tools/) v6.0+:

```sh
dotnet pack -c Release
dotnet test -c Release ./tests
````

Build and Test (Visual Studio)
-------------------

Open `./PackageUrl.sln` in Visual Studio 2022+, build solution and run tests using the `Test Explorer`.

Installation
-------------------

```sh
dotnet add <Path-to-Project-file> package packageurl-dotnet
```

or in project file, add:

```xml
<PackageReference Include="packageurl-dotnet" Version="1.3.0" />
```

Usage
-------------------

Import namespace:
```c#
import PackageUrl;
```

Creates a new PURL object from a string:
```c#
PackageURL purl = new PackageURL(purlString);
````

Creates a new PURL object from purl parameters:
```c#
PackageURL purl = new PackageURL(type, namespace, name, version, qualifiers, subpath);
````

License
-------------------

Permission to modify and redistribute is granted under the terms of the
[MIT License](https://github.com/package-url/packageurl-dotnet/blob/master/LICENSE).

[license-image]: https://img.shields.io/badge/license-mit%20license-brightgreen.svg
[license-url]: https://github.com/package-url/packageurl-dotnet/blob/master/LICENSE
