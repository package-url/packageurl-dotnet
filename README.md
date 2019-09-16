[![Unix CI](https://travis-ci.com/package-url/packageurl-dotnet.svg?branch=master)](https://travis-ci.com/package-url/packageurl-dotnet)
[![Windows Status](https://ci.appveyor.com/api/projects/status/github/package-url/packageurl-dotnet?svg=true)](https://ci.appveyor.com/project/package-url/packageurl-dotnet/branch/master)
[![License][license-image]][license-url]

Package URL (purl) for .NET
=========

This project implements a purl parser and class for .NET.

Build and Test (command line)
-------------------

From root of the repository, using dotnet-cli v2.1+:

```sh
dotnet pack -c Release
dotnet test -c Release ./tests
````

Build and Test (Visual Studio)
-------------------

Open `./PackageUrl.sln` in Visual Studio 2017+, build solution and run tests using the `Test Explorer`.

Installation
-------------------

```sh
dotnet add <Path-to-Project-file> package PackageUrl --version 1.0.0
```

or in project file, add:

```xml
<PackageReference Include="PackageUrl" Version="1.0.0" />
```

Usage
-------------------

Creates a new PURL object from a string:
```c#
PackageUrl purl = new PackageUrl(purlString);
````

Creates a new PURL object from purl parameters:
```c#
PackageUrl purl = new PackageUrl(type, namespace, name, version, qualifiers, subpath);
````

License
-------------------

Permission to modify and redistribute is granted under the terms of the
[MIT License](https://github.com/package-url/packageurl-dotnet/blob/master/LICENSE)

[license-image]: https://img.shields.io/badge/license-mit%20license-brightgreen.svg
[license-url]: https://github.com/package-url/packageurl-dotnet/blob/master/LICENSE
