#!/usr/bin/env python3
"""Mock tests for the TypeSafe Pareto helper. No network, no API keys."""
from __future__ import annotations

import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import score_pareto as sp

FIXTURE = Path(__file__).resolve().parent / "fixtures" / "zdt1_candidates.json"


class ExportTests(unittest.TestCase):
    def test_fixture_loads_eight_zdt1_like_candidates(self) -> None:
        payload = sp.load_export(FIXTURE)
        self.assertEqual(payload["problem"], "zdt1")
        self.assertEqual(len(payload["candidates"]), 8)
        ids = [c["id"] for c in payload["candidates"]]
        self.assertEqual(ids, [f"c{i}" for i in range(8)])
        self.assertFalse(payload["candidates"][-1]["feasible"])

    def test_raw_list_and_nondominated_alias(self) -> None:
        listed = sp.normalize_export([{"objectives": [0.2, 0.6]}])
        self.assertEqual(listed["candidates"][0]["id"], "c0")
        aliased = sp.normalize_export(
            {"NonDominatedSolutions": [{"id": "front0", "objectives": [0.1, 0.7]}]}
        )
        self.assertEqual(aliased["candidates"][0]["id"], "front0")

    def test_empty_export_rejected(self) -> None:
        with self.assertRaises(ValueError):
            sp.normalize_export({"candidates": []})

    def test_sample_caps_at_ten(self) -> None:
        fat = {"candidates": [{"id": f"x{i}", "objectives": [i / 20, 1 - i / 20]} for i in range(15)]}
        sampled = sp.sample_candidates(fat, limit=sp.SMOKE_LIMIT)
        self.assertEqual(len(sampled), 10)
        self.assertEqual(sampled[0]["id"], "x0")
        self.assertEqual(sampled[-1]["id"], "x9")


class QuestionTests(unittest.TestCase):
    def test_fanout_is_one_map_four_questions_per_candidate(self) -> None:
        payload = sp.load_export(FIXTURE)
        candidates = sp.sample_candidates(payload)
        questions = sp.build_questions(candidates)
        self.assertEqual(len(questions), 4 * len(candidates))
        self.assertLessEqual(len(candidates), 10)
        expected = sp.expected_question_ids(candidates)
        self.assertEqual(sorted(questions), sorted(expected))
        first = questions["c0_constraint_satisfaction"]
        self.assertEqual(first["type"], "score")
        self.assertGreaterEqual(len(first["criteria"]), 2)
        self.assertIn("candidates[0]", first["instructions"])
        choice = questions["c0_disposition"]
        self.assertEqual(choice["type"], "choice")
        self.assertEqual(set(choice["criteria"]), {"keep", "drop", "review"})

    def test_state_marks_layer_as_additive(self) -> None:
        payload = sp.load_export(FIXTURE)
        state = sp.build_state(payload, payload["candidates"][:2])
        self.assertIn("does not replace", state["layer"].lower())
        self.assertEqual(len(state["candidates"]), 2)
        self.assertEqual(state["repo"], "AppSprout-dev/Unsga3")


class MockClientTests(unittest.TestCase):
    def test_mock_response_matches_documented_shapes(self) -> None:
        payload = sp.load_export(FIXTURE)
        candidates = payload["candidates"]
        questions = sp.build_questions(candidates)
        state = sp.build_state(payload, candidates)
        response = sp.mock_system_one(state, questions, model="jev-latest")
        self.assertEqual(response["model"], "jev-latest")
        self.assertEqual(set(response["answers"]), set(questions))
        score = response["answers"]["c0_constraint_satisfaction"]
        self.assertEqual(score["type"], "score")
        self.assertIn("score", score)
        self.assertIn("legend", score)
        self.assertIn("probabilities", score)
        self.assertIn("confidence", score)
        self.assertAlmostEqual(sum(score["probabilities"].values()), 1.0, places=6)
        choice = response["answers"]["c7_disposition"]
        self.assertEqual(choice["type"], "choice")
        self.assertEqual(choice["choice"], "drop")
        self.assertAlmostEqual(sum(choice["probabilities"].values()), 1.0, places=6)

    def test_mock_drops_infeasible_and_off_front(self) -> None:
        payload = sp.load_export(FIXTURE)
        result = sp.run_pass(payload, force_mock=True, metrics_path=None)
        by_id = {row["id"]: row for row in result["ranking"]}
        self.assertEqual(by_id["c7"]["disposition"], "drop")
        self.assertEqual(by_id["c6"]["disposition"], "drop")
        self.assertEqual(by_id["c0"]["disposition"], "keep")
        self.assertGreater(by_id["c0"]["composite"], by_id["c7"]["composite"])

    def test_call_system_one_uses_mock_without_api_key(self) -> None:
        payload = sp.load_export(FIXTURE)
        candidates = payload["candidates"][:2]
        state = sp.build_state(payload, candidates)
        questions = sp.build_questions(candidates)
        with mock.patch.dict(os.environ, {}, clear=False):
            os.environ.pop(sp.API_KEY_ENV, None)
            response, mode, latency_ms = sp.call_system_one(
                state, questions, model="jev-latest", api_key=None, base_url=sp.DEFAULT_BASE_URL
            )
        self.assertEqual(mode, "mock")
        self.assertGreaterEqual(latency_ms, 0.0)
        self.assertEqual(len(response["answers"]), 8)


class MetricsTests(unittest.TestCase):
    def test_jsonl_append_schema(self) -> None:
        payload = sp.load_export(FIXTURE)
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "typesafe-runs.jsonl"
            first = sp.run_pass(payload, force_mock=True, metrics_path=path, notes="unit")
            second = sp.run_pass(payload, force_mock=True, metrics_path=path, notes="unit")
            lines = path.read_text(encoding="utf-8").strip().splitlines()
            self.assertEqual(len(lines), 2)
            rec = json.loads(lines[0])
            for key in (
                "ts",
                "experiment",
                "repo",
                "model",
                "latency_ms",
                "usage",
                "candidate_count",
                "answers",
                "notes",
            ):
                self.assertIn(key, rec)
            self.assertEqual(rec["experiment"], "unsga3_pareto_score")
            self.assertEqual(rec["repo"], "AppSprout-dev/Unsga3")
            self.assertEqual(rec["candidate_count"], 8)
            self.assertEqual(rec["model"], "jev-latest")
            self.assertIn("mode=mock", rec["notes"])
            self.assertTrue(rec["answers"])
            self.assertEqual(first["metrics"]["candidate_count"], 8)
            self.assertEqual(second["metrics_path"], str(path))

    def test_default_metrics_path(self) -> None:
        self.assertEqual(sp.default_metrics_path(), sp.REPO_ROOT / "metrics" / "typesafe-runs.jsonl")


class HttpClientTests(unittest.TestCase):
    def test_injected_http_post_used_when_key_present(self) -> None:
        payload = sp.load_export(FIXTURE)
        captured: dict[str, object] = {}

        def fake_post(state, questions, *, model, api_key, base_url, timeout):
            captured["n_questions"] = len(questions)
            captured["api_key"] = api_key
            captured["url_base"] = base_url
            captured["state_n"] = len(state["candidates"])
            return {
                "model": model,
                "answers": sp.mock_system_one(state, questions, model=model)["answers"],
                "usage": {"input_tokens": 12, "output_tokens": 4},
            }

        result = sp.run_pass(
            payload,
            api_key="test-not-a-real-key",
            force_mock=False,
            metrics_path=None,
            http_post=fake_post,
        )
        self.assertEqual(result["mode"], "http")
        self.assertEqual(captured["n_questions"], 32)
        self.assertEqual(captured["api_key"], "test-not-a-real-key")
        self.assertEqual(captured["url_base"], "https://api.typesafe.ai")
        self.assertEqual(result["metrics"]["usage"]["input_tokens"], 12)

    def test_systemone_url_matches_docs(self) -> None:
        self.assertEqual(
            sp._systemone_url("https://api.typesafe.ai"),
            "https://api.typesafe.ai/v1/systemone",
        )

    def test_force_mock_wins_over_key(self) -> None:
        payload = sp.load_export(FIXTURE)

        def boom(*_a, **_k):
            raise AssertionError("live client must not run under --force-mock")

        result = sp.run_pass(
            payload,
            api_key="test-not-a-real-key",
            force_mock=True,
            metrics_path=None,
            http_post=boom,
        )
        self.assertEqual(result["mode"], "mock")


class CliTests(unittest.TestCase):
    def test_smoke_cli_writes_metrics(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            metrics = Path(tmp) / "typesafe-runs.jsonl"
            with mock.patch("sys.stdout", new=mock.Mock()):
                rc = sp.main(["--smoke", "--force-mock", "--metrics", str(metrics)])
            self.assertEqual(rc, 0)
            self.assertTrue(metrics.is_file())
            rec = json.loads(metrics.read_text(encoding="utf-8").splitlines()[0])
            self.assertEqual(rec["experiment"], "unsga3_pareto_score")
            self.assertLessEqual(rec["candidate_count"], 10)

    def test_missing_args_exits_2(self) -> None:
        self.assertEqual(sp.main([]), 2)


if __name__ == "__main__":
    unittest.main()
