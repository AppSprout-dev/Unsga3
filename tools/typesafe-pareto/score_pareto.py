#!/usr/bin/env python3
"""TypeSafe / Jev System One pass over U-NSGA-III Pareto candidates.

Additive semantic layer: Score + Choice judgments for a small candidate
sample. This does not replace NSGA-III / U-NSGA-III objectives.

Docs (do not invent APIs):
  https://docs.typesafe.ai/api.md
  https://docs.typesafe.ai/sdk/python.md
  https://docs.typesafe.ai/primitives.md
  https://docs.typesafe.ai/patterns/fan-out.md

Usage:
  python tools/typesafe-pareto/score_pareto.py --smoke
  python tools/typesafe-pareto/score_pareto.py --candidates path.json

Live POST https://api.typesafe.ai/v1/systemone only when TYPESAFE_API_KEY
is set (and --force-mock is not). CI uses the in-process mock.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence
from urllib.parse import urljoin

# Optional official SDK (https://docs.typesafe.ai/sdk/python.md).
# CI does not install it; HTTP fallback matches the documented REST API.
try:
    from typesafe_sdk import TypeSafeClient
except ImportError:  # pragma: no cover - exercised when the extra is absent
    TypeSafeClient = None

EXPERIMENT = "unsga3_pareto_score"
REPO = "AppSprout-dev/Unsga3"
DEFAULT_MODEL = "jev-latest"
DEFAULT_BASE_URL = "https://api.typesafe.ai"
SYSTEMONE_PATH = "/v1/systemone"
API_KEY_ENV = "TYPESAFE_API_KEY"
BASE_URL_ENV = "TYPESAFE_BASE_URL"
DEFAULT_MODEL_ENV = "TYPESAFE_DEFAULT_MODEL"
DEFAULT_TIMEOUT_S = 10.0
SMOKE_LIMIT = 10
METRIC_FILENAME = "typesafe-runs.jsonl"

# Ordered Score levels (index 0 = worst). See https://docs.typesafe.ai/primitives/score.md
CONSTRAINT_LEVELS = (
    "Severe constraint violation; candidate is clearly infeasible.",
    "Feasibility is marginal; slack is thin or a constraint is close to breaking.",
    "Constraints are satisfied with little unused slack.",
    "Constraints are comfortably satisfied.",
)
DIVERSITY_LEVELS = (
    "Nearly duplicates another candidate on the supplied front.",
    "Occupies a moderately populated region of the front.",
    "Adds coverage in a sparsely sampled region.",
)
EXPLOIT_LEVELS = (
    "Pure exploitation of a known knee or dense cluster.",
    "Balanced local refinement and spread along the front.",
    "Explores a sparse or extreme region of the front.",
)
DISPOSITION_CRITERIA = {
    "keep": "Retain as a high-value Pareto representative.",
    "drop": "Redundant, infeasible, or low semantic value; drop from the shortlist.",
    "review": "Ambiguous; a human should inspect before keeping.",
}

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
DEFAULT_FIXTURE = HERE / "fixtures" / "zdt1_candidates.json"
DEFAULT_METRICS = REPO_ROOT / "metrics" / METRIC_FILENAME


class TypeSafeRequestError(RuntimeError):
    """Raised when the TypeSafe HTTP API returns an error or cannot be reached."""


def repo_root() -> Path:
    return REPO_ROOT


def default_metrics_path() -> Path:
    return DEFAULT_METRICS


def load_export(path: Path) -> dict[str, Any]:
    """Load a candidate JSON export (object with candidates, or a raw list)."""
    raw = json.loads(path.read_text(encoding="utf-8"))
    return normalize_export(raw, source_path=str(path))


def normalize_export(raw: Any, *, source_path: str | None = None) -> dict[str, Any]:
    if isinstance(raw, list):
        payload: dict[str, Any] = {"candidates": raw}
    elif isinstance(raw, dict):
        payload = dict(raw)
    else:
        raise ValueError("Candidate export must be a JSON object or array.")

    candidates = payload.get("candidates")
    if candidates is None and "NonDominatedSolutions" in payload:
        candidates = payload["NonDominatedSolutions"]
        payload["candidates"] = candidates
    if not isinstance(candidates, list) or not candidates:
        raise ValueError("Candidate export needs a non-empty 'candidates' array.")

    normalized: list[dict[str, Any]] = []
    for i, item in enumerate(candidates):
        if not isinstance(item, dict):
            raise ValueError(f"Candidate {i} must be an object.")
        cand = dict(item)
        cand.setdefault("id", f"c{i}")
        objectives = cand.get("objectives")
        if not isinstance(objectives, list) or not objectives:
            raise ValueError(f"Candidate {cand['id']} needs a non-empty 'objectives' array.")
        cand.setdefault("variables", [])
        cand.setdefault("constraints", [])
        cv = cand.get("constraint_violation")
        if cv is None:
            cv = sum(float(g) for g in cand["constraints"] if isinstance(g, (int, float)) and g > 0)
            cand["constraint_violation"] = cv
        cand.setdefault("feasible", float(cand["constraint_violation"]) <= 0)
        normalized.append(cand)
    payload["candidates"] = normalized
    if source_path:
        payload.setdefault("source_path", source_path)
    payload.setdefault("algorithm", "U-NSGA-III")
    return payload


def sample_candidates(payload: Mapping[str, Any], limit: int = SMOKE_LIMIT) -> list[dict[str, Any]]:
    if limit < 1:
        raise ValueError("limit must be >= 1")
    candidates = list(payload["candidates"])
    return candidates[:limit]


def build_state(payload: Mapping[str, Any], candidates: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    """Structured state for System One. Question instructions use dotted paths."""
    return {
        "repo": REPO,
        "experiment": EXPERIMENT,
        "algorithm": payload.get("algorithm", "U-NSGA-III"),
        "problem": payload.get("problem"),
        "source": payload.get("source"),
        "layer": (
            "Additive TypeSafe / Jev semantic layer beside classical Pareto fronts. "
            "It does not replace NSGA-III / U-NSGA-III objective vectors."
        ),
        "candidates": [dict(c) for c in candidates],
    }


def _qid(candidate_id: str, suffix: str) -> str:
    safe = "".join(ch if ch.isalnum() or ch in "-_" else "_" for ch in str(candidate_id))
    return f"{safe}_{suffix}"


def build_questions(candidates: Sequence[Mapping[str, Any]]) -> dict[str, dict[str, Any]]:
    """One System One call: per-candidate Score + Choice (speculative fan-out)."""
    questions: dict[str, dict[str, Any]] = {}
    for i, cand in enumerate(candidates):
        cid = str(cand["id"])
        path = f"candidates[{i}]"
        questions[_qid(cid, "constraint_satisfaction")] = {
            "type": "score",
            "instructions": (
                f"How well does `{path}` satisfy constraints, given "
                f"`{path}.feasible` and `{path}.constraint_violation` "
                f"(g<=0 form; 0 means feasible)?"
            ),
            "criteria": list(CONSTRAINT_LEVELS),
        }
        questions[_qid(cid, "diversity_value")] = {
            "type": "score",
            "instructions": (
                f"How much unique coverage does `{path}` add relative to the other "
                f"entries in `candidates`, looking at `{path}.objectives`?"
            ),
            "criteria": list(DIVERSITY_LEVELS),
        }
        questions[_qid(cid, "exploit_vs_explore")] = {
            "type": "score",
            "instructions": (
                f"On the exploit-vs-explore spectrum, where does `{path}` sit "
                f"given `{path}.objectives` and the spread of `candidates`?"
            ),
            "criteria": list(EXPLOIT_LEVELS),
        }
        questions[_qid(cid, "disposition")] = {
            "type": "choice",
            "instructions": (
                f"Should `{path}` be kept, dropped, or reviewed as a shortlist "
                f"member? Use feasibility, front rank if present, objective "
                f"vectors, and redundancy versus the rest of `candidates`."
            ),
            "criteria": dict(DISPOSITION_CRITERIA),
        }
    return questions


def expected_question_ids(candidates: Sequence[Mapping[str, Any]]) -> list[str]:
    ids: list[str] = []
    for cand in candidates:
        cid = str(cand["id"])
        ids.extend(
            [
                _qid(cid, "constraint_satisfaction"),
                _qid(cid, "diversity_value"),
                _qid(cid, "exploit_vs_explore"),
                _qid(cid, "disposition"),
            ]
        )
    return ids


def _zdt1_pf_gap(objectives: Sequence[Any]) -> float | None:
    if len(objectives) < 2:
        return None
    try:
        f1 = float(objectives[0])
        f2 = float(objectives[1])
    except (TypeError, ValueError):
        return None
    if f1 < 0:
        return None
    pf = 1.0 - (f1 ** 0.5)
    return max(0.0, f2 - pf)


def _nearest_objective_gap(index: int, candidates: Sequence[Mapping[str, Any]]) -> float:
    obj = candidates[index].get("objectives") or []
    best = float("inf")
    for j, other in enumerate(candidates):
        if j == index:
            continue
        other_obj = other.get("objectives") or []
        n = min(len(obj), len(other_obj))
        if n == 0:
            continue
        dist = sum((float(obj[k]) - float(other_obj[k])) ** 2 for k in range(n)) ** 0.5
        best = min(best, dist)
    return 0.0 if best is float("inf") else best


def _round_dist(dist: Mapping[str, float]) -> dict[str, float]:
    rounded = {k: round(float(v), 6) for k, v in dist.items()}
    if rounded:
        peak_key = max(rounded, key=rounded.get)
        rounded[peak_key] = round(rounded[peak_key] + (1.0 - sum(rounded.values())), 6)
    return rounded


def _peaked(options: Sequence[str], winner: str, peak: float = 0.78) -> dict[str, float]:
    rest = (1.0 - peak) / max(len(options) - 1, 1)
    return _round_dist({opt: (peak if opt == winner else rest) for opt in options})


def mock_system_one(
    state: Mapping[str, Any],
    questions: Mapping[str, Mapping[str, Any]],
    *,
    model: str = DEFAULT_MODEL,
) -> dict[str, Any]:
    """Deterministic stand-in matching the documented System One response shape."""
    candidates = list(state.get("candidates") or [])
    by_id = {str(c.get("id")): (i, c) for i, c in enumerate(candidates)}
    answers: dict[str, Any] = {}
    dimensions = (
        "constraint_satisfaction",
        "diversity_value",
        "exploit_vs_explore",
        "disposition",
    )

    for qid, question in questions.items():
        qtype = question["type"]
        dimension = qid
        cand_id = qid
        for dim in dimensions:
            token = f"_{dim}"
            if qid.endswith(token):
                cand_id = qid[: -len(token)]
                dimension = dim
                break
        if cand_id in by_id:
            idx, cand = by_id[cand_id]
        else:
            idx = 0
            cand = candidates[0] if candidates else {}

        feasible = bool(cand.get("feasible", True))
        cv = float(cand.get("constraint_violation") or 0.0)
        gap = _zdt1_pf_gap(cand.get("objectives") or [])
        spread = _nearest_objective_gap(idx, candidates)

        if qtype == "score":
            criteria = list(question["criteria"])
            if dimension == "constraint_satisfaction":
                if not feasible or cv > 0.1:
                    level = 0
                elif cv > 0:
                    level = 1
                elif gap is not None and gap > 0.15:
                    level = 2
                else:
                    level = len(criteria) - 1
            elif dimension == "diversity_value":
                if spread < 0.08:
                    level = 0
                elif spread < 0.25:
                    level = 1
                else:
                    level = 2
            elif dimension == "exploit_vs_explore":
                objs = cand.get("objectives") or [0.5]
                f1 = float(objs[0])
                if f1 <= 0.05 or f1 >= 0.95:
                    level = 2
                elif 0.35 <= f1 <= 0.65:
                    level = 0
                else:
                    level = 1
            else:
                level = min(1, len(criteria) - 1)
            level = max(0, min(level, len(criteria) - 1))
            peak = 0.8
            raw_probs = {
                str(i): (peak if i == level else (1.0 - peak) / max(len(criteria) - 1, 1))
                for i in range(len(criteria))
            }
            probs = _round_dist(raw_probs)
            score = round(sum(int(k) * v for k, v in probs.items()), 6)
            answers[qid] = {
                "type": "score",
                "score": score,
                "legend": {str(i): criteria[i] for i in range(len(criteria))},
                "probabilities": probs,
                "confidence": peak,
            }
        elif qtype == "choice":
            options = list(question["criteria"].keys())
            if not feasible or cv > 0:
                winner = "drop" if "drop" in options else options[-1]
            elif gap is not None and gap > 0.15:
                winner = "drop" if "drop" in options else options[-1]
            elif spread < 0.08:
                winner = "review" if "review" in options else options[0]
            else:
                winner = "keep" if "keep" in options else options[0]
            answers[qid] = {
                "type": "choice",
                "choice": winner,
                "probabilities": _peaked(options, winner),
                "confidence": 0.78,
            }
        else:
            raise ValueError(f"Unsupported question type in mock: {qtype}")

    return {
        "model": model,
        "answers": answers,
        "usage": {"input_tokens": 0, "output_tokens": 0},
    }


def _systemone_url(base_url: str) -> str:
    root = base_url.rstrip("/") + "/"
    return urljoin(root, SYSTEMONE_PATH.lstrip("/"))


def _http_system_one(
    state: Mapping[str, Any],
    questions: Mapping[str, Mapping[str, Any]],
    *,
    model: str,
    api_key: str,
    base_url: str,
    timeout: float,
) -> dict[str, Any]:
    body = json.dumps({"state": state, "model": model, "questions": questions}).encode("utf-8")
    request = urllib.request.Request(
        _systemone_url(base_url),
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as resp:
            payload = json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise TypeSafeRequestError(f"TypeSafe HTTP {exc.code}: {detail[:500]}") from exc
    except urllib.error.URLError as exc:
        raise TypeSafeRequestError(f"TypeSafe connection failed: {exc.reason}") from exc
    if not isinstance(payload, dict) or "answers" not in payload:
        raise TypeSafeRequestError("TypeSafe response missing 'answers'.")
    return payload


def _sdk_system_one(
    state: Mapping[str, Any],
    questions: Mapping[str, Mapping[str, Any]],
    *,
    model: str,
    api_key: str,
    base_url: str,
    timeout: float,
) -> dict[str, Any]:
    if TypeSafeClient is None:
        raise TypeSafeRequestError("typesafe-sdk is not installed.")
    with TypeSafeClient(api_key=api_key, model=model, base_url=base_url, timeout=timeout) as client:
        result = client.system_one(state=state, questions=questions, model=model)
        raw = getattr(result, "raw_http_response", None)
        if raw is not None:
            payload = raw.json()
            if isinstance(payload, dict) and "answers" in payload:
                return payload
        usage = getattr(result, "usage", None)
        answers: dict[str, Any] = {}
        for key, answer in getattr(result, "answers", {}).items():
            answers[key] = _answer_to_dict(answer)
        return {
            "model": getattr(result, "model", model),
            "answers": answers,
            "usage": {
                "input_tokens": getattr(usage, "input_tokens", None),
                "output_tokens": getattr(usage, "output_tokens", None),
            },
        }


def _answer_to_dict(answer: Any) -> dict[str, Any]:
    if isinstance(answer, dict):
        return dict(answer)
    qtype = getattr(answer, "type", None)
    if qtype == "score" or hasattr(answer, "score"):
        legend = getattr(answer, "legend", {})
        probs = getattr(answer, "probabilities", {})
        return {
            "type": "score",
            "score": getattr(answer, "score"),
            "legend": {str(k): v for k, v in dict(legend).items()},
            "probabilities": {str(k): v for k, v in dict(probs).items()},
            "confidence": getattr(answer, "confidence", None),
        }
    if qtype == "choice" or hasattr(answer, "choice"):
        return {
            "type": "choice",
            "choice": getattr(answer, "choice"),
            "probabilities": dict(getattr(answer, "probabilities", {})),
            "confidence": getattr(answer, "confidence", None),
        }
    if qtype == "noul" or hasattr(answer, "noul"):
        return {"type": "noul", "noul": getattr(answer, "noul")}
    raise TypeError(f"Unrecognized TypeSafe answer: {answer!r}")


def resolve_api_key(explicit: str | None = None) -> str | None:
    key = explicit if explicit is not None else os.environ.get(API_KEY_ENV)
    if key is None:
        return None
    key = key.strip()
    return key or None


def resolve_model(explicit: str | None = None) -> str:
    if explicit:
        return explicit
    env = (os.environ.get(DEFAULT_MODEL_ENV) or "").strip()
    return env or DEFAULT_MODEL


def resolve_base_url(explicit: str | None = None) -> str:
    if explicit:
        return explicit.rstrip("/")
    env = (os.environ.get(BASE_URL_ENV) or "").strip()
    return (env or DEFAULT_BASE_URL).rstrip("/")


def call_system_one(
    state: Mapping[str, Any],
    questions: Mapping[str, Mapping[str, Any]],
    *,
    model: str,
    api_key: str | None,
    base_url: str,
    timeout: float = DEFAULT_TIMEOUT_S,
    force_mock: bool = False,
    http_post: Callable[..., dict[str, Any]] | None = None,
) -> tuple[dict[str, Any], str, float]:
    """Return (response, mode, latency_ms). mode is 'mock', 'sdk', or 'http'."""
    started = time.perf_counter()
    if force_mock or not api_key:
        response = mock_system_one(state, questions, model=model)
        mode = "mock"
    elif http_post is not None:
        response = http_post(state, questions, model=model, api_key=api_key, base_url=base_url, timeout=timeout)
        mode = "http"
    elif TypeSafeClient is not None:
        response = _sdk_system_one(
            state, questions, model=model, api_key=api_key, base_url=base_url, timeout=timeout
        )
        mode = "sdk"
    else:
        response = _http_system_one(
            state, questions, model=model, api_key=api_key, base_url=base_url, timeout=timeout
        )
        mode = "http"
    latency_ms = (time.perf_counter() - started) * 1000.0
    return response, mode, latency_ms


def rank_candidates(
    candidates: Sequence[Mapping[str, Any]],
    answers: Mapping[str, Mapping[str, Any]],
) -> list[dict[str, Any]]:
    """Compose Score answers in code (weights live here, not in the prompt)."""
    rows: list[dict[str, Any]] = []
    for cand in candidates:
        cid = str(cand["id"])
        constraint = _score_value(answers.get(_qid(cid, "constraint_satisfaction")))
        diversity = _score_value(answers.get(_qid(cid, "diversity_value")))
        exploit = _score_value(answers.get(_qid(cid, "exploit_vs_explore")))
        disposition = answers.get(_qid(cid, "disposition")) or {}
        # Normalize each Score onto [0, 1] using its own max level.
        c_norm = _normalize_score(constraint, len(CONSTRAINT_LEVELS) - 1)
        d_norm = _normalize_score(diversity, len(DIVERSITY_LEVELS) - 1)
        e_norm = _normalize_score(exploit, len(EXPLOIT_LEVELS) - 1)
        composite = 0.45 * c_norm + 0.35 * d_norm + 0.20 * e_norm
        rows.append(
            {
                "id": cid,
                "objectives": list(cand.get("objectives") or []),
                "feasible": bool(cand.get("feasible", True)),
                "constraint_satisfaction": constraint,
                "diversity_value": diversity,
                "exploit_vs_explore": exploit,
                "disposition": disposition.get("choice"),
                "disposition_confidence": disposition.get("confidence"),
                "composite": composite,
            }
        )
    rows.sort(key=lambda r: (-r["composite"], r["id"]))
    return rows


def _score_value(answer: Mapping[str, Any] | None) -> float | None:
    if not answer:
        return None
    value = answer.get("score")
    return float(value) if isinstance(value, (int, float)) else None


def _normalize_score(value: float | None, max_level: int) -> float:
    if value is None or max_level <= 0:
        return 0.0
    return max(0.0, min(1.0, float(value) / max_level))


def metrics_record(
    *,
    model: str,
    latency_ms: float,
    usage: Mapping[str, Any] | None,
    candidate_count: int,
    answers: Mapping[str, Any],
    notes: str,
    extra: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    record: dict[str, Any] = {
        "ts": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "experiment": EXPERIMENT,
        "repo": REPO,
        "model": model,
        "latency_ms": round(latency_ms, 3),
        "usage": dict(usage) if usage else {"input_tokens": None, "output_tokens": None},
        "candidate_count": candidate_count,
        "answers": dict(answers),
        "notes": notes,
    }
    if extra:
        record.update(extra)
    return record


def append_metrics(path: Path, record: Mapping[str, Any]) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    line = json.dumps(record, separators=(",", ":"), ensure_ascii=False)
    with path.open("a", encoding="utf-8") as fh:
        fh.write(line + "\n")
    return path


def format_ranking_table(rows: Sequence[Mapping[str, Any]]) -> str:
    headers = (
        "id",
        "f1",
        "f2",
        "feas",
        "constraint",
        "diversity",
        "exploit",
        "disposition",
        "composite",
    )
    lines = ["  ".join(headers)]
    for row in rows:
        objs = row.get("objectives") or []
        f1 = f"{float(objs[0]):.4f}" if len(objs) > 0 else "-"
        f2 = f"{float(objs[1]):.4f}" if len(objs) > 1 else "-"
        c = row.get("constraint_satisfaction")
        d = row.get("diversity_value")
        e = row.get("exploit_vs_explore")
        lines.append(
            "  ".join(
                [
                    str(row.get("id")),
                    f1,
                    f2,
                    "Y" if row.get("feasible") else "N",
                    "-" if c is None else f"{c:.2f}",
                    "-" if d is None else f"{d:.2f}",
                    "-" if e is None else f"{e:.2f}",
                    str(row.get("disposition") or "-"),
                    f"{float(row.get('composite') or 0.0):.3f}",
                ]
            )
        )
    return "\n".join(lines)


def run_pass(
    payload: Mapping[str, Any],
    *,
    limit: int = SMOKE_LIMIT,
    model: str | None = None,
    api_key: str | None = None,
    base_url: str | None = None,
    timeout: float = DEFAULT_TIMEOUT_S,
    force_mock: bool = False,
    metrics_path: Path | None = DEFAULT_METRICS,
    notes: str | None = None,
    http_post: Callable[..., dict[str, Any]] | None = None,
) -> dict[str, Any]:
    candidates = sample_candidates(payload, limit=limit)
    state = build_state(payload, candidates)
    questions = build_questions(candidates)
    resolved_model = resolve_model(model)
    resolved_key = resolve_api_key(api_key)
    resolved_base = resolve_base_url(base_url)
    response, mode, latency_ms = call_system_one(
        state,
        questions,
        model=resolved_model,
        api_key=resolved_key,
        base_url=resolved_base,
        timeout=timeout,
        force_mock=force_mock,
        http_post=http_post,
    )
    answers = response.get("answers") or {}
    rows = rank_candidates(candidates, answers)
    note_bits = [notes] if notes else []
    note_bits.append(f"mode={mode}")
    if mode == "mock":
        note_bits.append("CI/mock System One; live call requires TYPESAFE_API_KEY")
    if len(payload["candidates"]) > len(candidates):
        note_bits.append(f"sampled {len(candidates)} of {len(payload['candidates'])}")
    record = metrics_record(
        model=response.get("model", resolved_model),
        latency_ms=latency_ms,
        usage=response.get("usage"),
        candidate_count=len(candidates),
        answers=answers,
        notes="; ".join(note_bits),
        extra={"mode": mode, "question_count": len(questions)},
    )
    written = None
    if metrics_path is not None:
        written = append_metrics(metrics_path, record)
    return {
        "state": state,
        "questions": questions,
        "response": response,
        "ranking": rows,
        "metrics": record,
        "metrics_path": str(written) if written else None,
        "mode": mode,
        "latency_ms": latency_ms,
    }


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument(
        "--candidates",
        type=Path,
        default=None,
        help="JSON export of candidates (default: fixture when --smoke)",
    )
    p.add_argument("--smoke", action="store_true", help=f"Run fixture sample (≤{SMOKE_LIMIT} candidates)")
    p.add_argument("--force-mock", action="store_true", help="Skip live API even if TYPESAFE_API_KEY is set")
    p.add_argument("--limit", type=int, default=SMOKE_LIMIT)
    p.add_argument("--model", default=None, help=f"System One model (default {DEFAULT_MODEL})")
    p.add_argument("--metrics", type=Path, default=DEFAULT_METRICS)
    p.add_argument("--no-metrics", action="store_true")
    p.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT_S)
    p.add_argument("--json", action="store_true", help="Print full result JSON")
    return p.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if args.candidates is None:
        if not args.smoke:
            print("Pass --candidates PATH or --smoke.", file=sys.stderr)
            return 2
        path = DEFAULT_FIXTURE
    else:
        path = args.candidates
    if not path.is_file():
        print(f"Candidate file not found: {path}", file=sys.stderr)
        return 2

    payload = load_export(path)
    metrics_path = None if args.no_metrics else args.metrics
    result = run_pass(
        payload,
        limit=args.limit,
        model=args.model,
        force_mock=args.force_mock,
        metrics_path=metrics_path,
        notes="smoke fixture" if args.smoke or path == DEFAULT_FIXTURE else f"export {path.name}",
        timeout=args.timeout,
    )

    print(f"TypeSafe Pareto pass | mode={result['mode']} model={result['metrics']['model']} "
          f"candidates={result['metrics']['candidate_count']} questions={len(result['questions'])} "
          f"latency_ms={result['latency_ms']:.1f}")
    print()
    print(format_ranking_table(result["ranking"]))
    if result["metrics_path"]:
        print()
        print(f"metrics: {result['metrics_path']}")
    if args.json:
        print()
        print(json.dumps({
            "mode": result["mode"],
            "ranking": result["ranking"],
            "metrics": result["metrics"],
            "answers": result["response"].get("answers"),
        }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
