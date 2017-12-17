using System;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Linq;
public class DLSupport
{
    static ModuleBuilder moduleBuilder;
    static AssemblyBuilder assemblyBuilder;
    static DLSupport()
    {
        IsOnWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DLSupportAssembly"), AssemblyBuilderAccess.Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule("DLSupportModule");
    }
    static bool IsOnWindows;

    [DllImport("dl")]
    static extern IntPtr dlopen(string path, int flag = 1);
    [DllImport("dl")]
    static extern IntPtr dlsym(IntPtr handle, string symbol);
    [DllImport("dl")]
    static extern int dlclose(IntPtr handle);
    [DllImport("dl")]
    static extern IntPtr dlerror();

    [DllImport("kernel32.dll")]
    static extern IntPtr LoadLibrary(string path);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetProcAddress(IntPtr handle, string symbol);

    [DllImport("kernel32.dll")]
    static extern bool FreeLibrary(IntPtr handle);
    
    private IntPtr libraryHandle {get;set;}
    
    public DLSupport(string path)
    {
        if (IsOnWindows)
        {
            libraryHandle = LoadLibrary(path);
            if (libraryHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        else
        {
            libraryHandle = dlopen(path);
            if (libraryHandle == IntPtr.Zero)
            {
                var errorPtr = dlerror();
                if (errorPtr == IntPtr.Zero)
                    throw new Exception("Library could not be loaded and error information from dl library could not be found...");
                var msg = Marshal.PtrToStringAuto(errorPtr);
                throw new Exception(string.Format("Library could not be loaded: {0}", msg));
            }
        }
    }
    public IntPtr LoadSymbol(string sym) =>
        IsOnWindows ? GetProcAddress(libraryHandle, sym) : dlsym(libraryHandle, sym);

    public T LoadFunction<T>(string sym) {
        var symPointer = LoadSymbol(sym);
        return Marshal.GetDelegateForFunctionPointer<T>(symPointer);
    }

    /// <summary>
    /// Unsafe Dispose will free the loaded library handle, if any of the functions or variables are still in use after this library handle is freed,
    /// segmentation fault will occur and can potentially crash the Runtime. Do note however that Library Handle can be shared between the Runtime and this class, 
    /// so if that library handle is freed then both Runtime and this class will be affected.
    /// In normal use case, this shouldn't be used at all.
    /// </summary>
    public void UnsafeDispose()
    {
        if (IsOnWindows)
            FreeLibrary(libraryHandle);
        else
            dlclose(libraryHandle);
    }

    static bool IsMethodAcceptable(MethodInfo info)
    {
        return !info.ReturnParameter.ParameterType.IsClass &&
                !info.GetParameters().Any(I => I.ParameterType.IsClass);
    }

    /// <summary>
    /// Attempts to resolve interface to C Library via C# Interface by dynamically creating C# Class during runtime
    /// and return a new instance of the said class. This approach does not resolve any C++ implication such as name manglings.
    /// </summary>
    public static T ResolveAndActivateInterface<T>(string libraryPath)
    {
        var type = typeof(T);
        if (!type.IsInterface) throw new Exception("The generic argument type must be an interface! Please review the documentation on how to use this.");

        // Let's determine a name for our class!
        string typeName;
        if (type.Name.StartsWith("I"))
            typeName = type.Name.Substring(1);
        else
            typeName = $"Generated_{type.Name}";

        if (string.IsNullOrWhiteSpace(typeName))
            typeName = $"Generated_{type.Name}";
        
        //typeName = $"{typeName}{Guid.NewGuid().ToString().Replace("-", "_")}";
        // Let's create a new type!
        var typeBuilder = moduleBuilder.DefineType(typeName,
            TypeAttributes.AutoClass | TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed,
            typeof(DLSupport), new [] {type});

                // Now the constructor
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
            CallingConventions.Standard, new [] {typeof(string)});

        constructorBuilder.DefineParameter(1, ParameterAttributes.In, "libraryPath");
        var il = constructorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // Load instance
        il.Emit(OpCodes.Ldarg_1); // Load libraryPath parameter
        il.Emit(OpCodes.Call, typeof(DLSupport).GetConstructors().First(I => 
            I.GetParameters().Length == 1 &&
            I.GetParameters()[0].ParameterType == typeof(string)));
        // Let's define our methods!
        foreach (var method in type.GetMethods())
        {
            var parameters = method.GetParameters();

            // Declare a delegate type!
            var delegateBuilder = moduleBuilder.DefineType($"{method.Name}_dt", 
                                                    TypeAttributes.Class | TypeAttributes.Public |
                                                    TypeAttributes.Sealed | TypeAttributes.AnsiClass |
                                                    TypeAttributes.AutoClass, typeof(MulticastDelegate));

            ConstructorBuilder delegateCtorBuilder = delegateBuilder.DefineConstructor(MethodAttributes.RTSpecialName | MethodAttributes.HideBySig |
                                                                                  MethodAttributes.Public, CallingConventions.Standard, 
                                                                                  new Type[] { typeof(object), typeof(System.IntPtr) });

            delegateCtorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var delegateMethodBuilder = delegateBuilder.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | 
                                                                   MethodAttributes.NewSlot | MethodAttributes.Virtual, 
                                                                   method.ReturnType, parameters.Select(I => I.ParameterType).ToArray());

            delegateMethodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var delegateBuilderType = delegateBuilder.CreateType();

            // Create a delegate property!
            var delegateField = typeBuilder.DefineField($"{method.Name}_dtm", delegateBuilderType, FieldAttributes.Public);
            var methodBuilder = typeBuilder.DefineMethod(method.Name, 
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot, 
                System.Reflection.CallingConventions.Standard, 
                method.ReturnType, 
                parameters.Select(I => I.ParameterType).ToArray());

            // Let's create a method that simply invoke the delegate
            var methodIL = methodBuilder.GetILGenerator();
            methodIL.Emit(OpCodes.Ldarg_0);
            methodIL.Emit(OpCodes.Ldfld, delegateField);
            for (int I = 1; I <= parameters.Length; I++)
                methodIL.Emit(OpCodes.Ldarg, I);
            methodIL.EmitCall(OpCodes.Call, delegateBuilderType.GetMethod("Invoke"), null);
            methodIL.Emit(OpCodes.Ret);

            // Assign Delegate from Function Pointer
            il.Emit(OpCodes.Ldarg_0); // This is for storing field delegate, it needs the "this" reference
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, method.Name);
            il.EmitCall(OpCodes.Call, typeof(DLSupport).GetMethod("LoadFunction").MakeGenericMethod(delegateBuilderType), null);
            il.Emit(OpCodes.Stfld, delegateField);
        }
        foreach (var property in type.GetProperties())
        {
            if (property.CanRead)
            {
                
            }

            if (property.CanWrite)
            {

            }
        }
        il.Emit(OpCodes.Ret);
        return (T)Activator.CreateInstance(typeBuilder.CreateType(), libraryPath);
    }
}