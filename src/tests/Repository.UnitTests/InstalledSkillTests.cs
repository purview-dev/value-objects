using System.Diagnostics;
using System.Text.Json;

namespace Purview.ValueObjects.Repository;

/// <summary>
/// Guards how the skills installed from other repositories sit in the working tree.
/// </summary>
/// <remarks>
/// <para>
/// The install belongs to the skills CLI, not to git. <c>.agents/skills/</c> is gitignored, because committing it
/// would publish someone else's skills as this repository's own - a skill this repository installs but does not
/// publish would become discoverable through <c>skills add purview-dev/value-objects</c> - and would put a second
/// writer on a path the CLI owns. Only <c>skills-lock.json</c> (source, skill path and content hash of every entry)
/// is committed, so a fresh clone restores the recorded set with <c>just skills-install</c>.
/// </para>
/// <para>
/// The install is therefore checked only when one is present: a fresh clone has none, and its absence is the
/// state these tests allow. <c>SkillTests</c> validates this repository's own skills in <c>skills/</c>; these are
/// someone else's, so their content is not checked here, only their placement.
/// </para>
/// </remarks>
public sealed class InstalledSkillTests
{
	static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

	[Test]
	public async Task GitIgnore_GivenTheRepository_KeepsTheInstallAndPerAgentFoldersOut(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var rules = await File.ReadAllLinesAsync(Path.Combine(RepositoryRoot.Value, ".gitignore"), cancellationToken);

		// Act
		var required = new[] { ".agents/skills/", "/.claude/" };
		var missing = required.Where(rule => !rules.Contains(rule, StringComparer.Ordinal)).ToArray();

		// Assert
		await Assert
			.That(missing)
			.IsEmpty()
			.Because(
				"the skills CLI owns .agents/skills/ and the per-agent folders, so both stay out of git: "
					+ string.Join(", ", missing)
			);
	}

	[Test]
	public async Task Lockfile_GivenEveryEntry_RecordsWhatAFreshCloneRestores(CancellationToken cancellationToken)
	{
		// Arrange
		var recorded = await ReadLockfileAsync(cancellationToken);

		// Act
		var incomplete = recorded
			.Where(static skill =>
				skill.Source.Length == 0 || skill.SkillPath.Length == 0 || skill.ComputedHash.Length == 0
			)
			.Select(static skill => skill.Name)
			.ToArray();

		// Assert
		await Assert
			.That(recorded)
			.IsNotEmpty()
			.Because("skills-lock.json is the only tracked half of the install, so it must record at least one skill");
		await Assert
			.That(incomplete)
			.IsEmpty()
			.Because(
				$"an entry without a source, skill path and hash cannot be restored: {string.Join(", ", incomplete)}"
			);
	}

	[Test]
	public async Task AgentsSkills_GivenAnInstall_MatchesTheLockfile(CancellationToken cancellationToken)
	{
		// Arrange
		var recorded = await ReadLockfileAsync(cancellationToken);

		if (!Directory.Exists(AgentsSkillsFolder()))
		{
			// A fresh clone has no install. That is the arrangement, not a gap: the lockfile is tracked and
			// `just skills-install` writes the rest.
			await Assert
				.That(File.Exists(LockfilePath()))
				.IsTrue()
				.Because("without skills-lock.json a clone cannot restore the skills the repository depends on");
			return;
		}

		var installed = InstalledSkillNames();

		// Act
		var unrecorded = installed.Where(name => !recorded.Any(skill => skill.Name == name)).ToArray();
		var missing = recorded.Select(static skill => skill.Name).Where(name => !installed.Contains(name)).ToArray();

		// Assert
		await Assert
			.That(unrecorded)
			.IsEmpty()
			.Because(
				"every folder in .agents/skills/ must be written by the skills CLI, which records it in "
					+ $"skills-lock.json: {string.Join(", ", unrecorded)}"
			);
		await Assert
			.That(missing)
			.IsEmpty()
			.Because(
				"skills-lock.json records skills the install does not hold. Restore them with `just skills-install`: "
					+ string.Join(", ", missing)
			);
	}

	[Test]
	public async Task AgentsSkills_GivenTheAuthoredSkills_HoldsNoCopyOfThem(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		var authored = Directory
			.GetDirectories(Path.Combine(RepositoryRoot.Value, "skills"))
			.Select(static path => Path.GetFileName(path))
			.ToArray();

		// Act
		var copied = authored
			.Where(static name => Directory.Exists(Path.Combine(AgentsSkillsFolder(), name)))
			.ToArray();

		// Assert
		await Assert
			.That(copied)
			.IsEmpty()
			.Because(
				"this repository's own skills live in skills/ only; a copy in .agents/skills/ would drift from "
					+ $"the authored one: {string.Join(", ", copied)}"
			);
	}

	[Test]
	public async Task ClaudeFolder_GivenTheInstalledSkills_HoldsNoCopy(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		var claudeSkills = Path.Combine(RepositoryRoot.Value, ".claude", "skills");

		// Act
		var exists = Directory.Exists(claudeSkills);

		// Assert
		await Assert
			.That(exists)
			.IsFalse()
			.Because(
				"skills are installed into .agents/skills/ only. A .claude/skills/ folder means the CLI ran without "
					+ "`-a universal`; delete it and reinstall with `-a universal`"
			);
	}

	[Test]
	public async Task Repository_GivenAgentInstructions_HasNoClaudeMd(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		string[] candidates =
		[
			Path.Combine(RepositoryRoot.Value, "CLAUDE.md"),
			Path.Combine(RepositoryRoot.Value, ".github", "CLAUDE.md"),
		];

		// Act
		var present = candidates.Where(File.Exists).ToArray();

		// Assert
		await Assert
			.That(present)
			.IsEmpty()
			.Because(
				"this repository is not agent-specific: AGENTS.md is its one instruction file, so a CLAUDE.md "
					+ $"must not be added: {string.Join(", ", present)}"
			);
	}

	[Test]
	public async Task AgentsSkills_GivenEverythingTheCLIWrites_IsIgnoredAndNeverTracked(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the folder is a local install, so nothing under it may be tracked, and the rules that
		// keep it (and every per-agent folder) out of this repository must be in place. This is what stops
		// a skill the repository installs but does not publish being found by `skills add
		// purview-dev/value-objects`.
		var tracked = await TrackedFilesUnderAgentsSkillsAsync(cancellationToken);
		var ignores = await AgentsSkillsIgnoreRulesAsync(cancellationToken);

		// Assert
		await Assert
			.That(tracked)
			.IsEmpty()
			.Because(
				"nothing under .agents/skills/ may be tracked: it is a local install, reproducible from "
					+ $"skills-lock.json with `just skills-install`: {string.Join(", ", tracked)}"
			);
		await Assert
			.That(ignores.Agents)
			.IsTrue()
			.Because(".gitignore must keep .agents/skills/ out of this repository");
		await Assert.That(ignores.Claude).IsTrue().Because(".gitignore must keep .claude/ out of this repository");
	}

	/// <summary>What git tracks under <c>.agents/skills/</c>, asked of the repository itself.</summary>
	static async Task<string[]> TrackedFilesUnderAgentsSkillsAsync(CancellationToken cancellationToken)
	{
		using Process process = new()
		{
			StartInfo = new ProcessStartInfo("git", "ls-files .agents/skills")
			{
				WorkingDirectory = RepositoryRoot.Value,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			},
		};
		process.Start();
		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		return
		[
			.. output
				.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Order(StringComparer.Ordinal),
		];
	}

	/// <summary>The ignore rules for the two agent folders, as <c>.gitignore</c> spells them.</summary>
	static async Task<(bool Agents, bool Claude)> AgentsSkillsIgnoreRulesAsync(CancellationToken cancellationToken)
	{
		var lines = await File.ReadAllLinesAsync(Path.Combine(RepositoryRoot.Value, ".gitignore"), cancellationToken);
		var rules = lines.Select(static line => line.Trim()).ToHashSet(StringComparer.Ordinal);
		return (
			rules.Overlaps([".agents/skills/", ".agents/skills", "/.agents/skills/", ".agents/"]),
			rules.Overlaps([".claude/", ".claude", "/.claude/", "/.claude"])
		);
	}

	static string AgentsSkillsFolder() => Path.Combine(RepositoryRoot.Value, ".agents", "skills");

	static string LockfilePath() => Path.Combine(RepositoryRoot.Value, "skills-lock.json");

	static string[] InstalledSkillNames() =>
		!Directory.Exists(AgentsSkillsFolder())
			? []
			: [.. Directory.GetDirectories(AgentsSkillsFolder()).Select(static path => Path.GetFileName(path))];

	static async Task<LockedSkill[]> ReadLockfileAsync(CancellationToken cancellationToken)
	{
		await using var stream = File.OpenRead(LockfilePath());
		using var lockfile = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		return
		[
			.. lockfile
				.RootElement.GetProperty("skills")
				.EnumerateObject()
				.Select(static property => new LockedSkill(
					property.Name,
					property.Value.GetProperty("source").GetString() ?? string.Empty,
					property.Value.GetProperty("skillPath").GetString() ?? string.Empty,
					property.Value.GetProperty("computedHash").GetString() ?? string.Empty
				))
				.OrderBy(static skill => skill.Name, StringComparer.Ordinal),
		];
	}

	static string FindRepositoryRoot()
	{
		for (
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			directory is not null;
			directory = directory.Parent
		)
		{
			if (
				File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
				&& File.Exists(Path.Combine(directory.FullName, "skills-lock.json"))
				&& Directory.Exists(Path.Combine(directory.FullName, "src", "src"))
			)
				return directory.FullName;
		}

		throw new InvalidOperationException(
			$"No repository root above '{AppContext.BaseDirectory}' carries AGENTS.md, skills-lock.json and src/src."
		);
	}

	sealed record LockedSkill(string Name, string Source, string SkillPath, string ComputedHash);
}
