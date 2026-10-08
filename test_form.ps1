$dict = New-Object 'System.Collections.Generic.Dictionary[string,string]'
$dict.Add("test", "123")
$content = New-Object System.Net.Http.FormUrlEncodedContent($dict)
Write-Host "Result:" $content.ReadAsStringAsync().Result
