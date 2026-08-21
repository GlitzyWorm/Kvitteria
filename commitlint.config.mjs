// Release notes and version bumps are derived from commit messages, so a message that does not parse
// is a change that silently never ships. History before the packaging work used a `feature:` type
// that Conventional Commits does not define, which is exactly what this prevents recurring.
export default {
	extends: ['@commitlint/config-conventional'],
	rules: {
		// The types release-please is configured to understand. Anything else is a typo.
		'type-enum': [
			2,
			'always',
			['feat', 'fix', 'perf', 'refactor', 'build', 'ci', 'docs', 'test', 'style', 'chore', 'revert']
		],

		// Long subjects get truncated in changelogs and release titles.
		'header-max-length': [2, 'always', 100],

		// Off: scopes are useful here (compose, backend, auth) but inventing a closed list would mean
		// editing this file to describe a new area of the codebase.
		'scope-enum': [0]
	}
};
