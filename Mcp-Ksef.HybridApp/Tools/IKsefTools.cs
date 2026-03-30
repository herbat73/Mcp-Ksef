using KSeF.Client.Core.Models.Invoices;
using ModelContextProtocol.Protocol;

namespace McpKsef.HybridApp.Tools;

public interface IKsefTools
{
    Task<string> GetInvoice(string ksefNumber, CancellationToken cancellationToken);
    Task<PagedInvoiceResponse> QueryInvoices(
        DateTime dataFakturyOd,
        DateTime dataFakturyDo,
        CancellationToken cancellationToken,
        InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1,
        DateType dateType = DateType.Issue
    );
    Task<PagedInvoiceResponse> GetInvoiceByInvoiceNumber(string invoiceNumber, CancellationToken cancellationToken);
    Task<PagedInvoiceResponse> GetInvoiceByBuyerNip(string nip, CancellationToken cancellationToken);
    Task<PagedInvoiceResponse> GetInvoiceByBuyerVatUe(string vatUe, CancellationToken cancellationToken);
    Task<string> GetInvoiceUrl(string ksefNumber, CancellationToken cancellationToken);
    Task<IEnumerable<ContentBlock>> GetInvoiceQrWithKsef(string ksefNumber, CancellationToken cancellationToken);
}