#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Pipeline trien khai SECMS (GDDB Thuong Tin) len IIS - Windows Server 2022.

.DESCRIPTION
  1) Dung site GDDB_TT_API / GDDB_TT_UI neu dang chay (tranh lock file)
  2) dotnet publish Secms.Api -> tao site IIS (neu chua co) + binding HTTP/HTTPS
  3) npm ci/install + npm run build FE -> tao site IIS (neu chua co) + binding HTTP/HTTPS
  4) Mo lai 2 site

.NOTES
  Yeu cau tren server:
  - IIS + ASP.NET Core Hosting Bundle (.NET 8)
  - URL Rewrite Module (cho SPA Vue)
  - Certificate Friendly Name: hndedu-cloudflare-origin (LocalMachine\My)
  - Node.js LTS, .NET 8 SDK

  Chay (PowerShell Admin):
    powershell -ExecutionPolicy Bypass -File .\release.ps1

  Doi hostname neu domain khac anh DNS:
    .\release.ps1 -ApiHostName 'gddb-thuong-tin-api.hndedu.com' -UiHostName 'gddb-thuong-tin.hndedu.com'
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = $PSScriptRoot,
    [string]$DeployRoot = 'C:\inetpub\gddb-thuong-tin',
    [string]$ApiSiteName = 'GDDB_TT_API',
    [string]$UiSiteName = 'GDDB_TT_UI',
    [string]$ApiHostName = 'gddb-thuong-tin-api.hndedu.com',
    [string]$UiHostName = 'gddb-thuong-tin.hndedu.com',
    [string]$CertFriendlyName = 'hndedu-cloudflare-origin',
    [string]$DotnetConfiguration = 'Release',
    [ValidateSet('win-x64', 'win-x86')]
    [string]$DotnetRuntime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-WebAdministration {
    Import-Module WebAdministration -ErrorAction Stop
}

function Test-IisSiteExists {
    param([string]$Name)
    return $null -ne (Get-Website -Name $Name -ErrorAction SilentlyContinue)
}

function Get-SiteState {
    param([string]$Name)
    $site = Get-Website -Name $Name -ErrorAction SilentlyContinue
    if (-not $site) { return $null }
    return [string]$site.State
}

function Stop-IisSiteSafe {
    param([string]$Name)
    if (-not (Test-IisSiteExists -Name $Name)) {
        Write-Host "  Site '$Name' chua ton tai - bo qua dung." -ForegroundColor DarkGray
        return
    }
    $state = Get-SiteState -Name $Name
    if ($state -eq 'Started') {
        Write-Host "  Dang dung site '$Name'..."
        Stop-Website -Name $Name
        Start-Sleep -Seconds 1
    }
    else {
        Write-Host "  Site '$Name' dang o trang thai $state - khong can dung." -ForegroundColor DarkGray
    }

    $poolName = (Get-Item "IIS:\Sites\$Name").applicationPool
    if ($poolName) {
        $pool = Get-WebAppPoolState -Name $poolName -ErrorAction SilentlyContinue
        if ($pool -and $pool.Value -eq 'Started') {
            Write-Host "  Dang dung AppPool '$poolName'..."
            Stop-WebAppPool -Name $poolName
            Start-Sleep -Seconds 1
        }
    }
}

function Start-IisSiteSafe {
    param([string]$Name)
    if (-not (Test-IisSiteExists -Name $Name)) {
        Write-Host "  Site '$Name' khong ton tai - bo qua mo." -ForegroundColor Yellow
        return
    }
    $poolName = (Get-Item "IIS:\Sites\$Name").applicationPool
    if ($poolName) {
        $pool = Get-WebAppPoolState -Name $poolName -ErrorAction SilentlyContinue
        if ($pool -and $pool.Value -ne 'Started') {
            Write-Host "  Dang khoi dong AppPool '$poolName'..."
            Start-WebAppPool -Name $poolName
        }
    }
    $state = Get-SiteState -Name $Name
    if ($state -ne 'Started') {
        Write-Host "  Dang khoi dong site '$Name'..."
        Start-Website -Name $Name
    }
    else {
        Write-Host "  Site '$Name' da dang chay." -ForegroundColor DarkGray
    }
}

function Get-OriginCertificate {
    param([string]$FriendlyName)
    $certs = @(Get-ChildItem -Path 'Cert:\LocalMachine\My' |
        Where-Object { $_.FriendlyName -eq $FriendlyName })
    if ($certs.Count -eq 0) {
        throw "Khong tim thay certificate FriendlyName='$FriendlyName' trong LocalMachine\My."
    }
    return ($certs | Sort-Object NotAfter -Descending | Select-Object -First 1)
}

function Ensure-AppPool {
    param(
        [string]$Name,
        [ValidateSet('NoManagedCode', 'v4.0')]
        [string]$ManagedRuntimeVersion = 'NoManagedCode'
    )
    if (-not (Test-Path "IIS:\AppPools\$Name")) {
        Write-Host "  Tao AppPool '$Name'..."
        New-WebAppPool -Name $Name | Out-Null
        Set-ItemProperty "IIS:\AppPools\$Name" -Name managedRuntimeVersion -Value $ManagedRuntimeVersion
        Set-ItemProperty "IIS:\AppPools\$Name" -Name managedPipelineMode -Value 'Integrated'
        Set-ItemProperty "IIS:\AppPools\$Name" -Name startMode -Value 'AlwaysRunning'
        Set-ItemProperty "IIS:\AppPools\$Name" -Name enable32BitAppOnWin64 -Value $false
    }
}

function Ensure-SiteWithBindings {
    param(
        [string]$SiteName,
        [string]$PhysicalPath,
        [string]$HostName,
        [string]$AppPoolName,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [ValidateSet('NoManagedCode', 'v4.0')]
        [string]$ManagedRuntimeVersion = 'NoManagedCode'
    )

    if (Test-IisSiteExists -Name $SiteName) {
        Write-Host "  Site '$SiteName' da co - bo qua tao site/binding." -ForegroundColor DarkGray
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
        return
    }

    Ensure-AppPool -Name $AppPoolName -ManagedRuntimeVersion $ManagedRuntimeVersion

    Write-Host "  Tao site '$SiteName' -> $PhysicalPath"
    New-Website -Name $SiteName `
        -PhysicalPath $PhysicalPath `
        -ApplicationPool $AppPoolName `
        -HostHeader $HostName `
        -Port 80 `
        -Force | Out-Null

    $httpsBindingInfo = "*:443:$HostName"
    $existingHttps = Get-WebBinding -Name $SiteName -Protocol 'https' -ErrorAction SilentlyContinue |
        Where-Object { $_.bindingInformation -eq $httpsBindingInfo }
    if (-not $existingHttps) {
        Write-Host "  Them binding https://$HostName (SNI) voi cert '$($Certificate.FriendlyName)'..."
        New-WebBinding -Name $SiteName -Protocol 'https' -Port 443 -HostHeader $HostName -SslFlags 1
    }

    $httpsBinding = Get-WebBinding -Name $SiteName -Protocol 'https' |
        Where-Object { $_.bindingInformation -eq $httpsBindingInfo } |
        Select-Object -First 1
    if (-not $httpsBinding) {
        throw "Khong lay duoc HTTPS binding cho site '$SiteName'."
    }
    $httpsBinding.AddSslCertificate($Certificate.Thumbprint, 'My')
    Write-Host "  Da gan cert thumbprint $($Certificate.Thumbprint)" -ForegroundColor Green
}

function Publish-Backend {
    param(
        [string]$ProjectPath,
        [string]$OutputPath
    )
    if (-not (Test-Path $ProjectPath)) {
        throw "Khong tim thay project: $ProjectPath"
    }
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $OutputPath 'logs') | Out-Null

    Write-Host "  dotnet publish -> $OutputPath"
    & dotnet publish $ProjectPath `
        -c $DotnetConfiguration `
        -r $DotnetRuntime `
        --self-contained false `
        -o $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish that bai (exit $LASTEXITCODE)."
    }
}

function Publish-Frontend {
    param(
        [string]$FrontendPath,
        [string]$OutputPath
    )
    if (-not (Test-Path $FrontendPath)) {
        throw "Khong tim thay frontend: $FrontendPath"
    }

    Push-Location $FrontendPath
    try {
        if (Test-Path 'package-lock.json') {
            Write-Host '  npm ci...'
            & npm ci
            if ($LASTEXITCODE -ne 0) {
                Write-Host '  npm ci that bai - fallback npm install...' -ForegroundColor Yellow
                & npm install
                if ($LASTEXITCODE -ne 0) { throw "npm install that bai (exit $LASTEXITCODE)." }
            }
        }
        else {
            Write-Host '  npm install...'
            & npm install
            if ($LASTEXITCODE -ne 0) { throw "npm install that bai (exit $LASTEXITCODE)." }
        }

        Write-Host '  npm run build...'
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build that bai (exit $LASTEXITCODE)." }
    }
    finally {
        Pop-Location
    }

    $dist = Join-Path $FrontendPath 'dist'
    if (-not (Test-Path $dist)) {
        throw "Khong tim thay thu muc build: $dist"
    }

    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    Write-Host "  Dong bo dist -> $OutputPath"
    & robocopy $dist $OutputPath /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy that bai (exit $LASTEXITCODE)."
    }
    $global:LASTEXITCODE = 0
}

# -------------------- main --------------------
$startedAt = Get-Date
Write-Host 'SECMS release pipeline' -ForegroundColor Green
Write-Host "Repo:   $RepoRoot"
Write-Host "Deploy: $DeployRoot"
Write-Host "API:    $ApiHostName  (site $ApiSiteName)"
Write-Host "UI:     $UiHostName   (site $UiSiteName)"

Ensure-WebAdministration

$apiProject = Join-Path $RepoRoot 'src\backend\Secms.Api\Secms.Api.csproj'
$frontendDir = Join-Path $RepoRoot 'src\frontend'
$apiDeploy = Join-Path $DeployRoot 'api'
$uiDeploy = Join-Path $DeployRoot 'ui'
$apiPool = "${ApiSiteName}_Pool"
$uiPool = "${UiSiteName}_Pool"

$cert = Get-OriginCertificate -FriendlyName $CertFriendlyName
Write-Host "Cert: $($cert.FriendlyName) ($($cert.Thumbprint)) het han $($cert.NotAfter.ToString('yyyy-MM-dd'))"

Write-Step '1/4 Dung site IIS (neu co) de tranh lock process'
Stop-IisSiteSafe -Name $ApiSiteName
Stop-IisSiteSafe -Name $UiSiteName

Write-Step '2/4 Trien khai Secms.Api (dotnet publish)'
Publish-Backend -ProjectPath $apiProject -OutputPath $apiDeploy
Ensure-SiteWithBindings `
    -SiteName $ApiSiteName `
    -PhysicalPath $apiDeploy `
    -HostName $ApiHostName `
    -AppPoolName $apiPool `
    -Certificate $cert `
    -ManagedRuntimeVersion 'NoManagedCode'

Write-Step '3/4 Trien khai Frontend (npm install + build)'
Publish-Frontend -FrontendPath $frontendDir -OutputPath $uiDeploy
Ensure-SiteWithBindings `
    -SiteName $UiSiteName `
    -PhysicalPath $uiDeploy `
    -HostName $UiHostName `
    -AppPoolName $uiPool `
    -Certificate $cert `
    -ManagedRuntimeVersion 'NoManagedCode'

Write-Step '4/4 Mo lai site IIS'
Start-IisSiteSafe -Name $ApiSiteName
Start-IisSiteSafe -Name $UiSiteName

$elapsed = (Get-Date) - $startedAt
Write-Host ''
Write-Host "Hoan tat trong $([int]$elapsed.TotalSeconds)s." -ForegroundColor Green
Write-Host "  API: https://$ApiHostName/swagger"
Write-Host "  UI:  https://$UiHostName/"
