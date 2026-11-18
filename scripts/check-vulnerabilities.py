"""Bloqueia o gate se o NuGet identificar vulnerabilidades nas dependências da API."""
import json
import sys

with open(sys.argv[1], encoding="utf-8") as source:
    report = json.load(source)
findings = []
for project in report.get("projects", []):
    for framework in project.get("frameworks", []):
        for package in framework.get("topLevelPackages", []) + framework.get("transitivePackages", []):
            for issue in package.get("vulnerabilities", []):
                findings.append((package["id"], issue.get("severity"), issue.get("advisoryurl")))
for finding in findings:
    print(*finding)
if findings:
    raise SystemExit(1)
print("NuGet: nenhuma vulnerabilidade reportada nas dependências da API.")
