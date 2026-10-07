// Deliberately no [assembly: InternalsVisibleTo(...)].
//
// This assembly declares no internal members, so the grant conveyed nothing, and it leaked a
// test-assembly name into the shipped public metadata of a 1.0 package. The repository rule in
// AGENTS.md ("Source generator rules") also applies: the Purview.SourceGeneratorFramework merge
// pass strips every InternalsVisibleTo declaration, so cross-component contracts must be public.
// Expose shared identity publicly (see DiagnosticLibrary) rather than reinstating a grant here.
