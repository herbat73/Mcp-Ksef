using KSeF.Client.Core.Models.Invoices;
using ModelContextProtocol.Protocol;

namespace McpKsef.HybridApp.Tools;

public interface IKsefTools
{
    Task<string> GetInvoice(string ksefNumber, CancellationToken cancellationToken);
    Task<PagedInvoiceResponse> GetInvoicesListForGivenDate(
        DateTime dataFakturyOd,
        DateTime dataFakturyDo,
        CancellationToken cancellationToken);
    Task<PagedInvoiceResponse> GetInvoiceByInvoiceNumber(string invoiceNumber, CancellationToken cancellationToken);
    Task<PagedInvoiceResponse> GetInvoiceByBuyerNip(string nip, CancellationToken cancellationToken);
    Task<PagedInvoiceResponse> GetInvoiceByBuyerVatUe(string vatUe, CancellationToken cancellationToken);
    Task<string> GetInvoiceUrl(string ksefNumber, CancellationToken cancellationToken);
    Task<IEnumerable<ContentBlock>> GetInvoiceQrWithKsef(string ksefNumber, CancellationToken cancellationToken);
}