"""Build independent desktop pets from one shared core; no image generation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parent
PETS = ROOT
CORE = ROOT / 'core'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def read_config(path):
    cfg = json.loads(path.read_text(encoding='utf-8-sig'))
    if not re.fullmatch(r'[A-Za-z][A-Za-z0-9]{2,60}', cfg['app_id']):
        raise ValueError('Invalid app_id: ' + str(path))
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+', cfg['version']):
        raise ValueError('Version must have four numeric components')
    for key in ['display_name', 'short_name', 'install_title']:
        if not isinstance(cfg[key], str) or not cfg[key].strip() or any(c in cfg[key] for c in '\r\n\\"<>:&/|?*'):
            raise ValueError('Invalid name field: ' + key)
    if type(cfg['pixel']) is not bool:
        raise ValueError('pixel must be a boolean')
    if len(cfg['colors']) != 4 or any(len(rgb) != 3 or any(type(x) is not int or not 0 <= x <= 255 for x in rgb) for rgb in cfg['colors']):
        raise ValueError('colors must contain four RGB triplets')
    return cfg

def build(path, cfg, atlas_override=None):
    project = path.parent
    atlas = Path(atlas_override).resolve() if atlas_override else (project / cfg['atlas']).resolve()
    # Both approved pets use v2 8x11 atlases, 192x208 per cell.
    import struct
    with atlas.open('rb') as f:
        header = f.read(24)
    if header[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', header[16:24]) != (1536, 2288):
        raise ValueError('Expected a validated 1536x2288 PNG atlas: ' + str(atlas))
    validation = project / 'assets/final/validation-extended.json'
    if validation.exists() and not json.loads(validation.read_text(encoding='utf-8-sig')).get('ok'):
        raise ValueError('Artwork validation failed')
    version = '.'.join(cfg['version'].split('.')[:3])
    output = project / ('release-v' + version)
    generated = ROOT / 'build' / cfg['app_id']
    qa = generated / 'qa'
    output.mkdir(exist_ok=True)
    qa.mkdir(parents=True, exist_ok=True)
    tokens = {key.upper(): str(value) for key, value in cfg.items() if isinstance(value, str)}
    tokens['PIXEL'] = str(cfg['pixel']).lower()
    for key, value in zip(['INK', 'PANEL', 'ACCENT', 'PAPER'], cfg['colors']):
        tokens[key] = ','.join(map(str, value))
    for template in sorted(CORE.glob('*.in')):
        source = template.read_text(encoding='utf-8-sig')
        source = re.sub(r'@@([A-Z_]+)@@', lambda match: tokens[match[1]], source)
        (generated / template.stem).write_text(source, encoding='utf-8-sig')
    compiler = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    exe = output / (cfg['app_id'] + '.exe')
    cmd = [str(compiler), '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/out:' + str(exe),
           '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:Microsoft.CSharp.dll',
           '/resource:' + str(atlas) + ',pet.png', '/win32manifest:' + str(generated / 'app.manifest')]
    cmd += [str(generated / name) for name in ['Pet.cs', 'InfoWindow.cs', 'PetTheme.cs']]
    cmd += [str(CORE / 'WindowPolicy.cs')]
    subprocess.run(cmd, check=True)
    # Tests run against QA-only settings; never install or alter the user's existing app.
    result = qa / 'self-test.txt'
    result.write_text('RUNNING', encoding='utf-8')
    subprocess.run([str(exe), '--self-test', str(result)], check=True, timeout=45,
                   creationflags=subprocess.CREATE_NO_WINDOW)
    if not result.read_text(encoding='utf-8-sig').startswith('PASS:'):
        raise RuntimeError('Self-test did not pass: ' + str(result))
    setup = output / (cfg['app_id'] + '-Setup.exe')
    setup.write_bytes(exe.read_bytes())
    instructions = (project / '使用说明.txt').read_text(encoding='utf-8-sig')
    instructions += '\n\n共用核心版本\n本软件独立安装和运行，与其他角色使用不同的安装目录、进程标识和个人设置。\n置顶和全屏避让统一采用粉团已修复的实现：开启置顶及全屏自动隐藏后，前台全屏应用触发避让；退出全屏后恢复。\n真实游戏兼容性仍需实际测试。更新前请从托盘退出旧版。\n'
    (output / '使用说明.txt').write_text(instructions, encoding='utf-8-sig')
    source_hashes = {p.name: digest(p) for p in sorted(CORE.iterdir()) if p.is_file()}
    record = dict(app_id=cfg['app_id'], version=cfg['version'], core=source_hashes,
                  config_sha256=digest(path), atlas_sha256=digest(atlas), exe_sha256=digest(exe),
                  tests='PASS', real_game_tested=False)
    (output / '版本信息.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
    archive = project / (cfg['display_name'] + '-Windows-v' + version + '.zip')
    files = [exe, setup, output / '使用说明.txt', output / '版本信息.json']
    with ZipFile(archive, 'w', ZIP_DEFLATED) as z:
        for file in files:
            z.write(file, file.name)
    with ZipFile(archive) as z:
        assert z.testzip() is None
        assert z.namelist() == [f.name for f in files]
        assert all(z.read(f.name) == f.read_bytes() for f in files)
    print('PASS: ' + str(archive), flush=True)
    return dict(archive=str(archive), sha256=digest(archive), **record)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', help='Project folder or pet.config.json; omit to build all registered pets')
    parser.add_argument('--atlas', help='Optional atlas override; requires --project')
    args = parser.parse_args()
    if args.atlas and not args.project:
        parser.error('--atlas requires --project')
    paths = sorted(PETS.glob('*/pet.config.json'))
    configs = {p.resolve(): read_config(p) for p in paths}
    ids = [c['app_id'].lower() for c in configs.values()]
    names = [c['display_name'].lower() for c in configs.values()]
    if len(set(ids)) != len(ids) or len(set(names)) != len(names):
        raise ValueError('Each pet must have a unique app_id and display_name')
    if args.project:
        selected = Path(args.project).resolve()
        if selected.is_dir():
            selected /= 'pet.config.json'
        if selected not in configs:
            raise ValueError('Register the project under ' + str(PETS))
        configs = {selected: configs[selected]}
    if not configs:
        raise ValueError('No pet configurations found')
    results = [build(path, cfg, args.atlas) for path, cfg in configs.items()]
    (ROOT / 'last-build.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')

if __name__ == '__main__':
    main()
