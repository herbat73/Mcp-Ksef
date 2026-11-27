using KSeF.Client.Core.Models.Invoices;
using ModelContextProtocol.Protocol;

namespace McpKsef.HybridApp.Tools;

public interface IKsefTools
{
    Task<string> GetInvoice(string ksefNumber, CancellationToken cancellationToken);
    Task<McpPagedInvoiceResponse> QueryInvoices(
        DateTime dataFakturyOd,
        DateTime dataFakturyDo,
        CancellationToken cancellationToken,
        InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1,
        DateType dateType = DateType.Issue
    );
    Task<McpPagedInvoiceResponse> GetInvoiceByInvoiceNumber(string invoiceNumber, CancellationToken cancellationToken, InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1, DateType dateType = DateType.Issue);
    Task<McpPagedInvoiceResponse> GetInvoiceByBuyerNip(string nip, CancellationToken cancellationToken, InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1, DateType dateType = DateType.Issue);
    Task<McpPagedInvoiceResponse> GetInvoiceByBuyerVatUe(string vatUe, CancellationToken cancellationToken, InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1, DateType dateType = DateType.Issue);
    Task<string> GetInvoiceUrl(string ksefNumber, CancellationToken cancellationToken, InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1, DateType dateType = DateType.Issue);
    Task<IEnumerable<ContentBlock>> GetInvoiceQrWithKsef(string ksefNumber, CancellationToken cancellationToken, InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1, DateType dateType = DateType.Issue);
}