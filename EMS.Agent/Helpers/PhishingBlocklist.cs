namespace EMS.Agent.Helpers;

/// <summary>
/// The always-on default block list applied to every managed device on any
/// network. This is a small starter set built from well-known, purpose-built
/// security test domains (safe to block, and usable to verify that blocking
/// works). In production this should be expanded from a maintained threat
/// feed (e.g. URLhaus, PhishTank, OpenPhish) - either baked in at build time
/// or delivered centrally alongside the per-device list.
/// </summary>
public static class PhishingBlocklist
{
    public static readonly IReadOnlyList<string> Domains = new[]
    {
        // OpenDNS / Cisco Umbrella phishing + malware test domains.
        "internetbadguys.com",
        "examplemalwaredomain.com",
        "exampleadultsite.com",
        // Google Safe Browsing malware test domain.
        "testsafebrowsing.appspot.com",
    };
}
