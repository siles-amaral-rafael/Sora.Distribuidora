#!/usr/bin/env python3
"""Combine xUnit TRX and isolated API checks into the standalone portal report."""
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

WEB = Path(__file__).resolve().parents[1]
CATALOG = WEB / "test-portal/data/cases.json"
OUTPUT = WEB / "test-portal/data/latest.json"
RESULTS = WEB / "test-results"
TRX_DIR = Path(os.environ.get("SORA_IT_TRX_DIR", RESULTS / "dotnet"))


def scrub(value):
    value = str(value or "")
    value = re.sub(r"(?i)bearer\s+[a-z0-9._~-]+", "Bearer [OCULTO]", value)
    value = re.sub(r"(?i)(password|signingkey|accessToken)[=: ]+[^\s,;]+", r"\1=[OCULTO]", value)
    value = re.sub(r"[A-Za-z0-9_-]{32,}\.[A-Za-z0-9_-]{16,}\.[A-Za-z0-9_-]{16,}", "[JWT OCULTO]", value)
    return value[:1200]


def load_trx():
    found = {}
    passed = failed = duration_total = 0
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    for path in TRX_DIR.rglob("*.trx"):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for item in root.findall(".//t:UnitTestResult", ns):
            name = item.attrib.get("testName", "")
            match = re.search(r"((?:UT|IT|API)-[A-Z]+-\d+)", name)
            if not match:
                continue
            outcome = item.attrib.get("outcome", "")
            status = "APROBADO" if outcome.lower() == "passed" else "FALLIDO"
            passed += status == "APROBADO"
            failed += status == "FALLIDO"
            duration = item.attrib.get("duration", "PT0S")
            seconds = 0.0
            iso = re.fullmatch(r"PT(?:(\d+(?:\.\d+)?)H)?(?:(\d+(?:\.\d+)?)M)?(?:(\d+(?:\.\d+)?)S)?", duration)
            if iso:
                seconds = float(iso.group(1) or 0) * 3600 + float(iso.group(2) or 0) * 60 + float(iso.group(3) or 0)
            elif re.fullmatch(r"\d{2}:\d{2}:\d{2}(?:\.\d+)?", duration):
                hours, minutes, rest = duration.split(":")
                seconds = int(hours) * 3600 + int(minutes) * 60 + float(rest)
            duration_total += seconds * 1000
            error = item.find("t:Output/t:ErrorInfo/t:Message", ns)
            evidence = item.find("t:Output/t:StdOut", ns)
            case_id = match.group(1)
            error_text = scrub(error.text if error is not None else "")
            evidence_text = scrub(evidence.text if evidence is not None else "")
            if case_id not in found:
                found[case_id] = {"status": status, "durationMs": 0.0, "error": "", "evidence": ""}
            case = found[case_id]
            case["durationMs"] = round(case["durationMs"] + seconds * 1000, 2)
            if status == "FALLIDO":
                case["status"] = status
                case["error"] = error_text
            elif evidence_text:
                case["evidence"] = evidence_text
    return found, {"executed": passed + failed, "passed": passed, "failed": failed,
                   "durationMs": round(duration_total, 2)}


def load_api():
    path = Path(os.environ.get("SORA_IT_RESULT_FILE", RESULTS / "revision6/integration-results.json"))
    if not path.exists():
        return {}, {"executed": 0, "passed": 0, "failed": 0, "durationMs": 0}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}, {"executed": 0, "passed": 0, "failed": 0, "durationMs": 0}
    mapped = {row["id"]: {"status": row.get("status", "PENDIENTE"),
                         "durationMs": row.get("durationMs", 0),
                         "error": scrub(row.get("error", "")),
                         "evidence": scrub(row.get("evidence", ""))}
            for row in data.get("results", [])}
    summary = {"executed": len(data.get("results", [])), "passed": data.get("passed", 0),
               "failed": data.get("failed", 0), "durationMs": data.get("durationMs", 0)}
    return mapped, summary


def load_coverage():
    lines = {}
    for path in TRX_DIR.rglob("coverage.cobertura.xml"):
        try:
            root = ET.parse(path).getroot()
            for source_class in root.findall(".//class"):
                filename = source_class.get("filename", "")
                for item in source_class.findall("./lines/line"):
                    number = item.get("number", "")
                    key = (filename, number)
                    lines[key] = max(lines.get(key, 0), int(item.get("hits", "0")))
        except (ET.ParseError, OSError, ValueError):
            continue
    valid = len(lines)
    covered = sum(hits > 0 for hits in lines.values())
    return {"linePercent": round(covered * 100 / valid, 2), "linesCovered": covered, "linesValid": valid} if valid else None


def main():
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    actual, executed = load_trx()
    if os.environ.get("SORA_IT_INCLUDE_API_RESULTS") == "1":
        api_results, api_summary = load_api()
        actual.update(api_results)
        for field in ("executed", "passed", "failed", "durationMs"):
            executed[field] += api_summary[field]
    cases = []
    for item in catalog:
        row = dict(item)
        if item.get("status") == "FUERA DE ALCANCE":
            row.setdefault("durationMs", 0)
        elif item["id"] in actual:
            row.update(actual[item["id"]])
        else:
            row["status"] = item.get("status", "PENDIENTE")
            row.setdefault("durationMs", 0)
        cases.append(row)
    executed_rows = [x for x in cases if x["status"] in ("APROBADO", "FALLIDO")]
    pending = sum(x["status"] == "PENDIENTE" for x in cases)
    excluded = sum(x["status"] == "FUERA DE ALCANCE" for x in cases)
    result = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "source": "TRX xUnit + ejecución API/PostgreSQL aislada" if os.environ.get("SORA_IT_INCLUDE_API_RESULTS") == "1" else "TRX xUnit",
        "summary": {"totalCases": len(cases), **executed, "pending": pending, "excluded": excluded},
        "coverage": load_coverage(),
        "cases": cases,
    }
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Reporte portal: {executed['executed']} ejecutados ({executed['passed']} aprobados, {executed['failed']} fallidos), "
          f"{pending} pendientes y {excluded} fuera de alcance.")
    if executed["failed"]:
        print("Hay fallos; consulta test-portal/data/latest.json para el detalle sanitizado.", file=sys.stderr)


if __name__ == "__main__":
    main()
