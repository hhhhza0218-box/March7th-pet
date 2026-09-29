param([string]$Project, [string]$Atlas, [string]$Python = $env:PET_WORKSHOP_PYTHON)
$ErrorActionPreference = 'Stop'
if (!$Python) { $Python = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' }
if (!(Test-Path -LiteralPath $Python)) { throw 'Set PET_WORKSHOP_PYTHON or pass -Python with a Python 3 executable. End users do not need Python.' }
$buildArgs = @((Join-Path $PSScriptRoot 'build.py'))
if ($Project) { $buildArgs += @('--project', $Project) }
if ($Atlas) { $buildArgs += @('--atlas', $Atlas) }
& $Python @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build or verification failed. See the output above.' }
