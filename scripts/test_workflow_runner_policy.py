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
        self.assertIn("**", push)
        self.assertNotRegex(push, r"(?m)^    branches:")
        self.assertNotRegex(source, r"(?m)^  pull_request(?:_target)?:")

    def test_build_matrix_uses_shared_pool_only_for_trusted_main_linux(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        build = job_block(source, "build")
        runner_match = re.search(r"(?m)^    runs-on: (?P<runner>[^\n]+)$", build)
        self.assertIsNotNone(runner_match)
        runner = runner_match.group("runner")
        self.assertIn("matrix.os == 'ubuntu-latest'", runner)
        self.assertIn('"group":"ng-shared-main-jit"', runner)
        self.assertIn('"ng-shared-main-chaptarrng"', runner)
        self.assertIn('"ng-main-run-{0}"', runner)
        self.assertIn("github.run_id", runner)
        self.assertIn("github.event_name == 'push'", runner)
        self.assertIn("github.event_name == 'workflow_dispatch'", runner)
        self.assertIn("github.ref == 'refs/heads/main'", runner)
        self.assertIn("|| matrix.os", runner)
        self.assertNotIn("TRUSTED_MAIN_RUNNERS_ENABLED", source)

    def test_buildkit_proxy_is_limited_to_trusted_main_and_reaches_build_steps(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        docker_job = job_block(source, "docker-compatibility-build")
        expected_proxy_gate = (
            "BUILDKIT_EGRESS_PROXY: ${{ (vars.NG_HOSTED_FIRST != 'true' || vars.NG_FORCE_JIT == 'true' || github.run_attempt > 1) && (github.event_name == 'push' || "
            "github.event_name == 'workflow_dispatch') && github.ref == "
            "'refs/heads/main' && 'http://10.0.2.100:3128' || '' }}"
        )
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

    def test_validation_is_manual_or_tagged_without_pr_checks(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        push = event_block(source, "push")

        self.assertIn("workflow_dispatch:", source)
        self.assertRegex(push, r"(?m)^    tags:\s*$")
        self.assertNotRegex(push, r"(?m)^    branches:")
        self.assertNotRegex(source, r"(?m)^  pull_request(?:_target)?:")

    def test_security_scans_use_the_shared_pool_only_on_trusted_main_runs(self) -> None:
        workflow_dir = WORKFLOW.parent
        for workflow_name, job_name in (
            ("codeql.yml", "analyze"),
            ("semgrep-community.yml", "semgrep"),
        ):
            source = (workflow_dir / workflow_name).read_text(encoding="utf-8")
            job = job_block(source, job_name)
            runner_match = re.search(r"(?m)^    runs-on: (?P<runner>[^\n]+)$", job)
            self.assertIsNotNone(runner_match, workflow_name)
            runner = runner_match.group("runner")
            self.assertIn('"group":"ng-shared-main-jit"', runner)
            self.assertIn("github.event_name == 'push'", runner)
            self.assertIn("github.event_name == 'workflow_dispatch'", runner)
            self.assertIn("github.event_name == 'schedule'", runner)
            self.assertIn("github.ref == 'refs/heads/main'", runner)
            self.assertIn("|| 'ubuntu-latest'", runner)
        semgrep = (workflow_dir / "semgrep-community.yml").read_text(encoding="utf-8")
        self.assertIn("docker info", semgrep)
        self.assertIn("docker run", semgrep)

    def test_playwright_install_uses_hosted_linux_dependencies(self) -> None:
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
        self.assertIn("postgresql-client-16", migrations)
        self.assertIn("/usr/lib/postgresql/16/bin", migrations)
        self.assertIn("GITHUB_PATH", migrations)

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

