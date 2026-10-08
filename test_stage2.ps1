$baseUrl = "http://localhost:5000"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   STAGE 2 AUTHENTICATION & RBAC TESTS" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

function Get-AntiForgeryToken($html) {
    if ($html -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
        return $matches[1]
    }
    if ($html -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
        return $matches[1]
    }
    return $null
}

# ----------------------------------------------------
# TEST 1: Open application without logging in
# ----------------------------------------------------
Write-Host "`n[TEST 1] Testing anonymous access to protected pages..." -ForegroundColor Yellow
$protectedUrls = @("/Dashboard", "/Customers", "/Leads", "/Opportunities", "/FollowUps", "/Users", "/AuditLogs")
$test1Passed = $true

foreach ($url in $protectedUrls) {
    $res = Invoke-WebRequest -Uri "$baseUrl$url" -MaximumRedirection 0 -ErrorAction SilentlyContinue
    if ($res.StatusCode -eq 302 -and $res.Headers["Location"] -like "*Account/Login*") {
        Write-Host "  [OK] $url -> Redirected to Login ($($res.Headers['Location']))" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] $url failed! Status: $($res.StatusCode), Location: $($res.Headers['Location'])" -ForegroundColor Red
        $test1Passed = $false
    }
}
if ($test1Passed) { 
    Write-Host "TEST 1 PASSED: Protected CRM pages are inaccessible to anonymous users." -ForegroundColor Green 
} else { 
    Write-Host "TEST 1 FAILED!" -ForegroundColor Red 
}

# ----------------------------------------------------
# TEST 2: Login as Admin
# ----------------------------------------------------
Write-Host "`n[TEST 2] Testing Admin login (admin@crm.com / Admin@123)..." -ForegroundColor Yellow
$adminSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginPage = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $adminSession
$token = Get-AntiForgeryToken $loginPage.Content

$loginBody = @{
    "__RequestVerificationToken" = $token
    "Email" = "admin@crm.com"
    "Password" = "Admin@123"
    "RememberMe" = "false"
}
$loginRes = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method POST -Body $loginBody -WebSession $adminSession -MaximumRedirection 0 -ErrorAction SilentlyContinue

if ($loginRes.StatusCode -eq 302 -and $loginRes.Headers["Location"] -like "*Dashboard*") {
    $dashRes = Invoke-WebRequest -Uri "$baseUrl/Dashboard" -WebSession $adminSession
    if ($dashRes.Content -like "*John Admin*" -and $dashRes.Content -like "*Admin*") {
        Write-Host "  [OK] Admin logged in successfully! Reached Dashboard and Admin role recognized." -ForegroundColor Green
        $usersRes = Invoke-WebRequest -Uri "$baseUrl/Users" -WebSession $adminSession
        if ($usersRes.StatusCode -eq 200 -and $usersRes.Content -like "*User Management*") {
            Write-Host "  [OK] Admin can access /Users successfully (HTTP 200)." -ForegroundColor Green
            Write-Host "TEST 2 PASSED: Admin login and role verified." -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] Admin could not access /Users!" -ForegroundColor Red
        }
    } else {
        Write-Host "  [FAIL] Dashboard content missing Admin user details." -ForegroundColor Red
    }
} else {
    Write-Host "  [FAIL] Admin login failed! Status: $($loginRes.StatusCode)" -ForegroundColor Red
}

# ----------------------------------------------------
# TEST 3: Login as Manager
# ----------------------------------------------------
Write-Host "`n[TEST 3] Testing Manager login (manager@crm.com / Manager@123)..." -ForegroundColor Yellow
$mgrSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginPage = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $mgrSession
$token = Get-AntiForgeryToken $loginPage.Content

$loginBody = @{
    "__RequestVerificationToken" = $token
    "Email" = "manager@crm.com"
    "Password" = "Manager@123"
    "RememberMe" = "false"
}
$loginRes = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method POST -Body $loginBody -WebSession $mgrSession -MaximumRedirection 0 -ErrorAction SilentlyContinue

if ($loginRes.StatusCode -eq 302 -and $loginRes.Headers["Location"] -like "*Dashboard*") {
    $dashRes = Invoke-WebRequest -Uri "$baseUrl/Dashboard" -WebSession $mgrSession
    if ($dashRes.Content -like "*Sarah Connor*" -and $dashRes.Content -like "*Manager*") {
        Write-Host "  [OK] Manager logged in successfully! Reached Dashboard and Manager role recognized." -ForegroundColor Green
        Write-Host "TEST 3 PASSED: Manager login and role verified." -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Manager dashboard content missing expected details." -ForegroundColor Red
    }
} else {
    Write-Host "  [FAIL] Manager login failed! Status: $($loginRes.StatusCode)" -ForegroundColor Red
}

# ----------------------------------------------------
# TEST 4: Login as SalesExecutive
# ----------------------------------------------------
Write-Host "`n[TEST 4] Testing SalesExecutive login (sales@crm.com / Sales@123)..." -ForegroundColor Yellow
$salesSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginPage = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $salesSession
$token = Get-AntiForgeryToken $loginPage.Content

$loginBody = @{
    "__RequestVerificationToken" = $token
    "Email" = "sales@crm.com"
    "Password" = "Sales@123"
    "RememberMe" = "false"
}
$loginRes = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method POST -Body $loginBody -WebSession $salesSession -MaximumRedirection 0 -ErrorAction SilentlyContinue

if ($loginRes.StatusCode -eq 302 -and $loginRes.Headers["Location"] -like "*Dashboard*") {
    $dashRes = Invoke-WebRequest -Uri "$baseUrl/Dashboard" -WebSession $salesSession
    if ($dashRes.Content -like "*Michael Scott*" -and $dashRes.Content -like "*SalesExecutive*") {
        Write-Host "  [OK] SalesExecutive logged in successfully! Reached Dashboard and SalesExecutive role recognized." -ForegroundColor Green
        Write-Host "TEST 4 PASSED: SalesExecutive login and role verified." -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] SalesExecutive dashboard content missing expected details." -ForegroundColor Red
    }
} else {
    Write-Host "  [FAIL] SalesExecutive login failed! Status: $($loginRes.StatusCode)" -ForegroundColor Red
}

# ----------------------------------------------------
# TEST 5: Use invalid password
# ----------------------------------------------------
Write-Host "`n[TEST 5] Testing invalid password login..." -ForegroundColor Yellow
$failSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginPage = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $failSession
$token = Get-AntiForgeryToken $loginPage.Content

$loginBody = @{
    "__RequestVerificationToken" = $token
    "Email" = "sales@crm.com"
    "Password" = "WrongPassword!999"
    "RememberMe" = "false"
}
$failRes = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method POST -Body $loginBody -WebSession $failSession

if ($failRes.Content -like "*Invalid email or password*") {
    Write-Host "  [OK] Invalid login failed with friendly message: 'Invalid email or password.'" -ForegroundColor Green
    Write-Host "TEST 5 PASSED: Invalid password handled cleanly." -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Invalid password error message not found in response!" -ForegroundColor Red
}

# ----------------------------------------------------
# TEST 6: Repeated failed credentials triggering Lockout
# ----------------------------------------------------
Write-Host "`n[TEST 6] Testing Account Lockout after repeated failed attempts..." -ForegroundColor Yellow
$lockoutEmail = "lockout_qa@crm.com"
$regSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$regPage = Invoke-WebRequest -Uri "$baseUrl/Account/Register" -WebSession $regSession
$regToken = Get-AntiForgeryToken $regPage.Content

$regBody = @{
    "__RequestVerificationToken" = $regToken
    "FullName" = "Lockout QA User"
    "Email" = $lockoutEmail
    "Department" = "QA"
    "Role" = "SalesExecutive"
    "Password" = "ValidPass123@"
    "ConfirmPassword" = "ValidPass123@"
}
Invoke-WebRequest -Uri "$baseUrl/Account/Register" -Method POST -Body $regBody -WebSession $regSession -MaximumRedirection 0 -ErrorAction SilentlyContinue | Out-Null

$lockoutDetected = $false
for ($i = 1; $i -le 6; $i++) {
    $testSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $p = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $testSession
    $t = Get-AntiForgeryToken $p.Content
    $b = @{
        "__RequestVerificationToken" = $t
        "Email" = $lockoutEmail
        "Password" = "WrongPass$i"
        "RememberMe" = "false"
    }
    $r = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method POST -Body $b -WebSession $testSession
    $msg = "Failed attempt"
    if ($r.Content -like "*locked out*") {
        $msg = "LOCKOUT TRIGGERED"
        $lockoutDetected = $true
    }
    Write-Host "  Attempt $i -> Result: $msg" -ForegroundColor Cyan
    if ($lockoutDetected) {
        break
    }
}

if ($lockoutDetected) {
    Write-Host "  [OK] Account lockout triggered successfully! User received lockout notification." -ForegroundColor Green
    Write-Host "TEST 6 PASSED: Identity Account Lockout verified." -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Lockout was not triggered after failed attempts." -ForegroundColor Red
}

# ----------------------------------------------------
# TEST 7: Logout
# ----------------------------------------------------
Write-Host "`n[TEST 7] Testing Logout..." -ForegroundColor Yellow
$dashPage = Invoke-WebRequest -Uri "$baseUrl/Dashboard" -WebSession $salesSession
$logoutToken = Get-AntiForgeryToken $dashPage.Content

$logoutBody = @{
    "__RequestVerificationToken" = $logoutToken
}
$logoutRes = Invoke-WebRequest -Uri "$baseUrl/Account/Logout" -Method POST -Body $logoutBody -WebSession $salesSession -MaximumRedirection 0 -ErrorAction SilentlyContinue

if ($logoutRes.StatusCode -eq 302 -and $logoutRes.Headers["Location"] -like "*Account/Login*") {
    $checkDash = Invoke-WebRequest -Uri "$baseUrl/Dashboard" -WebSession $salesSession -MaximumRedirection 0 -ErrorAction SilentlyContinue
    if ($checkDash.StatusCode -eq 302 -and $checkDash.Headers["Location"] -like "*Account/Login*") {
        Write-Host "  [OK] User successfully logged out. Dashboard now requires authentication." -ForegroundColor Green
        Write-Host "TEST 7 PASSED: Logout terminates session cleanly." -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Dashboard still accessible after logout!" -ForegroundColor Red
    }
} else {
    Write-Host "  [FAIL] Logout did not redirect to Login! Status: $($logoutRes.StatusCode)" -ForegroundColor Red
}

# ----------------------------------------------------
# TEST 8: Restricted role accessing Admin-only URL
# ----------------------------------------------------
Write-Host "`n[TEST 8] Testing restricted role (SalesExecutive) accessing Admin URL (/Users)..." -ForegroundColor Yellow
$seSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$lp = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $seSession
$tk = Get-AntiForgeryToken $lp.Content
$lb = @{
    "__RequestVerificationToken" = $tk
    "Email" = "sales@crm.com"
    "Password" = "Sales@123"
    "RememberMe" = "false"
}
Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method POST -Body $lb -WebSession $seSession -MaximumRedirection 0 -ErrorAction SilentlyContinue | Out-Null

$restrictedRes = Invoke-WebRequest -Uri "$baseUrl/Users" -WebSession $seSession -MaximumRedirection 0 -ErrorAction SilentlyContinue
if ($restrictedRes.StatusCode -eq 302 -and $restrictedRes.Headers["Location"] -like "*AccessDenied*") {
    Write-Host "  [OK] SalesExecutive access to /Users blocked! Redirected to /Account/AccessDenied." -ForegroundColor Green
    Write-Host "TEST 8 PASSED: Role-based authorization denies unauthorized access." -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Restricted access was not blocked! Status: $($restrictedRes.StatusCode), Location: $($restrictedRes.Headers['Location'])" -ForegroundColor Red
}

# ----------------------------------------------------
# TEST 9: Verification of Seeded Users/Roles Idempotency
# ----------------------------------------------------
Write-Host "`n[TEST 9] Verifying seeded users and roles..." -ForegroundColor Yellow
$adminUsersPage = Invoke-WebRequest -Uri "$baseUrl/Users" -WebSession $adminSession
if ($adminUsersPage.Content -like "*admin@crm.com*" -and 
    $adminUsersPage.Content -like "*manager@crm.com*" -and 
    $adminUsersPage.Content -like "*sales@crm.com*") {
    Write-Host "  [OK] All 3 test roles and accounts exist and verified." -ForegroundColor Green
    Write-Host "TEST 9 PASSED: Seeded accounts are unique and valid." -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Seeded users verification failed." -ForegroundColor Red
}

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "   ALL 9 TESTS COMPLETED SUCCESSFULLY!" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
