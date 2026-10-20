param([string]$RunId='helper_ppo_01',[switch]$Resume)
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path (Join-Path $PSScriptRoot '../../../../../../../..')).Path
$pythonPath=Join-Path $projectPath 'Temp/MLAgents/env/Scripts/python.exe'
$buildPath=Join-Path $projectPath 'Temp/MLAgents/Build/HelperTraining.exe'
if(!(Test-Path -LiteralPath $pythonPath)){throw 'Project-only Python environment missing. Install Training~/requirements.txt with Python 3.10.12 and CUDA PyTorch 2.2.2+cu121.'}
if(!(Test-Path -LiteralPath $buildPath)){throw 'Build the helper training player using HelperTrainingBuilder first.'}
$trainArgs=@('-m','mlagents.trainers.learn',(Join-Path $PSScriptRoot 'helper_ppo.yaml'),'--env',$buildPath,'--run-id',$RunId,'--results-dir',(Join-Path $PSScriptRoot 'results'),'--torch-device','cuda','--no-graphics','--seed','1417')
if($Resume){$trainArgs+='--resume'}
& $pythonPath @trainArgs
exit $LASTEXITCODE
