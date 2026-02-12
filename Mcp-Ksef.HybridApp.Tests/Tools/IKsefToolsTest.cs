using System.Reflection;
using KSeF.Client.Core.Models.Invoices;

namespace McpKsef.HybridApp.Tools.Tests
{
    public class IKsefToolsTest
    {
        private static void AssertReturnsTaskOf(MethodInfo method, Type expectedGeneric)
        {
            Assert.NotNull(method);
            var returnType = method.ReturnType;
            Assert.True(returnType.IsGenericType, $"Method {method.Name} should return a generic Task<>.");
            Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());
            Assert.Equal(expectedGeneric, returnType.GetGenericArguments()[0]);
        }

        [Fact]
        public void Interface_IsPublicInterface()
        {
            var t = typeof(IKsefTools);
            Assert.True(t.IsInterface);
            Assert.True(t.IsPublic || t.IsNestedPublic);
        }

        [Fact]
        public void GetInvoice_MethodExists_ReturnsTaskOfString()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(nameof(IKsefTools.GetInvoice), [typeof(string)]);
            AssertReturnsTaskOf(m, typeof(string));
        }

        [Fact]
        public void GetInvoicesListForGivenDate_MethodExists_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(nameof(IKsefTools.GetInvoicesListForGivenDate), [typeof(DateTime), typeof(DateTime)]);
            AssertReturnsTaskOf(m, typeof(PagedInvoiceResponse));
        }

        [Fact]
        public void GetInvoiceByInvoiceNumber_MethodExists_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(nameof(IKsefTools.GetInvoiceByInvoiceNumber), [typeof(string)]);
            AssertReturnsTaskOf(m, typeof(PagedInvoiceResponse));
        }

        [Fact]
        public void GetInvoiceByBuyerNip_MethodExists_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(nameof(IKsefTools.GetInvoiceByBuyerNip), [typeof(string)]);
            AssertReturnsTaskOf(m, typeof(PagedInvoiceResponse));
        }

        [Fact]
        public void GetInvoiceByBuyerVatUe_MethodExists_ReturnsTaskOfPagedInvoiceResponse()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(nameof(IKsefTools.GetInvoiceByBuyerVatUe), [typeof(string)]);
            AssertReturnsTaskOf(m, typeof(PagedInvoiceResponse));
        }

        [Fact]
        public void GetInvoiceUrl_MethodExists_ReturnsTaskOfString()
        {
            var t = typeof(IKsefTools);
            var m = t.GetMethod(nameof(IKsefTools.GetInvoiceUrl), [typeof(string)]);
            AssertReturnsTaskOf(m, typeof(string));
        }

        [Fact]
        public void Interface_HasExpectedNumberOfMethods()
        {
            var t = typeof(IKsefTools);
            var methods = t.GetMethods();
            Assert.Equal(6, methods.Length);
            var expectedNames = new[]
            {
                nameof(IKsefTools.GetInvoice),
                nameof(IKsefTools.GetInvoicesListForGivenDate),
                nameof(IKsefTools.GetInvoiceByInvoiceNumber),
                nameof(IKsefTools.GetInvoiceByBuyerNip),
                nameof(IKsefTools.GetInvoiceByBuyerVatUe),
                nameof(IKsefTools.GetInvoiceUrl)
            };
            Assert.True(expectedNames.All(n => methods.Any(m => m.Name == n)));
        }
    }
}