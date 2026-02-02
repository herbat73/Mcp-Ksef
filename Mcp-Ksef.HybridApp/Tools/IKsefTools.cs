using KSeF.Client.Core.Models.Invoices;

namespace McpKsef.HybridApp.Tools;

public interface IKsefTools
{
    Task<string> GetInvoice(string ksefNumber);
    Task<PagedInvoiceResponse> GetInvoicesListForGivenDate(
        DateTime dataFakturyOd,
        DateTime dataFakturyDo);
}