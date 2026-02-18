namespace McpKsef.HybridApp.Helpers;

public class InfoHelperResultVo
{
    public bool IsKsefCertificateValid { get; set; }
    public bool IsKsefTokenValid { get; set; }
    public bool IsVatIdValid {get; set; }
    public bool IsValid => (IsKsefTokenValid || IsKsefCertificateValid) && IsVatIdValid;
}