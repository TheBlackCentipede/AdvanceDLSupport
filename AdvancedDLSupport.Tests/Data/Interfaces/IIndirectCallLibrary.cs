﻿namespace AdvancedDLSupport.Tests.Data
{
    public interface IIndirectCallLibrary
    {
        int Multiply(int a, int b);

        int GetStructAValueByRef(ref TestStruct strct);
        int GetStructAValueByValue(TestStruct strct);

        ref TestStruct GetInitializedStructByRef(int a, int b);
        TestStruct GetInitializedStructByValue(int a, int b);

        TestStruct? GetNullTestStruct();
        bool IsTestStructNull(TestStruct? strct);
    }
}
