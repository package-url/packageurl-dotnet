using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PackageUrl.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PackageUrlBenchmarks
{
    private readonly Dictionary<string, string> _purls = new()
    {
        ["Minimal"] = "pkg:npm/foobar@12.3.1",
        ["Namespace"] = "pkg:maven/org.apache.commons/io@1.3.4",
        ["Qualifiers"] =
            "pkg:docker/customer/dockerimage@sha256:244fd47e07d1004f0aed9c?repository_url=gcr.io",
        ["Full"] =
            "pkg:maven/org.apache/xmlgraphics-commons@1.5?classifier=sources&repository_url=repo.spring.io#src/main/java",
    };

    private readonly Dictionary<string, PackageUrl> _parsed = new();

    [ParamsSource(nameof(PurlNames))]
    public string Purl { get; set; } = null!;

    public static IEnumerable<string> PurlNames => ["Minimal", "Namespace", "Qualifiers", "Full"];

    [GlobalSetup]
    public void Setup()
    {
        foreach (var kvp in _purls)
        {
            _parsed[kvp.Key] = new PackageUrl(kvp.Value);
        }
    }

    // --- Parse ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Parse")]
    public PackageUrl Parse() => new(_purls[Purl]);

    // --- ToString ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ToString")]
    public string Serialize() => _parsed[Purl].ToString();

    // --- Equals ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Equals")]
    public bool Equal()
    {
        var purl = _parsed[Purl];
        return purl.Equals(purl);
    }

    // --- GetHashCode ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("GetHashCode")]
    public int Hash() => _parsed[Purl].GetHashCode();
}
