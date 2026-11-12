"""Fail closed for malformed audit output or high/critical vulnerabilities."""
import json
import sys

data = json.load(open(sys.argv[1], encoding='utf-8-sig'))
if not isinstance(data.get('projects'), list) or not data['projects']:
    raise SystemExit('NuGet audit did not return projects')
found = []
for project in data['projects']:
    for framework in project.get('frameworks', []):
        for kind in ('topLevelPackages', 'transitivePackages'):
            for package in framework.get(kind, []):
                for vulnerability in package.get('vulnerabilities', []):
                    if vulnerability['severity'].lower() in ('high', 'critical'):
                        found.append((package['id'], vulnerability['severity'], vulnerability['advisoryurl']))
if found:
    raise SystemExit(f'Blocking vulnerabilities: {found}')
