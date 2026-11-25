// Conventional Commits, plus `qol` (quality of life) for developer-experience changes:
// tooling, scripts, editor config, DX tweaks that are neither a feature nor a fix.
// Keep the type-enum array inline and comment-free so Show-CommitTypes can parse it.
export default {
	helpUrl: 'https://www.conventionalcommits.org/en/v1.0.0/',
	rules: {
		'body-leading-blank': [1, 'always'],
		// Warn rather than fail: URLs, stack traces and pasted output legitimately run long.
		'body-max-line-length': [1, 'always', 100],
		'footer-leading-blank': [1, 'always'],
		// Warn rather than fail: Co-authored-by and issue-link trailers are often long.
		'footer-max-line-length': [1, 'always', 100],
		'header-max-length': [2, 'always', 100],
		'header-trim': [2, 'always'],
		'subject-case': [2, 'never', ['sentence-case', 'start-case', 'pascal-case', 'upper-case']],
		'subject-empty': [2, 'never'],
		'subject-full-stop': [2, 'never', '.'],
		'type-case': [2, 'always', 'lower-case'],
		'type-empty': [2, 'never'],
		'type-enum': [
			2,
			'always',
			['build', 'chore', 'ci', 'docs', 'feat', 'fix', 'perf', 'qol', 'refactor', 'revert', 'style', 'test'],
		],
	},
};
