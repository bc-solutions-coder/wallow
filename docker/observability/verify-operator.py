#!/usr/bin/env python3
"""Verify the operator boundary against disposable Grafana; no Pangolin connection is made."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

root = Path(__file__).resolve().parent
project = os.environ.get('OBSERVABILITY_PROOF_PROJECT', 'wallow-245-proof')
base = ['docker', 'compose', '--project-name', project, '--env-file', str(root/'.env'),
        '--env-file', str(root/'.env.operator.example'), '-f', str(root/'compose.yml')]
env = {**os.environ, 'OBSERVABILITY_DATA_DIR': os.environ.get('OBSERVABILITY_DATA_DIR', '/tmp/wallow-245-data')}

def run(args):
    return subprocess.run(args, cwd=root.parent, env=env, check=True, text=True, capture_output=True).stdout

with tempfile.TemporaryDirectory() as directory:
    config = Path(directory)/'operators.json'
    config.write_text(json.dumps({'verified-operator': 'observability-proof-viewer'}))
    config.chmod(0o644)
    override = Path(directory)/'fixture.yml'
    override.write_text('''services:
  newt:
    profiles: [deployment]
    env_file: !reset []
  operator:
    volumes: !override
      - '''+str(config)+''':/etc/observability/operators.json:ro
''')
    compose = base + ['-f', str(root/'compose.operator.yml'), '-f', str(override)]
    def call(network, peer, url, identity):
        args = ['docker', 'run', '--rm', '--network', network]
        if peer: args += ['--ip', peer]
        code = '''import json,sys,urllib.request,urllib.error
try:
 r=urllib.request.urlopen(urllib.request.Request(sys.argv[1],headers={'Remote-User':sys.argv[2],'X-WEBAUTH-USER':'operator','X-WEBAUTH-ROLE':'Admin','Cookie':'grafana_session=forged'}),timeout=10)
 print(json.dumps({'status':r.status,'body':r.read().decode()}))
except urllib.error.HTTPError as e: print(json.dumps({'status':e.code}))
'''
        return json.loads(run(args+['python:3.13-alpine','python','-c',code,url,identity]))
    try:
        run(compose+['--profile','query','up','-d','--build','operator','grafana'])
        for _ in range(20):
            try:
                allowed = call(project+'_operator_edge', '172.29.241.2', 'http://operator:8082/api/user', 'verified-operator')
                if allowed['status'] == 200: break
            except subprocess.CalledProcessError: pass
            time.sleep(1)
        else: raise RuntimeError('Operator Grafana did not become ready')
        user = json.loads(allowed['body'])
        assert user['login'] == 'observability-proof-viewer' and not user['isGrafanaAdmin'], user
        organization = call(project+'_operator_edge','172.29.241.2','http://operator:8082/api/user/orgs','verified-operator')
        assert json.loads(organization['body'])[0]['role'] == 'Viewer', organization
        denied = call(project+'_operator_edge','172.29.241.2','http://operator:8082/api/user','ordinary-registrant')
        forged = call(project+'_operator_edge','172.29.241.5','http://operator:8082/api/user','verified-operator')
        direct = call(project+'_storage',None,'http://grafana:3000/api/user','verified-operator')
        assert denied['status'] == 403 and forged['status'] == 403 and direct['status'] == 401, (denied,forged,direct)
        assert not run(['docker','port',project+'-operator-1']).strip()
        assert not run(['docker','port',project+'-grafana-1']).strip()
        print(json.dumps(dict(localFixture=True,livePangolin=False,operator=200,role='Viewer',registrant=403,forgedPeer=403,directGrafana=401,publishedPorts=False)))
    finally:
        subprocess.run(compose+['rm','-s','-f','operator'],cwd=root.parent,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        subprocess.run(base+['--profile','query','up','-d','grafana'],cwd=root.parent,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=True)
