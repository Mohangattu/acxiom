$baseUrl = "http://localhost:5000"

# 1. GET Login page
$loginPage = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -SessionVariable session -UseBasicParsing
Write-Host "Login page status:" $loginPage.StatusCode

# Extract Antiforgery Token
$token = ""
if ($loginPage.Content -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
    $token = $matches[1]
} elseif ($loginPage.Content -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
    $token = $matches[1]
}
Write-Host "Token extracted:" ($token.Substring(0, 15) + "...")

# 2. POST Login credentials
$body = @{
    "__RequestVerificationToken" = $token
    "Email" = "admin@crm.com"
    "Password" = "Admin@123"
    "RememberMe" = "false"
}

$loginResponse = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method POST -Body $body -WebSession $session -UseBasicParsing
Write-Host "Post Login status:" $loginResponse.StatusCode
Write-Host "Post Login length:" $loginResponse.Content.Length
Write-Host "Contains 'John Admin':" ($loginResponse.Content -like "*John Admin*")
Write-Host "Contains 'Dashboard':" ($loginResponse.Content -like "*Dashboard*")
