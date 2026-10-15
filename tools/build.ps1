param(
 [string]$Unity='C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe',
 [string]$Blender='C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
 [string]$Python='python',
 [switch]$SkipModels
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $SkipModels){
 & $Blender --background --python (Join-Path $projectRoot 'Blender\build_clinic.py')
 if($LASTEXITCODE -ne 0){throw 'Blender model export failed'}
 # patient and gloved hands: needs the MPFB extension + makehuman_system_assets (CC0) installed in Blender
 & $Blender --background --python (Join-Path $projectRoot 'Blender\build_people.py')
 if($LASTEXITCODE -ne 0){throw 'MPFB people export failed'}
}
& $Python (Join-Path $PSScriptRoot 'subset_font.py')
if($LASTEXITCODE -ne 0){throw 'Font subset failed (install fonttools)'}
$unityArgs=@('-batchmode','-nographics','-projectPath',('"'+(Join-Path $projectRoot 'UnityProject')+'"'),'-executeMethod','ClinicBuild.Build','-buildTarget','WebGL','-quit','-logFile',('"'+(Join-Path $projectRoot 'Reports\unity-build.log')+'"'))
$buildProcess=Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru -Wait -WindowStyle Hidden
if($buildProcess.ExitCode -ne 0){throw 'Unity build failed; see Reports\unity-build.log'}
& node (Join-Path $PSScriptRoot 'verify-site.mjs')
if($LASTEXITCODE -ne 0){throw 'Web deployment preflight failed'}
