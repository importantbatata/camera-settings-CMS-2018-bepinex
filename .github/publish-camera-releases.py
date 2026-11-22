import base64
import hashlib
import json
import os
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

repo = os.environ['GITHUB_REPOSITORY']
api = 'https://api.github.com/repos/' + repo
token = os.environ['GITHUB_TOKEN']


def request(url, data=None, content_type='application/json'):
    headers = {'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
               'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'CMS18-camera-release-publisher'}
    if data is not None:
        headers['Content-Type'] = content_type
    with urllib.request.urlopen(urllib.request.Request(url, data=data, headers=headers), timeout=120) as response:
        return json.load(response)


packages = json.loads(Path('.github/release-packages.json').read_text())
for package in packages:
    version = package['version']
    tag = 'v' + version
    data = base64.b64decode(package['data'], validate=True)
    digest = 'sha256:' + hashlib.sha256(data).hexdigest()
    assert digest == 'sha256:' + package['sha256']
    body = '[Features, setup, and version history](https://github.com/' + repo + '/blob/main/README.md)'
    try:
        release = request(api + '/releases/tags/' + tag)
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        release = request(api + '/releases', json.dumps({
            'tag_name': tag, 'target_commitish': os.environ['GITHUB_SHA'],
            'name': tag, 'body': body, 'draft': False, 'prerelease': False,
            'make_latest': 'true' if version == '1.3.0' else 'false'}).encode())
    assets = request(api + '/releases/' + str(release['id']) + '/assets?per_page=100')
    asset = next((asset for asset in assets if asset['name'] == package['filename']), None)
    if asset is None:
        url = release['upload_url'].split('{')[0] + '?name=' + urllib.parse.quote(package['filename'])
        asset = request(url, data, 'application/zip')
    assert asset['size'] == package['size']
    assert asset.get('digest') == digest
    print('Verified ' + tag + ': ' + asset['name'], flush=True)

with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
    summary.write('Published all five versioned releases and verified every asset SHA-256.\n')
