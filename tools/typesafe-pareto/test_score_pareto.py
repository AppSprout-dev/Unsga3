#!/usr/bin/env python3
"""Mock tests for the TypeSafe Pareto helper. No network, no API keys."""
from __future__ import annotations

import http.client
import io
import json
import os
import socket
import sys
import tempfile
import unittest
import urllib.parse
import urllib.request
from pathlib import Path
from unittest import mock
from urllib.response import addinfourl

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


class _ScriptedHttpsHandler(urllib.request.HTTPSHandler):
    """Canned HTTPS responses. https_open does not touch the network."""

    def __init__(
        self,
        scripted: list[tuple[int, str, bytes]],
        seen: list[dict[str, str | None]],
    ) -> None:
        super().__init__()
        self._scripted = scripted
        self._seen = seen

    def https_open(self, req: urllib.request.Request):
        host = urllib.parse.urlsplit(req.full_url).hostname
        self._seen.append(
            {
                "url": req.full_url,
                "host": None if host is None else host.lower().rstrip("."),
                "authorization": req.get_header("Authorization"),
            }
        )
        if self._scripted:
            status, location, body = self._scripted.pop(0)
        else:
            status, location, body = 200, "", b'{"answers":{}}'
        headers = http.client.HTTPMessage()
        if location:
            headers["Location"] = location
        headers["Content-Type"] = "application/json"
        response = addinfourl(io.BytesIO(body), headers, req.full_url, status)
        response.msg = "Found" if status >= 300 else "OK"
        return response


def _patched_opener(handler: urllib.request.BaseHandler):
    """Install ``handler`` in place of the real HTTPS handler. No proxies."""
    real_build_opener = urllib.request.build_opener

    def build(*handlers: urllib.request.BaseHandler):
        return real_build_opener(urllib.request.ProxyHandler({}), handler, *handlers)

    return mock.patch("urllib.request.build_opener", side_effect=build)


def _block_network():
    """Fail the test if urllib falls through to a real connection."""
    boom = mock.Mock(side_effect=AssertionError("network"))
    return mock.patch.multiple(socket, create_connection=boom, getaddrinfo=boom)


class KeyAllowlistTests(unittest.TestCase):
    def test_allowlisted_default_base_url_ok(self) -> None:
        with mock.patch.dict(os.environ, {}, clear=False):
            os.environ.pop(sp.BASE_URL_ENV, None)
            self.assertEqual(sp.resolve_base_url(), "https://api.typesafe.ai")
            self.assertEqual(sp.resolve_base_url(None), "https://api.typesafe.ai")
        self.assertEqual(sp.ALLOWED_TYPESAFE_HOSTS, frozenset({"api.typesafe.ai"}))
        self.assertEqual(
            sp.resolve_base_url("https://api.typesafe.ai/"),
            "https://api.typesafe.ai",
        )
        self.assertEqual(
            sp.resolve_base_url("https://API.typesafe.ai"),
            "https://API.typesafe.ai",
        )
        seen: list[dict[str, str | None]] = []
        handler = _ScriptedHttpsHandler(
            [(200, "", b'{"model":"jev-latest","answers":{"ok":{"type":"choice","choice":"keep"}}}')],
            seen,
        )
        with _block_network(), _patched_opener(handler):
            payload = sp._http_system_one(
                {"candidates": []},
                {},
                model="jev-latest",
                api_key="test-key",
                base_url=sp.resolve_base_url("https://api.typesafe.ai"),
                timeout=1.0,
            )
        self.assertEqual(payload["answers"]["ok"]["choice"], "keep")
        self.assertEqual(len(seen), 1)
        self.assertEqual(seen[0]["host"], "api.typesafe.ai")
        self.assertEqual(seen[0]["url"], "https://api.typesafe.ai/v1/systemone")
        self.assertEqual(seen[0]["authorization"], "Bearer test-key")

    def test_disallowed_base_url_rejected_without_request(self) -> None:
        rejected = (
            "https://evil.example",
            "http://api.typesafe.ai",
            "https://api.typesafe.ai.evil.example",
            "https://notapi.typesafe.ai",
            "https://user:pass@api.typesafe.ai",
            "https://api.typesafe.ai:443@evil.example",
        )
        for url in rejected:
            with self.subTest(url=url):
                with self.assertRaises(sp.TypeSafeConfigError) as caught:
                    sp.resolve_base_url(url)
                message = str(caught.exception)
                self.assertIn("api.typesafe.ai", message)
                self.assertNotIn("test-key", message)

        with mock.patch.dict(os.environ, {sp.BASE_URL_ENV: "https://evil.example/v1"}):
            with self.assertRaises(sp.TypeSafeConfigError) as caught:
                sp.resolve_base_url(None)
            self.assertIn("evil.example", str(caught.exception))
            self.assertEqual(
                sp.resolve_base_url("https://api.typesafe.ai"),
                "https://api.typesafe.ai",
            )

        def boom(*_args, **_kwargs):
            raise AssertionError("request must not be sent")

        with mock.patch("urllib.request.Request", side_effect=boom), mock.patch(
            "urllib.request.build_opener", side_effect=boom
        ):
            with self.assertRaises(sp.TypeSafeConfigError):
                sp._http_system_one(
                    {},
                    {},
                    model="jev-latest",
                    api_key="test-key",
                    base_url="https://evil.example",
                    timeout=1.0,
                )

        with self.assertRaises(sp.TypeSafeConfigError):
            sp.call_system_one(
                {"candidates": []},
                {},
                model="jev-latest",
                api_key="test-key",
                base_url="https://evil.example",
                http_post=boom,
            )
        with self.assertRaises(sp.TypeSafeConfigError):
            sp._sdk_system_one(
                {},
                {},
                model="jev-latest",
                api_key="test-key",
                base_url="https://evil.example",
                timeout=1.0,
            )
        payload = sp.load_export(FIXTURE)
        with self.assertRaises(sp.TypeSafeConfigError):
            sp.run_pass(
                payload,
                api_key="test-key",
                base_url="https://evil.example",
                force_mock=True,
                metrics_path=None,
                http_post=boom,
            )

    def test_cross_host_redirect_does_not_send_authorization(self) -> None:
        seen: list[dict[str, str | None]] = []
        handler = _ScriptedHttpsHandler(
            [(302, "https://evil.example/collect", b"")],
            seen,
        )
        with _block_network(), _patched_opener(handler):
            with self.assertRaises(sp.TypeSafeRequestError) as caught:
                sp._http_system_one(
                    {"candidates": []},
                    {},
                    model="jev-latest",
                    api_key="test-key",
                    base_url="https://api.typesafe.ai",
                    timeout=1.0,
                )
        self.assertIn("evil.example", str(caught.exception))
        self.assertNotIn("test-key", str(caught.exception))
        self.assertEqual([row["host"] for row in seen], ["api.typesafe.ai"])
        self.assertEqual(seen[0]["authorization"], "Bearer test-key")
        self.assertTrue(all(row["host"] != "evil.example" for row in seen))
        self.assertTrue(all("evil.example" not in (row["url"] or "") for row in seen))

    def test_https_downgrade_redirect_is_refused(self) -> None:
        seen: list[dict[str, str | None]] = []
        handler = _ScriptedHttpsHandler(
            [(302, "http://api.typesafe.ai/v1/systemone", b"")],
            seen,
        )
        with _block_network(), _patched_opener(handler):
            with self.assertRaises(sp.TypeSafeRequestError) as caught:
                sp._http_system_one(
                    {"candidates": []},
                    {},
                    model="jev-latest",
                    api_key="test-key",
                    base_url="https://api.typesafe.ai",
                    timeout=1.0,
                )
        self.assertIn("http://api.typesafe.ai", str(caught.exception))
        self.assertEqual([row["host"] for row in seen], ["api.typesafe.ai"])
        self.assertEqual(seen[0]["authorization"], "Bearer test-key")
        self.assertTrue(all(row["url"].startswith("https://") for row in seen if row["url"]))

    def test_same_host_https_redirect_keeps_authorization(self) -> None:
        seen: list[dict[str, str | None]] = []
        handler = _ScriptedHttpsHandler(
            [
                (302, "https://api.typesafe.ai/v1/systemone/", b""),
                (200, "", b'{"answers":{"ok":{"type":"choice","choice":"keep"}}}'),
            ],
            seen,
        )
        with _block_network(), _patched_opener(handler):
            payload = sp._http_system_one(
                {"candidates": []},
                {},
                model="jev-latest",
                api_key="test-key",
                base_url="https://api.typesafe.ai",
                timeout=1.0,
            )
        self.assertEqual(payload["answers"]["ok"]["choice"], "keep")
        self.assertEqual(
            [row["host"] for row in seen],
            ["api.typesafe.ai", "api.typesafe.ai"],
        )
        self.assertEqual(
            [row["authorization"] for row in seen],
            ["Bearer test-key", "Bearer test-key"],
        )

    def test_cli_rejects_disallowed_base_url(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            metrics = Path(tmp) / "typesafe-runs.jsonl"
            stderr = io.StringIO()
            with mock.patch("sys.stderr", new=stderr), mock.patch("sys.stdout", new=io.StringIO()):
                rc = sp.main(
                    [
                        "--smoke",
                        "--force-mock",
                        "--metrics",
                        str(metrics),
                        "--base-url",
                        "https://evil.example",
                    ]
                )
        self.assertEqual(rc, 2)
        self.assertIn("evil.example", stderr.getvalue())
        self.assertFalse(metrics.exists())


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
