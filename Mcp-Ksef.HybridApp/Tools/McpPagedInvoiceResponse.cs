using System.Text.Json.Serialization;
using KSeF.Client.Core.Models.Invoices;

namespace McpKsef.HybridApp.Tools;

/// <summary>
/// One page of invoice metadata returned by the search tools.
/// Errors are not stored here: the tools throw McpException, which the MCP SDK reports as an error result.
/// </summary>
public class McpPagedInvoiceResponse : PagedInvoiceResponse
{
    /// <summary>Creates an empty page. Also used by System.Text.Json when deserializing.</summary>
    [JsonConstructor]
    public McpPagedInvoiceResponse()
    {
        Invoices = new List<InvoiceSummary>();
    }

    public McpPagedInvoiceResponse(PagedInvoiceResponse pagedInvoiceResponse)
    {
        Invoices = pagedInvoiceResponse.Invoices ?? new List<InvoiceSummary>();
        HasMore = pagedInvoiceResponse.HasMore;
        IsTruncated = pagedInvoiceResponse.IsTruncated;
        PermanentStorageHwmDate = pagedInvoiceResponse.PermanentStorageHwmDate;
    }
}
