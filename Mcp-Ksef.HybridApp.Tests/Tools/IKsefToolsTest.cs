using System.Reflection;
using KSeF.Client.Core.Models.Invoices;
using ModelContextProtocol.Protocol;

namespace McpKsef.HybridApp.Tools.Tests
{
    public class IKsefToolsTest
    {
        private static void AssertMethodParameters(
        MethodInfo method,
        params (string Name, Type Type)[] expectedParameters)
        {
            Assert.NotNull(method);

            var parameters = method.GetParameters();
            Assert.Equal(expectedParameters.Length, parameters.Length);

            for (var i = 0; i < expectedParameters.Length; i++)
            {
                Assert.Equal(expectedParameters[i].Name, parameters[i].Name);
                Assert.Equal(expectedParameters[i].Type, parameters[i].ParameterType);
            }
        }   

        [Fact]
        public void GetInvoice_MethodExists_WithCancellationToken_ReturnsTaskOfString()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(nameof(IKsefTools.GetInvoice), [typeof(string), typeof(CancellationToken)]);

            AssertReturnsTaskOf(m!, typeof(string));
            AssertMethodParameters(m!,
                ("ksefNumber", typeof(string)),
                ("cancellationToken", typeof(CancellationToken)));
        }

        [Fact]
        public void GetInvoicesListForGivenDate_MethodExists_WithCancellationToken_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(
                nameof(IKsefTools.GetInvoicesListForGivenDate),
                [typeof(DateTime), typeof(DateTime), typeof(CancellationToken)]);

            AssertReturnsTaskOf(m!, typeof(PagedInvoiceResponse));
            AssertMethodParameters(m!,
                ("dataFakturyOd", typeof(DateTime)),
                ("dataFakturyDo", typeof(DateTime)),
                ("cancellationToken", typeof(CancellationToken)));
        }

        [Fact]
        public void GetInvoiceByInvoiceNumber_MethodExists_WithCancellationToken_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(
                nameof(IKsefTools.GetInvoiceByInvoiceNumber),
                [typeof(string), typeof(CancellationToken)]);

            AssertReturnsTaskOf(m!, typeof(PagedInvoiceResponse));
            AssertMethodParameters(m!,
                ("invoiceNumber", typeof(string)),
                ("cancellationToken", typeof(CancellationToken)));
        }

        [Fact]
        public void GetInvoiceByBuyerNip_MethodExists_WithCancellationToken_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(
                nameof(IKsefTools.GetInvoiceByBuyerNip),
                [typeof(string), typeof(CancellationToken)]);

            AssertReturnsTaskOf(m!, typeof(PagedInvoiceResponse));
            AssertMethodParameters(m!,
                ("nip", typeof(string)),
                ("cancellationToken", typeof(CancellationToken)));
        }

        [Fact]
        public void GetInvoiceByBuyerVatUe_MethodExists_WithCancellationToken_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(
                nameof(IKsefTools.GetInvoiceByBuyerVatUe),
                [typeof(string), typeof(CancellationToken)]);

            AssertReturnsTaskOf(m!, typeof(PagedInvoiceResponse));
            AssertMethodParameters(m!,
                ("vatUe", typeof(string)),
                ("cancellationToken", typeof(CancellationToken)));
        }

        [Fact]
        public void GetInvoiceUrl_MethodExists_WithCancellationToken_ReturnsTaskOfString()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(
                nameof(IKsefTools.GetInvoiceUrl),
                [typeof(string), typeof(CancellationToken)]);

            AssertReturnsTaskOf(m!, typeof(string));
            AssertMethodParameters(m!,
                ("ksefNumber", typeof(string)),
                ("cancellationToken", typeof(CancellationToken)));
        }
        
        [Fact]
        public void GetInvoiceQrWithKsef_MethodExists_WithCancellationToken_ReturnsTaskOfString()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(
                nameof(IKsefTools.GetInvoiceQrWithKsef),
                [typeof(string), typeof(CancellationToken)]);

            AssertReturnsTaskOf(m!, typeof(IEnumerable<ContentBlock>));
            AssertMethodParameters(m!,
                ("ksefNumber", typeof(string)),
                ("cancellationToken", typeof(CancellationToken)));
        }

        [Fact]
        public void Interface_HasExpectedMethodSet_AndExactSignatures()
        {
            var t = typeof(IKsefTools);
            var methods = t.GetMethods();

            Assert.Equal(7, methods.Length);

            var expected = new Dictionary<string, Type[]>
            {
                [nameof(IKsefTools.GetInvoice)] = [typeof(string), typeof(CancellationToken)],
                [nameof(IKsefTools.GetInvoicesListForGivenDate)] = [typeof(DateTime), typeof(DateTime), typeof(CancellationToken)],
                [nameof(IKsefTools.GetInvoiceByInvoiceNumber)] = [typeof(string), typeof(CancellationToken)],
                [nameof(IKsefTools.GetInvoiceByBuyerNip)] = [typeof(string), typeof(CancellationToken)],
                [nameof(IKsefTools.GetInvoiceByBuyerVatUe)] = [typeof(string), typeof(CancellationToken)],
                [nameof(IKsefTools.GetInvoiceUrl)] = [typeof(string), typeof(CancellationToken)],
                [nameof(IKsefTools.GetInvoiceQrWithKsef)] = [typeof(string), typeof(CancellationToken)]
            };

            foreach (var (name, signature) in expected)
            {
                var matched = methods.Where(m =>
                    m.Name == name &&
                    m.GetParameters().Select(p => p.ParameterType).SequenceEqual(signature)).ToList();

                Assert.Single(matched);
            }
        }
        
        private static void AssertReturnsTaskOf(MethodInfo method, Type expectedGeneric)
        {
            Assert.NotNull(method);
            var returnType = method.ReturnType;
            Assert.True(returnType.IsGenericType, $"Method {method.Name} should return a generic Task<>.");
            Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());
            Assert.Equal(expectedGeneric, returnType.GetGenericArguments()[0]);
        }
    }
}