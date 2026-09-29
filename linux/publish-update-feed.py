#!/usr/bin/env python3
"""Publish feed only after matching assets have been published successfully."""
import base64
import hashlib
import json
import pathlib
import subprocess
import sys


def gh(*args):
    return subprocess.check_output(['gh', *args], text=True)


def main():
    directory, number, commit = pathlib.Path(sys.argv[1]), int(sys.argv[2]), sys.argv[3]
    tag = f'aioncl-linux-preview-{number}'
    release = json.loads(gh('release', 'view', tag, '--repo', 'AionCL/launcher', '--json', 'assets,targetCommitish'))
    if release['targetCommitish'] != commit:
        raise SystemExit('Release commit does not match this build')
    published = {asset['name']: asset for asset in release['assets']}
    feed = {'schemaVersion': 1, 'version': '2.5.42', 'preview': number}
    for kind, name in (
        ('deb', f'aioncl-launcher_2.5.42~preview{number}_amd64.deb'),
        ('rpm', f'aioncl-launcher-2.5.42-0.preview{number}.x86_64.rpm'),
        ('portable', f'AionCL-Launcher-2.5.42-linux-preview.{number}.zip'),
    ):
        path = directory / name
        if name not in published or published[name]['size'] != path.stat().st_size:
            raise SystemExit(f'Missing or unexpected published package: {name}')
        feed[kind] = {'url': published[name]['url'], 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
    branch = 'feat/linux-launcher'
    endpoint = 'repos/AionCL/launcher/contents/config/linux-updates.json'
    response = subprocess.run(['gh', 'api', endpoint + '?ref=' + branch], text=True, capture_output=True)
    current = json.loads(response.stdout) if response.returncode == 0 else None
    if current:
        previous = json.loads(base64.b64decode(current['content']))
        if previous['preview'] >= number:
            raise SystemExit('Refusing to overwrite a newer or equal Linux update feed')
    payload = {'message': f'chore(linux): publish preview {number} update feed [skip ci]', 'branch': branch,
               'content': base64.b64encode((json.dumps(feed, indent=2) + '\n').encode()).decode()}
    if current:
        payload['sha'] = current['sha']
    result = subprocess.run(['gh', 'api', '--method', 'PUT', endpoint, '--input', '-'],
                            input=json.dumps(payload), text=True, capture_output=True)
    if result.returncode:
        raise SystemExit(result.stderr)
    print(f'LINUX_UPDATE_FEED_PUBLISHED preview={number}')


if __name__ == '__main__':
    main()
