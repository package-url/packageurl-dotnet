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
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PackageUrl;

/// <summary>
/// Provides an object representation of a Package URL and easy access to its parts.
///
/// A purl is a URL composed of seven components:
/// scheme:type/namespace/name@version?qualifiers#subpath
///
/// Components are separated by a specific character for unambiguous parsing.
/// A purl must NOT contain a URL Authority i.e. there is no support for username,
/// password, host and port components. A namespace segment may sometimes look
/// like a host but its interpretation is specific to a type.
///
/// To read full-spec, visit <a href="https://github.com/package-url/purl-spec">https://github.com/package-url/purl-spec</a>
/// </summary>
[Serializable]
public sealed class PackageURL
{
    /// <summary>
    /// The url encoding of /.
    /// </summary>
    private const string EncodedSlash = "%2F";
    private const string EncodedColon = "%3A";

    private static readonly Regex s_typePattern = new Regex(
        "^[a-zA-Z][a-zA-Z0-9.-]+$",
        RegexOptions.Compiled
    );

    private static readonly Regex s_qualifierKeyPattern = new Regex(
        "^[a-zA-Z][a-zA-Z0-9._-]*$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// The PackageURL scheme constant.
    /// </summary>
    public string Scheme { get; private set; } = "pkg";

    /// <summary>
    /// The package "type" or package "protocol" such as nuget, npm, nuget, gem, pypi, etc.
    /// </summary>
    public string Type { get; private set; }

    /// <summary>
    /// The name prefix such as a Maven groupid, a Docker image owner, a GitHub user or organization.
    /// </summary>
    public string Namespace { get; private set; }

    /// <summary>
    /// The name of the package.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The version of the package.
    /// </summary>
    public string Version { get; private set; }

    /// <summary>
    /// Extra qualifying data for a package such as an OS, architecture, a distro, etc.
    /// <summary>
    public SortedDictionary<string, string> Qualifiers { get; private set; }

    /// <summary>
    /// Extra subpath within a package, relative to the package root.
    /// </summary>
    public string Subpath { get; private set; }

    /// <summary>
    /// Constructs a new PackageURL object by parsing the specified string.
    /// </summary>
    /// <param name="purl">A valid package URL string to parse.</param>
    /// <exception cref="MalformedPackageUrlException">Thrown when parsing fails.</exception>
    public PackageURL(string purl)
    {
        Parse(purl);
    }

    /// <summary>
    /// Constructs a new PackageURL object by specifying only the required
    /// parameters necessary to create a valid PackageURL.
    /// </summary>
    /// <param name="type">Type of package (i.e. nuget, npm, gem, etc).</param>
    /// <param name="name">Name of the package.</param>
    /// <exception cref="MalformedPackageUrlException">Thrown when parsing fails.</exception>
    public PackageURL(string type, string name)
        : this(type, null, name, null, null, null) { }

    /// <summary>
    /// Constructs a new PackageURL object.
    /// </summary>
    /// <param name="type">Type of package (i.e. nuget, npm, gem, etc).</param>
    /// <param name="namespace">Namespace of package (i.e. group, owner, organization).</param>
    /// <param name="name">Name of the package.</param>
    /// <param name="version">Version of the package.</param>
    /// <param name="qualifiers"><see cref="SortedDictionary{string, string}"/> of key/value pair qualifiers.</param>
    /// @param qualifiers an array of key/value pair qualifiers
    /// @param subpath the subpath string
    /// <exception cref="MalformedPackageUrlException">Thrown when parsing fails.</exception>
    public PackageURL(
        string type,
        string @namespace,
        string name,
        string version,
        SortedDictionary<string, string> qualifiers,
        string subpath
    )
    {
        Type = ValidateType(type);
        Namespace = ValidateNamespace(@namespace);
        Name = ValidateName(name);
        Version = version;
        Qualifiers = qualifiers;
        Subpath = ValidateSubpath(subpath);
    }

    /// <summary>
    /// Returns a canonicalized representation of the purl.
    /// </summary>
    public override string ToString()
    {
        var purl = new StringBuilder();
        purl.Append(Scheme).Append(':');
        if (Type != null)
        {
            purl.Append(Type);
        }
        purl.Append('/');
        if (Namespace != null)
        {
            string encodedNamespace = WebUtility.UrlEncode(Namespace).Replace(EncodedSlash, "/");
            purl.Append(encodedNamespace);
            purl.Append('/');
        }
        if (Name != null)
        {
            string encodedName = WebUtility.UrlEncode(Name).Replace(EncodedColon, ":");
            purl.Append(encodedName);
        }
        if (Version != null)
        {
            string encodedVersion = WebUtility.UrlEncode(Version).Replace(EncodedColon, ":");
            purl.Append('@').Append(encodedVersion);
        }
        if (Qualifiers != null && Qualifiers.Count > 0)
        {
            purl.Append("?");
            foreach (var pair in Qualifiers)
            {
                string encodedValue = WebUtility.UrlEncode(pair.Value).Replace(EncodedSlash, "/");
                purl.Append(pair.Key.ToLower());
                purl.Append('=');
                purl.Append(encodedValue);
                purl.Append('&');
            }
            purl.Remove(purl.Length - 1, 1);
        }
        if (Subpath != null)
        {
            string encodedSubpath = WebUtility
                .UrlEncode(Subpath)
                .Replace(EncodedSlash, "/")
                .Replace(EncodedColon, ":");
            purl.Append("#").Append(encodedSubpath);
        }
        return purl.ToString();
    }

    private void Parse(string purl)
    {
        if (purl == null || string.IsNullOrWhiteSpace(purl))
        {
            throw new MalformedPackageUrlException("The purl string is null or empty.");
        }

        // Validate scheme
        if (!purl.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
        {
            throw new MalformedPackageUrlException(
                "The purl scheme must be 'pkg'."
            );
        }

        // This is the purl (minus the scheme) that needs parsed.
        string remainder = purl.Substring(4);

        // A purl must not contain a URL authority (no userinfo or port)
        if (remainder.Length >= 2 && remainder[0] == '/' && remainder[1] == '/')
        {
            int authorityEnd = remainder.IndexOf('/', 2);
            string authority =
                authorityEnd == -1
                    ? remainder.Substring(2)
                    : remainder.Substring(2, authorityEnd - 2);

            if (authority.IndexOf('@') >= 0)
            {
                throw new MalformedPackageUrlException(
                    "A purl must not contain a user, password, or port."
                );
            }

            int colonIdx = authority.LastIndexOf(':');
            if (colonIdx >= 0 && colonIdx < authority.Length - 1)
            {
                bool isPort = true;
                for (int i = colonIdx + 1; i < authority.Length; i++)
                {
                    if (authority[i] < '0' || authority[i] > '9')
                    {
                        isPort = false;
                        break;
                    }
                }
                if (isPort)
                {
                    throw new MalformedPackageUrlException(
                        "A purl must not contain a user, password, or port."
                    );
                }
            }
        }

        if (remainder.Contains("#"))
        { // subpath is optional - check for existence
            int index = remainder.LastIndexOf("#");
            Subpath = ValidateSubpath(WebUtility.UrlDecode(remainder.Substring(index + 1)));
            remainder = remainder.Substring(0, index);
        }

        if (remainder.Contains("?"))
        { // qualifiers are optional - check for existence
            int index = remainder.LastIndexOf("?");
            Qualifiers = ValidateQualifiers(remainder.Substring(index + 1));
            remainder = remainder.Substring(0, index);
        }

        if (remainder.Contains("@"))
        { // version is optional - check for existence
            int index = remainder.LastIndexOf("@");
            Version = WebUtility.UrlDecode(remainder.Substring(index + 1));
            remainder = remainder.Substring(0, index);
        }

        // The 'remainder' should now consist of the type, an optional namespace, and the name

        // Strip zero or more '/' ('type')
        remainder = remainder.Trim('/');

        string[] firstPartArray = remainder.Split('/');
        if (firstPartArray.Length < 2)
        { // The array must contain a 'type' and a 'name' at minimum
            throw new MalformedPackageUrlException(
                "The purl must contain at least a type and a name (e.g., pkg:type/name)."
            );
        }

        Type = ValidateType(firstPartArray[0]);
        Name = ValidateName(WebUtility.UrlDecode(firstPartArray[firstPartArray.Length - 1]));

        // Test for namespaces
        if (firstPartArray.Length > 2)
        {
            string @namespace = "";
            int i;
            for (i = 1; i < firstPartArray.Length - 2; ++i)
            {
                @namespace += firstPartArray[i] + '/';
            }
            @namespace += firstPartArray[i];

            Namespace = ValidateNamespace(WebUtility.UrlDecode(@namespace));
        }
    }

    private static string ValidateType(string type)
    {
        if (type == null || !s_typePattern.IsMatch(type))
        {
            throw new MalformedPackageUrlException(
                "The purl type is invalid. Must be at least two characters, start with a letter, and contain only letters, digits, '.', or '-'."
            );
        }
        return type.ToLower();
    }

    private string ValidateNamespace(string @namespace)
    {
        if (@namespace == null)
        {
            return null;
        }
        foreach (var segment in @namespace.Split('/'))
        {
            if (segment.Length == 0)
            {
                throw new MalformedPackageUrlException(
                    "The purl namespace has an empty segment between '/' separators."
                );
            }
        }
        return Type switch
        {
            "bitbucket" or "github" or "pypi" or "gitlab" => @namespace.ToLower(),
            _ => @namespace,
        };
    }

    private string ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new MalformedPackageUrlException("The purl name must not be empty.");
        }
        return Type switch
        {
            "bitbucket" or "github" or "gitlab" => name.ToLower(),
            "pypi" => name.Replace('_', '-').ToLower(),
            _ => name,
        };
    }

    private static SortedDictionary<string, string> ValidateQualifiers(string qualifiers)
    {
        var list = new SortedDictionary<string, string>();
        string[] pairs = qualifiers.Split('&');
        foreach (var pair in pairs)
        {
            if (pair.Contains("="))
            {
                string[] kvpair = pair.Split(['='], 2);
                string key = kvpair[0].ToLower();
                string value = WebUtility.UrlDecode(kvpair[1]);

                if (!s_qualifierKeyPattern.IsMatch(key))
                {
                    throw new MalformedPackageUrlException(
                        $"Invalid purl qualifier key: '{key}'. Keys must start with a letter and contain only letters, digits, '.', '_', or '-'."
                    );
                }

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (list.ContainsKey(key))
                {
                    throw new MalformedPackageUrlException(
                        $"Duplicate purl qualifier key: '{key}'."
                    );
                }

                list.Add(key, value);
            }
        }
        return list;
    }

    private static string ValidateSubpath(string subpath)
    {
        if (subpath == null)
        {
            return null;
        }
        string[] segments = subpath.Trim('/').Split('/');
        var validSegments = new List<string>();
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                continue;
            }
            if (segment == "." || segment == "..")
            {
                throw new MalformedPackageUrlException(
                    $"The purl subpath must not contain '.' or '..' segments, but found: '{segment}'."
                );
            }
            validSegments.Add(segment);
        }
        return validSegments.Count > 0 ? string.Join("/", validSegments) : null;
    }
}
