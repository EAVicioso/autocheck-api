"""Compare the actual base commit against the tested merge. Never checks out the PR head alone."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import tarfile
import time
import urllib.request

root = Path(__file__).resolve().parent.parent
artifacts = root / 'artifacts'
artifacts.mkdir(exist_ok=True)
base_sha = os.environ.get('BASE_SHA', '')
if not re.fullmatch(r'[0-9a-f]{40}', base_sha) or base_sha == '0' * 40:
    base_sha = subprocess.check_output(['git', 'rev-parse', 'HEAD^'], cwd=root, text=True).strip()

def run(args, cwd=root, env=None):
    subprocess.run(args, cwd=cwd, env=env, check=True)

def build(path):
    run(['dotnet', 'restore', 'AutoCheck.slnx'], path)
    run(['dotnet', 'build', 'AutoCheck.slnx', '-c', 'Release', '--no-restore'], path)

def export(path, destination, port):
    env = os.environ.copy()
    env.update(ASPNETCORE_ENVIRONMENT='Development', Workers__Enabled='false', OpenApi__Enabled='true')
    with tempfile.TemporaryFile() as logs:
        process = subprocess.Popen(['dotnet', 'run', '--no-build', '--no-launch-profile', '-c', 'Release', '--project', str(path / 'AutoCheck.Api'), '--urls', f'http://127.0.0.1:{port}'], cwd=path, env=env, stdout=logs, stderr=logs,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        try:
            for _ in range(150):
                if process.poll() is not None:
                    logs.seek(0)
                    raise RuntimeError(logs.read().decode(errors='replace'))
                try:
                    with urllib.request.urlopen(f'http://127.0.0.1:{port}/openapi/v1.json', timeout=1) as response:
                        value = json.load(response)
                    destination.write_text(json.dumps(value), encoding='utf-8')
                    return
                except (OSError, TimeoutError):
                    time.sleep(0.1)
            raise RuntimeError('OpenAPI export timed out')
        finally:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)

with tempfile.TemporaryDirectory(prefix='autocheck-base-') as folder:
    baseline = Path(folder) / 'repository'
    baseline.mkdir()
    archive = Path(folder) / 'base.tar'
    with archive.open('wb') as output:
        subprocess.run(['git', 'archive', '--format=tar', base_sha], cwd=root, stdout=output, check=True)
    with tarfile.open(archive) as source:
        source.extractall(baseline, filter='data')
    try:
        if sys.argv[1] == 'api':
            build(baseline)
            build(root)
            export(baseline, artifacts / 'base-openapi.json', 5197)
            export(root, artifacts / 'current-openapi.json', 5198)
        elif sys.argv[1] == 'database':
            connection = os.environ.get('ConnectionStrings__Postgres', '')
            if 'autocheck_tests_upgrade' not in connection or not ('Host=127.0.0.1;' in connection or 'Host=localhost;' in connection):
                raise RuntimeError('Upgrade checks require the isolated loopback test database')
            if list((baseline / 'AutoCheck.Api').rglob('*ModelSnapshot.cs')):
                build(baseline)
                run(['dotnet', 'run', '--no-build', '--no-launch-profile', '-c', 'Release', '--project', 'AutoCheck.Api', '--', '--migrate'], baseline)
            # A sentinel checks that an upgrade does not replace the database.
            run(['psql', '-h', '127.0.0.1', '-U', 'postgres', '-d', 'autocheck_tests_upgrade', '-v', 'ON_ERROR_STOP=1', '-c', 'CREATE TABLE IF NOT EXISTS public.ci_sentinel (id integer primary key); INSERT INTO public.ci_sentinel VALUES (1) ON CONFLICT DO NOTHING;'], env={**os.environ, 'PGPASSWORD': 'postgres'})
            build(root)
            run(['dotnet', 'run', '--no-build', '--no-launch-profile', '-c', 'Release', '--project', 'AutoCheck.Api', '--', '--migrate'])
            run(['psql', '-h', '127.0.0.1', '-U', 'postgres', '-d', 'autocheck_tests_upgrade', '-v', 'ON_ERROR_STOP=1', '-c', "DO $$ BEGIN IF (SELECT count(*) FROM public.ci_sentinel) <> 1 THEN RAISE EXCEPTION 'Upgrade lost existing data'; END IF; END $$;"], env={**os.environ, 'PGPASSWORD': 'postgres'})
        else:
            raise RuntimeError('Expected api or database')
    finally:
        # TemporaryDirectory owns only this generated directory, never a repository checkout.
        if Path(folder).resolve().parent != Path(tempfile.gettempdir()).resolve():
            raise RuntimeError('Unexpected temporary directory location')
