using Shared.Consts;

namespace McpKsef.HybridApp.Helpers.Tests
{
    public class RunInfoHelperTest
    {
        private (bool result, string output) RunWithEnv(string ksefValue, string vatValue)
        {
            var originalOut = Console.Out;
            var sw = new StringWriter();
            Console.SetOut(sw);

            var origKsef = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);
            var origVat = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);

            try
            {
                Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, ksefValue);
                Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, vatValue);

                var result = RunInfoHelper.IsSettingsValidToRun();
                Console.Out.Flush();
                return (result, sw.ToString());
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, origKsef);
                Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, origVat);
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void BothSet_ReturnsTrue_NoOutput()
        {
            var (result, output) = RunWithEnv("valid-token", "PL1234567890");
            Assert.True(result);
            Assert.True(string.IsNullOrWhiteSpace(output));
        }

        [Fact]
        public void MissingKsef_ReturnsFalse_WritesKsefMessage()
        {
            var (result, output) = RunWithEnv(null, "PL1234567890");
            Assert.False(result);
            Assert.Contains(EnvironmentConsts.KsefToken, output);
            Assert.Contains("KSeF", output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MissingVat_ReturnsTrue_WritesVatMessage()
        {
            var (result, output) = RunWithEnv("valid-token", null);
            Assert.True(result);
            Assert.Contains(EnvironmentConsts.VatId, output);
        }

        [Fact]
        public void BothMissing_ReturnsFalse_WritesBothMessages()
        {
            var (result, output) = RunWithEnv(null, null);
            Assert.False(result);
            Assert.Contains(EnvironmentConsts.KsefToken, output);
            Assert.Contains(EnvironmentConsts.VatId, output);
        }
    }
}