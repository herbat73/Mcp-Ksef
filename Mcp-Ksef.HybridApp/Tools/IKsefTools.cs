namespace McpKsef.HybridApp.Tools;

public interface IKsefTools
{
    Task<string> GetInvoice(string ksefReferenceNumber);
}