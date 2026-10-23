import os
import re
import subprocess
import tempfile
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
    def test_push_trigger_is_main_only(self) -> None:
        push = event_block(WORKFLOW.read_text(encoding="utf-8"), "push")
        self.assertRegex(push, r"(?m)^    branches:\s*\[\s*main\s*\]$")
        self.assertNotIn("**", push)

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
            "BUILDKIT_EGRESS_PROXY: ${{ (github.event_name == 'push' || "
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
        self.assertIn("env.NO_PROXY=localhost,127.0.0.1", buildx)
        self.assertIn("NO_PROXY=localhost,127.0.0.1", build)

    def test_pr_body_edits_revalidate_notes_without_restarting_builds(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        pull_request = event_block(source, "pull_request")
        release_notes = job_block(source, "release-notes")
        build = job_block(source, "build")
        migrations = job_block(source, "postgres-migrations")

        self.assertRegex(pull_request, r"(?m)^    types:.*\bedited\b")
        self.assertIn("github.event.changes.body != null", release_notes)
        self.assertIn("github.event.action != 'edited'", build)
        self.assertIn("github.event.action != 'edited'", migrations)
        self.assertIn("group: build-${{ github.run_id }}-${{ github.run_attempt }}", source)
        self.assertIn("cancel-in-progress: false", source)

    def test_release_note_check_fetches_the_pr_head_after_merge(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        release_notes = job_block(source, "release-notes")

        self.assertIn("PR_NUMBER: ${{ github.event.pull_request.number }}", release_notes)
        fetch = (
            'git fetch --no-tags origin "refs/pull/${PR_NUMBER}/head:'
            'refs/remotes/pull/${PR_NUMBER}/head"'
        )
        self.assertIn(fetch, release_notes)
        self.assertIn('git cat-file -e "${HEAD_SHA}^{commit}"', release_notes)
        self.assertLess(release_notes.index(fetch), release_notes.index("node scripts/check-release-notes.mjs"))

    def test_retained_pull_ref_fetches_head_without_source_branch(self) -> None:
        with tempfile.TemporaryDirectory(prefix="chaptarr-pr-ref-") as scratch:
            root = Path(scratch)
            remote = root / "remote.git"
            seed = root / "seed"
            checkout = root / "checkout"
            subprocess.run(["git", "init", "--bare", str(remote)], check=True, capture_output=True)
            subprocess.run(["git", "init", "-b", "main", str(seed)], check=True, capture_output=True)
            environment = os.environ | {
                "GIT_AUTHOR_NAME": "Validation Test",
                "GIT_AUTHOR_EMAIL": "validation@example.invalid",
                "GIT_COMMITTER_NAME": "Validation Test",
                "GIT_COMMITTER_EMAIL": "validation@example.invalid",
            }

            def git(*args: str) -> str:
                result = subprocess.run(
                    ["git", "-C", str(seed), *args],
                    check=True,
                    capture_output=True,
                    text=True,
                    env=environment,
                )
                return result.stdout.strip()

            (seed / "base.txt").write_text("base\n", encoding="utf-8")
            git("add", "base.txt")
            git("commit", "-m", "base")
            git("remote", "add", "origin", str(remote))
            git("push", "origin", "main")
            (seed / "head.txt").write_text("pull request head\n", encoding="utf-8")
            git("add", "head.txt")
            git("commit", "-m", "pull request head")
            head = git("rev-parse", "HEAD")
            git("push", "origin", "HEAD:refs/heads/pr-branch")
            subprocess.run(
                ["git", "--git-dir", str(remote), "update-ref", "refs/pull/75/head", head],
                check=True,
                capture_output=True,
            )
            subprocess.run(
                ["git", "--git-dir", str(remote), "update-ref", "-d", "refs/heads/pr-branch"],
                check=True,
                capture_output=True,
            )
            subprocess.run(
                ["git", "clone", "--branch", "main", "--single-branch", str(remote), str(checkout)],
                check=True,
                capture_output=True,
            )

            subprocess.run(
                [
                    "git",
                    "-C",
                    str(checkout),
                    "fetch",
                    "--no-tags",
                    "origin",
                    "refs/pull/75/head:refs/remotes/pull/75/head",
                ],
                check=True,
                capture_output=True,
            )
            subprocess.run(
                ["git", "-C", str(checkout), "cat-file", "-e", f"{head}^{{commit}}"],
                check=True,
                capture_output=True,
            )

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
