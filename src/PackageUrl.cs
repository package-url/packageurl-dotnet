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

namespace PackageUrl;

/// <summary>
/// Represents a Package URL (PURL) as defined by ECMA-427.
/// <para>
/// A PURL is a URL composed of seven components:
/// <c>scheme:type/namespace/name@version?qualifiers#subpath</c>
/// </para>
/// <para>
/// Components are separated by specific characters for unambiguous parsing.
/// A PURL does not contain a URL Authority; there is no support for username,
/// password, host, or port components. A namespace segment may look like a host,
/// but its interpretation is specific to the type.
/// </para>
/// <para>
/// See <see href="https://ecma-tc54.github.io/ECMA-427/">ECMA-427</see> for the full specification.
/// </para>
/// </summary>
[Serializable]
public sealed class PackageUrl : IEquatable<PackageUrl>
{
    /// <summary>
    /// The URL scheme. Always <c>"pkg"</c>.
    /// </summary>
    public string Scheme { get; private set; } = "pkg";

    /// <summary>
    /// The package type, such as npm, nuget, gem, or pypi. The canonical form is lowercase.
    /// </summary>
    public string Type { get; private set; }

    /// <summary>
    /// A type-specific name prefix, such as a Maven groupId, a Docker image owner,
    /// or a GitHub user or organization. May be <see langword="null"/>.
    /// </summary>
    public string Namespace { get; private set; }

    /// <summary>
    /// The name of the package.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The version of the package, or <see langword="null"/> if unspecified.
    /// </summary>
    public string Version { get; private set; }

    /// <summary>
    /// Qualifier key/value pairs for the package, such as OS, architecture, or distribution.
    /// May be <see langword="null"/>.
    /// </summary>
    public SortedDictionary<string, string> Qualifiers { get; private set; }

    /// <summary>
    /// A path relative to the package root, or <see langword="null"/> if unspecified.
    /// </summary>
    public string Subpath { get; private set; }

    /// <summary>
    /// Parses <paramref name="purl"/> into a new <see cref="PackageUrl"/>.
    /// </summary>
    /// <param name="purl">A valid Package URL string (e.g. <c>"pkg:npm/foobar@12.3.1"</c>).</param>
    /// <exception cref="MalformedPackageUrlException">Thrown if <paramref name="purl"/> is not a valid Package URL.</exception>
    public PackageUrl(string purl)
    {
        Parse(purl);
    }

    /// <summary>
    /// Creates a <see cref="PackageUrl"/> with only the required type and name components.
    /// </summary>
    /// <param name="type">The package type (e.g. nuget, npm, gem).</param>
    /// <param name="name">The package name.</param>
    /// <exception cref="MalformedPackageUrlException">Thrown if <paramref name="type"/> or <paramref name="name"/> is invalid.</exception>
    public PackageUrl(string type, string name)
        : this(type, null, name, null, null, null) { }

    /// <summary>
    /// Creates a <see cref="PackageUrl"/> from individual components.
    /// </summary>
    /// <param name="type">The package type (e.g. nuget, npm, gem).</param>
    /// <param name="namespace">The type-specific namespace (e.g. Maven groupId, GitHub user), or <see langword="null"/>.</param>
    /// <param name="name">The package name.</param>
    /// <param name="version">The package version, or <see langword="null"/>.</param>
    /// <param name="qualifiers">Qualifier key/value pairs, or <see langword="null"/>.</param>
    /// <param name="subpath">A path relative to the package root, or <see langword="null"/>.</param>
    /// <exception cref="MalformedPackageUrlException">Thrown if any component is invalid.</exception>
    public PackageUrl(
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
    /// Returns the canonical string representation of this PURL.
    /// </summary>
    public override string ToString()
    {
        int capacity = 4 + 1; // "pkg:"  + "/"
        if (Type != null)
        {
            capacity += Type.Length;
        }

        if (Namespace != null)
        {
            capacity += Namespace.Length + 1;
        }

        if (Name != null)
        {
            capacity += Name.Length;
        }

        if (Version != null)
        {
            capacity += Version.Length + 1;
        }

        var purl = new StringBuilder(capacity);
        purl.Append(Scheme).Append(':');
        if (Type != null)
        {
            purl.Append(Type);
        }
        purl.Append('/');
        if (Namespace != null)
        {
            string encodedNamespace = PercentEncode(Namespace, "/");
            purl.Append(encodedNamespace);
            purl.Append('/');
        }
        if (Name != null)
        {
            string encodedName = PercentEncode(Name, ":");
            purl.Append(encodedName);
        }
        if (Version != null)
        {
            string encodedVersion = PercentEncode(Version, ":");
            purl.Append('@').Append(encodedVersion);
        }
        if (Qualifiers != null && Qualifiers.Count > 0)
        {
            purl.Append("?");
            foreach (var pair in Qualifiers)
            {
                string encodedValue = PercentEncode(pair.Value, "/");
                purl.Append(pair.Key.ToLowerInvariant());
                purl.Append('=');
                purl.Append(encodedValue);
                purl.Append('&');
            }
            purl.Remove(purl.Length - 1, 1);
        }
        if (Subpath != null)
        {
            string encodedSubpath = PercentEncode(Subpath, "/:");
            purl.Append("#").Append(encodedSubpath);
        }
        return purl.ToString();
    }

    /// <inheritdoc />
    public bool Equals(PackageUrl other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Type, other.Type, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
            && string.Equals(Version, other.Version, StringComparison.Ordinal)
            && string.Equals(Subpath, other.Subpath, StringComparison.Ordinal)
            && QualifiersEqual(Qualifiers, other.Qualifiers);
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as PackageUrl);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Type?.GetHashCode() ?? 0);
            hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
            hash = hash * 31 + (Name?.GetHashCode() ?? 0);
            hash = hash * 31 + (Version?.GetHashCode() ?? 0);
            hash = hash * 31 + (Subpath?.GetHashCode() ?? 0);
            if (Qualifiers != null)
            {
                foreach (var pair in Qualifiers)
                {
                    hash = hash * 31 + pair.Key.GetHashCode();
                    hash = hash * 31 + pair.Value.GetHashCode();
                }
            }
            return hash;
        }
    }

    public static bool operator ==(PackageUrl left, PackageUrl right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(PackageUrl left, PackageUrl right) => !(left == right);

    private static bool QualifiersEqual(
        SortedDictionary<string, string> a,
        SortedDictionary<string, string> b
    )
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var pair in a)
        {
            if (
                !b.TryGetValue(pair.Key, out string value)
                || !string.Equals(pair.Value, value, StringComparison.Ordinal)
            )
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Percent-encodes a string per RFC 3986 §2.1 using the PURL allowed set.
    /// Characters in the allowed set (alphanumeric plus .-_~) are not encoded.
    /// Characters listed in <paramref name="preserve"/> are also kept unencoded.
    /// </summary>
    private static string PercentEncode(string value, string preserve = null)
    {
        // Fast path: check if encoding is needed at all
        bool needsEncoding = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (
                (c >= 'A' && c <= 'Z')
                || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9')
                || c == '.'
                || c == '-'
                || c == '_'
                || c == '~'
                || (preserve != null && preserve.IndexOf(c) >= 0)
            )
            {
                continue;
            }
            needsEncoding = true;
            break;
        }
        if (!needsEncoding)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        foreach (byte b in bytes)
        {
            char c = (char)b;
            if (
                (c >= 'A' && c <= 'Z')
                || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9')
                || c == '.'
                || c == '-'
                || c == '_'
                || c == '~'
                || (preserve != null && preserve.IndexOf(c) >= 0)
            )
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%');
                sb.Append(b.ToString("X2"));
            }
        }
        return sb.ToString();
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
            throw new MalformedPackageUrlException("The purl scheme must be 'pkg'.");
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

        int subpathIndex = remainder.LastIndexOf('#');
        if (subpathIndex >= 0)
        {
            Subpath = ValidateSubpath(WebUtility.UrlDecode(remainder.Substring(subpathIndex + 1)));
            remainder = remainder.Substring(0, subpathIndex);
        }

        int qualifiersIndex = remainder.LastIndexOf('?');
        if (qualifiersIndex >= 0)
        {
            Qualifiers = ValidateQualifiers(remainder.Substring(qualifiersIndex + 1));
            remainder = remainder.Substring(0, qualifiersIndex);
        }

        int versionIndex = remainder.LastIndexOf('@');
        if (versionIndex >= 0)
        {
            Version = WebUtility.UrlDecode(remainder.Substring(versionIndex + 1));
            remainder = remainder.Substring(0, versionIndex);
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
            string @namespace = string.Join("/", firstPartArray, 1, firstPartArray.Length - 2);

            Namespace = ValidateNamespace(WebUtility.UrlDecode(@namespace));
        }
    }

    private static string ValidateType(string type)
    {
        if (type == null || type.Length < 2)
        {
            throw new MalformedPackageUrlException(
                "The purl type is invalid. Must be at least two characters, start with a letter, and contain only letters, digits, '.', or '-'."
            );
        }
        char first = type[0];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z')))
        {
            throw new MalformedPackageUrlException(
                "The purl type is invalid. Must be at least two characters, start with a letter, and contain only letters, digits, '.', or '-'."
            );
        }
        for (int i = 1; i < type.Length; i++)
        {
            char c = type[i];
            if (
                !(
                    (c >= 'A' && c <= 'Z')
                    || (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '-'
                )
            )
            {
                throw new MalformedPackageUrlException(
                    "The purl type is invalid. Must be at least two characters, start with a letter, and contain only letters, digits, '.', or '-'."
                );
            }
        }
        return type.ToLowerInvariant();
    }

    private string ValidateNamespace(string @namespace)
    {
        if (@namespace == null)
        {
            return null;
        }
        @namespace = @namespace.Trim('/');
        if (@namespace.Length == 0)
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
            "bitbucket" or "github" or "pypi" or "gitlab" => @namespace.ToLowerInvariant(),
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
            "bitbucket" or "github" or "gitlab" => name.ToLowerInvariant(),
            "pypi" => name.Replace('_', '-').ToLowerInvariant(),
            _ => name,
        };
    }

    private static bool IsValidQualifierKey(string key)
    {
        if (key == null || key.Length == 0)
        {
            return false;
        }
        char first = key[0];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z')))
        {
            return false;
        }
        for (int i = 1; i < key.Length; i++)
        {
            char c = key[i];
            if (
                !(
                    (c >= 'A' && c <= 'Z')
                    || (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '_'
                    || c == '-'
                )
            )
            {
                return false;
            }
        }
        return true;
    }

    private static SortedDictionary<string, string> ValidateQualifiers(string qualifiers)
    {
        var list = new SortedDictionary<string, string>();
        string[] pairs = qualifiers.Split('&');
        foreach (var pair in pairs)
        {
            int eqIndex = pair.IndexOf('=');
            if (eqIndex >= 0)
            {
                string key = pair.Substring(0, eqIndex).ToLowerInvariant();
                string value = WebUtility.UrlDecode(pair.Substring(eqIndex + 1));

                if (!IsValidQualifierKey(key))
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
        string trimmed = subpath.Trim('/');
        if (trimmed.Length == 0)
        {
            return null;
        }

        // Fast path: no dot-only segments and no empty segments means
        // the trimmed string can be returned as-is.
        bool hasEmptyOrDotSegment = false;
        string[] segments = trimmed.Split('/');
        foreach (var segment in segments)
        {
            if (segment == "." || segment == "..")
            {
                throw new MalformedPackageUrlException(
                    $"The purl subpath must not contain '.' or '..' segments, but found: '{segment}'."
                );
            }
            if (segment.Length == 0)
            {
                hasEmptyOrDotSegment = true;
            }
        }
        if (!hasEmptyOrDotSegment)
        {
            return trimmed;
        }

        var validSegments = new List<string>();
        foreach (var segment in segments)
        {
            if (segment.Length > 0)
            {
                validSegments.Add(segment);
            }
        }
        return validSegments.Count > 0 ? string.Join("/", validSegments) : null;
    }
}
