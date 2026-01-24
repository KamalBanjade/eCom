using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Payments.DTOs;
using Commerce.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Commerce.Infrastructure.Services;

public class KhaltiPaymentService : IKhaltiPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly KhaltiSettings _settings;
    private readonly ILogger<KhaltiPaymentService> _logger;

    public KhaltiPaymentService(
        HttpClient httpClient,
        IOptions<KhaltiSettings> settings,
        ILogger<KhaltiPaymentService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        // Configure HTTP client
        var baseUrl = _settings.BaseUrl.EndsWith("/") ? _settings.BaseUrl : _settings.BaseUrl + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Key", _settings.SecretKey);
    }

    public async Task<KhaltiInitiateResponse> InitiatePaymentAsync(
        KhaltiInitiateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initiating Khalti payment for order {OrderId}", request.PurchaseOrderId);

            var response = await _httpClient.PostAsJsonAsync(
                "initiate/", // Relative path (no leading slash)
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Khalti initiate failed: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Khalti initiate failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<KhaltiInitiateResponse>(cancellationToken);
            
            if (result == null)
                throw new InvalidOperationException("Khalti initiate response is null");

            _logger.LogInformation("Khalti payment initiated successfully. Pidx: {Pidx}", result.Pidx);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Khalti payment for order {OrderId}", request.PurchaseOrderId);
            throw;
        }
    }

    public async Task<KhaltiLookupResponse> LookupPaymentAsync(
        string pidx,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Looking up Khalti payment with Pidx: {Pidx}", pidx);

            var lookupRequest = new KhaltiLookupRequest { Pidx = pidx };

            var response = await _httpClient.PostAsJsonAsync(
                "lookup/", // Relative path
                lookupRequest,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Khalti returns 400 for Expired/Canceled/NotFound but with a valid JSON body
                // Try to parse it as a LookupResponse to see if we have a status
                try 
                {
                    var errorResult = JsonSerializer.Deserialize<KhaltiLookupResponse>(errorContent);
                    if (errorResult != null && !string.IsNullOrEmpty(errorResult.Status))
                    {
                        _logger.LogWarning("Khalti lookup returned {StatusCode} but with valid status: {Status}", response.StatusCode, errorResult.Status);
                        return errorResult;
                    }
                }
                catch
                {
                    // Ignore json parse error and throw original exception
                }

                _logger.LogError("Khalti lookup failed: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Khalti lookup failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<KhaltiLookupResponse>(cancellationToken);
            
            if (result == null)
                throw new InvalidOperationException("Khalti lookup response is null");

            _logger.LogInformation("Khalti lookup successful. Pidx: {Pidx}, Status: {Status}, Amount: {Amount}", 
                result.Pidx, result.Status, result.TotalAmount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up Khalti payment with Pidx: {Pidx}", pidx);
            throw;
        }
    }


public async Task<KhaltiRefundResponse> RefundPaymentAsync(
    string transactionId,
    long? amountInPaisa = null,
    string? mobile = null,
    CancellationToken cancellationToken = default)
{
    try
    {
        _logger.LogInformation(
            "Initiating Khalti refund for TransactionId: {TransactionId}, Amount: {Amount}, Mobile: {Mobile}", 
            transactionId, amountInPaisa, mobile);

        // Build refund request based on parameters
        var refundRequest = new KhaltiRefundRequest();
        
        if (amountInPaisa.HasValue)
        {
            refundRequest.Amount = amountInPaisa.Value;
        }
        
        if (!string.IsNullOrEmpty(mobile))
        {
            refundRequest.Mobile = mobile;
        }

        // Build complete URL using transaction_id (NOT pidx!)
        var refundBaseUrl = _settings.RefundUrl.TrimEnd('/');
        var fullUrl = $"{refundBaseUrl}/{transactionId}/refund/";
        
        _logger.LogInformation("Calling refund endpoint: {Url} with payload: {Payload}", 
            fullUrl, JsonSerializer.Serialize(refundRequest));

        // DIAGNOSTIC CONSOLE LOG
        Console.WriteLine("==========================================================");
        Console.WriteLine("[KHALTI API CALL DETAILS]");
        Console.WriteLine($"Full URL: {fullUrl}");
        Console.WriteLine($"Refund Base URL: {refundBaseUrl}");
        Console.WriteLine($"Transaction ID Parameter: {transactionId}");
        Console.WriteLine($"Request Body: {JsonSerializer.Serialize(refundRequest)}");
        Console.WriteLine("CRITICAL: Check if transactionId is actually Pidx!");
        Console.WriteLine("==========================================================");

        // Create dedicated HttpClient for refund (different base URL than payments)
        using var refundClient = new HttpClient();
        refundClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Key", _settings.SecretKey);

        var response = await refundClient.PostAsJsonAsync(
            fullUrl,
            refundRequest,
            cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Khalti refund failed: {StatusCode} - {Error}", 
                response.StatusCode, responseContent);
            throw new HttpRequestException($"Khalti refund failed: {response.StatusCode} - {responseContent}");
        }

        var result = JsonSerializer.Deserialize<KhaltiRefundResponse>(responseContent);
        
        if (result == null)
            throw new InvalidOperationException("Khalti refund response is null");

        _logger.LogInformation("Khalti refund successful. TransactionId: {TxnId}, Detail: {Detail}", 
            transactionId, result.Detail);

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing Khalti refund for TransactionId: {TransactionId}", transactionId);
        throw;
    }
}
}
