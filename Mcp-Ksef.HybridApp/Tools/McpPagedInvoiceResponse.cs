using KSeF.Client.Core.Models.Invoices;

namespace McpKsef.HybridApp.Tools;

public class McpPagedInvoiceResponse : PagedInvoiceResponse
{
    public bool Error => !string.IsNullOrEmpty(ErrorMessage);

    public string ErrorMessage { get; set; }
    
    public McpPagedInvoiceResponse(PagedInvoiceResponse pagedInvoiceResponse)
    {
        Invoices = pagedInvoiceResponse.Invoices;
        HasMore = pagedInvoiceResponse.HasMore;
        IsTruncated = pagedInvoiceResponse.IsTruncated;
        PermanentStorageHwmDate = pagedInvoiceResponse.PermanentStorageHwmDate;
    }

    public McpPagedInvoiceResponse(string message)
    {
        ErrorMessage = message;
    }
}