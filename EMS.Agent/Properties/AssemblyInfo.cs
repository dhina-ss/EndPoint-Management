using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

// The agent uses WMI and Windows Service hosting; it never runs elsewhere.
[assembly: SupportedOSPlatform("windows")]

// Lets the test project exercise internal pure-logic helpers (e.g. the
// hosts-file transformation) without touching real system files.
[assembly: InternalsVisibleTo("EMS.Agent.Tests")]
