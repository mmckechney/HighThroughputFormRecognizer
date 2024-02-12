#az login
param
(
	[Parameter(Mandatory=$true)]
    [string] $appName,
	[Parameter(Mandatory=$true)]
	[string] $location,
	[string] $myPublicIp, 
	[ValidateRange(1, 10)]
	[int] $docIntelligenceInstanceCount = 1,
	[bool] $codeDeployOnly = $false
)

$resourceGroupName ="rg-$appName-demo-$location"
$funcProcess = "fcn-$($appName)Process-demo-$location"
$funcMove = "fcn-$($appName)Mover-demo-$location"
$funcQueue = "fcn-$($appName)Queue-demo-$location"



$error.Clear()
$ErrorActionPreference = 'Stop'

Write-Host "Getting pulic IP" -ForegroundColor DarkCyan
if([string]::IsNullOrWhiteSpace($myPublicIp))
{
    $myPublicIp = (Invoke-WebRequest https://api.ipify.org?format=text).Content.Trim()
	Write-Host "Public IP: $myPublicIp" -ForegroundColor Green
}

Write-Host "Getting current user object id" -ForegroundColor DarkCyan
$currentUserObjectId = az ad signed-in-user show -o tsv --query id
Write-Host "Current User Object Id: $currentUserObjectId" -ForegroundColor Green

if($codeDeployOnly -eq $false)
{
	Write-Host "Deploying resources to Azure" -ForegroundColor DarkCyan
	az deployment sub create --location $location  --template-file ./infra/main.bicep `
		--parameters `
		location=$location `
		appName=$appName `
		myPublicIp=$myPublicIp `
		docIntelligenceInstanceCount=$docIntelligenceInstanceCount `
		currentUserObjectId=$currentUserObjectId 
}

if(!$?){ exit }


###########################
## Code Deployment
###########################

$scriptDir = Split-Path $script:MyInvocation.MyCommand.Path
dotnet clean -c release
dotnet clean -c debug

Write-Host "Deploying Form Processor Function App" -ForegroundColor DarkCyan
dotnet clean "./FormProcessorFunction/" 
dotnet publish "./FormProcessorFunction/"
$source = Join-Path -Path $scriptDir -ChildPath "./FormProcessorFunction/bin/Release/net8.0/publish"
$zip = $scriptDir + "build.zip"
if(Test-Path $zip) { Remove-Item $zip }
[io.compression.zipfile]::CreateFromDirectory($source,$zip)
az webapp deploy --name $funcProcess --resource-group $resourceGroupName --src-path $zip --type zip


Write-Host "Deploying File Mover Function App" -ForegroundColor DarkCyan
dotnet publish "./ProcessedFileMover/"
$source = Join-Path -Path $scriptDir -ChildPath "./ProcessedFileMover/bin/Release/net8.0/publish"
if(Test-Path $zip) { Remove-Item $zip }
[io.compression.zipfile]::CreateFromDirectory($source,$zip)
az webapp deploy --name $funcMove --resource-group $resourceGroupName --src-path $zip --type zip


Write-Host "Deploying File Queue Function App" -ForegroundColor DarkCyan
dotnet publish "./FormQueueFunction/"
$source = Join-Path -Path $scriptDir -ChildPath "./FormQueueFunction/bin/Release/net8.0/publish"
if(Test-Path $zip) { Remove-Item $zip }
[io.compression.zipfile]::CreateFromDirectory($source,$zip)
az webapp deploy --name $funcQueue --resource-group $resourceGroupName --src-path $zip --type zip

