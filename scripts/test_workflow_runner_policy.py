from pathlib import Path
import unittest


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


class WorkflowRunnerPolicyTests(unittest.TestCase):
    def test_push_trigger_is_main_only(self) -> None:
        push = event_block(WORKFLOW.read_text(encoding="utf-8"), "push")
        self.assertRegex(push, r"(?m)^    branches:\s*\[\s*main\s*\]$")
        self.assertNotIn("**", push)

    def test_persistent_runner_is_gated_to_main(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        runner = next(
            line.strip()
            for line in source.splitlines()
            if line.lstrip().startswith("runs-on:") and "self-hosted" in line
        )
        self.assertIn("github.event_name == 'push' && github.ref == 'refs/heads/main'", runner)
        self.assertIn("(github.event_name == 'workflow_dispatch' && github.ref == 'refs/heads/main')", runner)
        self.assertNotIn("startsWith(github.ref, 'refs/heads/')", runner)
        self.assertIn("fromJSON('[\"self-hosted\",\"Linux\",\"X64\",\"host\",\"kspls0\"]')", runner)
        self.assertIn("|| matrix.os", runner)


if __name__ == "__main__":
    unittest.main()
