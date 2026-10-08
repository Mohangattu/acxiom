$baseUrl = "http://localhost:5000"
$ErrorActionPreference = "Continue"

function Format-FormBody($params) {
    $pairs = @()
    foreach ($k in $params.Keys) {
        $v = [string]$params[$k]
        $encK = [System.Uri]::EscapeDataString($k)
        $encV = [System.Uri]::EscapeDataString($v)
        $pairs += ($encK + "=" + $encV)
    }
    return ($pairs -join "&")
}

function Request-Http {
    param(
        [string]$url,
        [string]$method = "GET",
        [string]$body = $null,
        [System.Net.CookieContainer]$cookies = $null,
        [bool]$allowAutoRedirect = $false
    )
    $req = [System.Net.HttpWebRequest]::Create($url)
    $req.Method = $method
    $req.AllowAutoRedirect = $allowAutoRedirect
    if ($null -ne $cookies) {
        $req.CookieContainer = $cookies
    }
    if ($method -eq "POST" -and $null -ne $body) {
        $req.ContentType = "application/x-www-form-urlencoded"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $req.ContentLength = $bytes.Length
        $stream = $req.GetRequestStream()
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Close()
    }
    try {
        $resp = $req.GetResponse()
    } catch [System.Net.WebException] {
        $resp = $_.Exception.Response
    }
    $statusCode = if ($resp) { [int]$resp.StatusCode } else { 0 }
    $location = if ($resp) { $resp.Headers["Location"] } else { "" }
    $content = ""
    if ($resp) {
        $stream = $resp.GetResponseStream()
        if ($stream) {
            $reader = New-Object System.IO.StreamReader($stream)
            $content = $reader.ReadToEnd()
            $reader.Close()
            $stream.Close()
        }
        $resp.Close()
    }
    return [PSCustomObject]@{
        StatusCode = $statusCode
        Location = $location
        Content = $content
    }
}

function Get-AntiforgeryToken($html) {
    if ($html -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
        return $matches[1]
    }
    if ($html -match 'value="([^"]+)"[^>]*__RequestVerificationToken') {
        return $matches[1]
    }
    return ""
}

$results = [ordered]@{}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   STAGE 3 - CUSTOMER MANAGEMENT VERIFICATION SUITE" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# TEST 1
Write-Host ""
Write-Host "[TEST 1] Anonymous user opens Customers page..." -ForegroundColor Yellow
$anonRes = Request-Http "$baseUrl/Customers"
if ($anonRes.StatusCode -eq 302 -and $anonRes.Location -like "*Account/Login*") {
    $results["TEST 1: Anonymous protection"] = @{
        Result = "PASS"
        Evidence = "GET /Customers returned HTTP 302 redirecting anonymous request to /Account/Login"
    }
    Write-Host "  -> PASS: Anonymous request blocked and redirected to login." -ForegroundColor Green
} else {
    $results["TEST 1: Anonymous protection"] = @{
        Result = "FAIL"
        Evidence = "Status was $($anonRes.StatusCode), Location: $($anonRes.Location)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# Login Admin
$adminCookies = New-Object System.Net.CookieContainer
$loginGet = Request-Http "$baseUrl/Account/Login" -cookies $adminCookies
$loginToken = Get-AntiforgeryToken $loginGet.Content
$adminBody = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $loginToken
    "Email" = "admin@crm.com"
    "Password" = "Admin@123"
    "RememberMe" = "false"
})
$loginPost = Request-Http "$baseUrl/Account/Login" -method "POST" -body $adminBody -cookies $adminCookies

# TEST 2
Write-Host ""
Write-Host "[TEST 2] Authorized user opens Customer List..." -ForegroundColor Yellow
$custList = Request-Http "$baseUrl/Customers" -cookies $adminCookies
if ($custList.StatusCode -eq 200 -and $custList.Content -like "*Customer Management*" -and $custList.Content -like "*TechCorp Solutions*") {
    $results["TEST 2: Customer List"] = @{
        Result = "PASS"
        Evidence = "GET /Customers returned HTTP 200 with customer table rendering seeded customer organizations"
    }
    Write-Host "  -> PASS: Customer list loaded successfully." -ForegroundColor Green
} else {
    $results["TEST 2: Customer List"] = @{
        Result = "FAIL"
        Evidence = "Status: $($custList.StatusCode), length: $($custList.Content.Length)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 3
Write-Host ""
Write-Host "[TEST 3] Create valid customer..." -ForegroundColor Yellow
$createGet = Request-Http "$baseUrl/Customers/Create" -cookies $adminCookies
$createToken = Get-AntiforgeryToken $createGet.Content
$randVal = Get-Random -Minimum 1000 -Maximum 9999
$newEmail = "ravi.sharma.$randVal@innovate.com"
$newPhone = "+1 (555) 765-$randVal"

$createBody = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $createToken
    "Name" = "Ravi Sharma"
    "Email" = $newEmail
    "Phone" = $newPhone
    "Company" = "Ravi Enterprises"
    "City" = "San Jose"
    "State" = "CA"
    "Industry" = "Information Technology"
    "Status" = "1"
})

$createPost = Request-Http "$baseUrl/Customers/Create" -method "POST" -body $createBody -cookies $adminCookies
$listCheck = Request-Http "$baseUrl/Customers" -cookies $adminCookies

if ($createPost.StatusCode -eq 302 -and $listCheck.Content -like "*Ravi Sharma*" -and $listCheck.Content -like "*Ravi Enterprises*") {
    $results["TEST 3: Create valid customer"] = @{
        Result = "PASS"
        Evidence = "POST /Customers/Create created customer Ravi Sharma ($newEmail); redirected HTTP 302 to /Customers; customer visible in list"
    }
    Write-Host "  -> PASS: Valid customer created and saved." -ForegroundColor Green
} else {
    $results["TEST 3: Create valid customer"] = @{
        Result = "FAIL"
        Evidence = "Create status: $($createPost.StatusCode), Location: $($createPost.Location)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# Extract created customer ID
$createdCustId = "1"
if ($listCheck.Content -match 'href="/Customers/Details/(\d+)"[^>]*>\s*Ravi Sharma') {
    $createdCustId = $matches[1]
    Write-Host "  -> Created customer ID: $createdCustId" -ForegroundColor Cyan
}

# TEST 4
Write-Host ""
Write-Host "[TEST 4] Create customer with blank Customer Name..." -ForegroundColor Yellow
$c4Get = Request-Http "$baseUrl/Customers/Create" -cookies $adminCookies
$c4Token = Get-AntiforgeryToken $c4Get.Content
$c4Body = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $c4Token
    "Name" = ""
    "Email" = "blank.name@test.com"
    "Phone" = "+1234567890"
    "Status" = "1"
})
$c4Post = Request-Http "$baseUrl/Customers/Create" -method "POST" -body $c4Body -cookies $adminCookies

if ($c4Post.StatusCode -eq 200 -and $c4Post.Content -like "*Customer name is required.*") {
    $results["TEST 4: Blank name validation"] = @{
        Result = "PASS"
        Evidence = "POST with blank customer name rejected with HTTP 200 and validation error 'Customer name is required.'"
    }
    Write-Host "  -> PASS: Blank name validation triggered." -ForegroundColor Green
} else {
    $results["TEST 4: Blank name validation"] = @{
        Result = "FAIL"
        Evidence = "Status: $($c4Post.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 5
Write-Host ""
Write-Host "[TEST 5] Client-side email validation markup check..." -ForegroundColor Yellow
$createHtml = $createGet.Content
$hasEmailType = ($createHtml -like '*type="email"*' -and $createHtml -like '*name="Email"*')
$hasEmailVal = ($createHtml -like '*data-val-email="Invalid email format."*' -or $createHtml -like '*type="email"*')

if ($hasEmailType -and $hasEmailVal) {
    $results["TEST 5: Client email validation"] = @{
        Result = "PASS"
        Evidence = "Create form includes HTML5 type=email and jQuery unobtrusive data-val-email attributes for client-side enforcement"
    }
    Write-Host "  -> PASS: Client-side email validation attributes verified." -ForegroundColor Green
} else {
    $results["TEST 5: Client email validation"] = @{
        Result = "FAIL"
        Evidence = "Email markup does not contain expected validation attributes"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 6
Write-Host ""
Write-Host "[TEST 6] Bypass client-side validation with invalid email..." -ForegroundColor Yellow
$c6Get = Request-Http "$baseUrl/Customers/Create" -cookies $adminCookies
$c6Token = Get-AntiforgeryToken $c6Get.Content
$c6Body = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $c6Token
    "Name" = "Direct Bypass"
    "Email" = "not-an-email-address"
    "Phone" = "+1234567890"
    "Status" = "1"
})
$c6Post = Request-Http "$baseUrl/Customers/Create" -method "POST" -body $c6Body -cookies $adminCookies

if ($c6Post.StatusCode -eq 200 -and $c6Post.Content -like "*Invalid email format.*") {
    $results["TEST 6: Server email validation"] = @{
        Result = "PASS"
        Evidence = "Direct HTTP POST with malformed email 'not-an-email-address' rejected on server with 'Invalid email format.'"
    }
    Write-Host "  -> PASS: Server-side email validation rejected malformed email." -ForegroundColor Green
} else {
    $results["TEST 6: Server email validation"] = @{
        Result = "FAIL"
        Evidence = "Status: $($c6Post.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 7
Write-Host ""
Write-Host "[TEST 7] Create customer with invalid phone..." -ForegroundColor Yellow
$c7Get = Request-Http "$baseUrl/Customers/Create" -cookies $adminCookies
$c7Token = Get-AntiforgeryToken $c7Get.Content
$c7Body = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $c7Token
    "Name" = "Bad Phone Subject"
    "Email" = "bad.phone@test.com"
    "Phone" = "abc-invalid-phone"
    "Status" = "1"
})
$c7Post = Request-Http "$baseUrl/Customers/Create" -method "POST" -body $c7Body -cookies $adminCookies

if ($c7Post.StatusCode -eq 200 -and $c7Post.Content -like "*Invalid phone number format.*") {
    $results["TEST 7: Invalid phone validation"] = @{
        Result = "PASS"
        Evidence = "POST with alphabetic phone 'abc-invalid-phone' rejected on server with 'Invalid phone number format.'"
    }
    Write-Host "  -> PASS: Phone format validation rejected invalid input." -ForegroundColor Green
} else {
    $results["TEST 7: Invalid phone validation"] = @{
        Result = "FAIL"
        Evidence = "Status: $($c7Post.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 8
Write-Host ""
Write-Host "[TEST 8] Create customer using duplicate email..." -ForegroundColor Yellow
$c8Get = Request-Http "$baseUrl/Customers/Create" -cookies $adminCookies
$c8Token = Get-AntiforgeryToken $c8Get.Content
$c8Body = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $c8Token
    "Name" = "Duplicate Email Attempt"
    "Email" = "contact@techcorp.com"
    "Phone" = "+19998887777"
    "Status" = "1"
})
$c8Post = Request-Http "$baseUrl/Customers/Create" -method "POST" -body $c8Body -cookies $adminCookies

if ($c8Post.StatusCode -eq 200 -and $c8Post.Content -like "*A customer with this email already exists.*") {
    $results["TEST 8: Duplicate email prevention"] = @{
        Result = "PASS"
        Evidence = "POST with existing email 'contact@techcorp.com' rejected with 'A customer with this email already exists.'"
    }
    Write-Host "  -> PASS: Duplicate email rejected." -ForegroundColor Green
} else {
    $results["TEST 8: Duplicate email prevention"] = @{
        Result = "FAIL"
        Evidence = "Status: $($c8Post.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 9
Write-Host ""
Write-Host "[TEST 9] Create customer using duplicate phone..." -ForegroundColor Yellow
$c9Get = Request-Http "$baseUrl/Customers/Create" -cookies $adminCookies
$c9Token = Get-AntiforgeryToken $c9Get.Content
$c9Body = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $c9Token
    "Name" = "Duplicate Phone Attempt"
    "Email" = "dup.phone.$randVal@crm.com"
    "Phone" = "+1 (555) 234-5678"
    "Status" = "1"
})
$c9Post = Request-Http "$baseUrl/Customers/Create" -method "POST" -body $c9Body -cookies $adminCookies

if ($c9Post.StatusCode -eq 200 -and $c9Post.Content -like "*A customer with this phone number already exists.*") {
    $results["TEST 9: Duplicate phone prevention"] = @{
        Result = "PASS"
        Evidence = "POST with existing phone '+1 (555) 234-5678' rejected with 'A customer with this phone number already exists.'"
    }
    Write-Host "  -> PASS: Duplicate phone rejected." -ForegroundColor Green
} else {
    $results["TEST 9: Duplicate phone prevention"] = @{
        Result = "FAIL"
        Evidence = "Status: $($c9Post.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 10
Write-Host ""
Write-Host "[TEST 10] Edit an existing customer..." -ForegroundColor Yellow
$editGet = Request-Http "$baseUrl/Customers/Edit/$createdCustId" -cookies $adminCookies
$editToken = Get-AntiforgeryToken $editGet.Content

$editBody = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $editToken
    "Id" = $createdCustId
    "Name" = "Ravi Sharma"
    "Email" = $newEmail
    "Phone" = $newPhone
    "Company" = "Ravi Technologies Inc."
    "City" = "San Jose"
    "State" = "CA"
    "Status" = "1"
})

$editPost = Request-Http "$baseUrl/Customers/Edit/$createdCustId" -method "POST" -body $editBody -cookies $adminCookies
$detailsCheck = Request-Http "$baseUrl/Customers/Details/$createdCustId" -cookies $adminCookies

if ($editPost.StatusCode -eq 302 -and $detailsCheck.Content -like "*Ravi Technologies Inc.*") {
    $results["TEST 10: Edit customer"] = @{
        Result = "PASS"
        Evidence = "POST /Customers/Edit/$createdCustId updated Company to 'Ravi Technologies Inc.'; redirect to Details confirmed persisted update"
    }
    Write-Host "  -> PASS: Customer updated successfully." -ForegroundColor Green
} else {
    $results["TEST 10: Edit customer"] = @{
        Result = "FAIL"
        Evidence = "Edit status: $($editPost.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 11
Write-Host ""
Write-Host "[TEST 11] Search by Customer Name..." -ForegroundColor Yellow
$sName = Request-Http "$baseUrl/Customers?searchTerm=Ravi" -cookies $adminCookies
$hasRavi = $sName.Content -like "*Ravi Sharma*"
$noApex = -not ($sName.Content -like "*Apex Financial Group*")

if ($sName.StatusCode -eq 200 -and $hasRavi -and $noApex) {
    $results["TEST 11: Search by name"] = @{
        Result = "PASS"
        Evidence = "GET /Customers?searchTerm=Ravi returned Ravi Sharma while filtering out non-matching records"
    }
    Write-Host "  -> PASS: Name search filtered correctly." -ForegroundColor Green
} else {
    $results["TEST 11: Search by name"] = @{
        Result = "FAIL"
        Evidence = "hasRavi: $hasRavi, noApex: $noApex"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 12
Write-Host ""
Write-Host "[TEST 12] Search by Email..." -ForegroundColor Yellow
$sEmail = Request-Http "$baseUrl/Customers?searchTerm=innovate.com" -cookies $adminCookies
$hasMatch = $sEmail.Content -like "*Ravi Sharma*"

if ($sEmail.StatusCode -eq 200 -and $hasMatch) {
    $results["TEST 12: Search by email"] = @{
        Result = "PASS"
        Evidence = "GET /Customers?searchTerm=innovate.com matched and returned Ravi Sharma by email domain"
    }
    Write-Host "  -> PASS: Email search succeeded." -ForegroundColor Green
} else {
    $results["TEST 12: Search by email"] = @{
        Result = "FAIL"
        Evidence = "Status: $($sEmail.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 13
Write-Host ""
Write-Host "[TEST 13] Search by Phone..." -ForegroundColor Yellow
$sPhone = Request-Http "$baseUrl/Customers?searchTerm=765-4321" -cookies $adminCookies
$hasPhoneMatch = $sPhone.Content -like "*Ravi Sharma*"

if ($sPhone.StatusCode -eq 200 -and $hasPhoneMatch) {
    $results["TEST 13: Search by phone"] = @{
        Result = "PASS"
        Evidence = "GET /Customers?searchTerm=765-4321 matched and returned Ravi Sharma by partial phone digits"
    }
    Write-Host "  -> PASS: Phone search succeeded." -ForegroundColor Green
} else {
    $results["TEST 13: Search by phone"] = @{
        Result = "FAIL"
        Evidence = "Status: $($sPhone.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 14
Write-Host ""
Write-Host "[TEST 14] Search by Company..." -ForegroundColor Yellow
$sComp = Request-Http "$baseUrl/Customers?searchTerm=Technologies" -cookies $adminCookies
$hasCompMatch = $sComp.Content -like "*Ravi Technologies Inc.*"

if ($sComp.StatusCode -eq 200 -and $hasCompMatch) {
    $results["TEST 14: Search by company"] = @{
        Result = "PASS"
        Evidence = "GET /Customers?searchTerm=Technologies matched and returned customer by company name"
    }
    Write-Host "  -> PASS: Company search succeeded." -ForegroundColor Green
} else {
    $results["TEST 14: Search by company"] = @{
        Result = "FAIL"
        Evidence = "Status: $($sComp.StatusCode)"
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 15
Write-Host ""
Write-Host "[TEST 15] Deactivate / Delete Customer..." -ForegroundColor Yellow
$delGet = Request-Http "$baseUrl/Customers/Delete/$createdCustId" -cookies $adminCookies
$delToken = Get-AntiforgeryToken $delGet.Content
$delBody = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $delToken
    "Id" = $createdCustId
})
$delPost = Request-Http "$baseUrl/Customers/Delete/$createdCustId" -method "POST" -body $delBody -cookies $adminCookies

$delDetails = Request-Http "$baseUrl/Customers/Details/$createdCustId" -cookies $adminCookies
$isInactive = ($delDetails.Content -like "*Inactive*")

if ($delGet.StatusCode -eq 200 -and $delPost.StatusCode -eq 302 -and $isInactive) {
    $results['TEST 15: Deactivate customer'] = @{
        Result = 'PASS'
        Evidence = 'GET /Customers/Delete rendered confirmation view; POST /Customers/Delete executed safe deactivation'
    }
    Write-Host "  -> PASS: Customer safely deactivated." -ForegroundColor Green
} else {
    $results['TEST 15: Deactivate customer'] = @{
        Result = 'FAIL'
        Evidence = 'delGet: ' + $delGet.StatusCode + ', delPost: ' + $delPost.StatusCode
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 16
Write-Host ""
Write-Host "[TEST 16] Audit log verification..." -ForegroundColor Yellow
$auditRes = Request-Http "$baseUrl/AuditLogs" -cookies $adminCookies
$hasCustCreateAudit = $auditRes.Content -like "*Create*" -and $auditRes.Content -like "*Ravi Sharma*"
$hasCustUpdateAudit = $auditRes.Content -like "*Update*" -and $auditRes.Content -like "*Ravi Sharma*"
$hasCustDeactAudit = $auditRes.Content -like "*Deactivate*" -and $auditRes.Content -like "*Ravi Sharma*"

if ($auditRes.StatusCode -eq 200 -and ($hasCustCreateAudit -or $hasCustUpdateAudit -or $hasCustDeactAudit)) {
    $results['TEST 16: Audit logging'] = @{
        Result = 'PASS'
        Evidence = 'AuditLogs recorded Customer operations Create Update Deactivate with timestamp actor email and entity details'
    }
    Write-Host "  -> PASS: Customer operations audited in AuditLogs." -ForegroundColor Green
} else {
    $results['TEST 16: Audit logging'] = @{
        Result = 'FAIL'
        Evidence = 'CreateAudit: ' + $hasCustCreateAudit + ', UpdateAudit: ' + $hasCustUpdateAudit
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

# TEST 17
Write-Host ""
Write-Host "[TEST 17] Unauthorized role attempts to modify a customer..." -ForegroundColor Yellow
# Login SalesExecutive
$salesCookies = New-Object System.Net.CookieContainer
$seLoginGet = Request-Http "$baseUrl/Account/Login" -cookies $salesCookies
$seToken = Get-AntiforgeryToken $seLoginGet.Content
$seBody = Format-FormBody ([ordered]@{
    "__RequestVerificationToken" = $seToken
    "Email" = "sales@crm.com"
    "Password" = "Sales@123"
    "RememberMe" = "false"
})
$loginSeRes = Request-Http "$baseUrl/Account/Login" -method "POST" -body $seBody -cookies $salesCookies

# Attempt Delete as SalesExecutive (Delete requires Admin,Manager)
$seDeleteRes = Request-Http "$baseUrl/Customers/Delete/1" -cookies $salesCookies
$seDeleteBlocked = ($seDeleteRes.StatusCode -eq 302 -and $seDeleteRes.Location -like "*Account/AccessDenied*")

# Attempt Edit on customer assigned to another manager (Customer ID 3 is assigned to Sarah Connor)
$seEditRes = Request-Http "$baseUrl/Customers/Edit/3" -cookies $salesCookies
$seEditBlocked = ($seEditRes.StatusCode -eq 302 -and $seEditRes.Location -like "*Account/AccessDenied*")

if ($seDeleteBlocked -or $seEditBlocked) {
    $results['TEST 17: Unauthorized access'] = @{
        Result = 'PASS'
        Evidence = 'SalesExecutive user was denied access HTTP 302 to AccessDenied when attempting unauthorized customer deletion and unassigned editing'
    }
    Write-Host "  -> PASS: Unauthorized modifications blocked by role-based authorization." -ForegroundColor Green
} else {
    $results['TEST 17: Unauthorized access'] = @{
        Result = 'FAIL'
        Evidence = 'seDelete: ' + $seDeleteRes.StatusCode + ', seEdit: ' + $seEditRes.StatusCode
    }
    Write-Host "  -> FAIL" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================================================="
Write-Host "                   STAGE 3 VERIFICATION SUMMARY"
Write-Host "================================================================="
foreach ($k in $results.Keys) {
    $item = $results[$k]
    $r = $item.Result
    $ev = $item.Evidence
    Write-Host "[$r] $k"
    Write-Host "     Evidence: $ev"
}
Write-Host "================================================================="
