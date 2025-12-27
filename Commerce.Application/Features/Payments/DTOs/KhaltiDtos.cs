using System.Text.Json.Serialization;

namespace Commerce.Application.Features.Payments.DTOs;

public class KhaltiInitiateRequest
{
    [JsonPropertyName("return_url")]
    public string ReturnUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("website_url")]
    public string WebsiteUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("amount")]
    public long Amount { get; set; }  // in paisa (NPR * 100)
    
    [JsonPropertyName("purchase_order_id")]
    public string PurchaseOrderId { get; set; } = string.Empty;
    
    [JsonPropertyName("purchase_order_name")]
    public string PurchaseOrderName { get; set; } = string.Empty;
    
    [JsonPropertyName("customer_info")]
    public KhaltiCustomerInfo CustomerInfo { get; set; } = null!;
}

public class KhaltiCustomerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;
}

public class KhaltiInitiateResponse
{
    [JsonPropertyName("pidx")]
    public string Pidx { get; set; } = string.Empty;
    
    [JsonPropertyName("payment_url")]
    public string PaymentUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }
}

public class KhaltiLookupRequest
{
    [JsonPropertyName("pidx")]
    public string Pidx { get; set; } = string.Empty;
}

public class KhaltiLookupResponse
{
    [JsonPropertyName("pidx")]
    public string Pidx { get; set; } = string.Empty;
    
    [JsonPropertyName("total_amount")]
    public long TotalAmount { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;  // "Completed", "Pending", "Initiated", "Expired", "User canceled"
    
    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }
    
    [JsonPropertyName("fee")]
    public long Fee { get; set; }
    
    [JsonPropertyName("refunded")]
    public bool Refunded { get; set; }
}
