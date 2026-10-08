$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$resp = $client.GetAsync("http://localhost:5000/Dashboard").Result
Write-Host "Status:" [int]$resp.StatusCode
Write-Host "Location:" $resp.Headers.Location.OriginalString
