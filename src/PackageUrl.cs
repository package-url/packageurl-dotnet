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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PackageUrl
{
    public class PackageUrl
    {
        private static readonly Regex SchemeRegex = new Regex(@"^[a-z][a-z0-9+.-]*$", RegexOptions.IgnoreCase);
        private static readonly Regex TypeRegex = new Regex(@"^[a-z][a-z0-9+.-]*$", RegexOptions.IgnoreCase);
        private static readonly Regex NameRegex = new Regex(@"^[a-zA-Z0-9_.\\-]+$", RegexOptions.Compiled);

        public string Scheme { get; private set; }
        public string Type { get; private set; }
        public string Namespace { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public Dictionary<string, string> Qualifiers { get; private set; }
        public string Subpath { get; private set; }

        public PackageUrl(
            string type,
            string name,
            string version = null,
            string @namespace = null,
            Dictionary<string, string> qualifiers = null,
            string subpath = null,
            string scheme = "pkg")
        {
            Scheme = ValidateScheme(scheme);
            Type = ValidateType(type);
            Namespace = NormalizeNamespace(@namespace);
            Name = ValidateName(name);
            Version = version?.Trim();
            Qualifiers = qualifiers != null
                ? new Dictionary<string, string>(qualifiers)
                : new Dictionary<string, string>();
            Subpath = NormalizeSubpath(subpath);
        }

        private static string ValidateScheme(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme) || !SchemeRegex.IsMatch(scheme))
                throw new ArgumentException($"Invalid scheme: {scheme}");
            return scheme.ToLowerInvariant();
        }

        private static string ValidateType(string type)
        {
            if (string.IsNullOrWhiteSpace(type) || !TypeRegex.IsMatch(type))
                throw new ArgumentException($"Invalid type: {type}");
            return type.ToLowerInvariant();
        }

        private static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !NameRegex.IsMatch(name))
                throw new ArgumentException($"Invalid name: {name}");
            return name;
        }

        private static string NormalizeNamespace(string ns)
        {
            if (string.IsNullOrWhiteSpace(ns))
                return null;

            return ns.Replace('\\', '/').Trim('/');
        }

        private static string NormalizeSubpath(string subpath)
        {
            if (string.IsNullOrWhiteSpace(subpath))
                return null;

            return subpath.Replace('\\', '/').Trim('/');
        }

        public static PackageUrl FromString(string purl)
        {
            if (string.IsNullOrWhiteSpace(purl))
                throw new ArgumentException("purl cannot be null or empty.");

            if (!purl.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Package URL must start with 'pkg:'");

            string remainder = purl.Substring(4);
            string scheme = "pkg";
            string subpath = null;
            string qualifiers = null;
            string version = null;

            // Extract subpath
            var subpathSplit = remainder.Split('#');
            if (subpathSplit.Length > 1)
            {
                remainder = subpathSplit[0];
                subpath = subpathSplit[1];
            }

            // Extract qualifiers
            var qualifierSplit = remainder.Split('?');
            if (qualifierSplit.Length > 1)
            {
                remainder = qualifierSplit[0];
                qualifiers = qualifierSplit[1];
            }

            // Extract version
            var versionSplit = remainder.Split('@');
            if (versionSplit.Length > 1)
            {
                remainder = versionSplit[0];
                version = versionSplit[1];
            }

            // Extract type / namespace / name
            var parts = remainder.Split('/');
            if (parts.Length < 2)
                throw new ArgumentException($"Invalid purl: {purl}");

            string type = parts[0];
            string name;
            string ns = null;

            if (parts.Length == 2)
                name = parts[1];
            else
            {
                ns = string.Join("/", parts.Skip(1).Take(parts.Length - 2));
                name = parts.Last();
            }

            var qualifiersDict = ParseQualifiers(qualifiers);

            return new PackageUrl(type, name, version, ns, qualifiersDict, subpath, scheme);
        }

        private static Dictionary<string, string> ParseQualifiers(string qualifiers)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(qualifiers))
                return dict;

            foreach (var pair in qualifiers.Split('&'))
            {
                var kv = pair.Split('=');
                if (kv.Length == 2)
                    dict[kv[0].ToLowerInvariant()] = kv[1];
            }

            return dict;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append($"{Scheme}:{Type}/");

            if (!string.IsNullOrWhiteSpace(Namespace))
                builder.Append($"{Namespace.TrimEnd('/')}/");

            builder.Append(Name);

            if (!string.IsNullOrWhiteSpace(Version))
                builder.Append($"@{Version}");

            if (Qualifiers?.Count > 0)
            {
                builder.Append("?");
                builder.Append(string.Join("&", Qualifiers.Select(kv => $"{kv.Key}={kv.Value}")));
            }

            if (!string.IsNullOrWhiteSpace(Subpath))
                builder.Append($"#{Subpath}");

            return builder.ToString();
        }

        public string ToCoordinates() => $"{Type}/{Namespace}/{Name}@{Version}";
    }
}
