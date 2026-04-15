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
public sealed class PackageURL : IEquatable<PackageURL>
{
    private readonly int _hashCode;

    /// <summary>
    /// The URL scheme. Always <c>"pkg"</c>.
    /// </summary>
    public string Scheme { get; private set; } = "pkg";

    /// <summary>
    /// The package type, such as npm, nuget, gem, or pypi. The canonical form is lowercase.
    /// </summary>
    public string Type { get; private set; } = null!;

    /// <summary>
    /// A type-specific name prefix, such as a Maven groupId, a Docker image owner,
    /// or a GitHub user or organization. May be <see langword="null"/>.
    /// </summary>
    public string? Namespace { get; private set; }

    /// <summary>
    /// The name of the package.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// The version of the package, or <see langword="null"/> if unspecified.
    /// </summary>
    public string? Version { get; private set; }

    /// <summary>
    /// Qualifier key/value pairs for the package, such as OS, architecture, or distribution.
    /// May be <see langword="null"/>.
    /// </summary>
    public SortedDictionary<string, string>? Qualifiers { get; private set; }

    /// <summary>
    /// A path relative to the package root, or <see langword="null"/> if unspecified.
    /// </summary>
    public string? Subpath { get; private set; }

    /// <summary>
    /// Parses <paramref name="purl"/> into a new <see cref="PackageURL"/>.
    /// </summary>
    /// <param name="purl">A valid Package URL string (e.g. <c>"pkg:npm/foobar@12.3.1"</c>).</param>
    /// <exception cref="MalformedPackageUrlException">Thrown if <paramref name="purl"/> is not a valid Package URL.</exception>
    public PackageURL(string purl)
    {
        Parse(purl);
        _hashCode = ComputeHashCode();
    }

    /// <summary>
    /// Creates a <see cref="PackageURL"/> with only the required type and name components.
    /// </summary>
    /// <param name="type">The package type (e.g. nuget, npm, gem).</param>
    /// <param name="name">The package name.</param>
    /// <exception cref="MalformedPackageUrlException">Thrown if <paramref name="type"/> or <paramref name="name"/> is invalid.</exception>
    public PackageURL(string type, string name)
        : this(type, null, name, null, null, null) { }

    /// <summary>
    /// Creates a <see cref="PackageURL"/> from individual components.
    /// </summary>
    /// <param name="type">The package type (e.g. nuget, npm, gem).</param>
    /// <param name="namespace">The type-specific namespace (e.g. Maven groupId, GitHub user), or <see langword="null"/>.</param>
    /// <param name="name">The package name.</param>
    /// <param name="version">The package version, or <see langword="null"/>.</param>
    /// <param name="qualifiers">Qualifier key/value pairs, or <see langword="null"/>.</param>
    /// <param name="subpath">A path relative to the package root, or <see langword="null"/>.</param>
    /// <exception cref="MalformedPackageUrlException">Thrown if any component is invalid.</exception>
    public PackageURL(
        string type,
        string? @namespace,
        string name,
        string? version,
        SortedDictionary<string, string>? qualifiers,
        string? subpath
    )
    {
        Type = ValidateType(type);
        Qualifiers = ValidateQualifierEntries(qualifiers);
        Namespace = ValidateNamespace(@namespace);
        Name = ValidateName(name);
        Version = ValidateVersion(version);
        Subpath = ValidateSubpath(subpath);
        ValidateTypeConstraints();
        _hashCode = ComputeHashCode();
    }

    /// <summary>
    /// Returns the canonical string representation of this PURL.
    /// </summary>
    public override string ToString()
    {
#if NET8_0_OR_GREATER
        return ToStringSpan();
#else
        return ToStringBuilder();
#endif
    }

#if NET8_0_OR_GREATER
    private string ToStringSpan()
    {
        // First pass: compute exact length
        int length = 4 + 1; // "pkg:" + "/"
        length += Type.Length;

        if (Namespace != null)
        {
            length += PercentEncodedLength(Namespace, "/:");
            length += 1; // trailing '/'
        }

        length += PercentEncodedLength(Name, ":");

        if (Version != null)
        {
            length += 1; // '@'
            length += PercentEncodedLength(Version, ":");
        }

        if (Qualifiers != null && Qualifiers.Count > 0)
        {
            length += 1; // '?'
            bool first = true;
            foreach (var pair in Qualifiers)
            {
                if (!first)
                {
                    length += 1; // '&'
                }

                first = false;
                length += pair.Key.Length; // keys are already lowercase
                length += 1; // '='
                length += PercentEncodedLength(pair.Value, ":");
            }
        }

        if (Subpath != null)
        {
            length += 1; // '#'
            length += PercentEncodedLength(Subpath, "/:");
        }

        return string.Create(
            length,
            this,
            static (span, self) =>
            {
                int pos = 0;

                // "pkg:"
                "pkg:".AsSpan().CopyTo(span);
                pos += 4;

                // type
                self.Type.AsSpan().CopyTo(span.Slice(pos));
                pos += self.Type.Length;

                // "/"
                span[pos++] = '/';

                // namespace
                if (self.Namespace != null)
                {
                    pos += PercentEncodeInto(span.Slice(pos), self.Namespace, "/:");
                    span[pos++] = '/';
                }

                // name
                pos += PercentEncodeInto(span.Slice(pos), self.Name, ":");

                // version
                if (self.Version != null)
                {
                    span[pos++] = '@';
                    pos += PercentEncodeInto(span.Slice(pos), self.Version, ":");
                }

                // qualifiers
                if (self.Qualifiers != null && self.Qualifiers.Count > 0)
                {
                    span[pos++] = '?';
                    bool first = true;
                    foreach (var pair in self.Qualifiers)
                    {
                        if (!first)
                        {
                            span[pos++] = '&';
                        }

                        first = false;
                        pair.Key.AsSpan().CopyTo(span.Slice(pos));
                        pos += pair.Key.Length;
                        span[pos++] = '=';
                        pos += PercentEncodeInto(span.Slice(pos), pair.Value, ":");
                    }
                }

                // subpath
                if (self.Subpath != null)
                {
                    span[pos++] = '#';
                    pos += PercentEncodeInto(span.Slice(pos), self.Subpath, "/:");
                }
            }
        );
    }
#endif

    private string ToStringBuilder()
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

        if (Qualifiers != null)
        {
            foreach (var pair in Qualifiers)
            {
                capacity += pair.Key.Length + 1 + pair.Value.Length + 1;
            }
        }

        if (Subpath != null)
        {
            capacity += Subpath.Length + 1;
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
            string encodedNamespace = PercentEncode(Namespace, "/:");
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
            purl.Append('?');
            foreach (var pair in Qualifiers)
            {
                string encodedValue = PercentEncode(pair.Value, ":");
                purl.Append(pair.Key);
                purl.Append('=');
                purl.Append(encodedValue);
                purl.Append('&');
            }
            purl.Remove(purl.Length - 1, 1);
        }
        if (Subpath != null)
        {
            string encodedSubpath = PercentEncode(Subpath, "/:");
            purl.Append('#').Append(encodedSubpath);
        }
        return purl.ToString();
    }

    /// <inheritdoc />
    public bool Equals(PackageURL? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _hashCode == other._hashCode
            && string.Equals(Type, other.Type, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
            && string.Equals(Version, other.Version, StringComparison.Ordinal)
            && string.Equals(Subpath, other.Subpath, StringComparison.Ordinal)
            && QualifiersEqual(Qualifiers, other.Qualifiers);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PackageURL);

    /// <inheritdoc />
    public override int GetHashCode() => _hashCode;

    private int ComputeHashCode()
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

    public static bool operator ==(PackageURL? left, PackageURL? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(PackageURL? left, PackageURL? right) => !(left == right);

    private static bool QualifiersEqual(
        SortedDictionary<string, string>? a,
        SortedDictionary<string, string>? b
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
                !b.TryGetValue(pair.Key, out string? value)
                || !string.Equals(pair.Value, value, StringComparison.Ordinal)
            )
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Decodes percent-encoded sequences (%XX) without treating '+' as space.
    /// Unlike <see cref="WebUtility.UrlDecode"/>, which uses form-encoding rules,
    /// PURL uses strict percent-encoding where '+' is a literal character.
    /// Consecutive percent-encoded bytes are accumulated and decoded as a single
    /// UTF-8 sequence per ECMA-427 §5.4.
    /// </summary>
    private static string PercentDecode(string value)
    {
        if (value.IndexOf('%') < 0)
        {
            return value;
        }

#if NET8_0_OR_GREATER
        return PercentDecodeSpan(value.AsSpan());
#else
        return PercentDecodeLegacy(value);
#endif
    }

#if NET8_0_OR_GREATER
    private static string PercentDecodeSpan(ReadOnlySpan<char> value)
    {
        if (value.IndexOf('%') < 0)
        {
            return value.ToString();
        }

        Span<byte> byteBuffer =
            value.Length <= 256 ? stackalloc byte[value.Length] : new byte[value.Length];
        int byteCount = 0;

        char[] outputArray = new char[value.Length];
        int outputPos = 0;

        for (int i = 0; i < value.Length; i++)
        {
            if (
                value[i] == '%'
                && i + 2 < value.Length
                && IsHexDigit(value[i + 1])
                && IsHexDigit(value[i + 2])
            )
            {
                int hi = HexVal(value[i + 1]);
                int lo = HexVal(value[i + 2]);
                byteBuffer[byteCount++] = (byte)((hi << 4) | lo);
                i += 2;
            }
            else
            {
                if (byteCount > 0)
                {
                    int charCount = Encoding.UTF8.GetChars(
                        byteBuffer.Slice(0, byteCount),
                        outputArray.AsSpan(outputPos)
                    );
                    outputPos += charCount;
                    byteCount = 0;
                }
                outputArray[outputPos++] = value[i];
            }
        }
        if (byteCount > 0)
        {
            int charCount = Encoding.UTF8.GetChars(
                byteBuffer.Slice(0, byteCount),
                outputArray.AsSpan(outputPos)
            );
            outputPos += charCount;
        }
        return new string(outputArray, 0, outputPos);
    }
#endif

    private static string PercentDecodeLegacy(string value)
    {
        var sb = new StringBuilder(value.Length);
        var byteBuffer = new List<byte>();
        for (int i = 0; i < value.Length; i++)
        {
            if (
                value[i] == '%'
                && i + 2 < value.Length
                && IsHexDigit(value[i + 1])
                && IsHexDigit(value[i + 2])
            )
            {
                int hi = HexVal(value[i + 1]);
                int lo = HexVal(value[i + 2]);
                byteBuffer.Add((byte)((hi << 4) | lo));
                i += 2;
            }
            else
            {
                if (byteBuffer.Count > 0)
                {
                    sb.Append(Encoding.UTF8.GetString(byteBuffer.ToArray()));
                    byteBuffer.Clear();
                }
                sb.Append(value[i]);
            }
        }
        if (byteBuffer.Count > 0)
        {
            sb.Append(Encoding.UTF8.GetString(byteBuffer.ToArray()));
        }
        return sb.ToString();
    }

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');

    private static int HexVal(char c) =>
        c >= '0' && c <= '9' ? c - '0'
        : c >= 'A' && c <= 'F' ? c - 'A' + 10
        : c - 'a' + 10;

    private static bool IsUnreserved(char c) =>
        (c >= 'A' && c <= 'Z')
        || (c >= 'a' && c <= 'z')
        || (c >= '0' && c <= '9')
        || c == '.'
        || c == '-'
        || c == '_'
        || c == '~';

    /// <summary>
    /// Percent-encodes a string per RFC 3986 §2.1 using the PURL allowed set.
    /// Characters in the allowed set (alphanumeric plus .-_~) are not encoded.
    /// Characters listed in <paramref name="preserve"/> are also kept unencoded.
    /// </summary>
    private static string PercentEncode(string value, string? preserve = null)
    {
        // Fast path: check if encoding is needed at all
        bool needsEncoding = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (IsUnreserved(c) || (preserve != null && preserve.IndexOf(c) >= 0))
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
            if (IsUnreserved(c) || (preserve != null && preserve.IndexOf(c) >= 0))
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

#if NET8_0_OR_GREATER
    private static bool NeedsPercentEncoding(string value, string preserve)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!IsUnreserved(c) && preserve.IndexOf(c) < 0)
            {
                return true;
            }
        }
        return false;
    }

    private static int PercentEncodedLength(string value, string preserve)
    {
        if (!NeedsPercentEncoding(value, preserve))
        {
            return value.Length;
        }

        int length = 0;
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        foreach (byte b in bytes)
        {
            char c = (char)b;
            if (IsUnreserved(c) || preserve.IndexOf(c) >= 0)
            {
                length += 1;
            }
            else
            {
                length += 3;
            }
        }
        return length;
    }

    private static int PercentEncodeInto(Span<char> destination, string value, string preserve)
    {
        if (!NeedsPercentEncoding(value, preserve))
        {
            value.AsSpan().CopyTo(destination);
            return value.Length;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        int pos = 0;
        foreach (byte b in bytes)
        {
            char c = (char)b;
            if (IsUnreserved(c) || preserve.IndexOf(c) >= 0)
            {
                destination[pos++] = c;
            }
            else
            {
                destination[pos++] = '%';
                destination[pos++] = HexChar(b >> 4);
                destination[pos++] = HexChar(b & 0xF);
            }
        }
        return pos;
    }

    private static char HexChar(int nibble) =>
        (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
#endif

    private void Parse(string purl)
    {
        if (purl == null || string.IsNullOrWhiteSpace(purl))
        {
            throw new MalformedPackageUrlException("The purl string is null or empty.");
        }

#if NET8_0_OR_GREATER
        ParseSpan(purl);
#else
        ParseLegacy(purl);
#endif
    }

#if NET8_0_OR_GREATER
    private void ParseSpan(string purl)
    {
        ReadOnlySpan<char> span = purl.AsSpan();

        // Validate scheme
        if (
            span.Length < 4
            || (span[0] != 'p' && span[0] != 'P')
            || (span[1] != 'k' && span[1] != 'K')
            || (span[2] != 'g' && span[2] != 'G')
            || span[3] != ':'
        )
        {
            throw new MalformedPackageUrlException("The purl scheme must be 'pkg'.");
        }

        ReadOnlySpan<char> remainder = span.Slice(4);

        // A purl must not contain a URL authority (no userinfo or port)
        if (remainder.Length >= 2 && remainder[0] == '/' && remainder[1] == '/')
        {
            int authorityEnd = remainder.Slice(2).IndexOf('/');
            ReadOnlySpan<char> authority =
                authorityEnd == -1 ? remainder.Slice(2) : remainder.Slice(2, authorityEnd);

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

        // Per RFC 3986, the fragment starts at the first '#'.
        int subpathIndex = remainder.IndexOf('#');
        if (subpathIndex >= 0)
        {
            Subpath = ValidateSubpath(PercentDecodeSpan(remainder.Slice(subpathIndex + 1)));
            remainder = remainder.Slice(0, subpathIndex);
        }

        // Per RFC 3986, the query starts at the first '?'
        int qualifiersIndex = remainder.IndexOf('?');
        if (qualifiersIndex >= 0)
        {
            Qualifiers = ValidateQualifiersSpan(remainder.Slice(qualifiersIndex + 1));
            remainder = remainder.Slice(0, qualifiersIndex);
        }

        // The version '@' separator can only appear in the last path segment.
        int lastSlash = remainder.LastIndexOf('/');
        int versionIndex = remainder.LastIndexOf('@');
        if (versionIndex >= 0 && versionIndex > lastSlash)
        {
            Version = PercentDecodeSpan(remainder.Slice(versionIndex + 1));
            remainder = remainder.Slice(0, versionIndex);
        }

        // Strip leading '/' characters
        while (remainder.Length > 0 && remainder[0] == '/')
        {
            remainder = remainder.Slice(1);
        }

        // Find first '/' to split type from the rest
        int firstSlashIdx = remainder.IndexOf('/');
        if (firstSlashIdx < 0)
        {
            throw new MalformedPackageUrlException(
                "The purl must contain at least a type and a name (e.g., pkg:type/name)."
            );
        }

        Type = ValidateType(remainder.Slice(0, firstSlashIdx).ToString());

        ReadOnlySpan<char> pathRemainder = remainder.Slice(firstSlashIdx + 1);

        // Find last '/' in pathRemainder to split name from namespace
        int lastPathSlash = pathRemainder.LastIndexOf('/');
        if (lastPathSlash < 0)
        {
            // No namespace, just name
            Name = ValidateName(PercentDecodeSpan(pathRemainder));
        }
        else
        {
            // name is after the last slash
            Name = ValidateName(PercentDecodeSpan(pathRemainder.Slice(lastPathSlash + 1)));

            // namespace is everything before the last slash
            ReadOnlySpan<char> nsSpan = pathRemainder.Slice(0, lastPathSlash);

            // Decode each segment individually per ECMA-427 §5.6.3
            var nsBuilder = new StringBuilder(nsSpan.Length);
            bool firstSeg = true;
            while (nsSpan.Length > 0)
            {
                int slashIdx = nsSpan.IndexOf('/');
                ReadOnlySpan<char> segment;
                if (slashIdx < 0)
                {
                    segment = nsSpan;
                    nsSpan = ReadOnlySpan<char>.Empty;
                }
                else
                {
                    segment = nsSpan.Slice(0, slashIdx);
                    nsSpan = nsSpan.Slice(slashIdx + 1);
                }

                string decoded = PercentDecodeSpan(segment);
                if (decoded.IndexOf('/') >= 0)
                {
                    throw new MalformedPackageUrlException(
                        "A purl namespace segment must not contain '/' when percent-decoded."
                    );
                }

                if (!firstSeg)
                {
                    nsBuilder.Append('/');
                }

                firstSeg = false;
                nsBuilder.Append(decoded);
            }

            Namespace = ValidateNamespace(nsBuilder.ToString());
        }

        Version = ValidateVersion(Version);
        ValidateTypeConstraints();
    }

    private static SortedDictionary<string, string> ValidateQualifiersSpan(
        ReadOnlySpan<char> qualifiers
    )
    {
        var list = new SortedDictionary<string, string>();

        while (qualifiers.Length > 0)
        {
            int ampIdx = qualifiers.IndexOf('&');
            ReadOnlySpan<char> pair;
            if (ampIdx < 0)
            {
                pair = qualifiers;
                qualifiers = ReadOnlySpan<char>.Empty;
            }
            else
            {
                pair = qualifiers.Slice(0, ampIdx);
                qualifiers = qualifiers.Slice(ampIdx + 1);
            }

            int eqIndex = pair.IndexOf('=');
            if (eqIndex >= 0)
            {
                string key = pair.Slice(0, eqIndex).ToString().ToLowerInvariant();
                string value = PercentDecodeSpan(pair.Slice(eqIndex + 1));

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
#endif

    private void ParseLegacy(string purl)
    {
        // Validate scheme
        if (!purl.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
        {
            throw new MalformedPackageUrlException("The purl scheme must be 'pkg'.");
        }

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

        // Per RFC 3986, the fragment starts at the first '#'.
        int subpathIndex = remainder.IndexOf('#');
        if (subpathIndex >= 0)
        {
            Subpath = ValidateSubpath(PercentDecode(remainder.Substring(subpathIndex + 1)));
            remainder = remainder.Substring(0, subpathIndex);
        }

        // Per RFC 3986, the query starts at the first '?' (before any fragment,
        // which has already been stripped above).
        int qualifiersIndex = remainder.IndexOf('?');
        if (qualifiersIndex >= 0)
        {
            Qualifiers = ValidateQualifiers(remainder.Substring(qualifiersIndex + 1));
            remainder = remainder.Substring(0, qualifiersIndex);
        }

        // The version '@' separator can only appear in the last path segment.
        // Earlier '@' characters (e.g. pkg:npm/@scope/name) are part of the namespace.
        int lastSlash = remainder.LastIndexOf('/');
        int versionIndex = remainder.LastIndexOf('@');
        if (versionIndex >= 0 && versionIndex > lastSlash)
        {
            Version = PercentDecode(remainder.Substring(versionIndex + 1));
            remainder = remainder.Substring(0, versionIndex);
        }

        // The 'remainder' should now consist of the type, an optional namespace, and the name

        // Strip leading '/' characters (e.g. "//type/..." from authority-like prefix).
        // Do NOT strip trailing '/' — a trailing slash indicates an empty name segment.
        remainder = remainder.TrimStart('/');

        string[] firstPartArray = remainder.Split('/');
        if (firstPartArray.Length < 2)
        { // The array must contain a 'type' and a 'name' at minimum
            throw new MalformedPackageUrlException(
                "The purl must contain at least a type and a name (e.g., pkg:type/name)."
            );
        }

        Type = ValidateType(firstPartArray[0]);
        Name = ValidateName(PercentDecode(firstPartArray[firstPartArray.Length - 1]));
        Version = ValidateVersion(Version);

        // Test for namespaces
        if (firstPartArray.Length > 2)
        {
            // Decode each namespace segment individually per ECMA-427 §5.6.3.
            // A decoded segment must not contain '/' characters; decoding the
            // joined string would silently turn %2F into a segment separator.
            var nsSegments = new string[firstPartArray.Length - 2];
            for (int i = 1; i < firstPartArray.Length - 1; i++)
            {
                string decoded = PercentDecode(firstPartArray[i]);
                if (decoded.IndexOf('/') >= 0)
                {
                    throw new MalformedPackageUrlException(
                        "A purl namespace segment must not contain '/' when percent-decoded."
                    );
                }
                nsSegments[i - 1] = decoded;
            }
            string @namespace = string.Join("/", nsSegments);

            Namespace = ValidateNamespace(@namespace);
        }

        ValidateTypeConstraints();
    }

    private static string ValidateType(string type)
    {
        if (type == null || type.Length == 0)
        {
            throw new MalformedPackageUrlException(
                "The purl type is invalid. Must start with a letter and contain only letters, digits, '.', or '-'."
            );
        }
        char first = type[0];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z')))
        {
            throw new MalformedPackageUrlException(
                "The purl type is invalid. Must start with a letter and contain only letters, digits, '.', or '-'."
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
                    "The purl type is invalid. Must start with a letter and contain only letters, digits, '.', or '-'."
                );
            }
        }
        return type.ToLowerInvariant();
    }

    private string? ValidateNamespace(string? @namespace)
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
            "alpm"
            or "apk"
            or "bitbucket"
            or "composer"
            or "deb"
            or "github"
            or "gitlab"
            or "golang"
            or "hex"
            or "luarocks"
            or "npm"
            or "pypi"
            or "qpkg"
            or "rpm"
            or "vscode-extension"
            or "yocto" => @namespace.ToLowerInvariant(),
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
            "alpm"
            or "apk"
            or "bitbucket"
            or "bitnami"
            or "composer"
            or "deb"
            or "github"
            or "gitlab"
            or "golang"
            or "hex"
            or "luarocks"
            or "npm"
            or "oci"
            or "otp"
            or "pub"
            or "vscode-extension" => name.ToLowerInvariant(),
            "pypi" => name.Replace('_', '-').ToLowerInvariant(),
            "mlflow" => AdjustMlflowName(name),
            _ => name,
        };
    }

    private string AdjustMlflowName(string name)
    {
        if (Qualifiers != null && Qualifiers.TryGetValue("repository_url", out string? repoUrl))
        {
            if (repoUrl.Contains("azureml"))
            {
                return name;
            }
            if (repoUrl.Contains("databricks"))
            {
                return name.ToLowerInvariant();
            }
        }
        return name;
    }

    private string? ValidateVersion(string? version)
    {
        if (version == null)
        {
            return null;
        }
        return Type switch
        {
            "huggingface" or "oci" or "pypi" or "vscode-extension" => version.ToLowerInvariant(),
            _ => version,
        };
    }

    /// <summary>
    /// Validates type-specific constraints after all components have been set.
    /// </summary>
    private void ValidateTypeConstraints()
    {
        switch (Type)
        {
            // Types that require a namespace
            case "alpm":
            case "apk":
            case "bitbucket":
            case "composer":
            case "deb":
            case "github":
            // https://github.com/package-url/purl-spec/issues/817
            // case "golang":
            case "huggingface":
            case "maven":
            case "qpkg":
            case "rpm":
                if (Namespace == null)
                {
                    throw new MalformedPackageUrlException($"A {Type} purl must have a namespace.");
                }
                break;
            case "cpan":
                if (Namespace == null)
                {
                    throw new MalformedPackageUrlException(
                        "A cpan purl must have a namespace (author)."
                    );
                }
                if (Name.Contains("::"))
                {
                    throw new MalformedPackageUrlException(
                        "A cpan distribution name must not contain '::'."
                    );
                }
                break;
            case "swift":
                if (Namespace == null)
                {
                    throw new MalformedPackageUrlException("A swift purl must have a namespace.");
                }
                break;
            case "vscode-extension":
                if (Namespace == null)
                {
                    throw new MalformedPackageUrlException(
                        "A vscode-extension purl must have a namespace (publisher)."
                    );
                }
                break;

            // Types that prohibit a namespace
            case "bazel":
            case "bitnami":
            case "cargo":
            case "cocoapods":
            case "conda":
            case "cran":
            case "gem":
            case "hackage":
            case "mlflow":
            case "nuget":
            case "oci":
            case "opam":
            case "otp":
            case "pub":
            case "pypi":
                if (Namespace != null)
                {
                    throw new MalformedPackageUrlException(
                        $"A {Type} purl must not have a namespace."
                    );
                }
                break;
            case "julia":
                if (Namespace != null)
                {
                    throw new MalformedPackageUrlException(
                        "A julia purl must not have a namespace."
                    );
                }
                if (Qualifiers == null || !Qualifiers.ContainsKey("uuid"))
                {
                    throw new MalformedPackageUrlException(
                        "A julia purl must have a 'uuid' qualifier."
                    );
                }
                break;

            // Types with required qualifiers
            case "swid":
                if (Qualifiers == null || !Qualifiers.ContainsKey("tag_id"))
                {
                    throw new MalformedPackageUrlException(
                        "A swid purl must have a 'tag_id' qualifier."
                    );
                }
                break;
        }
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
                string value = PercentDecode(pair.Substring(eqIndex + 1));

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

    /// <summary>
    /// Validates pre-built qualifier entries, normalizing keys to lowercase and
    /// filtering out entries with empty values per ECMA-427 §5.6.6.
    /// </summary>
    private static SortedDictionary<string, string>? ValidateQualifierEntries(
        SortedDictionary<string, string>? qualifiers
    )
    {
        if (qualifiers == null || qualifiers.Count == 0)
        {
            return qualifiers;
        }

        var normalized = new SortedDictionary<string, string>();
        foreach (var pair in qualifiers)
        {
            string key = pair.Key.ToLowerInvariant();

            if (!IsValidQualifierKey(key))
            {
                throw new MalformedPackageUrlException(
                    $"Invalid purl qualifier key: '{key}'. Keys must start with a letter and contain only letters, digits, '.', '_', or '-'."
                );
            }

            if (string.IsNullOrEmpty(pair.Value))
            {
                continue;
            }

            if (normalized.ContainsKey(key))
            {
                throw new MalformedPackageUrlException($"Duplicate purl qualifier key: '{key}'.");
            }

            normalized.Add(key, pair.Value);
        }
        return normalized.Count > 0 ? normalized : null;
    }

    private static string? ValidateSubpath(string? subpath)
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
