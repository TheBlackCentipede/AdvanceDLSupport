﻿using AdvancedDLSupport.Tests.Data;
using AdvancedDLSupport.Tests.TestBases;
using Xunit;

namespace AdvancedDLSupport.Tests.Integration
{
    public class IndirectCallsIntegrationTests : LibraryTestBase<IIndirectCallLibrary>
    {
        private const string LibraryName = "IndirectCallTests";

        public IndirectCallsIntegrationTests() : base(LibraryName)
        {
        }

        protected override ImplementationOptions GetImplementationOptions()
        {
            return ImplementationOptions.UseIndirectCalls;
        }

        [Fact]
        public void CanCallSimpleFunction()
        {
            var result = Library.Multiply(5, 5);

            Assert.Equal(25, result);
        }

        [Fact]
        public void CanCallFunctionWithByRefParameter()
        {
            var data = new TestStruct { A = 5, B = 15 };
            var result = Library.GetStructAValueByRef(ref data);

            Assert.Equal(data.A, result);
        }

        [Fact]
        public void CanCallFunctionWithByValueParameter()
        {
            var data = new TestStruct { A = 5, B = 15 };
            var result = Library.GetStructAValueByValue(data);

            Assert.Equal(data.A, result);
        }

        [Fact]
        public void CanCallFunctionWithByRefReturnValue()
        {
            const int a = 5;
            const int b = 15;

            ref var result = ref Library.GetInitializedStructByRef(a, b);

            Assert.Equal(a, result.A);
            Assert.Equal(b, result.B);
        }

        [Fact]
        public void CanCallFunctionWithByValueReturnValue()
        {
            const int a = 5;
            const int b = 15;

            var result = Library.GetInitializedStructByValue(a, b);

            Assert.Equal(a, result.A);
            Assert.Equal(b, result.B);
        }

        [Fact]
        public void CanCallFunctionWithNullableReturnValue()
        {
            var result = Library.GetNullTestStruct();

            Assert.Null(result);
        }

        [Fact]
        public void CanCallFunctionWithNullableParameter()
        {
            var resultNull = Library.IsTestStructNull(null);
            var resultNotNull = Library.IsTestStructNull(new TestStruct());

            Assert.True(resultNull);
            Assert.False(resultNotNull);
        }
    }
}