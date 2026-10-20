$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path (Join-Path $PSScriptRoot '../../../../../../../..')).Path
$uvCommand=Get-Command uv -ErrorAction SilentlyContinue
if($null -eq $uvCommand){throw 'Install the free uv Python package manager first. This script uses only a project-local Python environment.'}
$runtimeFolder=Join-Path $projectPath 'Temp/MLAgents/Python'
$environmentFolder=Join-Path $projectPath 'Temp/MLAgents/env'
& $uvCommand.Source python install 3.10.12 --install-dir $runtimeFolder --no-managed-python
if($LASTEXITCODE -ne 0){throw 'Python download failed'}
$runtimePython=Join-Path $runtimeFolder 'cpython-3.10.12-windows-x86_64-none/python.exe'
$environmentPython=Join-Path $environmentFolder 'Scripts/python.exe'
if(!(Test-Path -LiteralPath $environmentPython)){
    & $uvCommand.Source venv --python $runtimePython $environmentFolder
    if($LASTEXITCODE -ne 0){throw 'Project-local environment creation failed'}
}
& $environmentPython -c "import sys; assert sys.version_info[:3] == (3,10,12), 'Existing environment preserved: Python version differs'"
if($LASTEXITCODE -ne 0){throw 'Existing environment version mismatch; nothing was deleted'}
& $uvCommand.Source pip install --python $environmentPython torch==2.2.2 --index-url https://download.pytorch.org/whl/cu121 --link-mode copy
if($LASTEXITCODE -ne 0){throw 'CUDA PyTorch install failed'}
& $uvCommand.Source pip install --python $environmentPython -r (Join-Path $PSScriptRoot 'requirements.txt') --link-mode copy
if($LASTEXITCODE -ne 0){throw 'ML-Agents install failed'}
& $environmentPython -c "import torch; assert torch.cuda.is_available(), 'CUDA GPU unavailable'; print(torch.__version__, torch.cuda.get_device_name(0))"
if($LASTEXITCODE -ne 0){throw 'CUDA runtime verification failed'}
