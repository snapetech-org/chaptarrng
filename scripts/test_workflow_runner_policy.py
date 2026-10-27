import re
import unittest
from pathlib import Path


WORKFLOW = Path(__file__).resolve().parents[1] / ".github" / "workflows" / "build.yml"


def event_block(source: str, event: str) -> str:
    lines = source.splitlines()
    start = lines.index(f"  {event}:")
    body = []
    for line in lines[start + 1 :]:
        if line.strip() and len(line) - len(line.lstrip()) <= 2:
            break
        body.append(line)
    return "\n".join(body)


def job_block(source: str, job: str) -> str:
    lines = source.splitlines()
    start = lines.index(f"  {job}:")
    body = []
    for line in lines[start + 1 :]:
        if line.strip() and len(line) - len(line.lstrip()) <= 2:
            break
        body.append(line)
    return "\n".join(body)


def step_block(source: str, name: str) -> str:
    lines = source.splitlines()
    marker = f"    - name: {name}"
    start = lines.index(marker)
    body = []
    for line in lines[start + 1 :]:
        if line.startswith("    - name:"):
            break
        body.append(line)
    return "\n".join(body)


class WorkflowRunnerPolicyTests(unittest.TestCase):
    def test_push_trigger_is_tag_only(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        push = event_block(source, "push")
        self.assertRegex(push, r"(?m)^    tags:\s*$")
        self.assertIn("'v*'", push)
        self.assertNotRegex(push, r"(?m)^    branches:")
        target = event_block(source, "pull_request_target")
        self.assertIn("branches: [main]", target)
        self.assertIn("types: [opened, reopened, synchronize, ready_for_review]", target)
        self.assertNotRegex(source, r"(?m)^  pull_request:")

    def test_build_matrix_requests_private_capacity_for_each_platform(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        build = job_block(source, "build")
        runner_match = re.search(r"(?m)^    runs-on: (?P<runner>[^\n]+)$", build)
        self.assertIsNotNone(runner_match)
        runner = runner_match.group("runner").replace("\\", "")
        self.assertIn("matrix.platform == 'windows-x64'", runner)
        self.assertIn("matrix.platform == 'macos-intel'", runner)
        self.assertIn('"group":"ng-shared-main-jit"', runner)
        self.assertIn('"ng-shared-main-chaptarrng"', runner)
        self.assertIn('"ng-main-run-{0}"', runner)
        self.assertIn("github.run_id", runner)
        self.assertIn('"group":"packer-windows"', runner)
        self.assertIn('"on-demand"', runner)
        self.assertIn('"ChaptarrNG"', runner)
        self.assertIn('"gha-{0}-{1}-build"', runner)
        self.assertNotRegex(runner, r"ubuntu-latest|windows-latest|macos-[0-9]")
        self.assertNotIn("|| matrix.os", runner)
        self.assertNotIn("TRUSTED_MAIN_RUNNERS_ENABLED", source)

    def test_all_workflow_jobs_use_private_runner_groups_or_labels(self) -> None:
        workflow_dir = WORKFLOW.parent
        for workflow_path in sorted(workflow_dir.glob("*.yml")):
            source = workflow_path.read_text(encoding="utf-8")
            lines = source.splitlines()
            for index, line in enumerate(lines):
                if not re.match(r"^\s*runs-on:", line):
                    continue
                self.assertNotRegex(
                    line,
                    r"runs-on:.*\b(?:ubuntu-latest|ubuntu-[0-9]|windows-latest|macos-[0-9])",
                    str(workflow_path),
                )
                self.assertNotRegex(
                    line,
                    r"\|\|\s*['\"]?(?:ubuntu-|windows-latest|macos-[0-9])|\|\|\s*matrix\.os",
                    str(workflow_path),
                )
                if line.rstrip().endswith("runs-on:"):
                    block = "\n".join(lines[index : index + 3])
                    self.assertIn("group:", block, str(workflow_path))
                else:
                    self.assertRegex(
                        line,
                        r"self-hosted|group|fromJSON",
                        f"{workflow_path}: {line}",
                    )

    def test_windows_release_jobs_use_the_private_packer_route(self) -> None:
        windows_release_jobs = (
            ("release.yml", "windows-migration-installer", "windows-migration-installer"),
            ("release-distribution.yml", "build-chocolatey", "build-chocolatey"),
            ("release-distribution.yml", "publish-chocolatey", "publish-chocolatey"),
        )
        for workflow_name, job_name, label_suffix in windows_release_jobs:
            source = (WORKFLOW.parent / workflow_name).read_text(encoding="utf-8")
            job = job_block(source, job_name)
            runner_match = re.search(r"(?m)^    runs-on: (?P<runner>[^\n]+)$", job)
            self.assertIsNotNone(runner_match, f"{workflow_name}:{job_name}")
            runner = runner_match.group("runner").replace("\\", "")
            self.assertIn('"group":"packer-windows"', runner)
            self.assertIn('"on-demand"', runner)
            self.assertIn('"ChaptarrNG"', runner)
            self.assertIn(f'"gha-{{0}}-{label_suffix}"', runner)
            self.assertNotRegex(runner, r"windows-latest|ubuntu-latest|macos-[0-9]")

        for workflow_name in ("build.yml", "release.yml", "release-distribution.yml"):
            source = (WORKFLOW.parent / workflow_name).read_text(encoding="utf-8")
            self.assertNotRegex(source, r"(?m)^\s+runs-on: windows-latest\s*$")

    def test_buildkit_proxy_is_limited_to_trusted_main_and_reaches_build_steps(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        docker_job = job_block(source, "docker-compatibility-build")
        expected_proxy_gate = "BUILDKIT_EGRESS_PROXY: ${{ github.ref == 'refs/heads/main' && 'http://10.0.2.100:3128' || '' }}"
        self.assertIn(expected_proxy_gate, docker_job)

        buildx = step_block(source, "Set up Docker Buildx")
        build = step_block(
            source, "Build image for all published architectures without pushing"
        )
        for proxy_name in ("HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy"):
            self.assertIn(
                f"env.{proxy_name}=${{{{ env.BUILDKIT_EGRESS_PROXY }}}}", buildx
            )
            self.assertIn(
                f"{proxy_name}=${{{{ env.BUILDKIT_EGRESS_PROXY }}}}", build
            )
        buildx_driver_options = [
            line.strip()
            for line in buildx.splitlines()
            if line.strip().startswith("env.")
        ]
        self.assertTrue(buildx_driver_options)
        self.assertTrue(all("," not in option for option in buildx_driver_options))
        self.assertIn("NO_PROXY=localhost,127.0.0.1", build)

    def test_owner_pr_trigger_dispatches_only_metadata_to_private_validation(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        push = event_block(source, "push")

        self.assertIn("workflow_dispatch:", source)
        self.assertRegex(push, r"(?m)^    tags:\s*$")
        self.assertIn("'v*'", push)
        self.assertNotRegex(push, r"(?m)^    branches:")
        self.assertNotRegex(source, r"(?m)^  pull_request:")

        dispatcher = job_block(source, "dispatch-owner-pr")
        for token in (
            "github.event_name == 'pull_request_target'",
            "github.actor == 'snapetech'",
            "github.event.pull_request.user.login == 'snapetech'",
            "github.event.pull_request.head.repo.full_name == github.repository",
            "github.event.pull_request.head.repo.id == github.repository_id",
            "github.event.pull_request.base.ref == 'main'",
            "actions: write",
            "contents: none",
            '"ref": "main"',
        ):
            self.assertIn(token, dispatcher)
        self.assertNotIn("actions/checkout", dispatcher)
        self.assertNotIn("secrets.", dispatcher)

        policy = job_block(source, "private-runner-policy")
        self.assertIn("github.ref == 'refs/heads/main'", policy)
        self.assertIn("gha-{0}-private-runner-policy", policy)

        validation = job_block(source, "owner-pr-validation")
        self.assertIn("github.actor == 'github-actions[bot]'", validation)
        self.assertIn("ref: ${{ inputs.pr_sha }}", validation)
        self.assertIn("persist-credentials: false", validation)
        self.assertIn("node scripts/check-release-notes.mjs", validation)

        reporter = job_block(source, "report-owner-pr-check")
        self.assertIn("checks: write", reporter)
        self.assertIn("name: Report trusted owner PR validation", reporter)
        self.assertIn('"name": "ChaptarrNG owner PR validation"', reporter)

        release = (WORKFLOW.parent / "release.yml").read_text(encoding="utf-8")
        release_validation = job_block(release, "validate")
        for permission in (
            "actions: write",
            "checks: write",
            "contents: read",
            "pull-requests: read",
        ):
            self.assertIn(permission, release_validation)

    def test_fedora_rpm_acceptance_is_an_exact_head_private_jit_dispatch(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("rpm_bridge_only:", source)
        self.assertIn("inputs.rpm_bridge_only != true", job_block(source, "build"))

        rpm_job = job_block(source, "rpm-bridge-acceptance")
        for token in (
            "github.event_name == 'workflow_dispatch'",
            "inputs.rpm_bridge_only == true",
            "github.ref == 'refs/heads/main'",
            '"group":"ng-shared-main-jit"',
            '"gha-{0}-rpm-bridge-acceptance"',
            "ref: ${{ github.sha }}",
            'test "$(git rev-parse HEAD)" = "$GITHUB_SHA"',
            "TASK_SCRATCH_DIR",
            "packaging/scripts/test-build-bookshelfng-rpm-bridge.sh",
        ):
            self.assertIn(token, rpm_job)

        self.assertIn("inputs.rpm_bridge_only != true", job_block(source, "postgres-migrations"))
        self.assertIn("inputs.rpm_bridge_only != true", job_block(source, "docker-compatibility-build"))

    def test_legacy_channel_contracts_are_an_exact_head_private_jit_dispatch(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("channel_contracts_only:", source)
        self.assertIn("inputs.channel_contracts_only != true", job_block(source, "build"))
        job = job_block(source, "channel-contracts")
        for token in (
            "github.event_name == 'workflow_dispatch'",
            "inputs.channel_contracts_only == true",
            "github.ref == 'refs/heads/main'",
            '"group":"ng-shared-main-jit"',
            '"gha-{0}-channel-contracts"',
            "ref: ${{ github.sha }}",
            'test "$(git rev-parse HEAD)" = "$GITHUB_SHA"',
            "node --test packaging/scripts/test-bookshelf-legacy-channels.mjs",
            "python3 scripts/test_workflow_runner_policy.py",
        ):
            self.assertIn(token, job)
        self.assertIn("inputs.channel_contracts_only != true", job_block(source, "postgres-migrations"))
        self.assertIn("inputs.channel_contracts_only != true", job_block(source, "docker-compatibility-build"))

    def test_security_scans_use_private_jit_without_hosted_fallback(self) -> None:
        workflow_dir = WORKFLOW.parent
        self.assertFalse((workflow_dir / "codeql.yml").exists())
        self.assertTrue((workflow_dir / "codeql.yml.disabled").is_file())
        workflow_name = "semgrep-community.yml"
        source = (workflow_dir / workflow_name).read_text(encoding="utf-8")
        job = job_block(source, "semgrep")
        runner_match = re.search(r"(?m)^    runs-on: (?P<runner>[^\n]+)$", job)
        self.assertIsNotNone(runner_match, workflow_name)
        runner = runner_match.group("runner").replace("\\", "")
        self.assertIn('"group":"ng-shared-main-jit"', runner)
        self.assertIn('"self-hosted"', runner)
        self.assertIn('"ng-main-run-{0}"', runner)
        self.assertNotIn("ubuntu-latest", runner)
        semgrep = (workflow_dir / "semgrep-community.yml").read_text(encoding="utf-8")
        self.assertIn("docker info", semgrep)
        self.assertIn("docker run", semgrep)

    def test_playwright_install_uses_linux_dependencies(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("Install Chromium and system dependencies on Linux", source)
        self.assertIn("install --with-deps chromium", source)
        self.assertNotIn("runner.environment", source)
        linux_install = step_block(source, "Install Chromium and system dependencies on Linux")
        self.assertNotIn("pwsh", linux_install)
        self.assertIn('"$driver/node/linux-x64/node"', linux_install)
        self.assertIn('"$driver/package/cli.js"', linux_install)

    def test_postgres_restore_uses_client_matching_server_major(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        migrations = job_block(source, "postgres-migrations")
        self.assertIn("image: postgres:16", migrations)
        self.assertIn(
            "PostgreSQL 16 client binaries must be installed by the private JIT broker",
            migrations,
        )
        self.assertIn('postgres:16 "$tool" "$@"', migrations)
        self.assertIn("/usr/lib/postgresql/16/bin", migrations)
        self.assertIn("GITHUB_PATH", migrations)
        self.assertNotIn("apt-get", migrations)

    def test_frontend_build_runs_after_tests_and_before_publish(self) -> None:
        build = job_block(WORKFLOW.read_text(encoding="utf-8"), "build")

        tests = build.index("- name: Test\n")
        frontend_build = build.index("- name: Build frontend")
        publish = build.index("- name: Publish")

        self.assertIn(
            "dotnet test src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj",
            build[tests:frontend_build],
        )
        self.assertLess(frontend_build, publish)


if __name__ == "__main__":
    unittest.main()

