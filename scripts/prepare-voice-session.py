#!/usr/bin/env python3
"""Refresh ADC on the Mac; transfer only a short-lived token over adb stdin.
No secrets are printed, put in argv, or written on the Mac. Debug APK only.
"""
import argparse
import datetime
import json
from pathlib import Path
import shutil
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--device', required=True)
parser.add_argument('--location', default='us-central1')
parser.add_argument('--model', default='gemini-live-2.5-flash-native-audio')
parser.add_argument('--check-only', action='store_true')
parser.add_argument('--probe-audio', type=Path, help='Synthetic PCM16 mono 16 kHz file; never a private recording')
args = parser.parse_args()
package = 'com.smartcity.ar.environmentcheck'
adb = shutil.which('adb') or '/opt/homebrew/bin/adb'
# Reuse gcloud's bundled authentication library without adding a project dependency.
gcloud = Path(shutil.which('gcloud') or '/opt/homebrew/share/google-cloud-sdk/bin/gcloud').resolve()
third_party = gcloud.parent.parent / 'lib/third_party'
sys.path.insert(0, str(third_party))
try:
    import google.auth
    from google.auth.transport.requests import Request
    credentials, project = google.auth.default(scopes=['https://www.googleapis.com/auth/cloud-platform'])
    credentials.refresh(Request())
    expiry = credentials.expiry
    if expiry.tzinfo is None:
        expiry = expiry.replace(tzinfo=datetime.timezone.utc)
    if (expiry - datetime.datetime.now(datetime.timezone.utc)).total_seconds() < 120:
        raise RuntimeError('Insufficient token lifetime')
    payload = json.dumps(dict(project='kateiso-core', location=args.location, model=args.model,
                              accessToken=credentials.token, expiresAt=int(expiry.timestamp()))).encode()
except Exception as exc:
    print('ADC preparation failed (' + type(exc).__name__ + '). No credentials printed.', file=sys.stderr)
    sys.exit(1)
if args.check_only:
    print('ADC refreshed; expires=' + expiry.isoformat() + '; token not exported.')
    sys.exit(0)

def private_write(name, value):
    # Names below are fixed. Secrets are bytes on stdin, never command arguments.
    command = [adb, '-s', args.device, 'shell', '-T', 'run-as', package, 'sh', '-c',
               "'umask 077; mkdir -p files; cat > files/" + name + "'"]
    result = subprocess.run(command, input=value, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError('Private injection failed; check debug APK and adb connection')
try:
    private_write('voice-session.json', payload)
    if args.probe_audio:
        pcm = args.probe_audio.read_bytes()
        if len(pcm) > 640000 or len(pcm) % 2:
            raise ValueError('Probe must be <=20 seconds of PCM16 mono 16kHz')
        private_write('voice-probe.pcm', pcm)
        for i in range(1, 8):
            extra = args.probe_audio.parent / ('voice-probe-' + str(i) + '.pcm')
            if extra.exists():
                data = extra.read_bytes()
                if len(data) > 640000 or len(data) % 2:
                    raise ValueError('Invalid synthetic follow-up PCM')
                private_write(extra.name, data)
        private_write('voice-probe.json', b'{}')
    print('Short-lived credential injected into app-private storage; expires=' + expiry.isoformat())
except Exception as exc:
    print('Injection failed (' + type(exc).__name__ + '). No credentials printed.', file=sys.stderr)
    sys.exit(1)
