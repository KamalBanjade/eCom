$baseUrl = "https://localhost:7213/api"
$email = "customer-$(Get-Random)@example.com"
$password = "Customer123!"

# Ignore SSL errors for development
if (-not ([System.Management.Automation.PSTypeName]'TrustAllCertsPolicy').Type) {
    add-type @"
        using System.Net;
        using System.Security.Cryptography.X509Certificates;
        public class TrustAllCertsPolicy : ICertificatePolicy {
            public bool CheckValidationResult(
                ServicePoint srvPoint, X509Certificate certificate,
                WebRequest request, int certificateProblem) {
                return true;
            }
        }
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
}

function Test-Endpoint {
    param($Name, $Method, $Url, $Body, $Headers)
    Write-Host "Testing $Name..." -NoNewline
    try {
        $params = @{
            Uri         = $Url
            Method      = $Method
            ContentType = "application/json"
        }
        if ($Body) { $params.Body = ($Body | ConvertTo-Json) }
        if ($Headers) { $params.Headers = $Headers }

        $response = Invoke-RestMethod @params
        
        if ($response.success -eq $false) {
            Write-Host " FAIL (API Error: $($response.message))" -ForegroundColor Red
            return $null
        }

        Write-Host " PASS" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host " FAIL (Exception)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader $_.Exception.Response.GetResponseStream()
            Write-Host "  Status: $($_.Exception.Response.StatusCode)"
            Write-Host "  Response: $($reader.ReadToEnd())"
        }
        else {
            Write-Host "  Error: $($_.Exception.Message)"
        }
        return $null
    }
}

Write-Host "=== Starting E2E Tests ===" -ForegroundColor Cyan

# 1. Register
$regBody = @{
    email       = $email
    password    = $password
    firstName   = "Test"
    lastName    = "User"
    phoneNumber = "1234567890"
}
$regResult = Test-Endpoint "Register Customer ($email)" "Post" "$baseUrl/auth/register" $regBody $null

# 2. Login Customer
$loginBody = @{
    email    = $email
    password = $password
}
$loginResult = Test-Endpoint "Login Customer" "Post" "$baseUrl/auth/login" $loginBody $null

if ($loginResult -and $loginResult.success) {
    $token = $loginResult.data.accessToken
    $headers = @{ Authorization = "Bearer $token" }
    Write-Host "  got token: $($token.Substring(0, 10))..." -ForegroundColor Gray

    # 3. Get My Orders
    Test-Endpoint "Get My Orders (Customer)" "Get" "$baseUrl/order" $null $headers
    
    # 4. Create Order
    Test-Endpoint "Create Order" "Post" "$baseUrl/order" @{} $headers

    # 5. Access Admin Endpoint (Should Fail)
    Write-Host "Testing Admin Access Restriction..." -NoNewline
    try {
        Invoke-RestMethod -Uri "$baseUrl/order/admin" -Method Get -Headers $headers -ContentType "application/json" -ErrorAction Stop | Out-Null
        Write-Host " FAIL (Expected 403, got 200)" -ForegroundColor Red
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::Forbidden) {
            Write-Host " PASS (Got 403 Forbidden)" -ForegroundColor Green
        }
        else {
            Write-Host " FAIL (Expected 403, got $($_.Exception.Response.StatusCode))" -ForegroundColor Red
        }
    }
}
else {
    Write-Host "Skipping Customer tests due to login failure" -ForegroundColor Yellow
}

# 6. Login Admin
Write-Host "`n=== Testing Admin Flow ===" -ForegroundColor Cyan
$adminBody = @{
    email    = "admin@ecommerce.com"
    password = "Admin123!"
}
$adminLoginResult = Test-Endpoint "Login Admin (Seeded)" "Post" "$baseUrl/auth/login" $adminBody $null

if ($adminLoginResult -and $adminLoginResult.success) {
    $adminToken = $adminLoginResult.data.accessToken
    $adminHeaders = @{ Authorization = "Bearer $adminToken" }
    Write-Host "  got token: $($adminToken.Substring(0, 10))..." -ForegroundColor Gray

    # 7. Get All Orders
    Test-Endpoint "Get All Orders (Admin)" "Get" "$baseUrl/order/admin" $null $adminHeaders
}
else {
    Write-Host "Skipping Admin tests due to login failure" -ForegroundColor Yellow
}
