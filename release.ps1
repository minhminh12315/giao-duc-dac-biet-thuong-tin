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
    [string]$RepoRoot = '',
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

# PSScriptRoot co the rong neu script khong chay bang -File / bi goi qua runner.
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $RepoRoot = $PSScriptRoot
    }
    elseif (-not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
        $RepoRoot = Split-Path -Parent -Path $PSCommandPath
    }
    elseif (-not [string]::IsNullOrWhiteSpace($MyInvocation.MyCommand.Path)) {
        $RepoRoot = Split-Path -Parent -Path $MyInvocation.MyCommand.Path
    }
    elseif (-not [string]::IsNullOrWhiteSpace($MyInvocation.MyCommand.Definition)) {
        $def = $MyInvocation.MyCommand.Definition
        if (Test-Path -LiteralPath $def) {
            $RepoRoot = Split-Path -Parent -Path $def
        }
    }
}
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Get-Location).Path
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

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
    }
    # OnDemand on dinh hon AlwaysRunning (AlwaysRunning can Application Initialization).
    Set-ItemProperty "IIS:\AppPools\$Name" -Name managedRuntimeVersion -Value $ManagedRuntimeVersion
    Set-ItemProperty "IIS:\AppPools\$Name" -Name managedPipelineMode -Value 'Integrated'
    Set-ItemProperty "IIS:\AppPools\$Name" -Name startMode -Value 'OnDemand'
    Set-ItemProperty "IIS:\AppPools\$Name" -Name enable32BitAppOnWin64 -Value $false
    Set-ItemProperty "IIS:\AppPools\$Name" -Name processModel.identityType -Value 'ApplicationPoolIdentity'
    # Tranh khoa pool sau 1-2 lan crash (thuong gap sau deploy).
    Set-ItemProperty "IIS:\AppPools\$Name" -Name failure.rapidFailProtection -Value $true
    Set-ItemProperty "IIS:\AppPools\$Name" -Name failure.rapidFailProtectionMaxCrashes -Value 5
    Set-ItemProperty "IIS:\AppPools\$Name" -Name recycling.periodicRestart.time -Value ([TimeSpan]::FromHours(0))
}

function Set-DeployAcl {
    param(
        [string]$Path,
        [string]$AppPoolName
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
    $acl = Get-Acl -LiteralPath $Path
    $rules = @(
        (New-Object System.Security.AccessControl.FileSystemAccessRule(
            'IIS_IUSRS', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')),
        (New-Object System.Security.AccessControl.FileSystemAccessRule(
            "IIS AppPool\$AppPoolName", 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    )
    # API can ghi logs + App_Data/uploads
    if ($AppPoolName -like '*API*') {
        $rules += New-Object System.Security.AccessControl.FileSystemAccessRule(
            "IIS AppPool\$AppPoolName", 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    }
    foreach ($rule in $rules) {
        $acl.SetAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
    Write-Host "  ACL OK: $Path ($AppPoolName)"
}

function Ensure-DefaultDocument {
    param([string]$SiteName)
    $path = "IIS:\Sites\$SiteName"
    if (-not (Test-Path $path)) { return }
    try {
        $files = Get-WebConfigurationProperty -PSPath $path -Filter 'system.webServer/defaultDocument/files' -Name '.' -ErrorAction Stop
        $existing = @()
        if ($files -and $files.Collection) {
            $existing = @($files.Collection | ForEach-Object { $_.value })
        }
        if ($existing -notcontains 'index.html') {
            Add-WebConfigurationProperty -PSPath $path `
                -Filter 'system.webServer/defaultDocument/files' `
                -Name '.' `
                -Value @{ value = 'index.html' }
            Write-Host "  Them defaultDocument index.html cho '$SiteName'"
        }
    }
    catch {
        Write-Host "  Bo qua defaultDocument: $($_.Exception.Message)" -ForegroundColor DarkGray
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
        # Neu pool bi khoa (503), recycle/start lai.
        $pool = Get-WebAppPoolState -Name $poolName -ErrorAction SilentlyContinue
        if ($pool -and $pool.Value -eq 'Stopped') {
            Write-Host "  Dang khoi dong AppPool '$poolName'..."
            try {
                Start-WebAppPool -Name $poolName
            }
            catch {
                Write-Host "  Start pool that bai, thu recycle..." -ForegroundColor Yellow
                Restart-WebAppPool -Name $poolName -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 1
                Start-WebAppPool -Name $poolName
            }
            Start-Sleep -Seconds 2
        }
    }
    $state = Get-SiteState -Name $Name
    if ($state -ne 'Started') {
        Write-Host "  Dang khoi dong site '$Name'..."
        Start-Website -Name $Name
        Start-Sleep -Seconds 1
    }
    else {
        Write-Host "  Site '$Name' da dang chay." -ForegroundColor DarkGray
    }

    $poolState = if ($poolName) { (Get-WebAppPoolState -Name $poolName).Value } else { 'N/A' }
    $siteState = Get-SiteState -Name $Name
    Write-Host "  Trang thai: site=$siteState, pool=$poolState"
    if ($siteState -ne 'Started' -or ($poolName -and $poolState -ne 'Started')) {
        Write-Host "  CANH BAO: '$Name' chua chay - day la nguyen nhan 503." -ForegroundColor Red
        Write-Host "  Kiem tra Event Viewer > Windows Logs > Application (IIS / ASP.NET Core)." -ForegroundColor Yellow
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

    Ensure-AppPool -Name $AppPoolName -ManagedRuntimeVersion $ManagedRuntimeVersion
    Set-DeployAcl -Path $PhysicalPath -AppPoolName $AppPoolName

    if (Test-IisSiteExists -Name $SiteName) {
        Write-Host "  Site '$SiteName' da co - cap nhat physicalPath + AppPool." -ForegroundColor DarkGray
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
        Ensure-DefaultDocument -SiteName $SiteName
        return
    }

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
    Ensure-DefaultDocument -SiteName $SiteName
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

    $exe = Join-Path $OutputPath 'Secms.Api.exe'
    $dll = Join-Path $OutputPath 'Secms.Api.dll'
    $webConfig = Join-Path $OutputPath 'web.config'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Thieu Secms.Api.exe sau publish. IIS can apphost (.exe), khong chi .dll."
    }
    if (-not (Test-Path -LiteralPath $dll)) {
        throw "Thieu Secms.Api.dll sau publish."
    }
    if (-not (Test-Path -LiteralPath $webConfig)) {
        throw "Thieu web.config sau publish."
    }
    Write-Host "  OK: Secms.Api.exe + web.config"
}

function Test-UrlRewriteInstalled {
    $dll = Join-Path $env:SystemRoot 'System32\inetsrv\rewrite.dll'
    return (Test-Path -LiteralPath $dll)
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

    # Thieu URL Rewrite -> web.config co <rewrite> gay loi cau hinh site (thuong 500.19, co the keo 503).
    $uiWebConfig = Join-Path $OutputPath 'web.config'
    if ((Test-Path -LiteralPath $uiWebConfig) -and -not (Test-UrlRewriteInstalled)) {
        Write-Host '  CANH BAO: chua cai URL Rewrite Module - go section rewrite trong web.config FE.' -ForegroundColor Yellow
        [xml]$xml = Get-Content -LiteralPath $uiWebConfig -Raw
        $rewrite = $xml.SelectSingleNode('//rewrite')
        if ($rewrite -and $rewrite.ParentNode) {
            [void]$rewrite.ParentNode.RemoveChild($rewrite)
            $xml.Save($uiWebConfig)
        }
    }

    if (-not (Test-Path (Join-Path $OutputPath 'index.html'))) {
        throw "Thieu index.html trong thu muc UI deploy."
    }
}

function Assert-AspNetCoreHostingBundle {
    $candidates = @(
        "${env:ProgramFiles}\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll",
        "${env:ProgramFiles(x86)}\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    )
    $found = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $found) {
        throw @"
Chua cai ASP.NET Core Hosting Bundle (.NET 8).
Native (`dotnet run`) van chay duoc, nhung IIS se 503/500.30.
Tai: https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer
Sau khi cai xong: iisreset
"@
    }
    Write-Host "  AspNetCore Module V2: $found"
}

function Test-LocalIisSmoke {
    param(
        [string]$HostName,
        [string]$Path = '/',
        [int]$Port = 80
    )
    try {
        $url = "http://127.0.0.1:$Port$Path"
        Write-Host "  Smoke $url (Host: $HostName) ..."
        $resp = Invoke-WebRequest -Uri $url -Headers @{ Host = $HostName } -UseBasicParsing -TimeoutSec 20
        Write-Host "  -> HTTP $($resp.StatusCode)" -ForegroundColor Green
    }
    catch {
        $status = $null
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        Write-Host "  -> THAT BAI status=$status : $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Goi y: xem Event Viewer / $DeployRoot\api\logs\stdout_*.log" -ForegroundColor Yellow
    }
}

# -------------------- main --------------------
$startedAt = Get-Date
Write-Host 'SECMS release pipeline' -ForegroundColor Green
Write-Host "Repo:   $RepoRoot"
Write-Host "Deploy: $DeployRoot"
Write-Host "API:    $ApiHostName  (site $ApiSiteName)"
Write-Host "UI:     $UiHostName   (site $UiSiteName)"

Ensure-WebAdministration
Assert-AspNetCoreHostingBundle
if (-not (Test-UrlRewriteInstalled)) {
    Write-Host 'CANH BAO: URL Rewrite Module chua cai - SPA deep-link co the 404 (trang chu van mo duoc).' -ForegroundColor Yellow
    Write-Host '  Tai: https://www.iis.net/downloads/microsoft/url-rewrite' -ForegroundColor Yellow
}

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

Write-Step '4/4 Mo lai site IIS + smoke local'
Start-IisSiteSafe -Name $ApiSiteName
Start-IisSiteSafe -Name $UiSiteName
Start-Sleep -Seconds 2
Test-LocalIisSmoke -HostName $UiHostName -Path '/'
Test-LocalIisSmoke -HostName $ApiHostName -Path '/swagger/index.html'

$elapsed = (Get-Date) - $startedAt
Write-Host ''
Write-Host "Hoan tat trong $([int]$elapsed.TotalSeconds)s." -ForegroundColor Green
Write-Host "  API: https://$ApiHostName/swagger"
Write-Host "  UI:  https://$UiHostName/"
Write-Host "  Neu local smoke OK ma domain van 503: kiem tra Cloudflare (SSL Full, proxy orange cloud, IP origin)."
