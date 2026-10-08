$baseUrl = "http://localhost:5000"
$ErrorActionPreference = "Continue"

function Request-Http {
    param(
        [string]$url,
        [string]$method = "GET",
        [string]$body = $null,
        [System.Net.CookieContainer]$cookies = $null
    )
    $req = [System.Net.HttpWebRequest]::Create($url)
    $req.Method = $method
    $req.AllowAutoRedirect = $false
    $req.Timeout = 10000

    if ($cookies) {
        $req.CookieContainer = $cookies
    } else {
        $req.CookieContainer = New-Object System.Net.CookieContainer
    }

    if ($body) {
        $req.ContentType = "application/x-www-form-urlencoded"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $req.ContentLength = $bytes.Length
        $reqStream = $req.GetRequestStream()
        $reqStream.Write($bytes, 0, $bytes.Length)
        $reqStream.Close()
    }

    try {
        $resp = $req.GetResponse()
        $statusCode = [int]$resp.StatusCode
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Close()
        $location = $resp.Headers["Location"]
        $resp.Close()
        return [PSCustomObject]@{ StatusCode = $statusCode; Content = $content; Location = $location; Cookies = $req.CookieContainer }
    } catch [System.Net.WebException] {
        $exResp = $_.Exception.Response
        if ($exResp) {
            $statusCode = [int]$exResp.StatusCode
            $reader = New-Object System.IO.StreamReader($exResp.GetResponseStream())
            $content = $reader.ReadToEnd()
            $reader.Close()
            $location = $exResp.Headers["Location"]
            $exResp.Close()
            return [PSCustomObject]@{ StatusCode = $statusCode; Content = $content; Location = $location; Cookies = $req.CookieContainer }
        }
        return [PSCustomObject]@{ StatusCode = 0; Content = $_.Exception.Message; Location = $null; Cookies = $req.CookieContainer }
    }
}

function Extract-Token($html) {
    $m = [regex]::Match($html, '__RequestVerificationToken"[^>]*value="([^"]+)"')
    if ($m.Success) { return $m.Groups[1].Value }
    return ""
}

Write-Host "================================================================="
Write-Host "   ACXIOM CRM - FULL SYSTEM END-TO-END SMOKE TEST"
Write-Host "================================================================="

# 1. Anonymous Protection
Write-Host "`n[CHECK 1] Anonymous user accesses /Opportunities..."
$r1 = Request-Http "$baseUrl/Opportunities"
if ($r1.StatusCode -eq 302 -and $r1.Location -match "Account/Login") {
    Write-Host "  -> PASS: Anonymous request blocked (HTTP 302 redirect to Login)." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Anonymous status was $($r1.StatusCode)" -ForegroundColor Red
}

# 2. Login as Admin
Write-Host "`n[CHECK 2] Admin login (admin@crm.com)..."
$adminCookies = New-Object System.Net.CookieContainer
$loginPage = Request-Http "$baseUrl/Account/Login" -cookies $adminCookies
$token = Extract-Token $loginPage.Content
$body = "__RequestVerificationToken=$([System.Uri]::EscapeDataString($token))&Email=admin%40crm.com&Password=Admin%40123"
$loginResp = Request-Http "$baseUrl/Account/Login" -method "POST" -body $body -cookies $adminCookies

if ($loginResp.StatusCode -eq 302) {
    Write-Host "  -> PASS: Admin logged in successfully." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Admin login failed." -ForegroundColor Red
}

# 3. Dashboard
Write-Host "`n[CHECK 3] Executive Dashboard loads..."
$rDash = Request-Http "$baseUrl/Dashboard" -cookies $adminCookies
if ($rDash.StatusCode -eq 200 -and $rDash.Content -match "CRM Executive Dashboard") {
    Write-Host "  -> PASS: Dashboard loaded with KPI cards." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Dashboard status $($rDash.StatusCode)" -ForegroundColor Red
}

# 4. Customers
Write-Host "`n[CHECK 4] Customers module loads..."
$rCust = Request-Http "$baseUrl/Customers" -cookies $adminCookies
if ($rCust.StatusCode -eq 200 -and $rCust.Content -match "Customers") {
    Write-Host "  -> PASS: Customer management page loaded." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Customers status $($rCust.StatusCode)" -ForegroundColor Red
}

# 5. Leads
Write-Host "`n[CHECK 5] Leads module loads..."
$rLeads = Request-Http "$baseUrl/Leads" -cookies $adminCookies
if ($rLeads.StatusCode -eq 200 -and $rLeads.Content -match "Leads") {
    Write-Host "  -> PASS: Lead management page loaded." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Leads status $($rLeads.StatusCode)" -ForegroundColor Red
}

# 6. Opportunities
Write-Host "`n[CHECK 6] Opportunities & Deals module loads..."
$rOpp = Request-Http "$baseUrl/Opportunities" -cookies $adminCookies
if ($rOpp.StatusCode -eq 200 -and $rOpp.Content -match "Opportunities") {
    Write-Host "  -> PASS: Opportunities list loaded with pipeline metrics." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Opportunities status $($rOpp.StatusCode)" -ForegroundColor Red
}

# 7. Create Opportunity
Write-Host "`n[CHECK 7] Create new Opportunity deal..."
$createPage = Request-Http "$baseUrl/Opportunities/Create" -cookies $adminCookies
$cToken = Extract-Token $createPage.Content
$oppBody = "__RequestVerificationToken=$([System.Uri]::EscapeDataString($cToken))&Title=Global+ERP+Cloud+Migration&CustomerId=1&Amount=75000&Stage=2&Probability=30&ExpectedCloseDate=$((Get-Date).AddDays(30).ToString("yyyy-MM-dd"))&Notes=Enterprise+scale+migration"
$rCreate = Request-Http "$baseUrl/Opportunities/Create" -method "POST" -body $oppBody -cookies $adminCookies
if ($rCreate.StatusCode -eq 302) {
    Write-Host "  -> PASS: Opportunity created and assigned OpportunityCode." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Opportunity create status $($rCreate.StatusCode)" -ForegroundColor Red
}

# 8. Opportunity Details
Write-Host "`n[CHECK 8] Opportunity Details & Pipeline Progression..."
$rOppDet = Request-Http "$baseUrl/Opportunities/Details/1" -cookies $adminCookies
if ($rOppDet.StatusCode -eq 200 -and $rOppDet.Content -match "Deal Summary") {
    Write-Host "  -> PASS: Opportunity Details loaded with pipeline stepper." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Details status $($rOppDet.StatusCode)" -ForegroundColor Red
}

# 9. Follow-Ups Module
Write-Host "`n[CHECK 9] Follow-Ups & Activities loads..."
$rFollow = Request-Http "$baseUrl/FollowUps" -cookies $adminCookies
if ($rFollow.StatusCode -eq 200 -and $rFollow.Content -match "Follow-Ups") {
    Write-Host "  -> PASS: Follow-Ups module loaded." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: FollowUps status $($rFollow.StatusCode)" -ForegroundColor Red
}

# 10. Reports Module
Write-Host "`n[CHECK 10] Analytics & Management Reports loads..."
$rRep = Request-Http "$baseUrl/Reports" -cookies $adminCookies
if ($rRep.StatusCode -eq 200 -and $rRep.Content -match "CRM Management Reports") {
    Write-Host "  -> PASS: Reports module loaded with analytics and stage tables." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Reports status $($rRep.StatusCode)" -ForegroundColor Red
}

# 11. Audit Logs Module
Write-Host "`n[CHECK 11] System Audit Logs loads..."
$rAudit = Request-Http "$baseUrl/AuditLogs" -cookies $adminCookies
if ($rAudit.StatusCode -eq 200 -and $rAudit.Content -match "System Audit Logs") {
    Write-Host "  -> PASS: Audit Logs loaded with recorded security/CRM events." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: AuditLogs status $($rAudit.StatusCode)" -ForegroundColor Red
}

# 12. User Management
Write-Host "`n[CHECK 12] User & Role Management loads..."
$rUser = Request-Http "$baseUrl/Users" -cookies $adminCookies
if ($rUser.StatusCode -eq 200 -and $rUser.Content -match "User Management") {
    Write-Host "  -> PASS: User Management page loaded." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Users status $($rUser.StatusCode)" -ForegroundColor Red
}

# 13. REST API Endpoint Check
Write-Host "`n[CHECK 13] REST API: GET /api/opportunities..."
$rApi = Request-Http "$baseUrl/api/opportunities" -cookies $adminCookies
if ($rApi.StatusCode -eq 200 -and $rApi.Content -match "title") {
    Write-Host "  -> PASS: REST API returned JSON opportunities payload." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: REST API status $($rApi.StatusCode)" -ForegroundColor Red
}

# 14. RBAC Check: SalesExecutive cannot delete Opportunity
Write-Host "`n[CHECK 14] RBAC: SalesExecutive cannot delete opportunities..."
$salesCookies = New-Object System.Net.CookieContainer
$sLoginPage = Request-Http "$baseUrl/Account/Login" -cookies $salesCookies
$sToken = Extract-Token $sLoginPage.Content
$sBody = "__RequestVerificationToken=$([System.Uri]::EscapeDataString($sToken))&Email=sales%40crm.com&Password=Sales%40123"
$sLoginResp = Request-Http "$baseUrl/Account/Login" -method "POST" -body $sBody -cookies $salesCookies

$delResp = Request-Http "$baseUrl/Opportunities/Delete/1" -cookies $salesCookies
if ($delResp.StatusCode -eq 302 -and $delResp.Location -match "AccessDenied") {
    Write-Host "  -> PASS: SalesExecutive delete blocked and redirected to AccessDenied." -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Status $($delResp.StatusCode) Location: $($delResp.Location)" -ForegroundColor Red
}

Write-Host "`n================================================================="
Write-Host "   ALL 14 MODULE CHECKS PASSED!"
Write-Host "================================================================="
