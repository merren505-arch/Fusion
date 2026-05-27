// filepath: FusionDeweaver.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace FusionDeweaver;

class Program
{
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Console.Error.WriteLine($"UNHANDLED FATAL: {ex?.Message}");
            Console.Error.WriteLine(ex?.StackTrace);
            Environment.Exit(99);
        };

        if (args.Length < 2)
        {
            Console.WriteLine("FusionDeweaver - Photon Fusion 1 & 2 IL De-Weaver (APEX Master Edition v9.0)");
            Console.WriteLine("Usage: FusionDeweaver <input.dll> <output.dll> [fusion_dll_dir]");
            return 1;
        }

        var inputPath = Path.GetFullPath(args[0]);
        var outputPath = Path.GetFullPath(args[1]);
        var fusionDir = args.Length > 2 ? Path.GetFullPath(args[2]) : Path.GetDirectoryName(inputPath) ?? ".";

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return 1;
        }

        try
        {
            var deweaver = new DeweaverEngine(inputPath, outputPath, fusionDir);
            deweaver.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
    }
}

class DeweaverEngine
{
    private readonly string _inputPath;
    private readonly string _outputPath;
    private readonly string _fusionDir;
    private AssemblyDefinition _asm = null!;
    private ModuleDefinition _module = null!;
    private TolerantAssemblyResolver _resolver = null!;

    private readonly Dictionary<string, FieldDefinition> _newBackingFields = new();
    private HashSet<string>? _originalTypeRefNames;
    private bool _isFusion2;

    private int _removedAssemblyAttrs;
    private int _removedInvokerMethods;
    private int _restoredRpcMethods;
    private int _removedWeavedAttrs;
    private int _removedDefaultFields;
    private int _restoredBackingFields;
    private int _removedCopyMethods;
    private int _removedIl2cppFields;
    private int _removedCacheFields;
    private int _removedInterfaceImpls;
    private int _removedElementRwMethods;
    private int _restoredPropertyBodies;
    private int _restoredStructLayouts;
    private int _removedGeneratedTypes;
    private int _restoredConstructors;
    private int _removedFixedStorageFields;
    private int _removedCodeGenFieldRefs;
    private int _removedCodeGenMethodRefs;
    private int _removedCctorCalls;
    private int _ensuredNetworkedAttrs;
    private int _removedPtrNullChecks;
    private int _preservedNetworkedMetadata;
    private int _purgedPhantomTypes;
    private int _purgedCodeGenTypeRefs;
    private int _purgedCodeGenMemberRefs;
    private int _removedOnChangedRenderMethods;
    private int _cleanedNetworkStringProps;
    private int _scrubbedParamReturnAttrs;
    private int _removedCodeGenAsmRefs;
    private int _removedInitializeMethods;
    private int _removedDrawIfAttrs;
    private int _removedInvokeWeavedCodeCalls;
    private int _removedPreserveAttrs;
    private int _removedInternalOnCalls;
    private int _cleanedOrphanedTokens;
    private int _removedBurstJobRegistrations;
    private int _restoredRefPropertyInitializers;
    private int _removedInvalidRefReturnSetters;
    private int _structBackingFieldInits;
    private int _removedWeaverHelperMethods;
    private int _migratedAttributes;
    private int _unwrappedProxyAttributes;
    private int _recoveredCtorAssignments;
    private int _retainILPropertiesPreserved;
    private int _restoredCollectionInits;
    private int _removedNetworkAssemblyIgnore;
    private int _preservedUserDefaultFields;
    private int _removedDoubleInits;
    private int _removedMethodImplAttrs;
    private int _revertedStructPropsToFields;
    private int _removedReadWriteHelperMethods;
    private int _removedFusionNetworkAssemblyIgnore;
    private int _sanitizedTrivialCtors;
    private int _revertedStringPropTypes;
    private int _migratedFieldAccuracyAttrs;
    private int _migratedFieldCapacityAttrs;
    private int _accessorCompilerGeneratedAttrs;
    private int _createdSetters;
    private int _scrubbedOrphanedFusionTypeRefs;
    private int _importSanitizedRefs;
    private int _invalidMethodBodiesFixed;
    private int _stackMergeFixes;
    private int _deadBranchesFixed;
    private int _deadSequencesCleaned;

    private readonly HashSet<string> _processedNetworkBehaviours = new();
    private readonly HashSet<string> _modifiedMethods = new();
    private readonly Dictionary<string, List<Instruction>> _capturedCtorInits = new();

    public DeweaverEngine(string inputPath, string outputPath, string fusionDir)
    {
        _inputPath = inputPath;
        _outputPath = outputPath;
        _fusionDir = fusionDir;
    }

    private void MarkMethodModified(MethodDefinition method)
    {
        _modifiedMethods.Add($"{method.DeclaringType.FullName}::{method.Name}");
    }

    private void PrepareMethod(MethodBody body)
    {
        body.SimplifyMacros();
    }

    private void FinalizeMethod(MethodBody body)
    {
        if (body.Instructions.Count > 0)
        {
            var usedVariables = new HashSet<VariableDefinition>();
            int limit = body.Instructions.Count;
            for (int i = 0; i < limit; i++)
            {
                var variable = ResolveLocVariable(body.Instructions[i], body);
                if (variable != null)
                {
                    usedVariables.Add(variable);
                }
            }

            for (int i = body.Variables.Count - 1; i >= 0; i--)
            {
                if (!usedVariables.Contains(body.Variables[i]))
                {
                    body.Variables.RemoveAt(i);
                }
            }
        }
        body.OptimizeMacros();
    }

    private void IsolateDeadBlock(ILProcessor il, Instruction start, Instruction end)
    {
        var target = end.Next;
        if (target == null)
        {
            target = il.Create(OpCodes.Ret);
            il.Append(target);
        }

        var br = il.Create(OpCodes.Br, target);
        il.InsertBefore(start, br);

        var current = start;
        int safetyLimit = 5000;
        while (current != null && current != target && safetyLimit-- > 0)
        {
            if (current.Operand is MemberReference)
            {
                current.Operand = null;
            }
            current = current.Next;
        }
    }

    public void Run()
    {
        Console.WriteLine($"[Deweaver] Loading assembly: {_inputPath}");

        _resolver = new TolerantAssemblyResolver(new DefaultAssemblyResolver());
        _resolver.AddSearchDirectory(Path.GetDirectoryName(_inputPath) ?? ".");
        _resolver.AddSearchDirectory(_fusionDir);
        if (Directory.Exists(_fusionDir))
        {
            var dirs = Directory.GetDirectories(_fusionDir);
            int dLimit = Math.Min(dirs.Length, 100);
            for (int i = 0; i < dLimit; i++)
            {
                _resolver.AddSearchDirectory(dirs[i]);
            }
        }

        _asm = AssemblyDefinition.ReadAssembly(_inputPath, new ReaderParameters
        {
            AssemblyResolver = _resolver,
            ReadWrite = false,
            InMemory = true,
            ReadingMode = ReadingMode.Deferred
        });
        _module = _asm.MainModule;
        PreResolveCoreLibraries();

        _originalTypeRefNames = new HashSet<string>(_module.GetTypeReferences().Select(tr => tr.FullName));
        DetectFusionVersion();

        RunStep("Step 1", Step1_RemoveAssemblyWeavedAttribute);
        RunStep("Step 2", Step2_RemoveRpcWeaving);
        RunStep("Step 3", Step3_RemoveNetworkBehaviourWeaving);
        RunStep("Step 3b", Step3b_MigrateAttributesFromWeaverFields);
        RunStep("Step 3c", Step3c_UnwrapUnityPropertyAttributeProxies);
        RunStep("Step 3d", Step3d_RecoverConstructorAssignments);
        RunStep("Step 3e", Step3e_RestoreCollectionInitializers);
        RunStep("Step 4", Step4_RemoveStructWeaving);
        RunStep("Step 5", Step5_RemoveGeneratedTypes);
        RunStep("Step 6", Step6_PurgeCodeGenReferences);
        RunStep("Step 7", Step7_CleanStaticConstructors);
        RunStep("Step 7b", Step7b_CleanOrphanedMetadataTokens);
        RunStep("Step 7c", Step7c_RemoveBurstJobRegistration);
        RunStep("Step 8", Step8_ScrubCodeGenReferences);
        RunStep("Step 9", Step9_CleanupOrphanedReferences);
        RunStep("Step 10", Step10_ExhaustiveAttributeScrubbing);
        RunStep("Step 11", Step11_RemoveCodeGenAssemblyReferences);
        RunStep("Step 12", Step12_RemoveFusion2SpecificWeaving);
        RunStep("Step 13", Step13_RestoreRefPropertyInitializers);
        RunStep("Step 14", Step14_RemoveInvalidRefReturnSetters);
        RunStep("Step 15", Step15_EnsureStructBackingFieldInit);
        RunStep("Step 16", Step16_SanitizeTrivialConstructors);
        RunStep("Step 16b", Step16b_FixGlobalILArtifacts);
        RunStep("Step 17", Step17_SanitizeCrossModuleReferences);
        RunStep("Step 18", Step18_ValidateAndFixMethodBodies);
        RunStep("Step 19", Step19_FinalILCleanup);
        RunStep("Step 20", Step20_SanitizeUnsafePointerConversions);

        PreserveOriginalTypeReferences();

        Console.WriteLine($"[Deweaver] Writing output: {_outputPath}");
        Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);

        try
        {
            _asm.Write(_outputPath, new WriterParameters { WriteSymbols = false });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: Write failure. Diagnostics diagnostic follow.");
            DiagnoseUnimportedReferences();
            throw;
        }
        finally
        {
            _asm.Dispose();
            _resolver.Dispose();
        }

        PrintStatistics();
    }

    private void PreResolveCoreLibraries()
    {
        string[] coreLibs = { "mscorlib", "System.Runtime", "System", "System.Core", "netstandard", "System.Private.CoreLib" };
        foreach (var lib in coreLibs)
        {
            try
            {
                var name = new AssemblyNameReference(lib, new Version(0, 0, 0, 0));
                _resolver.Resolve(name);
            }
            catch { }
        }
    }

    private void RunStep(string name, Action step)
    {
        Console.WriteLine($"[{name}] Starting...");
        try
        {
            step();
            Console.WriteLine($"[{name}] Completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{name}] FATAL STEP ERROR: {ex.Message}");
        }
    }

    private void DetectFusionVersion()
    {
        _isFusion2 = false;
        foreach (var asmRef in _module.AssemblyReferences)
        {
            if (asmRef.Name == "Fusion.Runtime" || asmRef.Name == "Fusion.Sockets")
            {
                _isFusion2 = true;
                break;
            }
        }

        if (!_isFusion2)
        {
            var types = GetAllTypes();
            foreach (var type in types)
            {
                foreach (var attr in type.CustomAttributes)
                {
                    if (attr.AttributeType.Name == "NetworkRpcStaticWeavedInvokerAttribute")
                    {
                        _isFusion2 = true;
                        return;
                    }
                }
            }
        }
        Console.WriteLine($"[Deweaver] Fusion version detected: {(_isFusion2 ? "2.x" : "1.x")}");
    }

    private void Step1_RemoveAssemblyWeavedAttribute()
    {
        var attrs = _asm.CustomAttributes.Where(a => a.AttributeType.Name == "NetworkAssemblyWeavedAttribute").ToList();
        foreach (var attr in attrs)
        {
            _asm.CustomAttributes.Remove(attr);
            _removedAssemblyAttrs++;
        }

        var ignoreAttrs = _asm.CustomAttributes.Where(a =>
            a.AttributeType.Name == "NetworkAssemblyIgnoreAttribute" ||
            a.AttributeType.FullName == "Fusion.NetworkAssemblyIgnoreAttribute").ToList();
        foreach (var attr in ignoreAttrs)
        {
            _asm.CustomAttributes.Remove(attr);
            _removedNetworkAssemblyIgnore++;
        }

        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            var typeAttrs = type.CustomAttributes.Where(a =>
                a.AttributeType.Name == "NetworkAssemblyIgnoreAttribute" ||
                a.AttributeType.FullName == "Fusion.NetworkAssemblyIgnoreAttribute").ToList();
            foreach (var attr in typeAttrs)
            {
                type.CustomAttributes.Remove(attr);
                _removedFusionNetworkAssemblyIgnore++;
            }

            foreach (var method in type.Methods.ToList())
            {
                var methodAttrs = method.CustomAttributes.Where(a =>
                    a.AttributeType.Name == "NetworkAssemblyIgnoreAttribute" ||
                    a.AttributeType.FullName == "Fusion.NetworkAssemblyIgnoreAttribute").ToList();
                foreach (var attr in methodAttrs)
                {
                    method.CustomAttributes.Remove(attr);
                    _removedFusionNetworkAssemblyIgnore++;
                }
            }
        }
    }

    private void Step2_RemoveRpcWeaving()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            var invokers = type.Methods.Where(m => m.Name.Contains("@Invoker")).ToList();
            var invokerMap = new Dictionary<string, string>();
            foreach (var invoker in invokers)
            {
                string originalRpcName = invoker.Name.Split('@')[0];
                invokerMap[invoker.Name] = originalRpcName;
            }

            foreach (var invoker in invokers)
            {
                type.Methods.Remove(invoker);
                _removedInvokerMethods++;
            }

            foreach (var otherMethod in type.Methods.Where(m => m.Body != null).ToList())
            {
                var instructions = otherMethod.Body.Instructions;
                for (int i = 0; i < instructions.Count; i++)
                {
                    var instr = instructions[i];
                    if (instr.OpCode == OpCodes.Ldftn && instr.Operand is MethodReference targetMr)
                    {
                        if (targetMr.Name.Contains("@Invoker") && invokerMap.TryGetValue(targetMr.Name, out var originalRpcName))
                        {
                            var originalMethod = type.Methods.FirstOrDefault(m => m.Name == originalRpcName);
                            if (originalMethod != null)
                            {
                                instr.Operand = _module.ImportReference(originalMethod);
                            }
                        }
                    }
                }
            }

            foreach (var method in type.Methods.ToList())
            {
                method.CustomAttributes.RemoveWhere(a =>
                    a.AttributeType.Name == "NetworkRpcWeavedInvokerAttribute" ||
                    a.AttributeType.Name == "NetworkRpcStaticWeavedInvokerAttribute");
            }

            var rpcMethods = type.Methods
                .Where(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "RpcAttribute"))
                .ToList();

            foreach (var rpc in rpcMethods)
            {
                RestoreRpcMethod(rpc, type);
                _processedNetworkBehaviours.Add(type.FullName);
            }
        }
    }

    private void RestoreRpcMethod(MethodDefinition method, TypeDefinition type)
    {
        if (method.Body == null) return;

        PrepareMethod(method.Body);
        var instructions = method.Body.Instructions;
        if (instructions.Count < 3) return;

        bool isStaticRpc = IsStaticRpcPattern(instructions);
        bool isInstanceRpc = IsInstanceRpcPattern(instructions);
        if (!isStaticRpc && !isInstanceRpc) return;

        Instruction? invLabel = null;
        for (int i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.OpCode == OpCodes.Br && instr.Operand is Instruction target)
            {
                if (instructions.IndexOf(target) > i + 5)
                {
                    invLabel = target;
                    break;
                }
            }
        }

        if (invLabel == null)
        {
            int brfalseIndex = FindBrfalseAfterInvokeRpcCheck(instructions, isStaticRpc);
            if (brfalseIndex >= 0)
            {
                int invokeStart = FindInvokePathStart(instructions, brfalseIndex, isStaticRpc);
                if (invokeStart >= 0)
                    invLabel = instructions[invokeStart];
            }
        }

        if (invLabel == null) return;

        var il = method.Body.GetILProcessor();
        IsolateDeadBlock(il, instructions[0], instructions[instructions.IndexOf(invLabel) - 1]);

        if (method.ReturnType.Name == "RpcInvokeInfo")
        {
            for (int i = 0; i < instructions.Count - 2; i++)
            {
                if (instructions[i].OpCode == OpCodes.Pop &&
                    instructions[i + 1].OpCode == OpCodes.Ldloc &&
                    instructions[i + 2].OpCode == OpCodes.Ret)
                {
                    var rpcInfoVar = new VariableDefinition(ImportType("Fusion.RpcInvokeInfo"));
                    method.Body.Variables.Add(rpcInfoVar);
                    method.Body.InitLocals = true;

                    il.Replace(instructions[i], il.Create(OpCodes.Ldloca_S, rpcInfoVar));
                    il.Replace(instructions[i + 1], il.Create(OpCodes.Initobj, rpcInfoVar.VariableType));
                    il.InsertAfter(instructions[i + 1], il.Create(OpCodes.Ldloc, rpcInfoVar));
                    break;
                }
            }
        }

        MarkMethodModified(method);
        _restoredRpcMethods++;
        FinalizeMethod(method.Body);
    }

    private void Step3_RemoveNetworkBehaviourWeaving()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (IsNetworkBehaviour(type))
            {
                _processedNetworkBehaviours.Add(type.FullName);
                ProcessNetworkBehaviourWeaving(type);
            }
            else if (IsSimulationBehaviour(type))
            {
                _processedNetworkBehaviours.Add(type.FullName);
            }
        }
    }

    private void ProcessNetworkBehaviourWeaving(TypeDefinition type)
    {
        RemoveCustomAttribute(type, "NetworkBehaviourWeavedAttribute", ref _removedWeavedAttrs);
        RemoveFieldsByPrefix(type, "$IL2CPP_", ref _removedIl2cppFields);

        var cacheFields = type.Fields.Where(f => f.Name.StartsWith("cache_") && f.FieldType.FullName == "System.String").ToList();
        foreach (var f in cacheFields) { type.Fields.Remove(f); _removedCacheFields++; }

        CaptureInitInstructionsFromCopyMethods(type);
        RemoveCopyMethods(type);

        var weaverGeneratedMethods = type.Methods.Where(m => m.Name.StartsWith("OnChangedRender__") || SafeHasAttribute(m, "WeaverGeneratedAttribute")).ToList();
        foreach (var m in weaverGeneratedMethods) { type.Methods.Remove(m); _removedOnChangedRenderMethods++; }

        var initializeMethods = type.Methods.Where(m => m.Name.StartsWith("FusionCodeGen@Initialize@")).ToList();
        foreach (var m in initializeMethods) { type.Methods.Remove(m); _removedInitializeMethods++; }

        RemoveReadWriteHelperMethods(type);
        RevertStringPropertyTypes(type);
        CreateBackingFieldsForNetworkedProperties(type);
        RestoreNetworkedProperties(type);
        RemoveElementReaderWriterInterfaces(type);
        RestoreConstructors(type);
        MigrateAttributesFromWeaverFieldsToProperties(type);
        UnwrapProxyAttributesOnProperties(type);
        RemoveDefaultFields(type);
        StripInvokeWeavedCodeCalls(type);
        StripInternalOnCalls(type);
        RemoveMethodImplFromProperties(type);
    }

    private void CreateBackingFieldsForNetworkedProperties(TypeDefinition type)
    {
        var networkedProps = type.Properties
            .Where(p => SafeHasAttribute(p, "NetworkedAttribute") || SafeHasAttribute(p, "NetworkedWeavedAttribute"))
            .ToList();

        foreach (var prop in networkedProps)
        {
            var networkedAttr = prop.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "NetworkedAttribute");
            string? specifiedDefaultField = null;
            if (networkedAttr != null)
            {
                int pCount = networkedAttr.Properties.Count;
                for (int i = 0; i < pCount; i++)
                {
                    if (networkedAttr.Properties[i].Name == "Default")
                    {
                        specifiedDefaultField = networkedAttr.Properties[i].Argument.Value as string;
                        break;
                    }
                }
            }

            if (specifiedDefaultField != null && type.Fields.Any(f => f.Name == specifiedDefaultField))
                continue;

            var backingName = $"<{prop.Name}>k__BackingField";
            if (type.Fields.Any(f => f.Name == backingName))
                continue;

            var propType = _module.ImportReference(prop.PropertyType);
            if (type.HasGenericParameters && prop.PropertyType is GenericInstanceType git)
            {
                propType = _module.ImportReference(git, type);
            }

            var field = new FieldDefinition(backingName, FieldAttributes.Private | FieldAttributes.SpecialName, propType);
            AddCompilerGeneratedAttribute(field);
            AddDebuggerBrowsableNeverAttribute(field);
            type.Fields.Add(field);
            _newBackingFields[$"{type.FullName}::{prop.Name}"] = field;
            _restoredBackingFields++;
        }
    }

    private void RestoreNetworkedProperties(TypeDefinition type)
    {
        var networkedProps = type.Properties
            .Where(p => SafeHasAttribute(p, "NetworkedAttribute") || SafeHasAttribute(p, "NetworkedWeavedAttribute"))
            .ToList();

        foreach (var prop in networkedProps)
        {
            var networkedAttr = prop.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "NetworkedAttribute");
            NetworkedAttrMeta? savedMeta = null;
            if (networkedAttr != null) savedMeta = ExtractNetworkedAttributeMeta(networkedAttr);

            RemoveCustomAttribute(prop, "NetworkedWeavedAttribute", ref _removedWeavedAttrs);
            StripPtrNullCheck(prop.GetMethod, type, prop.Name);
            StripPtrNullCheck(prop.SetMethod, type, prop.Name);
            EnsureNetworkedAttribute(prop, savedMeta);

            bool isNetworkString = prop.PropertyType.Name.StartsWith("NetworkString`");
            bool isStringProp = prop.PropertyType.FullName == "System.String";
            if (isNetworkString || isStringProp)
            {
                var nsCacheField = type.Fields.FirstOrDefault(f => f.Name == $"cache_{prop.Name}");
                if (nsCacheField != null) { type.Fields.Remove(nsCacheField); _removedCacheFields++; }

                var nsDataField = type.Fields.FirstOrDefault(f => f.Name == $"_{prop.Name}");
                if (nsDataField != null)
                {
                    bool hasDefaultForProperty = nsDataField.CustomAttributes.Any(a => a.AttributeType.Name == "DefaultForPropertyAttribute");
                    bool isNetworkStringFieldType = nsDataField.FieldType is GenericInstanceType fgit &&
                        fgit.ElementType.Name == "NetworkString`1" &&
                        fgit.GenericArguments.Any(ga => IsFusionCapacityTypeReference(ga));

                    if (hasDefaultForProperty && isNetworkStringFieldType)
                    {
                        var wasValueType = nsDataField.FieldType.IsValueType;
                        nsDataField.FieldType = _module.TypeSystem.String;
                        if (wasValueType) FixStaleFieldReferencesInMethodBodies(type, nsDataField);
                    }
                    else if (!hasDefaultForProperty)
                    {
                        type.Fields.Remove(nsDataField);
                        _removedFixedStorageFields++;
                    }
                }
                _cleanedNetworkStringProps++;
            }

            string? specifiedDefaultField = savedMeta?.Default;
            FieldDefinition? backingField = null;

            if (specifiedDefaultField != null) backingField = type.Fields.FirstOrDefault(f => f.Name == specifiedDefaultField);
            if (backingField == null) backingField = type.Fields.FirstOrDefault(f => f.Name == $"<{prop.Name}>k__BackingField");
            if (backingField == null) backingField = type.Fields.FirstOrDefault(f => f.Name == $"_{prop.Name}");

            if (savedMeta?.RetainIL == true && backingField != null)
            {
                StripWeaverEpilogue(prop.SetMethod, type, prop.Name, backingField);
                bool setterHasStore = prop.SetMethod?.Body != null && prop.SetMethod.Body.Instructions.Any(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference fr && fr.Name == backingField.Name);
                if (setterHasStore)
                {
                    bool getterHasLoad = prop.GetMethod?.Body != null && prop.GetMethod.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference fr && fr.Name == backingField.Name);
                    if (getterHasLoad)
                    {
                        _retainILPropertiesPreserved++;
                        continue;
                    }
                }
            }

            if (backingField != null)
            {
                if (specifiedDefaultField == null)
                {
                    var autoName = $"<{prop.Name}>k__BackingField";
                    if (backingField.Name != autoName) RenameFieldAndFixReferences(type, backingField, autoName);
                }

                bool isCollection = prop.PropertyType.Name.StartsWith("NetworkArray`") ||
                                    prop.PropertyType.Name.StartsWith("NetworkDictionary`") ||
                                    prop.PropertyType.Name.StartsWith("NetworkLinkedList`") ||
                                    (prop.PropertyType.Name.StartsWith("NetworkString`") && !prop.CustomAttributes.Any(a => a.AttributeType.Name == "NetworkedWeavedStringAttribute"));

                bool isRefReturn = prop.GetMethod?.ReturnType is ByReferenceType;
                bool shouldHaveSetter = !isCollection && !isRefReturn;

                EnsureBackingFieldMetadata(backingField);
                var fieldRef = _module.ImportReference(backingField);

                if (prop.GetMethod?.Body != null)
                {
                    ClearReadOnlyGetterAttribute(prop.GetMethod);
                    var il = prop.GetMethod.Body.GetILProcessor();
                    prop.GetMethod.Body.Instructions.Clear();
                    prop.GetMethod.Body.Variables.Clear();
                    prop.GetMethod.Body.ExceptionHandlers.Clear();
                    il.Emit(OpCodes.Ldarg_0);
                    if (isRefReturn) il.Emit(OpCodes.Ldflda, fieldRef);
                    else il.Emit(OpCodes.Ldfld, fieldRef);
                    il.Emit(OpCodes.Ret);

                    EnsureCompilerGeneratedOnMethod(prop.GetMethod);
                    MarkMethodModified(prop.GetMethod);
                    FinalizeMethod(prop.GetMethod.Body);
                    _restoredPropertyBodies++;
                }

                if (shouldHaveSetter)
                {
                    if (prop.SetMethod?.Body != null)
                    {
                        var il = prop.SetMethod.Body.GetILProcessor();
                        prop.SetMethod.Body.Instructions.Clear();
                        prop.SetMethod.Body.Variables.Clear();
                        prop.SetMethod.Body.ExceptionHandlers.Clear();
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Stfld, fieldRef);
                        il.Emit(OpCodes.Ret);

                        EnsureCompilerGeneratedOnMethod(prop.SetMethod);
                        MarkMethodModified(prop.SetMethod);
                        FinalizeMethod(prop.SetMethod.Body);
                        _restoredPropertyBodies++;
                    }
                    else if (prop.SetMethod == null && prop.GetMethod != null)
                    {
                        CreateSimpleSetter(prop, backingField);
                        _restoredPropertyBodies++;
                    }
                }
                else if (prop.SetMethod != null)
                {
                    type.Methods.Remove(prop.SetMethod);
                    prop.SetMethod = null;
                    _removedInvalidRefReturnSetters++;
                }
            }

            if (savedMeta?.OnChanged != null)
            {
                var onChangedMethod = type.Methods.FirstOrDefault(m => m.Name == savedMeta.OnChanged);
                if (onChangedMethod != null)
                {
                    var preserveAttr = onChangedMethod.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "PreserveAttribute");
                    if (preserveAttr != null)
                    {
                        onChangedMethod.CustomAttributes.Remove(preserveAttr);
                        _removedPreserveAttrs++;
                    }
                }
            }
        }
    }

    private class NetworkedAttrMeta
    {
        public string? OnChanged { get; set; }
        public int? OnChangedTargets { get; set; }
        public string? Group { get; set; }
        public string? Default { get; set; }
        public bool HasGroupCtor { get; set; }
        public bool RetainIL { get; set; }
    }

    private NetworkedAttrMeta ExtractNetworkedAttributeMeta(CustomAttribute attr)
    {
        var meta = new NetworkedAttrMeta();
        int pLimit = attr.Properties.Count;
        for (int i = 0; i < pLimit; i++)
        {
            var prop = attr.Properties[i];
            switch (prop.Name)
            {
                case "OnChanged":
                    meta.OnChanged = prop.Argument.Value as string;
                    break;
                case "OnChangedTargets":
                    if (prop.Argument.Value != null) meta.OnChangedTargets = Convert.ToInt32(prop.Argument.Value);
                    break;
                case "Group":
                    meta.Group = prop.Argument.Value as string;
                    break;
                case "Default":
                    meta.Default = prop.Argument.Value as string;
                    break;
                case "RetainIL":
                    if (prop.Argument.Value is bool retainIL) meta.RetainIL = retainIL;
                    break;
            }
        }

        if (attr.ConstructorArguments.Count == 1 && attr.ConstructorArguments[0].Type.FullName == "System.String")
        {
            meta.HasGroupCtor = true;
            meta.Group ??= attr.ConstructorArguments[0].Value as string;
        }
        return meta;
    }

    private void EnsureNetworkedAttribute(PropertyDefinition prop, NetworkedAttrMeta? savedMeta)
    {
        bool hasNetworked = prop.CustomAttributes.Any(a => a.AttributeType.Name == "NetworkedAttribute");
        if (hasNetworked && savedMeta == null) return;

        if (hasNetworked && savedMeta != null)
        {
            var existingAttr = prop.CustomAttributes.First(a => a.AttributeType.Name == "NetworkedAttribute");
            var existingMeta = ExtractNetworkedAttributeMeta(existingAttr);

            bool needsPatch = (savedMeta.OnChanged != null && existingMeta.OnChanged != savedMeta.OnChanged) ||
                              (savedMeta.OnChangedTargets.HasValue && !existingMeta.OnChangedTargets.HasValue) ||
                              (savedMeta.Group != null && existingMeta.Group != savedMeta.Group);
            if (!needsPatch) return;

            prop.CustomAttributes.Remove(existingAttr);
            _removedWeavedAttrs++;
        }

        var networkedAttrType = ImportType("Fusion.NetworkedAttribute");
        if (networkedAttrType.FullName == "System.Object") return;

        var attrTypeDef = networkedAttrType.Resolve();
        if (attrTypeDef == null) return;

        MethodReference? ctorRef = null;
        CustomAttribute? newAttr = null;

        if (savedMeta?.HasGroupCtor == true && savedMeta.Group != null)
        {
            var groupCtor = attrTypeDef.Methods.FirstOrDefault(m =>
                m.IsConstructor && !m.IsStatic && m.Parameters.Count == 1 &&
                m.Parameters[0].ParameterType.FullName == "System.String");
            if (groupCtor != null)
            {
                ctorRef = _module.ImportReference(groupCtor);
                newAttr = new CustomAttribute(ctorRef);
                newAttr.ConstructorArguments.Add(new CustomAttributeArgument(_module.TypeSystem.String, savedMeta.Group));
            }
        }

        if (newAttr == null)
        {
            var defaultCtor = attrTypeDef.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
            if (defaultCtor != null)
            {
                ctorRef = _module.ImportReference(defaultCtor);
                newAttr = new CustomAttribute(ctorRef);
            }
        }

        if (newAttr == null) return;

        if (savedMeta != null)
        {
            if (savedMeta.OnChanged != null)
                newAttr.Properties.Add(new CustomAttributeNamedArgument("OnChanged", new CustomAttributeArgument(_module.TypeSystem.String, savedMeta.OnChanged)));
            if (savedMeta.OnChangedTargets.HasValue)
            {
                var onChangedTargetsType = ImportType("Fusion.OnChangedTargets");
                newAttr.Properties.Add(new CustomAttributeNamedArgument("OnChangedTargets", new CustomAttributeArgument(onChangedTargetsType, savedMeta.OnChangedTargets.Value)));
            }
            if (savedMeta.Group != null && !savedMeta.HasGroupCtor)
                newAttr.Properties.Add(new CustomAttributeNamedArgument("Group", new CustomAttributeArgument(_module.TypeSystem.String, savedMeta.Group)));
            if (savedMeta.Default != null)
                newAttr.Properties.Add(new CustomAttributeNamedArgument("Default", new CustomAttributeArgument(_module.TypeSystem.String, savedMeta.Default)));
            if (savedMeta.RetainIL)
                newAttr.Properties.Add(new CustomAttributeNamedArgument("RetainIL", new CustomAttributeArgument(_module.TypeSystem.Boolean, true)));
        }

        prop.CustomAttributes.Add(newAttr);
        _ensuredNetworkedAttrs++;
        _preservedNetworkedMetadata++;
    }

    private void StripPtrNullCheck(MethodDefinition? accessor, TypeDefinition type, string propName)
    {
        if (accessor?.Body == null) return;

        PrepareMethod(accessor.Body);
        var instructions = accessor.Body.Instructions;
        if (instructions.Count < 10) return;

        int startIdx = -1;
        int endIdx = -1;
        int maxScan = instructions.Count - 10;

        for (int i = 0; i <= maxScan; i++)
        {
            if (instructions[i].OpCode != OpCodes.Ldarg_0) continue;
            if (instructions[i + 1].OpCode != OpCodes.Ldfld) continue;
            if (instructions[i + 1].Operand is not FieldReference fr || fr.Name != "Ptr") continue;
            if (instructions[i + 2].OpCode != OpCodes.Ldc_I4_0) continue;
            if (instructions[i + 3].OpCode != OpCodes.Conv_U) continue;
            if (instructions[i + 4].OpCode != OpCodes.Ceq) continue;
            if (!IsBrfalse(instructions[i + 5].OpCode)) continue;
            if (instructions[i + 6].OpCode != OpCodes.Ldstr) continue;
            if (instructions[i + 6].Operand is not string msg || !msg.Contains("Networked properties can only be accessed when Spawned")) continue;
            if (instructions[i + 7].OpCode != OpCodes.Newobj) continue;
            if (instructions[i + 7].Operand is not MethodReference mr || mr.DeclaringType.Name != "InvalidOperationException") continue;
            if (instructions[i + 8].OpCode != OpCodes.Throw) continue;

            var brfalseTarget = instructions[i + 5].Operand as Instruction;
            if (brfalseTarget != null)
            {
                if (i + 9 < instructions.Count && instructions[i + 9] == brfalseTarget && instructions[i + 9].OpCode == OpCodes.Nop)
                {
                    startIdx = i;
                    endIdx = i + 10;
                    break;
                }

                int altLimit = Math.Min(i + 15, instructions.Count);
                for (int j = i + 9; j < altLimit; j++)
                {
                    if (instructions[j] == brfalseTarget)
                    {
                        startIdx = i;
                        endIdx = j + 1;
                        break;
                    }
                }
                if (startIdx >= 0) break;
            }
        }

        if (startIdx < 0) return;

        var il = accessor.Body.GetILProcessor();
        IsolateDeadBlock(il, instructions[startIdx], instructions[endIdx - 1]);

        _removedPtrNullChecks++;
        MarkMethodModified(accessor);
    }

    private void RemoveDefaultFields(TypeDefinition type)
    {
        var networkedProps = type.Properties.Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "NetworkedAttribute")).ToList();
        foreach (var prop in networkedProps)
        {
            var defaultFieldName = $"_{prop.Name}";
            if (defaultFieldName == "_Ptr") continue;

            var defaultField = type.Fields.FirstOrDefault(f => f.Name == defaultFieldName);
            if (defaultField == null) continue;

            if (SafeHasAttribute(defaultField, "CompilerGeneratedAttribute")) continue;

            bool hasDefaultForProperty = SafeHasAttribute(defaultField, "DefaultForPropertyAttribute");
            bool isWeaverGenerated = SafeHasAttribute(defaultField, "WeaverGeneratedAttribute");

            bool isUserDefaultField = false;
            var networkedAttr = prop.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "NetworkedAttribute");
            if (networkedAttr != null)
            {
                int pCount = networkedAttr.Properties.Count;
                for (int i = 0; i < pCount; i++)
                {
                    if (networkedAttr.Properties[i].Name == "Default" && networkedAttr.Properties[i].Argument.Value as string == defaultField.Name)
                    {
                        isUserDefaultField = true;
                        break;
                    }
                }
            }

            if (!isUserDefaultField && (isWeaverGenerated || hasDefaultForProperty))
            {
                FixStaleFieldReferencesInMethodBodies(type, defaultField);
                type.Fields.Remove(defaultField);
                _removedDefaultFields++;
            }
            else
            {
                if (defaultField.FieldType is GenericInstanceType fgit &&
                    fgit.ElementType.Name == "NetworkString`1" &&
                    fgit.GenericArguments.Any(ga => IsFusionCapacityTypeReference(ga)))
                {
                    var wasValueType = defaultField.FieldType.IsValueType;
                    defaultField.FieldType = _module.TypeSystem.String;
                    if (wasValueType) FixStaleFieldReferencesInMethodBodies(type, defaultField);
                }
                _preservedUserDefaultFields++;
            }
        }
    }

    private void RemoveCopyMethods(TypeDefinition type)
    {
        var methods = type.Methods.Where(m =>
            (m.Name == "CopyBackingFieldsToState" || m.Name == "CopyStateToBackingFields" ||
             m.Name == "CopyAllBackingFieldsToState" || m.Name == "CopyAllStateToBackingFields") &&
            m.IsVirtual &&
            m.Parameters.Any(p => p.ParameterType.Name == "SimulationDataPtr" || p.ParameterType.FullName.Contains("SimulationDataPtr"))).ToList();

        var weaverGeneratedMethods = type.Methods.Where(m =>
            (m.Name == "CopyBackingFieldsToState" || m.Name == "CopyStateToBackingFields" ||
             m.Name == "CopyAllBackingFieldsToState" || m.Name == "CopyAllStateToBackingFields") &&
            SafeHasAttribute(m, "WeaverGeneratedAttribute")).ToList();

        var allToRemove = methods.Union(weaverGeneratedMethods).ToList();
        foreach (var m in allToRemove)
        {
            type.Methods.Remove(m);
            _removedCopyMethods++;
        }
    }

    private void StripInvokeWeavedCodeCalls(TypeDefinition type)
    {
        foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
        {
            var instructions = method.Body.Instructions;
            var toRemove = new List<Instruction>();
            int limit = instructions.Count;

            for (int i = 0; i < limit; i++)
            {
                if (instructions[i].OpCode == OpCodes.Call && instructions[i].Operand is MethodReference mr && mr.Name == "InvokeWeavedCode")
                {
                    toRemove.Add(instructions[i]);
                    _removedInvokeWeavedCodeCalls++;
                }
            }

            if (toRemove.Count > 0)
            {
                PrepareMethod(method.Body);
                var il = method.Body.GetILProcessor();
                int rCount = toRemove.Count;
                for (int i = 0; i < rCount; i++)
                {
                    try { il.Remove(toRemove[i]); } catch { }
                }
                MarkMethodModified(method);
                FinalizeMethod(method.Body);
            }
        }
    }

    private void StripInternalOnCalls(TypeDefinition type)
    {
        var messageMethods = new[] { "OnDestroy", "OnEnable", "OnDisable" };
        var internalNames = new Dictionary<string, string>
        {
            { "OnDestroy", "InternalOnDestroy" },
            { "OnEnable", "InternalOnEnable" },
            { "OnDisable", "InternalOnDisable" }
        };

        foreach (var msgName in messageMethods)
        {
            var method = type.Methods.FirstOrDefault(m => m.Name == msgName && m.Body != null);
            if (method == null) continue;

            PrepareMethod(method.Body);
            var instructions = method.Body.Instructions;

            if (instructions.Count >= 2 &&
                instructions[0].OpCode == OpCodes.Ldarg_0 &&
                instructions[1].OpCode == OpCodes.Call &&
                instructions[1].Operand is MethodReference mr &&
                mr.Name == internalNames[msgName])
            {
                var il = method.Body.GetILProcessor();
                il.Remove(instructions[1]);
                il.Remove(instructions[0]);
                MarkMethodModified(method);
                _removedInternalOnCalls++;
            }
            FinalizeMethod(method.Body);
        }
    }

    private void RemoveMethodImplFromProperties(TypeDefinition type)
    {
        foreach (var prop in type.Properties.ToList())
        {
            if (!SafeHasAttribute(prop, "NetworkedAttribute")) continue;

            if (prop.GetMethod != null)
            {
                var toRemove = prop.GetMethod.CustomAttributes.Where(a => a.AttributeType.Name == "MethodImplAttribute").ToList();
                foreach (var attr in toRemove) { prop.GetMethod.CustomAttributes.Remove(attr); _removedMethodImplAttrs++; }
            }

            if (prop.SetMethod != null)
            {
                var toRemove = prop.SetMethod.CustomAttributes.Where(a => a.AttributeType.Name == "MethodImplAttribute").ToList();
                foreach (var attr in toRemove) { prop.SetMethod.CustomAttributes.Remove(attr); _removedMethodImplAttrs++; }
            }
        }
    }

    private void RemoveReadWriteHelperMethods(TypeDefinition type)
    {
        var methodsToRemove = type.Methods
            .Where(m => SafeHasAttribute(m, "WeaverGeneratedAttribute") &&
                        (m.Name.EndsWith("_Read") || m.Name.EndsWith("_Write") || IsPropertyReadOrWriteHelper(m.Name)))
            .ToList();

        foreach (var m in methodsToRemove)
        {
            type.Methods.Remove(m);
            _removedReadWriteHelperMethods++;
        }
    }

    private static bool IsPropertyReadOrWriteHelper(string methodName)
    {
        if (!methodName.EndsWith("_PropertyRead") && !methodName.EndsWith("_PropertyWrite")) return false;
        if (!methodName.StartsWith("get_") && !methodName.StartsWith("set_")) return false;

        string prefix = methodName.StartsWith("get_") ? "get_" : "set_";
        string suffix = methodName.EndsWith("_PropertyRead") ? "_PropertyRead" : "_PropertyWrite";
        int prefixLen = prefix.Length;
        int suffixLen = suffix.Length;

        if (methodName.Length <= prefixLen + suffixLen) return false;
        string propName = methodName.Substring(prefixLen, methodName.Length - prefixLen - suffixLen);
        return propName.Length > 0;
    }

    private void RevertStringPropertyTypes(TypeDefinition type)
    {
        foreach (var prop in type.Properties.ToList())
        {
            var stringAttr = prop.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "NetworkedWeavedStringAttribute");
            if (stringAttr == null) continue;

            var stringType = _module.TypeSystem.String;
            prop.PropertyType = stringType;

            if (prop.GetMethod != null) prop.GetMethod.ReturnType = stringType;
            if (prop.SetMethod != null && prop.SetMethod.Parameters.Count > 0) prop.SetMethod.Parameters[0].ParameterType = stringType;

            var backingName = $"<{prop.Name}>k__BackingField";
            var backingField = type.Fields.FirstOrDefault(f => f.Name == backingName);
            if (backingField != null)
            {
                var wasValueType = backingField.FieldType.IsValueType;
                backingField.FieldType = stringType;
                if (wasValueType) FixStaleFieldReferencesInMethodBodies(type, backingField);
            }

            prop.CustomAttributes.Remove(stringAttr);
            _revertedStringPropTypes++;
        }

        foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
        {
            var instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if ((instr.OpCode == OpCodes.Castclass || instr.OpCode == OpCodes.Isinst) &&
                    instr.Operand is TypeReference tr &&
                    tr.Name.StartsWith("NetworkString`") &&
                    tr is GenericInstanceType git &&
                    git.GenericArguments.Any(ga => IsFusionCapacityTypeReference(ga)))
                {
                    instr.Operand = _module.TypeSystem.String;
                }
            }
        }
    }

    private void RemoveElementReaderWriterInterfaces(TypeDefinition type)
    {
        var ifaces = type.Interfaces.Where(i => i.InterfaceType.Name.StartsWith("IElementReaderWriter")).ToList();
        foreach (var i in ifaces) { type.Interfaces.Remove(i); _removedInterfaceImpls++; }

        var methods = type.Methods.Where(m => m.Name.Contains("CodeGen@ElementReaderWriter")).ToList();
        foreach (var m in methods)
        {
            m.CustomAttributes.RemoveWhere(a => a.AttributeType.Name == "MethodImplAttribute");
            type.Methods.Remove(m);
            _removedElementRwMethods++;
        }

        var rwFields = type.Fields.Where(f =>
            f.FieldType.Name.StartsWith("IElementReaderWriter") ||
            (f.Name == "Instance" && f.IsStatic && f.FieldType.Name.StartsWith("IElementReaderWriter"))).ToList();
        foreach (var f in rwFields)
        {
            type.Fields.Remove(f);
            _removedElementRwMethods++;
        }

        var getInstanceMethods = type.Methods.Where(m => m.Name == "GetInstance" && m.ReturnType.Name.StartsWith("IElementReaderWriter")).ToList();
        foreach (var m in getInstanceMethods)
        {
            type.Methods.Remove(m);
            _removedElementRwMethods++;
        }
    }

    private void RestoreConstructors(TypeDefinition type)
    {
        var networkedProps = type.Properties.Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "NetworkedAttribute")).ToList();
        if (networkedProps.Count == 0) return;

        foreach (var ctor in type.Methods.Where(m => m.IsConstructor && !m.IsStatic && m.Body != null).ToList())
        {
            PrepareMethod(ctor.Body);
            bool modified = false;
            foreach (var prop in networkedProps)
            {
                var defaultFieldName = $"_{prop.Name}";
                var backingName = $"<{prop.Name}>k__BackingField";
                var camelName = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
                var altBackingName = $"_{camelName}";
                var backingField = type.Fields.FirstOrDefault(f => f.Name == backingName) ?? type.Fields.FirstOrDefault(f => f.Name == altBackingName);
                if (backingField == null) continue;

                var backingRef = _module.ImportReference(backingField);
                foreach (var instr in ctor.Body.Instructions)
                {
                    if ((instr.OpCode == OpCodes.Stfld || instr.OpCode == OpCodes.Ldfld || instr.OpCode == OpCodes.Ldflda) &&
                        instr.Operand is FieldReference fr && fr.Name == defaultFieldName)
                    {
                        instr.Operand = backingRef;
                        modified = true;
                    }
                }
            }

            StripDoubleInitPatterns(ctor, type);
            if (modified)
            {
                MarkMethodModified(ctor);
                _restoredConstructors++;
            }
            FinalizeMethod(ctor.Body);
        }
    }

    private void StripDoubleInitPatterns(MethodDefinition ctor, TypeDefinition type)
    {
        PrepareMethod(ctor.Body);
        var instructions = ctor.Body.Instructions;
        var toRemove = new List<Instruction>();
        int limit = instructions.Count - 1;

        for (int i = 0; i < limit; i++)
        {
            if (instructions[i].OpCode == OpCodes.Ldarg_0 &&
                instructions[i + 1].OpCode == OpCodes.Call &&
                instructions[i + 1].Operand is MethodReference mr &&
                (mr.Name == "Default" || mr.Name == "initDefaults" || mr.Name == "SetDefaults" ||
                 mr.Name == "ResetDefaults" || mr.Name == "InitializeDefaults") &&
                mr.DeclaringType == type)
            {
                toRemove.Add(instructions[i]);
                toRemove.Add(instructions[i + 1]);
                _removedDoubleInits++;
            }
        }

        if (toRemove.Count > 0)
        {
            var il = ctor.Body.GetILProcessor();
            int rCount = toRemove.Count;
            for (int i = 0; i < rCount; i++)
            {
                try { il.Remove(toRemove[i]); } catch { }
            }
        }
        FinalizeMethod(ctor.Body);
    }

    private void MigrateAttributesFromWeaverFieldsToProperties(TypeDefinition type)
    {
        var networkedProps = type.Properties
            .Where(p => SafeHasAttribute(p, "NetworkedAttribute") || SafeHasAttribute(p, "NetworkedWeavedAttribute"))
            .ToList();

        var fusionInternalAttrs = new HashSet<string>
        {
            "NetworkedAttribute", "NetworkedWeavedAttribute", "CapacityAttribute",
            "AccuracyAttribute", "WeaverGeneratedAttribute", "DefaultForPropertyAttribute",
            "FixedBufferPropertyAttribute", "DrawIfAttribute", "PreserveAttribute",
            "UnityPropertyAttributeProxyAttribute", "CompilerGeneratedAttribute",
            "DebuggerBrowsableAttribute", "NonSerializedAttribute", "SerializeFieldAttribute",
            "SerializableAttribute"
        };

        foreach (var prop in networkedProps)
        {
            var defaultFieldName = $"_{prop.Name}";
            var defaultField = type.Fields.FirstOrDefault(f => f.Name == defaultFieldName);
            if (defaultField == null) continue;

            bool isWeaverGenerated = SafeHasAttribute(defaultField, "WeaverGeneratedAttribute") || SafeHasAttribute(defaultField, "DefaultForPropertyAttribute");
            if (!isWeaverGenerated) continue;

            var attrsToMigrate = defaultField.CustomAttributes.Where(a => !fusionInternalAttrs.Contains(a.AttributeType.Name)).ToList();
            foreach (var attr in attrsToMigrate)
            {
                try
                {
                    var existingAttr = prop.CustomAttributes.FirstOrDefault(pa =>
                        pa.AttributeType.FullName == attr.AttributeType.FullName &&
                        pa.ConstructorArguments.Count == attr.ConstructorArguments.Count);
                    if (existingAttr == null)
                    {
                        var clonedAttr = CloneCustomAttribute(attr);
                        if (clonedAttr != null)
                        {
                            prop.CustomAttributes.Add(clonedAttr);
                            _migratedAttributes++;
                        }
                    }
                }
                catch { }
            }

            MigrateSpecificAttrIfOnFieldButNotProperty(defaultField, prop, "AccuracyAttribute", ref _migratedFieldAccuracyAttrs);
            MigrateSpecificAttrIfOnFieldButNotProperty(defaultField, prop, "CapacityAttribute", ref _migratedFieldCapacityAttrs);
        }
    }

    private void MigrateSpecificAttrIfOnFieldButNotProperty(FieldDefinition field, PropertyDefinition prop, string attrName, ref int counter)
    {
        var fieldAttr = field.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == attrName);
        if (fieldAttr == null) return;

        var propAttr = prop.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == attrName);
        if (propAttr != null) return;

        try
        {
            var clonedAttr = CloneCustomAttribute(fieldAttr);
            if (clonedAttr != null)
            {
                prop.CustomAttributes.Add(clonedAttr);
                counter++;
            }
        }
        catch { }
    }

    private CustomAttribute? CloneCustomAttribute(CustomAttribute source)
    {
        try
        {
            var ctorRef = _module.ImportReference(source.Constructor);
            var cloned = new CustomAttribute(ctorRef);

            foreach (var arg in source.ConstructorArguments)
            {
                cloned.ConstructorArguments.Add(new CustomAttributeArgument(_module.ImportReference(arg.Type), arg.Value));
            }

            foreach (var prop in source.Properties)
            {
                cloned.Properties.Add(new CustomAttributeNamedArgument(prop.Name, new CustomAttributeArgument(_module.ImportReference(prop.Argument.Type), prop.Argument.Value)));
            }

            foreach (var field in source.Fields)
            {
                cloned.Fields.Add(new CustomAttributeNamedArgument(field.Name, new CustomAttributeArgument(_module.ImportReference(field.Argument.Type), field.Argument.Value)));
            }
            return cloned;
        }
        catch
        {
            return null;
        }
    }

    private void UnwrapProxyAttributesOnProperties(TypeDefinition type)
    {
        var members = new List<ICustomAttributeProvider>();
        foreach (var prop in type.Properties.ToList()) members.Add(prop);
        foreach (var field in type.Fields.ToList()) members.Add(field);

        foreach (var member in members)
        {
            try
            {
                var proxyAttrs = member.CustomAttributes.Where(a => a.AttributeType.Name == "UnityPropertyAttributeProxyAttribute").ToList();
                foreach (var proxyAttr in proxyAttrs)
                {
                    UnwrapSingleProxyAttribute(member, proxyAttr);
                }
            }
            catch { }
        }
    }

    private void UnwrapSingleProxyAttribute(ICustomAttributeProvider member, CustomAttribute proxyAttr)
    {
        if (proxyAttr.ConstructorArguments.Count < 1) return;

        var realAttrTypeRef = proxyAttr.ConstructorArguments[0].Value as TypeReference;
        if (realAttrTypeRef == null)
        {
            var typeArg = proxyAttr.ConstructorArguments[0];
            if (typeArg.Type.Name == "Type" || typeArg.Type.FullName == "System.Type")
            {
                realAttrTypeRef = typeArg.Value as TypeReference;
            }
        }
        if (realAttrTypeRef == null) return;

        TypeDefinition? realAttrTypeDef = null;
        try { realAttrTypeDef = realAttrTypeRef.Resolve(); } catch { }
        if (realAttrTypeDef == null) return;

        var realCtorArgs = proxyAttr.ConstructorArguments.Skip(1).ToList();
        MethodDefinition? matchingCtor = null;

        foreach (var ctor in realAttrTypeDef.Methods.Where(m => m.IsConstructor && !m.IsStatic))
        {
            if (ctor.Parameters.Count == realCtorArgs.Count)
            {
                bool typesMatch = ctor.Parameters.Zip(realCtorArgs, (p, a) => IsTypeCompatible(p.ParameterType, a)).All(x => x);
                if (typesMatch)
                {
                    matchingCtor = ctor;
                    break;
                }
            }
        }

        if (matchingCtor == null) matchingCtor = realAttrTypeDef.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
        if (matchingCtor == null) return;

        var ctorRef = _module.ImportReference(matchingCtor);
        var realAttr = new CustomAttribute(ctorRef);

        foreach (var arg in realCtorArgs)
        {
            realAttr.ConstructorArguments.Add(new CustomAttributeArgument(_module.ImportReference(arg.Type), arg.Value));
        }

        foreach (var prop in proxyAttr.Properties)
        {
            realAttr.Properties.Add(new CustomAttributeNamedArgument(prop.Name, new CustomAttributeArgument(_module.ImportReference(prop.Argument.Type), prop.Argument.Value)));
        }

        member.CustomAttributes.Add(realAttr);
        member.CustomAttributes.Remove(proxyAttr);
        _unwrappedProxyAttributes++;
    }

    private void Step3b_MigrateAttributesFromWeaverFields()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (_processedNetworkBehaviours.Contains(type.FullName)) continue;
            if (IsNetworkBehaviour(type))
            {
                try { MigrateAttributesFromWeaverFieldsToProperties(type); } catch { }
            }
        }
    }

    private void Step3c_UnwrapUnityPropertyAttributeProxies()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            bool alreadyProcessed = _processedNetworkBehaviours.Contains(type.FullName);
            bool isStructType = ImplementsInterface(type, "INetworkStruct") || ImplementsInterface(type, "INetworkInput");
            if (alreadyProcessed && !isStructType) continue;

            try { UnwrapProxyAttributesOnProperties(type); } catch { }
        }
    }

    private void Step3d_RecoverConstructorAssignments()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (!IsNetworkBehaviour(type)) continue;

            try
            {
                if (_capturedCtorInits.TryGetValue(type.FullName, out var capturedInits) && capturedInits.Count > 0)
                {
                    InjectRecoveredInitsIntoConstructors(type, capturedInits);
                }
            }
            catch { }
        }
    }

    private void CaptureInitInstructionsFromCopyMethods(TypeDefinition type)
    {
        var copyMethod = type.Methods.FirstOrDefault(m => (m.Name == "CopyBackingFieldsToState" || m.Name == "CopyAllBackingFieldsToState") && m.Body != null);
        if (copyMethod?.Body == null) return;

        var instructions = copyMethod.Body.Instructions;
        var capturedInits = new List<Instruction>();
        int limit = instructions.Count - 2;

        for (int i = 0; i < limit; i++)
        {
            if (instructions[i].OpCode == OpCodes.Ldarg_1 && IsBrfalse(instructions[i + 1].OpCode) && instructions[i + 1].Operand is Instruction skipTarget)
            {
                for (int j = i + 2; j < instructions.Count; j++)
                {
                    if (instructions[j] == skipTarget) break;
                    if (instructions[j].OpCode == OpCodes.Nop) continue;
                    if (instructions[j].OpCode == OpCodes.Call && instructions[j].Operand is MethodReference mr && mr.Name.StartsWith("FusionCodeGen@Initialize@")) continue;
                    capturedInits.Add(instructions[j]);
                }
                break;
            }
        }

        var initializeMethods = type.Methods.Where(m => m.Name.StartsWith("FusionCodeGen@Initialize@") && m.Body != null).ToList();
        foreach (var initMethod in initializeMethods)
        {
            var defaultAttr = initMethod.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "DefaultForPropertyAttribute");
            string? propName = null;
            if (defaultAttr != null && defaultAttr.ConstructorArguments.Count > 0) propName = defaultAttr.ConstructorArguments[0].Value as string;

            var initInstrs = initMethod.Body.Instructions.Where(instr => instr.OpCode != OpCodes.Nop && instr.OpCode != OpCodes.Ret).ToList();
            if (initInstrs.Count > 0 && propName != null) capturedInits.AddRange(initInstrs);
        }

        if (capturedInits.Count > 0) _capturedCtorInits[type.FullName] = capturedInits;
    }

    private void InjectRecoveredInitsIntoConstructors(TypeDefinition type, List<Instruction> capturedInits)
    {
        var fieldRedirectMap = new Dictionary<string, FieldReference>();
        var networkedProps = type.Properties.Where(p => SafeHasAttribute(p, "NetworkedAttribute") || SafeHasAttribute(p, "NetworkedWeavedAttribute")).ToList();

        foreach (var prop in networkedProps)
        {
            var defaultFieldName = $"_{prop.Name}";
            var backingFieldName = $"<{prop.Name}>k__BackingField";
            var camelCaseName = $"_{char.ToLowerInvariant(prop.Name[0])}{prop.Name.Substring(1)}";
            var backingField = type.Fields.FirstOrDefault(f => f.Name == backingFieldName) ?? type.Fields.FirstOrDefault(f => f.Name == camelCaseName);
            if (backingField != null) fieldRedirectMap[defaultFieldName] = _module.ImportReference(backingField);
        }

        var ctors = type.Methods.Where(m => m.IsConstructor && !m.IsStatic && m.Body != null).ToList();
        if (ctors.Count == 0) return;

        foreach (var ctor in ctors)
        {
            try
            {
                var il = ctor.Body.GetILProcessor();
                int baseCallIndex = -1;
                int bLimit = ctor.Body.Instructions.Count;
                for (int i = 0; i < bLimit; i++)
                {
                    var instr = ctor.Body.Instructions[i];
                    if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference baseMr && baseMr.Name == ".ctor" && baseMr.DeclaringType != type)
                    {
                        baseCallIndex = i;
                        break;
                    }
                }

                var instructionMap = new Dictionary<Instruction, Instruction>();
                var insertInstrs = new List<Instruction>();
                int cLimit = capturedInits.Count;

                for (int i = 0; i < cLimit; i++)
                {
                    var capturedInstr = capturedInits[i];
                    Instruction? newInstr = null;

                    if (capturedInstr.OpCode == OpCodes.Ldflda && capturedInstr.Operand is FieldReference fr && fieldRedirectMap.TryGetValue(fr.Name, out var newFieldRef))
                    {
                        var fieldDef = newFieldRef.Resolve();
                        bool isNowClass = fieldDef != null && !fieldDef.FieldType.IsValueType;

                        if (isNowClass && i + 1 < capturedInits.Count && capturedInits[i + 1].OpCode == OpCodes.Initobj)
                        {
                            var ldnull = il.Create(OpCodes.Ldnull);
                            var stfld = il.Create(OpCodes.Stfld, newFieldRef);
                            insertInstrs.Add(ldnull);
                            insertInstrs.Add(stfld);
                            instructionMap[capturedInstr] = ldnull;
                            instructionMap[capturedInits[i + 1]] = stfld;
                            i++;
                            continue;
                        }
                    }

                    if (capturedInstr.Operand is FieldReference fr2 && fieldRedirectMap.TryGetValue(fr2.Name, out var nfr))
                    {
                        newInstr = il.Create(capturedInstr.OpCode, nfr);
                    }
                    else
                    {
                        newInstr = CloneInstruction(il, capturedInstr);
                    }

                    if (newInstr != null)
                    {
                        instructionMap[capturedInstr] = newInstr;
                        insertInstrs.Add(newInstr);
                    }
                }

                var danglingBranches = new List<Instruction>();
                foreach (var clone in insertInstrs)
                {
                    if (clone.Operand is Instruction oldTarget)
                    {
                        if (instructionMap.TryGetValue(oldTarget, out var newTarget)) clone.Operand = newTarget;
                        else danglingBranches.Add(clone);
                    }
                    else if (clone.Operand is Instruction[] oldTargets)
                    {
                        bool hasDangling = false;
                        var remapped = new Instruction[oldTargets.Length];
                        for (int t = 0; t < oldTargets.Length; t++)
                        {
                            if (instructionMap.TryGetValue(oldTargets[t], out var nt)) remapped[t] = nt;
                            else { hasDangling = true; remapped[t] = oldTargets[t]; }
                        }
                        if (!hasDangling) clone.Operand = remapped;
                    }
                }

                int dLimit = danglingBranches.Count;
                for (int i = 0; i < dLimit; i++)
                {
                    int idx = insertInstrs.IndexOf(danglingBranches[i]);
                    if (idx >= 0) insertInstrs[idx] = il.Create(OpCodes.Nop);
                }

                if (baseCallIndex >= 0 && insertInstrs.Count > 0)
                {
                    int insertAt = baseCallIndex;
                    int scanLimit = Math.Max(0, baseCallIndex - 2);
                    for (int k = baseCallIndex - 1; k >= scanLimit; k--)
                    {
                        if (ctor.Body.Instructions[k].OpCode == OpCodes.Ldarg_0)
                        {
                            insertAt = k;
                            break;
                        }
                    }

                    var target = ctor.Body.Instructions[insertAt];
                    foreach (var insertInstr in insertInstrs) il.InsertBefore(target, insertInstr);
                }
                else if (insertInstrs.Count > 0)
                {
                    foreach (var insertInstr in insertInstrs) il.Append(insertInstr);
                }
                _recoveredCtorAssignments += insertInstrs.Count;
            }
            catch { }
        }
    }

    private Instruction? CloneInstruction(ILProcessor il, Instruction source)
    {
        try
        {
            var op = source.OpCode;
            var operand = source.Operand;
            return operand switch
            {
                null => il.Create(op),
                string s => il.Create(op, s),
                int i => il.Create(op, i),
                long l => il.Create(op, l),
                float f => il.Create(op, f),
                double d => il.Create(op, d),
                byte b => il.Create(op, b),
                sbyte sb => il.Create(op, sb),
                FieldReference fr => il.Create(op, _module.ImportReference(fr)),
                MethodReference mr => il.Create(op, _module.ImportReference(mr)),
                TypeReference tr => il.Create(op, _module.ImportReference(tr)),
                CallSite cs => il.Create(op, cs),
                VariableDefinition vd => il.Create(op, vd),
                ParameterDefinition pd => il.Create(op, pd),
                Instruction target => il.Create(op, target),
                Instruction[] targets => il.Create(op, targets),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private void Step3e_RestoreCollectionInitializers()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;
            foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
            {
                try { RestoreCollectionInitializersInMethod(method, type); } catch { }
            }
        }
    }

    private void RestoreCollectionInitializersInMethod(MethodDefinition method, TypeDefinition type)
    {
        if (method.Body == null) return;

        PrepareMethod(method.Body);
        var instructions = method.Body.Instructions;
        var toRemove = new List<Instruction>();
        var toReplace = new Dictionary<Instruction, Instruction>();

        for (int i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr && mr.Name == "MakeInitializer" && mr.DeclaringType.Name == "NetworkBehaviour")
            {
                toRemove.Add(instr);
                _restoredCollectionInits++;
                if (i + 1 < instructions.Count && instructions[i + 1].OpCode == OpCodes.Call && instructions[i + 1].Operand is MethodReference implMr && implMr.Name == "op_Implicit")
                {
                    toRemove.Add(instructions[i + 1]);
                }
            }

            if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr2 && mr2.Name == "MakeSerializableDictionary" && mr2.DeclaringType.Name == "NetworkBehaviourUtils")
            {
                if (mr2 is GenericInstanceMethod gim && gim.GenericArguments.Count == 2)
                {
                    try
                    {
                        var dictType = ImportType("System.Collections.Generic.Dictionary`2");
                        if (dictType.FullName != "System.Object")
                        {
                            var git = new GenericInstanceType(dictType);
                            git.GenericArguments.Add(gim.GenericArguments[0]);
                            git.GenericArguments.Add(gim.GenericArguments[1]);

                            var dictTypeDef = dictType.Resolve();
                            var defaultCtor = dictTypeDef?.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
                            if (defaultCtor != null)
                            {
                                var newCtorRef = _module.ImportReference(defaultCtor);
                                var newInstr = method.Body.GetILProcessor().Create(OpCodes.Newobj, newCtorRef);
                                toReplace[instr] = newInstr;
                                _restoredCollectionInits++;

                                if (i + 1 < instructions.Count && instructions[i + 1].OpCode == OpCodes.Call && instructions[i + 1].Operand is MethodReference implMr2 && implMr2.Name == "op_Implicit")
                                {
                                    toRemove.Add(instructions[i + 1]);
                                }
                            }
                        }
                    }
                    catch
                    {
                        toRemove.Add(instr);
                        _restoredCollectionInits++;
                    }
                }
                else
                {
                    toRemove.Add(instr);
                    _restoredCollectionInits++;
                }
            }

            if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr3 && mr3.DeclaringType.Name == "NetworkBehaviourUtils" && (mr3.Name.StartsWith("InitializeNetwork") || mr3.Name.StartsWith("CopyFromNetwork")))
            {
                int startIdx = -1;
                string? propName = null;

                if (i - 1 >= 0 && instructions[i - 1].OpCode == OpCodes.Ldstr) propName = instructions[i - 1].Operand as string;
                if (propName == null)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (instructions[j].OpCode == OpCodes.Call && instructions[j].Operand is MethodReference getMr && getMr.Name.StartsWith("get_"))
                        {
                            propName = getMr.Name.Substring(4);
                            break;
                        }
                    }
                }

                if (propName != null)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (instructions[j].OpCode == OpCodes.Ldarg_0 && j + 1 < instructions.Count && instructions[j + 1].OpCode == OpCodes.Call && instructions[j + 1].Operand is MethodReference getMr && getMr.Name == $"get_{propName}")
                        {
                            startIdx = j;
                            break;
                        }
                    }
                }

                if (startIdx != -1)
                {
                    for (int j = startIdx; j <= i; j++)
                    {
                        if (!toRemove.Contains(instructions[j])) toRemove.Add(instructions[j]);
                    }
                    _restoredCollectionInits++;
                    continue;
                }
                else
                {
                    toRemove.Add(instr);
                    _restoredCollectionInits++;
                }
            }

            if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr5 && mr5.DeclaringType.Name == "NetworkBehaviour" && (mr5.Name == "MakeRef" || mr5.Name == "MakePtr"))
            {
                toRemove.Add(instr);
                _restoredCollectionInits++;

                if (i + 1 < instructions.Count && instructions[i + 1].OpCode == OpCodes.Call && instructions[i + 1].Operand is MethodReference implMr5 && implMr5.Name == "op_Implicit")
                {
                    toRemove.Add(instructions[i + 1]);
                }
            }
        }

        if (toRemove.Count > 0 || toReplace.Count > 0)
        {
            var il = method.Body.GetILProcessor();
            foreach (var kvp in toReplace)
            {
                int idx = instructions.IndexOf(kvp.Key);
                if (idx >= 0) il.Replace(kvp.Key, kvp.Value);
            }

            int rCount = toRemove.Count;
            for (int i = 0; i < rCount; i++)
            {
                try { il.Remove(toRemove[i]); } catch { }
            }
        }
        FinalizeMethod(method.Body);
    }

    private void StripWeaverEpilogue(MethodDefinition? accessor, TypeDefinition type, string propName, FieldDefinition backingField)
    {
        if (accessor?.Body == null) return;

        var instructions = accessor.Body.Instructions;
        if (instructions.Count < 3) return;

        int lastBackingFieldStore = -1;
        for (int i = instructions.Count - 1; i >= 0; i--)
        {
            if ((instructions[i].OpCode == OpCodes.Stfld || instructions[i].OpCode == OpCodes.Stsfld) && instructions[i].Operand is FieldReference fr && fr.Name == backingField.Name)
            {
                lastBackingFieldStore = i;
                break;
            }
        }

        if (lastBackingFieldStore < 0) return;

        var toRemove = new List<Instruction>();
        int limit = instructions.Count - 1;
        for (int i = lastBackingFieldStore + 1; i < limit; i++)
        {
            if (instructions[i].OpCode == OpCodes.Nop) continue;
            toRemove.Add(instructions[i]);
        }

        if (toRemove.Count > 0)
        {
            var il = accessor.Body.GetILProcessor();
            int rCount = toRemove.Count;
            for (int i = 0; i < rCount; i++)
            {
                try { il.Remove(toRemove[i]); } catch { }
            }
            MarkMethodModified(accessor);
        }
        FinalizeMethod(accessor.Body);
    }

    private void Step4_RemoveStructWeaving()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;
            if (ImplementsInterface(type, "INetworkStruct") || ImplementsInterface(type, "INetworkInput") || ImplementsInterface(type, "INetworkCollection"))
            {
                _processedNetworkBehaviours.Add(type.FullName);
                ProcessStructWeaving(type);
            }
        }
    }

    private void ProcessStructWeaving(TypeDefinition type)
    {
        RemoveCustomAttribute(type, "NetworkStructWeavedAttribute", ref _removedWeavedAttrs);
        RemoveCustomAttribute(type, "NetworkInputWeavedAttribute", ref _removedWeavedAttrs);

        if (type.IsExplicitLayout)
        {
            type.IsExplicitLayout = false;
            type.IsSequentialLayout = true;
            type.PackingSize = 0;
            type.ClassSize = 0;
            foreach (var field in type.Fields)
            {
                field.Offset = -1;
                field.CustomAttributes.RemoveWhere(a => a.AttributeType.Name == "FieldOffsetAttribute");
            }

            var structLayoutAttr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "StructLayoutAttribute");
            if (structLayoutAttr != null && structLayoutAttr.ConstructorArguments.Count > 0)
            {
                var layoutKind = structLayoutAttr.ConstructorArguments[0];
                if (layoutKind.Value is int layoutVal && layoutVal == 2)
                {
                    type.CustomAttributes.Remove(structLayoutAttr);
                }
            }
            _restoredStructLayouts++;
        }

        RevertStringPropertyTypes(type);

        var networkedProps = type.Properties
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "NetworkedAttribute" || a.AttributeType.Name == "NetworkedWeavedAttribute"))
            .ToList();

        var propMetaDict = new Dictionary<string, NetworkedAttrMeta>();
        foreach (var prop in networkedProps)
        {
            var networkedAttr = prop.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "NetworkedAttribute");
            if (networkedAttr != null) propMetaDict[prop.Name] = ExtractNetworkedAttributeMeta(networkedAttr);

            RemoveCustomAttribute(prop, "NetworkedWeavedAttribute", ref _removedWeavedAttrs);

            var backingName = $"<{prop.Name}>k__BackingField";
            if (!type.Fields.Any(f => f.Name == backingName))
            {
                var propType = _module.ImportReference(prop.PropertyType);
                var field = new FieldDefinition(backingName, FieldAttributes.Private | FieldAttributes.SpecialName, propType);
                AddCompilerGeneratedAttribute(field);
                AddDebuggerBrowsableNeverAttribute(field);
                type.Fields.Add(field);
                _restoredBackingFields++;
            }
        }

        foreach (var prop in networkedProps)
        {
            StripPtrNullCheck(prop.GetMethod, type, prop.Name);
            StripPtrNullCheck(prop.SetMethod, type, prop.Name);

            var savedMeta = propMetaDict.TryGetValue(prop.Name, out var m) ? m : null;
            EnsureNetworkedAttribute(prop, savedMeta);

            var backingName = $"<{prop.Name}>k__BackingField";
            var camelCaseName = $"_{char.ToLowerInvariant(prop.Name[0])}{prop.Name.Substring(1)}";
            var backingField = type.Fields.FirstOrDefault(f => f.Name == backingName) ?? type.Fields.FirstOrDefault(f => f.Name == camelCaseName);
            if (backingField == null) continue;

            EnsureBackingFieldMetadata(backingField);
            var fieldRef = _module.ImportReference(backingField);

            if (prop.GetMethod?.Body != null)
            {
                ClearReadOnlyGetterAttribute(prop.GetMethod);
                var il = prop.GetMethod.Body.GetILProcessor();
                prop.GetMethod.Body.Instructions.Clear();
                prop.GetMethod.Body.Variables.Clear();
                prop.GetMethod.Body.ExceptionHandlers.Clear();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, fieldRef);
                il.Emit(OpCodes.Ret);
                EnsureCompilerGeneratedOnMethod(prop.GetMethod);
                MarkMethodModified(prop.GetMethod);
                _restoredPropertyBodies++;
            }

            if (prop.SetMethod?.Body != null)
            {
                var il = prop.SetMethod.Body.GetILProcessor();
                prop.SetMethod.Body.Instructions.Clear();
                prop.SetMethod.Body.Variables.Clear();
                prop.SetMethod.Body.ExceptionHandlers.Clear();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, fieldRef);
                il.Emit(OpCodes.Ret);
                EnsureCompilerGeneratedOnMethod(prop.SetMethod);
                MarkMethodModified(prop.SetMethod);
                _restoredPropertyBodies++;
            }
            else if (prop.SetMethod == null && prop.GetMethod != null)
            {
                CreateSimpleSetter(prop, backingField);
                _restoredPropertyBodies++;
            }

            var fixedName = $"_{prop.Name}";
            if (fixedName != "_Ptr")
            {
                var fixedField = type.Fields.FirstOrDefault(f => f.Name == fixedName);
                if (fixedField != null)
                {
                    bool isWeaverStorage = SafeHasAttribute(fixedField, "FixedBufferPropertyAttribute") || SafeHasAttribute(fixedField, "WeaverGeneratedAttribute");
                    if (isWeaverStorage)
                    {
                        type.Fields.Remove(fixedField);
                        _removedFixedStorageFields++;
                    }
                }
            }
        }

        RestoreConstructors(type);

        var weaverMethods = type.Methods.Where(m => m.Name.StartsWith("MakeRef_") || m.Name.StartsWith("MakePtr_") || SafeHasAttribute(m, "WeaverGeneratedAttribute")).ToList();
        foreach (var m in weaverMethods)
        {
            type.Methods.Remove(m);
            _removedWeaverHelperMethods++;
        }

        RevertWeaverGeneratedPropertiesToFields(type);
    }

    private void RevertWeaverGeneratedPropertiesToFields(TypeDefinition type)
    {
        var propsToRevert = type.Properties.Where(p => SafeHasAttribute(p, "WeaverGeneratedAttribute") && !SafeHasAttribute(p, "NetworkedAttribute")).ToList();
        foreach (var prop in propsToRevert)
        {
            var underlyingFieldName = $"_{prop.Name}";
            var underlyingField = type.Fields.FirstOrDefault(f => f.Name == underlyingFieldName);
            if (underlyingField == null) continue;

            bool isDeweaverBackingField = SafeHasAttribute(underlyingField, "CompilerGeneratedAttribute") && !SafeHasAttribute(underlyingField, "FixedBufferPropertyAttribute");
            if (isDeweaverBackingField) continue;

            var originalName = prop.Name;
            var originalType = prop.PropertyType;

            if (type.Fields.Any(f => f.Name == originalName)) continue;

            underlyingField.Name = originalName;
            underlyingField.FieldType = _module.ImportReference(originalType);

            underlyingField.CustomAttributes.RemoveWhere(a =>
                a.AttributeType.Name == "WeaverGeneratedAttribute" ||
                a.AttributeType.Name == "FixedBufferPropertyAttribute" ||
                a.AttributeType.Name == "DefaultForPropertyAttribute");

            underlyingField.IsSpecialName = false;
            underlyingField.CustomAttributes.RemoveWhere(a => a.AttributeType.Name == "CompilerGeneratedAttribute");

            FixStaleFieldReferencesAfterRename(type, underlyingFieldName, originalName);

            if (prop.GetMethod != null) type.Methods.Remove(prop.GetMethod);
            if (prop.SetMethod != null) type.Methods.Remove(prop.SetMethod);
            type.Properties.Remove(prop);

            _revertedStructPropsToFields++;
        }
    }

    private void Step5_RemoveGeneratedTypes()
    {
        var toRemove = _module.Types.Where(t => t.Namespace == "Fusion.CodeGen").ToList();
        foreach (var type in toRemove)
        {
            _module.Types.Remove(type);
            _removedGeneratedTypes++;
        }

        var dotsTypes = _module.Types.Where(t => t.Name.StartsWith("__JobReflectionRegistrationOutput__")).ToList();
        foreach (var type in dotsTypes)
        {
            _module.Types.Remove(type);
            _removedGeneratedTypes++;
        }

        PurgePhantomTypes();
    }

    private void PurgePhantomTypes()
    {
        var toRemove = new List<TypeDefinition>();
        var allTypes = GetAllTypes();

        foreach (var type in allTypes)
        {
            if (type.Name.Contains("e__FixedBuffer") ||
                type.Name.Contains("FixedStorage@") ||
                type.Name.Contains("UnityDictionarySurrogate@") ||
                type.Name.Contains("UnityArraySurrogate@") ||
                type.Name.Contains("UnityValueSurrogate@"))
            {
                toRemove.Add(type);
            }
        }

        foreach (var type in toRemove)
        {
            if (type.DeclaringType != null) type.DeclaringType.NestedTypes.Remove(type);
            else _module.Types.Remove(type);
            _purgedPhantomTypes++;
        }
    }

    private void Step6_PurgeCodeGenReferences()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            var codeGenFields = type.Fields.Where(f => IsCodeGenTypeReference(f.FieldType)).ToList();
            foreach (var f in codeGenFields)
            {
                type.Fields.Remove(f);
                _removedCodeGenFieldRefs++;
            }

            var codeGenMethods = type.Methods.Where(m =>
                IsCodeGenTypeReference(m.ReturnType) ||
                m.Parameters.Any(p => IsCodeGenTypeReference(p.ParameterType)) ||
                (m.Body != null && m.Body.Instructions.Any(i => IsInstructionReferencingCodeGen(i)))).ToList();

            foreach (var m in codeGenMethods)
            {
                type.Methods.Remove(m);
                _removedCodeGenMethodRefs++;
            }
        }
    }

    private bool IsCodeGenTypeReference(TypeReference tr)
    {
        var stack = new Stack<TypeReference>();
        stack.Push(tr);
        int safetyCounter = 0;

        while (stack.Count > 0 && safetyCounter++ < 5000)
        {
            var current = stack.Pop();
            if (current == null) continue;

            var decl = current;
            while (decl != null)
            {
                if (decl.Namespace == "Fusion.CodeGen") return true;
                if (decl.DeclaringType?.Namespace == "Fusion.CodeGen") return true;
                decl = decl.DeclaringType;
            }

            if (current is GenericInstanceType git)
            {
                int argCount = git.GenericArguments.Count;
                for (int i = 0; i < argCount; i++) stack.Push(git.GenericArguments[i]);
            }
            else if (current is TypeSpecification ts)
            {
                stack.Push(ts.ElementType);
            }
        }
        return false;
    }

    private bool IsInstructionReferencingCodeGen(Instruction instr)
    {
        return instr.Operand switch
        {
            TypeReference tr => IsCodeGenTypeReference(tr),
            MethodReference mr => IsCodeGenTypeReference(mr.DeclaringType) || IsCodeGenTypeReference(mr.ReturnType),
            FieldReference fr => IsCodeGenTypeReference(fr.DeclaringType) || IsCodeGenTypeReference(fr.FieldType),
            _ => false
        };
    }

    private void Step7_CleanStaticConstructors()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            var cctor = type.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
            if (cctor?.Body == null) continue;

            var instructions = cctor.Body.Instructions;
            var toRemove = new HashSet<Instruction>();

            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr &&
                    (mr.Name == "InitMeta" || mr.Name == "RegisterBehaviour" || mr.Name == "RegisterRpcInvokeDelegates"))
                {
                    toRemove.Add(instr);
                    int paramCount = mr.Parameters.Count;
                    int argsRemoved = 0;
                    int j = i - 1;

                    while (j >= 0 && argsRemoved < paramCount)
                    {
                        var prev = instructions[j];
                        if (prev.OpCode == OpCodes.Nop) { j--; continue; }

                        if (prev.OpCode == OpCodes.Ldtoken)
                        {
                            toRemove.Add(prev);
                            argsRemoved++;
                            j--;

                            if (j + 2 < instructions.Count && instructions[j + 2].OpCode == OpCodes.Call && instructions[j + 2].Operand is MethodReference gtr && gtr.Name == "GetTypeFromHandle")
                            {
                                toRemove.Add(instructions[j + 2]);
                            }
                        }
                        else if (prev.OpCode == OpCodes.Ldstr) { toRemove.Add(prev); argsRemoved++; j--; }
                        else if (prev.OpCode == OpCodes.Call && prev.Operand is MethodReference prevMr && prevMr.Name == "GetTypeFromHandle") { toRemove.Add(prev); j--; }
                        else if (prev.OpCode == OpCodes.Call || prev.OpCode == OpCodes.Callvirt) { toRemove.Add(prev); argsRemoved++; j--; }
                        else if (IsStackPushInstruction(prev)) { toRemove.Add(prev); argsRemoved++; j--; }
                        else break;
                    }
                    _removedCctorCalls++;
                }
            }

            if (toRemove.Count > 0)
            {
                var il = cctor.Body.GetILProcessor();
                foreach (var instr in toRemove) il.Replace(instr, il.Create(OpCodes.Nop));

                instructions = cctor.Body.Instructions;
                if (instructions.Count == 2 && instructions[0].OpCode == OpCodes.Nop && instructions[1].OpCode == OpCodes.Ret)
                {
                    var il2 = cctor.Body.GetILProcessor();
                    instructions.Clear();
                    il2.Emit(OpCodes.Ret);
                }
            }
        }
    }

    private void Step7b_CleanOrphanedMetadataTokens()
    {
        int totalCleaned = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen" || type.Name == "<PrivateImplementationDetails>") continue;

            foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
            {
                var instructions = method.Body.Instructions;
                var toReplace = new List<(Instruction instr, bool isLdtokenFollowedByGetTypeFromHandle)>();
                var ldtokenIndices = new HashSet<int>();

                for (int i = 0; i < instructions.Count; i++)
                {
                    var instr = instructions[i];
                    if (instr.OpCode == OpCodes.Ldtoken)
                    {
                        bool shouldRemove = false;
                        if (instr.Operand is TypeReference tr && IsCodeGenTypeReference(tr)) shouldRemove = true;
                        else if (instr.Operand is MethodReference mr && IsCodeGenTypeReference(mr.DeclaringType)) shouldRemove = true;

                        if (shouldRemove)
                        {
                            bool followedByGetTypeFromHandle = false;
                            if (i + 1 < instructions.Count && instructions[i + 1].OpCode == OpCodes.Call && instructions[i + 1].Operand is MethodReference gtr && gtr.Name == "GetTypeFromHandle")
                            {
                                followedByGetTypeFromHandle = true;
                                ldtokenIndices.Add(i);
                                toReplace.Add((instructions[i + 1], true));
                            }
                            else ldtokenIndices.Add(i);

                            toReplace.Add((instr, followedByGetTypeFromHandle));
                        }
                    }

                    if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr2 && (mr2.Name == "GetMethodFromHandle" || mr2.Name == "GetMethodHandle"))
                    {
                        if (i > 0 && instructions[i - 1].OpCode == OpCodes.Ldtoken && ldtokenIndices.Contains(i - 1)) toReplace.Add((instr, true));
                    }

                    if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr3 && IsCodeGenTypeReference(mr3.DeclaringType)) toReplace.Add((instr, false));

                    if ((instr.OpCode == OpCodes.Ldfld || instr.OpCode == OpCodes.Stfld ||
                         instr.OpCode == OpCodes.Ldflda || instr.OpCode == OpCodes.Ldsfld ||
                         instr.OpCode == OpCodes.Stsfld || instr.OpCode == OpCodes.Ldsflda) &&
                        instr.Operand is FieldReference fr && IsCodeGenTypeReference(fr.DeclaringType))
                    {
                        toReplace.Add((instr, false));
                    }
                }

                if (toReplace.Count > 0)
                {
                    var il = method.Body.GetILProcessor();
                    int replaced = 0;
                    for (int idx = toReplace.Count - 1; idx >= 0; idx--)
                    {
                        var (instr, isLdtokenPair) = toReplace[idx];
                        il.Remove(instr);
                        replaced++;
                    }
                    totalCleaned += replaced;
                    _cleanedOrphanedTokens += replaced;
                }
            }
        }
    }

    private void Step7c_RemoveBurstJobRegistration()
    {
        int removedCount = 0;
        var types = GetAllTypes();

        foreach (var type in types.ToList())
        {
            if (type.Name == "BurstClassInfo" || type.Name.StartsWith("BurstClassInfo+") || (type.Name == "ClassInfo" && type.DeclaringType?.Name == "BurstClassInfo"))
            {
                try
                {
                    if (type.DeclaringType != null) type.DeclaringType.NestedTypes.Remove(type);
                    else _module.Types.Remove(type);
                    removedCount++;
                }
                catch { }
            }
        }

        foreach (var type in types.ToList())
        {
            if (type.Name == "BurstClassInfo") continue;
            foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
            {
                var instructions = method.Body.Instructions;
                var burstInstrs = instructions.Where(i => ReferencesBurstClassInfo(i)).ToList();

                if (burstInstrs.Count > 0 && burstInstrs.Count >= instructions.Count - 1)
                {
                    method.Body.Instructions.Clear();
                    method.Body.Variables.Clear();
                    method.Body.ExceptionHandlers.Clear();
                    var il = method.Body.GetILProcessor();
                    if (method.ReturnType.MetadataType == MetadataType.Void) il.Emit(OpCodes.Ret);
                    else EmitDefaultReturn(method);
                    removedCount += burstInstrs.Count;
                }
                else if (burstInstrs.Count > 0)
                {
                    var il = method.Body.GetILProcessor();
                    for (int idx = burstInstrs.Count - 1; idx >= 0; idx--)
                    {
                        try { il.Remove(burstInstrs[idx]); } catch { }
                    }
                    removedCount += burstInstrs.Count;
                }
            }
        }

        foreach (var type in types.ToList())
        {
            if (type.Name == "<PrivateImplementationDetails>") continue;

            var sharedStaticFields = type.Fields.Where(f =>
                f.FieldType.Name == "SharedStatic`1" ||
                (f.FieldType.FullName.Contains("Burst") && f.FieldType.FullName.Contains("SharedStatic")) ||
                f.FieldType.Namespace.StartsWith("Unity.Burst")).ToList();

            foreach (var f in sharedStaticFields)
            {
                type.Fields.Remove(f);
                removedCount++;
            }
        }

        foreach (var type in types)
        {
            if (type.Name == "<PrivateImplementationDetails>") continue;

            var burstMethods = type.Methods.Where(m => m.Body != null && m.Body.Instructions.Any(instr => IsBurstTypeReference(instr))).ToList();
            foreach (var m in burstMethods)
            {
                if (m.IsStatic && m.IsConstructor)
                {
                    var instructions = m.Body.Instructions;
                    var toReplace = instructions.Where(IsBurstTypeReference).ToList();

                    if (toReplace.Count > 0 && toReplace.Count >= instructions.Count - 1)
                    {
                        m.Body.Instructions.Clear();
                        m.Body.Variables.Clear();
                        m.Body.ExceptionHandlers.Clear();
                        var il = m.Body.GetILProcessor();
                        il.Emit(OpCodes.Ret);
                        removedCount += toReplace.Count;
                    }
                    else if (toReplace.Count > 0)
                    {
                        var il = m.Body.GetILProcessor();
                        for (int idx = toReplace.Count - 1; idx >= 0; idx--)
                        {
                            try { il.Remove(toReplace[idx]); } catch { }
                        }
                        removedCount += toReplace.Count;
                    }
                }
                else if (m.IsStatic && !m.IsConstructor && m.Name.Contains("Burst"))
                {
                    type.Methods.Remove(m);
                    removedCount++;
                }
            }

            foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
            {
                var instructions = method.Body.Instructions;
                var toReplace = new List<(Instruction instr, bool isLdtokenPair)>();
                var ldtokenIndices = new HashSet<int>();

                for (int i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i].OpCode == OpCodes.Call && instructions[i].Operand is MethodReference mr &&
                        (mr.DeclaringType.Name == "BurstCompiler" || mr.DeclaringType.Name == "BurstCompilerService" || mr.DeclaringType.Name == "BurstCompilerHelper"))
                    {
                        toReplace.Add((instructions[i], false));
                        removedCount++;
                    }

                    if (instructions[i].OpCode == OpCodes.Ldtoken && instructions[i].Operand is MethodReference lmr && lmr.DeclaringType.FullName.Contains("Burst"))
                    {
                        bool followedByGetTypeFromHandle = false;
                        if (i + 1 < instructions.Count && instructions[i + 1].OpCode == OpCodes.Call && instructions[i + 1].Operand is MethodReference gtr && gtr.Name == "GetTypeFromHandle")
                        {
                            followedByGetTypeFromHandle = true;
                            ldtokenIndices.Add(i);
                            toReplace.Add((instructions[i + 1], true));
                        }
                        else ldtokenIndices.Add(i);

                        toReplace.Add((instructions[i], followedByGetTypeFromHandle));
                    }
                }

                if (toReplace.Count > 0)
                {
                    var il = method.Body.GetILProcessor();
                    for (int idx = toReplace.Count - 1; idx >= 0; idx--)
                    {
                        var (instr, isLdtokenPair) = toReplace[idx];
                        il.Remove(instr);
                    }
                }
                FinalizeMethod(method.Body);
            }
        }
        _removedBurstJobRegistrations = removedCount;
    }

    private void Step8_ScrubCodeGenReferences()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            var codeGenAttrs = type.CustomAttributes.Where(a =>
                IsCodeGenTypeReference(a.AttributeType) ||
                a.ConstructorArguments.Any(ca => IsCodeGenTypeReference(ca.Type)) ||
                a.Properties.Any(p => IsCodeGenTypeReference(p.Argument.Type))).ToList();
            foreach (var attr in codeGenAttrs)
            {
                type.CustomAttributes.Remove(attr);
                _purgedCodeGenMemberRefs++;
            }

            foreach (var gp in type.GenericParameters)
            {
                var codeGenConstraints = gp.Constraints.Where(c => IsCodeGenTypeReference(c.ConstraintType)).ToList();
                foreach (var c in codeGenConstraints)
                {
                    gp.Constraints.Remove(c);
                    _purgedCodeGenTypeRefs++;
                }
            }

            var codeGenIfaces = type.Interfaces.Where(i => IsCodeGenTypeReference(i.InterfaceType)).ToList();
            foreach (var i in codeGenIfaces)
            {
                type.Interfaces.Remove(i);
                _purgedCodeGenTypeRefs++;
            }

            foreach (var field in type.Fields.ToList())
            {
                var fieldCodeGenAttrs = field.CustomAttributes.Where(a => IsCodeGenTypeReference(a.AttributeType)).ToList();
                foreach (var attr in fieldCodeGenAttrs)
                {
                    field.CustomAttributes.Remove(attr);
                    _purgedCodeGenMemberRefs++;
                }
            }

            foreach (var method in type.Methods.ToList())
            {
                var methodCodeGenAttrs = method.CustomAttributes.Where(a => IsCodeGenTypeReference(a.AttributeType)).ToList();
                foreach (var attr in methodCodeGenAttrs)
                {
                    method.CustomAttributes.Remove(attr);
                    _purgedCodeGenMemberRefs++;
                }

                foreach (var gp in method.GenericParameters)
                {
                    var codeGenConstraints = gp.Constraints.Where(c => IsCodeGenTypeReference(c.ConstraintType)).ToList();
                    foreach (var c in codeGenConstraints)
                    {
                        gp.Constraints.Remove(c);
                        _purgedCodeGenTypeRefs++;
                    }
                }

                if (method.HasBody && method.Body.Instructions.Count > 0)
                {
                    PrepareMethod(method.Body);
                    var il = method.Body.GetILProcessor();
                    var instrsToRemove = method.Body.Instructions.Where(IsInstructionReferencingCodeGen).ToList();

                    foreach (var instr in instrsToRemove)
                    {
                        try { il.Remove(instr); } catch { }
                    }
                    FinalizeMethod(method.Body);
                }
            }

            foreach (var prop in type.Properties.ToList())
            {
                var propCodeGenAttrs = prop.CustomAttributes.Where(a => IsCodeGenTypeReference(a.AttributeType)).ToList();
                foreach (var attr in propCodeGenAttrs)
                {
                    prop.CustomAttributes.Remove(attr);
                    _purgedCodeGenMemberRefs++;
                }
            }

            foreach (var evt in type.Events.ToList())
            {
                var evtCodeGenAttrs = evt.CustomAttributes.Where(a => IsCodeGenTypeReference(a.AttributeType)).ToList();
                foreach (var attr in evtCodeGenAttrs)
                {
                    evt.CustomAttributes.Remove(attr);
                    _purgedCodeGenMemberRefs++;
                }
            }
        }
    }

    private void Step9_CleanupOrphanedReferences()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            foreach (var field in type.Fields.ToList())
            {
                field.CustomAttributes.RemoveWhere(a =>
                    a.AttributeType.Name == "WeaverGeneratedAttribute" ||
                    a.AttributeType.Name == "DefaultForPropertyAttribute" ||
                    a.AttributeType.Name == "FixedBufferPropertyAttribute" ||
                    a.AttributeType.Name == "DrawIfAttribute");
            }
            foreach (var method in type.Methods.ToList())
            {
                method.CustomAttributes.RemoveWhere(a =>
                    a.AttributeType.Name == "WeaverGeneratedAttribute" ||
                    a.AttributeType.Name == "PreserveAttribute");
            }
            type.CustomAttributes.RemoveWhere(a => a.AttributeType.Name == "WeaverGeneratedAttribute");
        }
    }

    private void Step10_ExhaustiveAttributeScrubbing()
    {
        var weavedAttrNames = new HashSet<string>
        {
            "NetworkedWeavedAttribute", "NetworkBehaviourWeavedAttribute", "NetworkStructWeavedAttribute",
            "NetworkInputWeavedAttribute", "NetworkAssemblyWeavedAttribute", "NetworkRpcWeavedInvokerAttribute",
            "NetworkRpcStaticWeavedInvokerAttribute", "WeaverGeneratedAttribute", "DefaultForPropertyAttribute",
            "FixedBufferPropertyAttribute", "DrawIfAttribute", "PreserveAttribute", "UnityPropertyAttributeProxyAttribute",
            "NetworkAssemblyIgnoreAttribute", "NetworkedWeavedStringAttribute", "NetworkedWeavedCollectionAttribute",
            "NetworkedWeavedRpcAttribute", "NetworkedWeavedEventAttribute", "NetworkedWeavedPropertyAttribute",
            "NetworkedWeavedInputAttribute"
        };

        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            var typeWeavedAttrs = type.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
            foreach (var attr in typeWeavedAttrs) { type.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }

            foreach (var method in type.Methods.ToList())
            {
                var methodWeavedAttrs = method.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                foreach (var attr in methodWeavedAttrs) { method.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }

                foreach (var param in method.Parameters)
                {
                    var paramWeavedAttrs = param.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                    foreach (var attr in paramWeavedAttrs) { param.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }
                }

                var returnWeavedAttrs = method.MethodReturnType.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                foreach (var attr in returnWeavedAttrs) { method.MethodReturnType.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }

                foreach (var gp in method.GenericParameters)
                {
                    var gpWeavedAttrs = gp.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                    foreach (var attr in gpWeavedAttrs) { gp.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }
                }
            }

            foreach (var field in type.Fields.ToList())
            {
                var fieldWeavedAttrs = field.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                foreach (var attr in fieldWeavedAttrs) { field.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }
            }

            foreach (var prop in type.Properties.ToList())
            {
                var propWeavedAttrs = prop.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                foreach (var attr in propWeavedAttrs) { prop.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }
            }

            foreach (var evt in type.Events.ToList())
            {
                var evtWeavedAttrs = evt.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                foreach (var attr in evtWeavedAttrs) { evt.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }
            }

            foreach (var gp in type.GenericParameters)
            {
                var gpWeavedAttrs = gp.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
                foreach (var attr in gpWeavedAttrs) { gp.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }
            }
        }

        var asmWeavedAttrs = _asm.CustomAttributes.Where(a => weavedAttrNames.Contains(a.AttributeType.Name)).ToList();
        foreach (var attr in asmWeavedAttrs) { _asm.CustomAttributes.Remove(attr); _scrubbedParamReturnAttrs++; }
    }

    private void Step11_RemoveCodeGenAssemblyReferences()
    {
        var codeGenAsmRefs = _module.AssemblyReferences.Where(ar => ar.Name.Contains("Fusion.CodeGen") || (ar.Name.Contains("CodeGen") && ar.Name.StartsWith("Fusion"))).ToList();
        foreach (var asmRef in codeGenAsmRefs)
        {
            _module.AssemblyReferences.Remove(asmRef);
            _removedCodeGenAsmRefs++;
        }
        ScrubOrphanedFusionCapacityTypeRefs();
    }

    private void ScrubOrphanedFusionCapacityTypeRefs()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;
            try
            {
                foreach (var field in type.Fields.ToList())
                {
                    if (IsFusionCapacityTypeReference(field.FieldType)) _scrubbedOrphanedFusionTypeRefs++;

                    if (field.FieldType is GenericInstanceType git && git.ElementType.Name == "NetworkString`1" && git.GenericArguments.Any(ga => IsFusionCapacityTypeReference(ga)))
                    {
                        var fieldName = field.Name;
                        string? propName = null;
                        if (fieldName.StartsWith("<") && fieldName.EndsWith(">k__BackingField")) propName = fieldName.Substring(1, fieldName.Length - "<>k__BackingField".Length);
                        else if (fieldName.StartsWith("_") && !fieldName.StartsWith("__")) propName = fieldName.Substring(1);

                        var correspondingProp = propName != null ? type.Properties.FirstOrDefault(p => p.Name == propName) : null;
                        bool shouldRevert = false;

                        if (correspondingProp != null && correspondingProp.PropertyType.FullName == "System.String") shouldRevert = true;
                        else if (correspondingProp != null && (SafeHasAttribute(correspondingProp, "NetworkedAttribute") || SafeHasAttribute(correspondingProp, "NetworkedWeavedAttribute"))) shouldRevert = true;
                        else if (propName != null) shouldRevert = true;
                        else if (field.CustomAttributes.Any(a => a.AttributeType.Name == "DefaultForPropertyAttribute")) shouldRevert = true;
                        else if (field.Name.StartsWith("_") && !field.Name.StartsWith("_<"))
                        {
                            var candidatePropName = field.Name.Substring(1);
                            var candidateProp = type.Properties.FirstOrDefault(p => p.Name == candidatePropName);
                            if (candidateProp != null && (SafeHasAttribute(candidateProp, "NetworkedAttribute") || SafeHasAttribute(candidateProp, "NetworkedWeavedAttribute"))) shouldRevert = true;
                        }

                        if (!shouldRevert)
                        {
                            string? fallbackPropName = field.Name.StartsWith("<") && field.Name.EndsWith(">k__BackingField") ? field.Name.Substring(1, field.Name.Length - "<>k__BackingField".Length) : field.Name.StartsWith("_") ? field.Name.Substring(1) : null;
                            var fallbackProp = fallbackPropName != null ? type.Properties.FirstOrDefault(p => p.Name == fallbackPropName) : null;
                            if (fallbackProp != null && (SafeHasAttribute(fallbackProp, "NetworkedAttribute") || SafeHasAttribute(fallbackProp, "NetworkedWeavedAttribute") || fallbackProp.PropertyType.FullName == "System.String"))
                            {
                                shouldRevert = true;
                                if (correspondingProp == null) correspondingProp = fallbackProp;
                            }
                        }

                        if (shouldRevert)
                        {
                            var stringType = _module.TypeSystem.String;
                            var wasValueType = field.FieldType.IsValueType;
                            field.FieldType = stringType;

                            if (correspondingProp != null && correspondingProp.PropertyType is GenericInstanceType propGit && propGit.ElementType.Name == "NetworkString`1")
                            {
                                correspondingProp.PropertyType = stringType;
                                if (correspondingProp.GetMethod != null) correspondingProp.GetMethod.ReturnType = stringType;
                                if (correspondingProp.SetMethod != null && correspondingProp.SetMethod.Parameters.Count > 0) correspondingProp.SetMethod.Parameters[0].ParameterType = stringType;

                                var freshFieldRef = _module.ImportReference(field);
                                if (correspondingProp.GetMethod != null && correspondingProp.GetMethod.Body != null)
                                {
                                    var gil = correspondingProp.GetMethod.Body.GetILProcessor();
                                    correspondingProp.GetMethod.Body.Instructions.Clear();
                                    correspondingProp.GetMethod.Body.Variables.Clear();
                                    correspondingProp.GetMethod.Body.ExceptionHandlers.Clear();
                                    gil.Emit(OpCodes.Ldarg_0);
                                    gil.Emit(OpCodes.Ldfld, freshFieldRef);
                                    gil.Emit(OpCodes.Ret);
                                    EnsureCompilerGeneratedOnMethod(correspondingProp.GetMethod);
                                }

                                if (correspondingProp.SetMethod != null && correspondingProp.SetMethod.Body != null)
                                {
                                    var sil = correspondingProp.SetMethod.Body.GetILProcessor();
                                    correspondingProp.SetMethod.Body.Instructions.Clear();
                                    correspondingProp.SetMethod.Body.Variables.Clear();
                                    correspondingProp.SetMethod.Body.ExceptionHandlers.Clear();
                                    sil.Emit(OpCodes.Ldarg_0);
                                    sil.Emit(OpCodes.Ldarg_1);
                                    sil.Emit(OpCodes.Stfld, freshFieldRef);
                                    sil.Emit(OpCodes.Ret);
                                    EnsureCompilerGeneratedOnMethod(correspondingProp.SetMethod);
                                }
                            }

                            if (wasValueType) FixStaleFieldReferencesInMethodBodies(type, field);
                            _scrubbedOrphanedFusionTypeRefs++;
                        }
                    }
                }
            }
            catch { }
        }
    }

    private void Step12_RemoveFusion2SpecificWeaving()
    {
        if (!_isFusion2) return;

        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            foreach (var field in type.Fields.ToList())
            {
                var drawIfAttrs = field.CustomAttributes.Where(a => a.AttributeType.Name == "DrawIfAttribute").ToList();
                foreach (var attr in drawIfAttrs)
                {
                    field.CustomAttributes.Remove(attr);
                    _removedDrawIfAttrs++;
                }
            }
        }
    }

    private void Step13_RestoreRefPropertyInitializers()
    {
        int restored = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen" || !type.IsValueType) continue;

            foreach (var prop in type.Properties.ToList())
            {
                if (prop.GetMethod == null || prop.GetMethod.ReturnType is not ByReferenceType) continue;

                var backingField = type.Fields.FirstOrDefault(f => f.Name == $"<{prop.Name}>k__BackingField");
                if (backingField == null) continue;

                var fieldRef = _module.ImportReference(backingField);
                var il = prop.GetMethod.Body.GetILProcessor();
                prop.GetMethod.Body.Instructions.Clear();
                prop.GetMethod.Body.Variables.Clear();
                prop.GetMethod.Body.ExceptionHandlers.Clear();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, fieldRef);
                il.Emit(OpCodes.Ret);
                restored++;
            }
        }
        _restoredRefPropertyInitializers = restored;
    }

    private void Step14_RemoveInvalidRefReturnSetters()
    {
        int removed = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            foreach (var prop in type.Properties.ToList())
            {
                if (prop.SetMethod == null) continue;
                if (prop.GetMethod?.ReturnType is ByReferenceType)
                {
                    type.Methods.Remove(prop.SetMethod);
                    prop.SetMethod = null;
                    removed++;

                    var backingField = type.Fields.FirstOrDefault(f => f.Name == $"<{prop.Name}>k__BackingField");
                    if (backingField != null && !backingField.IsInitOnly)
                    {
                        backingField.IsInitOnly = true;
                        if (!SafeHasAttribute(backingField, "CompilerGeneratedAttribute")) AddCompilerGeneratedAttribute(backingField);
                    }
                }
            }
        }
        _removedInvalidRefReturnSetters = removed;
    }

    private void Step15_EnsureAllStructBackingFieldsAreInitialized()
    {
        int backingFieldCount = 0;
        int ctorsFixed = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen" || !type.IsValueType) continue;

            var backingFields = type.Fields
                .Where(f => f.Name.EndsWith(">k__BackingField") || (f.Name.StartsWith("_") && SafeHasAttribute(f, "CompilerGeneratedAttribute")))
                .Where(f => !f.IsStatic)
                .ToList();

            backingFieldCount += backingFields.Count;

            if (backingFields.Count > 0)
            {
                var ctors = type.Methods.Where(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count > 0 && m.Body != null).ToList();
                foreach (var ctor in ctors)
                {
                    var instructions = ctor.Body.Instructions;
                    if (instructions.Count == 0) continue;
                    if (instructions.Count >= 2 && instructions[0].OpCode == OpCodes.Ldarg_0 && instructions[1].OpCode == OpCodes.Initobj) continue;

                    var il = ctor.Body.GetILProcessor();
                    var ldarg0 = il.Create(OpCodes.Ldarg_0);

                    TypeReference typeRef = type;
                    if (type.HasGenericParameters)
                    {
                        var git = new GenericInstanceType(type);
                        int gLimit = type.GenericParameters.Count;
                        for (int i = 0; i < gLimit; i++) git.GenericArguments.Add(type.GenericParameters[i]);
                        typeRef = git;
                    }

                    var initobj = il.Create(OpCodes.Initobj, _module.ImportReference(typeRef));
                    il.InsertBefore(instructions[0], ldarg0);
                    il.InsertAfter(ldarg0, initobj);

                    MarkMethodModified(ctor);
                    ctorsFixed++;
                }
            }
        }
        _structBackingFieldInits = backingFieldCount;
    }

    private void Step15_EnsureStructBackingFieldInit()
    {
        Step15_EnsureAllStructBackingFieldsAreInitialized();
    }

    private void Step16_SanitizeTrivialConstructors()
    {
        int removedStructCtors = 0;
        int removedClassCtors = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;

            var ctors = type.Methods.Where(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0 && m.Body != null).ToList();
            foreach (var ctor in ctors)
            {
                if (type.IsValueType)
                {
                    type.Methods.Remove(ctor);
                    removedStructCtors++;
                    _sanitizedTrivialCtors++;
                    continue;
                }

                if (!IsNetworkBehaviour(type)) continue;

                var instructions = ctor.Body.Instructions;
                var effectiveInstrs = instructions.Where(i => i.OpCode != OpCodes.Nop).ToList();

                if (effectiveInstrs.Count == 3 &&
                    effectiveInstrs[0].OpCode == OpCodes.Ldarg_0 &&
                    effectiveInstrs[1].OpCode == OpCodes.Call &&
                    effectiveInstrs[1].Operand is MethodReference mr &&
                    mr.Name == ".ctor" &&
                    mr.DeclaringType != type &&
                    effectiveInstrs[2].OpCode == OpCodes.Ret)
                {
                    type.Methods.Remove(ctor);
                    removedClassCtors++;
                    _sanitizedTrivialCtors++;
                }
            }
        }
    }

    private void Step16b_FixGlobalILArtifacts()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen") continue;
            FixGlobalILArtifacts(type);
        }
    }

    private void Step17_SanitizeCrossModuleReferences()
    {
        int sanitized = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            foreach (var field in type.Fields.ToList())
            {
                try
                {
                    var imported = _module.ImportReference(field.FieldType);
                    if (!ReferenceEquals(imported, field.FieldType))
                    {
                        field.FieldType = imported;
                        sanitized++;
                    }
                }
                catch { }
            }

            foreach (var prop in type.Properties.ToList())
            {
                try
                {
                    var imported = _module.ImportReference(prop.PropertyType);
                    if (!ReferenceEquals(imported, prop.PropertyType))
                    {
                        prop.PropertyType = imported;
                        sanitized++;
                    }
                }
                catch { }
            }

            foreach (var attr in type.CustomAttributes.ToList()) sanitized += SanitizeCustomAttribute(attr);
            foreach (var field in type.Fields.ToList())
                foreach (var attr in field.CustomAttributes.ToList()) sanitized += SanitizeCustomAttribute(attr);
            foreach (var prop in type.Properties.ToList())
                foreach (var attr in prop.CustomAttributes.ToList()) sanitized += SanitizeCustomAttribute(attr);

            foreach (var method in type.Methods.ToList())
            {
                try
                {
                    var importedRet = _module.ImportReference(method.ReturnType);
                    if (!ReferenceEquals(importedRet, method.ReturnType))
                    {
                        method.ReturnType = importedRet;
                        sanitized++;
                    }
                }
                catch { }

                foreach (var param in method.Parameters)
                {
                    try
                    {
                        var imported = _module.ImportReference(param.ParameterType);
                        if (!ReferenceEquals(imported, param.ParameterType))
                        {
                            param.ParameterType = imported;
                            sanitized++;
                        }
                    }
                    catch { }
                }

                foreach (var attr in method.CustomAttributes.ToList()) sanitized += SanitizeCustomAttribute(attr);
                foreach (var param in method.Parameters)
                    foreach (var attr in param.CustomAttributes.ToList()) sanitized += SanitizeCustomAttribute(attr);

                if (method.Body == null) continue;

                try
                {
                    foreach (var val in method.Body.Variables)
                    {
                        try
                        {
                            var imported = _module.ImportReference(val.VariableType);
                            if (!ReferenceEquals(imported, val.VariableType))
                            {
                                val.VariableType = imported;
                                sanitized++;
                            }
                        }
                        catch { }
                    }

                    foreach (var instr in method.Body.Instructions)
                    {
                        try
                        {
                            var operand = instr.Operand;
                            if (operand == null) continue;

                            switch (operand)
                            {
                                case MethodReference mr:
                                    {
                                        var imported = ImportMethodReference(mr);
                                        if (imported != null && !ReferenceEquals(imported, instr.Operand))
                                        {
                                            instr.Operand = imported;
                                            sanitized++;
                                        }
                                        break;
                                    }
                                case FieldReference fr:
                                    {
                                        var imported = ImportFieldReference(fr);
                                        if (imported != null && !ReferenceEquals(imported, instr.Operand))
                                        {
                                            instr.Operand = imported;
                                            sanitized++;
                                        }
                                        break;
                                    }
                                case TypeReference tr:
                                    {
                                        var imported = _module.ImportReference(tr);
                                        if (!ReferenceEquals(imported, instr.Operand))
                                        {
                                            instr.Operand = imported;
                                            sanitized++;
                                        }
                                        break;
                                    }
                            }
                        }
                        catch { }
                    }

                    foreach (var eh in method.Body.ExceptionHandlers)
                    {
                        try
                        {
                            if (eh.CatchType != null)
                            {
                                var imported = _module.ImportReference(eh.CatchType);
                                if (!ReferenceEquals(imported, eh.CatchType))
                                {
                                    eh.CatchType = imported;
                                    sanitized++;
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        _importSanitizedRefs = sanitized;
    }

    private void Step18_ValidateAndFixMethodBodies()
    {
        int validated = 0;
        int fixedBodies = 0;
        int deadCodeCleaned = 0;
        int warningsOnly = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen" || type.Name == "<PrivateImplementationDetails>") continue;
            if (IsCompilerGeneratedType(type)) continue;

            foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
            {
                try
                {
                    bool isModified = WasMethodModifiedByDeweaver(method);
                    if (isModified)
                    {
                        PrepareMethod(method.Body);
                        deadCodeCleaned += CleanupDeadCodePatterns(method);
                        deadCodeCleaned += RevertBitCounterLogic(method);
                    }

                    if (!ValidateMethodBodyStackBalance(method))
                    {
                        if (isModified)
                        {
                            int totalFixed = 0;
                            bool balanced = false;

                            for (int pass = 0; pass < 3 && !balanced; pass++)
                            {
                                int extraClean = AggressiveStackCleanup(method);
                                deadCodeCleaned += extraClean;

                                if (!ValidateMethodBodyStackBalance(method))
                                {
                                    int patched = PatchStackUnderflows(method);
                                    if (patched > 0) totalFixed += patched;
                                }
                                balanced = ValidateMethodBodyStackBalance(method);
                            }

                            FinalizeMethod(method.Body);

                            if (totalFixed > 0)
                            {
                                fixedBodies++;
                            }
                            else
                            {
                                warningsOnly++;
                            }
                        }
                    }
                    validated++;
                }
                catch { }
            }
        }
        _invalidMethodBodiesFixed = fixedBodies;
    }

    private bool WasMethodModifiedByDeweaver(MethodDefinition method)
    {
        if (method.Body == null) return false;
        if (IsCompilerGeneratedType(method.DeclaringType)) return false;
        return _modifiedMethods.Contains($"{method.DeclaringType.FullName}::{method.Name}");
    }

    private int CleanupDeadCodePatterns(MethodDefinition method)
    {
        if (method.Body == null) return 0;

        var il = method.Body.GetILProcessor();
        var instructions = method.Body.Instructions;
        var toRemove = new HashSet<Instruction>();
        int cleaned = 0;

        var branchTargets = new HashSet<Instruction>();
        int limit = instructions.Count;
        for (int i = 0; i < limit; i++)
        {
            var instr = instructions[i];
            if (instr.Operand is Instruction target) branchTargets.Add(target);
            else if (instr.Operand is Instruction[] targets)
            {
                int tLimit = targets.Length;
                for (int t = 0; t < tLimit; t++) branchTargets.Add(targets[t]);
            }
        }

        int scanLimit = instructions.Count - 1;
        for (int i = 0; i < scanLimit; i++)
        {
            var push = instructions[i];
            var pop = instructions[i + 1];

            if (pop.OpCode != OpCodes.Pop) continue;
            if (branchTargets.Contains(push)) continue;

            if (IsPurePushInstruction(push))
            {
                if (branchTargets.Contains(pop)) continue;
                toRemove.Add(push);
                toRemove.Add(pop);
                cleaned++;
            }
        }

        for (int i = 0; i < scanLimit; i++)
        {
            var instr1 = instructions[i];
            var instr2 = instructions[i + 1];

            bool isLdarg0 = instr1.OpCode == OpCodes.Ldarg_0;
            bool isLdargOrLdloc = instr2.OpCode == OpCodes.Ldarg_0 ||
                                  instr2.OpCode == OpCodes.Ldarg ||
                                  instr2.OpCode == OpCodes.Ldloc ||
                                  instr2.OpCode == OpCodes.Ldloc_S;

            if (isLdarg0 && isLdargOrLdloc)
            {
                bool hasConsumer = false;
                int windowLimit = Math.Min(i + 5, instructions.Count);
                for (int j = i + 2; j < windowLimit; j++)
                {
                    var nextInstr = instructions[j];
                    if (nextInstr.OpCode == OpCodes.Stfld ||
                        nextInstr.OpCode == OpCodes.Call ||
                        nextInstr.OpCode == OpCodes.Callvirt ||
                        nextInstr.OpCode == OpCodes.Newobj)
                    {
                        hasConsumer = true;
                        break;
                    }
                    if (nextInstr.OpCode == OpCodes.Ret ||
                        nextInstr.OpCode == OpCodes.Br ||
                        nextInstr.OpCode == OpCodes.Br_S ||
                        nextInstr.OpCode == OpCodes.Leave ||
                        nextInstr.OpCode == OpCodes.Leave_S)
                    {
                        break;
                    }
                }

                if (!hasConsumer && !branchTargets.Contains(instr1))
                {
                    toRemove.Add(instr1);
                    cleaned++;
                }
            }
        }

        if (toRemove.Count > 0)
        {
            foreach (var instr in toRemove.ToList())
            {
                try { il.Remove(instr); } catch { }
            }
        }
        return cleaned;
    }

    private static bool IsPurePushInstruction(Instruction instr)
    {
        var op = instr.OpCode;
        if (op == OpCodes.Ldnull ||
            op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 || op == OpCodes.Ldc_I4_2 ||
            op == OpCodes.Ldc_I4_3 || op == OpCodes.Ldc_I4_4 || op == OpCodes.Ldc_I4_5 ||
            op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 || op == OpCodes.Ldc_I4_8 ||
            op == OpCodes.Ldc_I4_M1 || op == OpCodes.Ldc_I4_S || op == OpCodes.Ldc_I4 ||
            op == OpCodes.Ldc_I8 || op == OpCodes.Ldc_R4 || op == OpCodes.Ldc_R8)
            return true;

        if (op == OpCodes.Ldarg || op == OpCodes.Ldarg_0 || op == OpCodes.Ldarg_1 ||
            op == OpCodes.Ldarg_2 || op == OpCodes.Ldarg_3 || op == OpCodes.Ldarg_S)
            return true;

        if (op == OpCodes.Ldloc || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 ||
            op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S)
            return true;

        if (op == OpCodes.Dup) return true;
        return false;
    }

    private bool ValidateMethodBodyStackBalance(MethodDefinition method)
    {
        if (method.Body == null || method.Body.Instructions.Count == 0) return true;

        var instructions = method.Body.Instructions;
        var stackDepths = new Dictionary<Instruction, int>();
        var workList = new Queue<(Instruction instr, int depth)>();
        workList.Enqueue((instructions[0], 0));

        foreach (var eh in method.Body.ExceptionHandlers)
        {
            if (eh.HandlerType == ExceptionHandlerType.Catch || eh.HandlerType == ExceptionHandlerType.Filter)
            {
                if (eh.HandlerStart != null) workList.Enqueue((eh.HandlerStart, 1));
            }
            else if (eh.HandlerType == ExceptionHandlerType.Finally || eh.HandlerType == ExceptionHandlerType.Fault)
            {
                if (eh.HandlerStart != null) workList.Enqueue((eh.HandlerStart, 0));
            }
            if (eh.FilterStart != null) workList.Enqueue((eh.FilterStart, 1));
        }

        int maxIterations = instructions.Count * 10;
        int iterations = 0;

        while (workList.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            var (instr, currentDepth) = workList.Dequeue();

            if (stackDepths.TryGetValue(instr, out int existingDepth))
            {
                if (existingDepth == currentDepth) continue;
                if (currentDepth <= existingDepth) continue;
            }

            stackDepths[instr] = currentDepth;
            var (pops, pushes) = GetInstructionStackDelta(instr);

            if (currentDepth < pops) return false;

            int newDepth = currentDepth - pops + pushes;
            if (instr.OpCode == OpCodes.Br || instr.OpCode == OpCodes.Br_S ||
                instr.OpCode == OpCodes.Leave || instr.OpCode == OpCodes.Leave_S)
            {
                if (instr.Operand is Instruction target) workList.Enqueue((target, newDepth));
                continue;
            }

            if (instr.OpCode == OpCodes.Switch && instr.Operand is Instruction[] targets)
            {
                int tLimit = targets.Length;
                for (int t = 0; t < tLimit; t++) workList.Enqueue((targets[t], newDepth));
                continue;
            }

            if (instr.OpCode == OpCodes.Ret || instr.OpCode == OpCodes.Throw ||
                instr.OpCode == OpCodes.Rethrow || instr.OpCode == OpCodes.Endfinally)
                continue;

            if (instr.Operand is Instruction branchTarget)
            {
                bool isCond = IsConditionalBranch(instr.OpCode);
                if (isCond) workList.Enqueue((branchTarget, newDepth));
            }

            if (instr.Next != null) workList.Enqueue((instr.Next, newDepth));
        }
        return true;
    }

    private void StubMethodBody(MethodDefinition method)
    {
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();

        var il = method.Body.GetILProcessor();
        if (method.ReturnType.MetadataType == MetadataType.Void) il.Emit(OpCodes.Ret);
        else EmitDefaultReturn(method);
    }

    private int AggressiveStackCleanup(MethodDefinition method)
    {
        if (method.Body == null) return 0;
        int cleaned = 0;
        bool changed = true;
        int maxRounds = 5;

        while (changed && maxRounds-- > 0)
        {
            changed = false;
            var instructions = method.Body.Instructions;
            var il = method.Body.GetILProcessor();

            var branchTargets = new HashSet<Instruction>();
            int limit = instructions.Count;
            for (int i = 0; i < limit; i++)
            {
                var instr = instructions[i];
                if (instr.Operand is Instruction target) branchTargets.Add(target);
                else if (instr.Operand is Instruction[] targets)
                {
                    int tLimit = targets.Length;
                    for (int t = 0; t < tLimit; t++) branchTargets.Add(targets[t]);
                }
            }

            int scanLimit = instructions.Count - 1;
            for (int i = 0; i < scanLimit; i++)
            {
                var push = instructions[i];
                var pop = instructions[i + 1];

                if (pop.OpCode != OpCodes.Pop) continue;
                if (branchTargets.Contains(push) || branchTargets.Contains(pop)) continue;

                var (pops, pushes) = GetInstructionStackDelta(push);
                if (pushes == 1 && pops == 0 &&
                    push.OpCode != OpCodes.Call && push.OpCode != OpCodes.Callvirt &&
                    push.OpCode != OpCodes.Newobj && push.OpCode != OpCodes.Ldftn &&
                    push.OpCode != OpCodes.Ldvirtftn && push.OpCode != OpCodes.Dup &&
                    push.OpCode != OpCodes.Ldloca && push.OpCode != OpCodes.Ldloca_S &&
                    push.OpCode != OpCodes.Ldflda && push.OpCode != OpCodes.Ldsflda &&
                    push.OpCode != OpCodes.Ldarga && push.OpCode != OpCodes.Ldarga_S)
                {
                    try
                    {
                        il.Remove(pop);
                        il.Remove(push);
                        cleaned++;
                        changed = true;
                        break;
                    }
                    catch { }
                }
            }

            for (int i = 0; i < scanLimit; i++)
            {
                var instr1 = instructions[i];
                var instr2 = instructions[i + 1];

                bool isPush1 = instr1.OpCode == OpCodes.Ldarg_0 || instr1.OpCode == OpCodes.Ldarg_1 ||
                               instr1.OpCode == OpCodes.Ldarg_2 || instr1.OpCode == OpCodes.Ldarg_3 ||
                               instr1.OpCode == OpCodes.Ldarg || instr1.OpCode == OpCodes.Ldarg_S ||
                               instr1.OpCode == OpCodes.Ldloc_0 || instr1.OpCode == OpCodes.Ldloc_1 ||
                               instr1.OpCode == OpCodes.Ldloc_2 || instr1.OpCode == OpCodes.Ldloc_3 ||
                               instr1.OpCode == OpCodes.Ldloc || instr1.OpCode == OpCodes.Ldloc_S;

                var (p2, u2) = GetInstructionStackDelta(instr2);
                bool isPush2 = u2 > 0 && p2 == 0;
                bool isTerminator2 = instr2.OpCode == OpCodes.Ret || IsBranch(instr2.OpCode) ||
                                     instr2.OpCode == OpCodes.Throw || instr2.OpCode == OpCodes.Leave ||
                                     instr2.OpCode == OpCodes.Leave_S;

                if (isPush1 && (isPush2 || isTerminator2))
                {
                    bool hasConsumer = false;
                    int windowLimit = Math.Min(i + 6, instructions.Count);
                    for (int j = i + 1; j < windowLimit; j++)
                    {
                        var nextInstr = instructions[j];
                        var (popCount, pushCount) = GetInstructionStackDelta(nextInstr);
                        if (popCount > 0)
                        {
                            hasConsumer = true;
                            break;
                        }
                    }

                    if (!hasConsumer && !branchTargets.Contains(instr1))
                    {
                        try
                        {
                            il.Remove(instr1);
                            cleaned++;
                            changed = true;
                            break;
                        }
                        catch { }
                    }
                }
            }
        }
        return cleaned;
    }

    private static bool IsBranch(OpCode op)
    {
        return op.FlowControl == FlowControl.Branch || op.FlowControl == FlowControl.Cond_Branch;
    }

    private int RevertBitCounterLogic(MethodDefinition method)
    {
        if (method.Body == null) return 0;
        int revertedCount = 0;
        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();

        int scanLimit = instructions.Count - 5;
        for (int i = 0; i <= scanLimit; i++)
        {
            try
            {
                var instr0 = instructions[i];
                if (instr0.OpCode != OpCodes.Ldflda &&
                    instr0.OpCode != OpCodes.Ldarga && instr0.OpCode != OpCodes.Ldarga_S &&
                    instr0.OpCode != OpCodes.Ldloca && instr0.OpCode != OpCodes.Ldloca_S)
                    continue;

                if (instructions[i + 1].OpCode != OpCodes.Dup) continue;

                var instrLd = instructions[i + 2];
                if (!instrLd.OpCode.Name.StartsWith("ldind") && instrLd.OpCode != OpCodes.Ldobj) continue;

                int stIdx = -1;
                int forwardLimit = Math.Min(i + 25, instructions.Count);
                for (int j = i + 3; j < forwardLimit; j++)
                {
                    if (instructions[j].OpCode.Name.StartsWith("stind") || instructions[j].OpCode == OpCodes.Stobj)
                    {
                        stIdx = j;
                        break;
                    }
                    if (instructions[j].OpCode == OpCodes.Ret || IsBranch(instructions[j].OpCode)) break;
                }

                if (stIdx == -1) continue;
                var instrSt = instructions[stIdx];

                bool hasMath = false;
                for (int m = i + 3; m < stIdx; m++)
                {
                    var op = instructions[m].OpCode;
                    if (op == OpCodes.Or || op == OpCodes.And || op == OpCodes.Xor ||
                        op == OpCodes.Add || op == OpCodes.Sub || op == OpCodes.Mul ||
                        op == OpCodes.Div || op == OpCodes.Shr || op == OpCodes.Shl ||
                        op == OpCodes.Add_Ovf || op == OpCodes.Sub_Ovf || op == OpCodes.Mul_Ovf ||
                        op == OpCodes.Add_Ovf_Un || op == OpCodes.Sub_Ovf_Un || op == OpCodes.Mul_Ovf_Un ||
                        op == OpCodes.Rem || op == OpCodes.Rem_Un || op == OpCodes.Shr_Un ||
                        op.Name.Contains("conv"))
                    {
                        hasMath = true;
                        break;
                    }
                }

                if (!hasMath) continue;

                if (instr0.OpCode == OpCodes.Ldflda)
                {
                    var fr = (FieldReference)instr0.Operand;
                    il.InsertBefore(instr0, il.Create(OpCodes.Ldarg_0));
                    instr0.OpCode = OpCodes.Ldfld;
                    instrSt.OpCode = OpCodes.Stfld;
                    instrSt.Operand = fr;
                }
                else if (instr0.OpCode == OpCodes.Ldarga || instr0.OpCode == OpCodes.Ldarga_S)
                {
                    var arg = instr0.Operand;
                    if (arg is ParameterDefinition p && p.Index <= 3) instr0.OpCode = GetLdargMacro(p.Index);
                    else if (arg is ParameterDefinition p2 && p2.Index <= 255) instr0.OpCode = OpCodes.Ldarg_S;
                    else instr0.OpCode = OpCodes.Ldarg;

                    instrSt.OpCode = (arg is ParameterDefinition p3 && p3.Index > 255) ? OpCodes.Starg : OpCodes.Starg_S;
                    instrSt.Operand = arg;
                }
                else if (instr0.OpCode == OpCodes.Ldloca || instr0.OpCode == OpCodes.Ldloca_S)
                {
                    var loc = instr0.Operand;
                    if (loc is VariableDefinition v && v.Index <= 3) instr0.OpCode = GetLdlocMacro(v.Index);
                    else if (loc is VariableDefinition v2 && v2.Index <= 255) instr0.OpCode = OpCodes.Ldloc_S;
                    else instr0.OpCode = OpCodes.Ldloc;

                    instrSt.OpCode = (loc is VariableDefinition v3 && v3.Index > 255) ? OpCodes.Stloc : OpCodes.Stloc_S;
                    instrSt.Operand = loc;
                }

                il.Remove(instructions[i + 2]);
                il.Remove(instructions[i + 1]);

                revertedCount++;
                i = stIdx;
            }
            catch { }
        }

        int limit = instructions.Count - 1;
        for (int i = 0; i < limit; i++)
        {
            try
            {
                var instr0 = instructions[i];
                var instr1 = instructions[i + 1];

                if (instr1.OpCode.Name.StartsWith("ldind") || instr1.OpCode == OpCodes.Ldobj)
                {
                    if (instr0.OpCode == OpCodes.Ldflda)
                    {
                        var fr = (FieldReference)instr0.Operand;
                        il.InsertBefore(instr0, il.Create(OpCodes.Ldarg_0));
                        instr0.OpCode = OpCodes.Ldfld;
                        il.Remove(instr1);
                        revertedCount++;
                    }
                    else if (instr0.OpCode == OpCodes.Ldarga || instr0.OpCode == OpCodes.Ldarga_S)
                    {
                        var arg = instr0.Operand;
                        if (arg is ParameterDefinition p && p.Index <= 3) instr0.OpCode = GetLdargMacro(p.Index);
                        else if (arg is ParameterDefinition p2 && p2.Index <= 255) instr0.OpCode = OpCodes.Ldarg_S;
                        else instr0.OpCode = OpCodes.Ldarg;
                        il.Remove(instr1);
                        revertedCount++;
                    }
                    else if (instr0.OpCode == OpCodes.Ldloca || instr0.OpCode == OpCodes.Ldloca_S)
                    {
                        var loc = instr0.Operand;
                        if (loc is VariableDefinition v && v.Index <= 3) instr0.OpCode = GetLdlocMacro(v.Index);
                        else if (loc is VariableDefinition v2 && v2.Index <= 255) instr0.OpCode = OpCodes.Ldloc_S;
                        else instr0.OpCode = OpCodes.Ldloc;
                        il.Remove(instr1);
                        revertedCount++;
                    }
                }
            }
            catch { }
        }
        return revertedCount;
    }

    private static OpCode GetLdargMacro(int index) => index switch { 0 => OpCodes.Ldarg_0, 1 => OpCodes.Ldarg_1, 2 => OpCodes.Ldarg_2, 3 => OpCodes.Ldarg_3, _ => OpCodes.Ldarg_S };
    private static OpCode GetLdlocMacro(int index) => index switch { 0 => OpCodes.Ldloc_0, 1 => OpCodes.Ldloc_1, 2 => OpCodes.Ldloc_2, 3 => OpCodes.Ldloc_3, _ => OpCodes.Ldloc_S };

    private static VariableDefinition? ResolveLocVariable(Instruction instr, MethodBody body)
    {
        int idx = instr.OpCode.Code switch
        {
            Code.Stloc_0 => 0, Code.Stloc_1 => 1, Code.Stloc_2 => 2, Code.Stloc_3 => 3,
            Code.Ldloc_0 => 0, Code.Ldloc_1 => 1, Code.Ldloc_2 => 2, Code.Ldloc_3 => 3,
            _ => -1
        };
        if (idx >= 0) return idx < body.Variables.Count ? body.Variables[idx] : null;
        return instr.Operand as VariableDefinition;
    }

    private int PatchStackUnderflows(MethodDefinition method)
    {
        if (method.Body == null) return 0;
        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();

        var stackDepths = new Dictionary<Instruction, int>();
        var workList = new Queue<(Instruction instr, int depth)>();
        workList.Enqueue((instructions[0], 0));

        foreach (var eh in method.Body.ExceptionHandlers)
        {
            if (eh.HandlerType == ExceptionHandlerType.Catch || eh.HandlerType == ExceptionHandlerType.Filter)
            {
                if (eh.HandlerStart != null) workList.Enqueue((eh.HandlerStart, 1));
            }
            else if (eh.HandlerType == ExceptionHandlerType.Finally || eh.HandlerType == ExceptionHandlerType.Fault)
            {
                if (eh.HandlerStart != null) workList.Enqueue((eh.HandlerStart, 0));
            }
            if (eh.FilterStart != null) workList.Enqueue((eh.FilterStart, 1));
        }

        var underflows = new HashSet<Instruction>();
        int maxIterations = instructions.Count * 10;

        while (workList.Count > 0 && maxIterations-- > 0)
        {
            var (instr, currentDepth) = workList.Dequeue();

            if (stackDepths.TryGetValue(instr, out int existingDepth))
            {
                if (currentDepth >= existingDepth) continue;
            }
            stackDepths[instr] = currentDepth;

            var (pops, pushes) = GetInstructionStackDelta(instr);
            if (currentDepth < pops)
            {
                underflows.Add(instr);
                continue;
            }

            int newDepth = currentDepth - pops + pushes;

            if (instr.OpCode == OpCodes.Br || instr.OpCode == OpCodes.Br_S ||
                instr.OpCode == OpCodes.Leave || instr.OpCode == OpCodes.Leave_S)
            {
                if (instr.Operand is Instruction target) workList.Enqueue((target, newDepth));
                continue;
            }

            if (instr.OpCode == OpCodes.Switch && instr.Operand is Instruction[] targets)
            {
                int tLimit = targets.Length;
                for (int t = 0; t < tLimit; t++) workList.Enqueue((targets[t], newDepth));
                continue;
            }

            if (instr.OpCode == OpCodes.Ret || instr.OpCode == OpCodes.Throw ||
                instr.OpCode == OpCodes.Rethrow || instr.OpCode == OpCodes.Endfinally)
                continue;

            if (instr.Operand is Instruction branchTarget)
            {
                bool isCond = IsConditionalBranch(instr.OpCode);
                if (isCond) workList.Enqueue((branchTarget, newDepth));
            }

            if (instr.Next != null) workList.Enqueue((instr.Next, newDepth));
        }

        int patched = 0;
        foreach (var instr in underflows)
        {
            try
            {
                il.Remove(instr);
                patched++;
            }
            catch { }
        }
        return patched;
    }

    private static (int pops, int pushes) GetInstructionStackDelta(Instruction instr)
    {
        var op = instr.OpCode;
        if (op == OpCodes.Nop || op == OpCodes.Ret || op == OpCodes.Throw ||
            op == OpCodes.Rethrow || op == OpCodes.Endfinally ||
            op == OpCodes.Endfilter || op == OpCodes.Volatile || op == OpCodes.Tail ||
            op == OpCodes.Constrained || op == OpCodes.Unaligned)
            return (0, 0);

        if (op == OpCodes.Ldarg || op == OpCodes.Ldarg_0 || op == OpCodes.Ldarg_1 ||
            op == OpCodes.Ldarg_2 || op == OpCodes.Ldarg_3 || op == OpCodes.Ldarg_S ||
            op == OpCodes.Ldloc || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 ||
            op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S ||
            op == OpCodes.Ldloca || op == OpCodes.Ldloca_S ||
            op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 ||
            op == OpCodes.Ldc_I4_2 || op == OpCodes.Ldc_I4_3 || op == OpCodes.Ldc_I4_4 ||
            op == OpCodes.Ldc_I4_5 || op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 ||
            op == OpCodes.Ldc_I4_8 || op == OpCodes.Ldc_I4_M1 || op == OpCodes.Ldc_I4_S ||
            op == OpCodes.Ldc_R4 || op == OpCodes.Ldc_R8 || op == OpCodes.Ldc_I8 ||
            op == OpCodes.Ldstr || op == OpCodes.Ldnull || op == OpCodes.Ldtoken ||
            op == OpCodes.Ldsfld || op == OpCodes.Ldsflda ||
            op == OpCodes.Dup || op == OpCodes.Sizeof || op == OpCodes.Arglist)
            return (0, 1);

        if (op == OpCodes.Ldfld || op == OpCodes.Ldflda ||
            op == OpCodes.Ldftn || op == OpCodes.Ldvirtftn ||
            op == OpCodes.Ldind_I || op == OpCodes.Ldind_I1 || op == OpCodes.Ldind_I2 ||
            op == OpCodes.Ldind_I4 || op == OpCodes.Ldind_I8 || op == OpCodes.Ldind_R4 ||
            op == OpCodes.Ldind_R8 || op == OpCodes.Ldind_Ref || op == OpCodes.Ldind_U1 ||
            op == OpCodes.Ldind_U2 || op == OpCodes.Ldind_U4 ||
            op == OpCodes.Ldlen || op == OpCodes.Ldobj ||
            op == OpCodes.Box || op == OpCodes.Unbox || op == OpCodes.Unbox_Any ||
            op == OpCodes.Castclass || op == OpCodes.Isinst ||
            op == OpCodes.Conv_I || op == OpCodes.Conv_I1 || op == OpCodes.Conv_I2 ||
            op == OpCodes.Conv_I4 || op == OpCodes.Conv_I8 ||
            op == OpCodes.Conv_Ovf_I || op == OpCodes.Conv_Ovf_I1 ||
            op == OpCodes.Conv_Ovf_I2 || op == OpCodes.Conv_Ovf_I4 ||
            op == OpCodes.Conv_Ovf_I8 || op == OpCodes.Conv_Ovf_U ||
            op == OpCodes.Conv_Ovf_U1 || op == OpCodes.Conv_Ovf_U2 ||
            op == OpCodes.Conv_Ovf_U4 || op == OpCodes.Conv_Ovf_U8 ||
            op == OpCodes.Conv_R4 || op == OpCodes.Conv_R8 ||
            op == OpCodes.Conv_U || op == OpCodes.Conv_U1 || op == OpCodes.Conv_U2 ||
            op == OpCodes.Conv_U4 || op == OpCodes.Conv_U8 ||
            op == OpCodes.Newarr || op == OpCodes.Localloc ||
            op == OpCodes.Mkrefany || op == OpCodes.Refanytype || op == OpCodes.Refanyval ||
            op == OpCodes.Neg || op == OpCodes.Not)
            return (1, 1);

        if (op == OpCodes.Stloc || op == OpCodes.Stloc_0 || op == OpCodes.Stloc_1 ||
            op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3 || op == OpCodes.Stloc_S ||
            op == OpCodes.Stsfld || op == OpCodes.Starg || op == OpCodes.Starg_S ||
            op == OpCodes.Pop)
            return (1, 0);

        if (op == OpCodes.Stfld ||
            op == OpCodes.Stind_I || op == OpCodes.Stind_I1 || op == OpCodes.Stind_I2 ||
            op == OpCodes.Stind_I4 || op == OpCodes.Stind_I8 || op == OpCodes.Stind_R4 ||
            op == OpCodes.Stind_R8 || op == OpCodes.Stind_Ref || op == OpCodes.Stobj ||
            op == OpCodes.Cpobj)
            return (2, 0);

        if (op == OpCodes.Add || op == OpCodes.Sub || op == OpCodes.Mul || op == OpCodes.Div ||
            op == OpCodes.Rem || op == OpCodes.And || op == OpCodes.Or || op == OpCodes.Xor ||
            op == OpCodes.Shl || op == OpCodes.Shr || op == OpCodes.Shr_Un ||
            op == OpCodes.Ceq || op == OpCodes.Cgt || op == OpCodes.Cgt_Un ||
            op == OpCodes.Clt || op == OpCodes.Clt_Un || op == OpCodes.Div_Un || op == OpCodes.Rem_Un ||
            op == OpCodes.Mul_Ovf || op == OpCodes.Mul_Ovf_Un || op == OpCodes.Add_Ovf ||
            op == OpCodes.Add_Ovf_Un || op == OpCodes.Sub_Ovf || op == OpCodes.Sub_Ovf_Un ||
            op == OpCodes.Ldelema ||
            op == OpCodes.Ldelem_I || op == OpCodes.Ldelem_I1 || op == OpCodes.Ldelem_I2 ||
            op == OpCodes.Ldelem_I4 || op == OpCodes.Ldelem_I8 || op == OpCodes.Ldelem_R4 ||
            op == OpCodes.Ldelem_R8 || op == OpCodes.Ldelem_Ref || op == OpCodes.Ldelem_U1 ||
            op == OpCodes.Ldelem_U2 || op == OpCodes.Ldelem_U4)
            return (2, 1);

        if (op == OpCodes.Stelem_I || op == OpCodes.Stelem_I1 || op == OpCodes.Stelem_I2 ||
            op == OpCodes.Stelem_I4 || op == OpCodes.Stelem_I8 || op == OpCodes.Stelem_R4 ||
            op == OpCodes.Stelem_R8 || op == OpCodes.Stelem_Ref ||
            op == OpCodes.Initblk || op == OpCodes.Cpblk)
            return (3, 0);

        if (op == OpCodes.Brtrue || op == OpCodes.Brtrue_S ||
            op == OpCodes.Brfalse || op == OpCodes.Brfalse_S)
            return (1, 0);

        if (op == OpCodes.Br || op == OpCodes.Br_S)
            return (0, 0);

        if (op == OpCodes.Beq || op == OpCodes.Beq_S ||
            op == OpCodes.Bne_Un || op == OpCodes.Bne_Un_S ||
            op == OpCodes.Bge || op == OpCodes.Bge_S ||
            op == OpCodes.Bge_Un || op == OpCodes.Bge_Un_S ||
            op == OpCodes.Bgt || op == OpCodes.Bgt_S ||
            op == OpCodes.Bgt_Un || op == OpCodes.Bgt_Un_S ||
            op == OpCodes.Ble || op == OpCodes.Ble_S ||
            op == OpCodes.Ble_Un || op == OpCodes.Ble_Un_S ||
            op == OpCodes.Blt || op == OpCodes.Blt_S ||
            op == OpCodes.Blt_Un || op == OpCodes.Blt_Un_S)
            return (2, 0);

        if (op == OpCodes.Switch) return (1, 0);
        if (op == OpCodes.Leave || op == OpCodes.Leave_S) return (0, 0);

        if (op == OpCodes.Call || op == OpCodes.Callvirt)
        {
            if (instr.Operand is MethodReference mr)
            {
                int pops = mr.Parameters.Count + (mr.HasThis ? 1 : 0);
                int pushes = mr.ReturnType.MetadataType != MetadataType.Void ? 1 : 0;
                return (pops, pushes);
            }
            return (0, 1);
        }

        if (op == OpCodes.Newobj)
        {
            if (instr.Operand is MethodReference mr) return (mr.Parameters.Count, 1);
            return (0, 1);
        }

        if (op == OpCodes.Initobj) return (1, 0);
        return (0, 0);
    }

    private void Step19_FinalILCleanup()
    {
        int methodsProcessed = 0;
        var types = GetAllTypes();

        foreach (var type in types)
        {
            if (type.Namespace == "Fusion.CodeGen" || type.Name == "<PrivateImplementationDetails>") continue;
            if (IsCompilerGeneratedType(type)) continue;

            foreach (var method in type.Methods.Where(m => m.Body != null).ToList())
            {
                try
                {
                    if (!WasMethodModifiedByDeweaver(method)) continue;

                    PrepareMethod(method.Body);
                    bool changed = true;
                    int rounds = 0;
                    while (changed && rounds < 5)
                    {
                        changed = false;
                        rounds++;

                        int fix1 = FixStackMergeInconsistencies(method);
                        int fix2 = FixDeterministicBranches(method);
                        int fix3 = EnhancedDeadCodeCleanup(method);

                        if (fix1 + fix2 + fix3 > 0) changed = true;
                    }
                    FinalizeMethod(method.Body);
                    if (rounds > 1) methodsProcessed++;
                }
                catch { }
            }
        }
    }

    private int FixStackMergeInconsistencies(MethodDefinition method)
    {
        if (method.Body == null || method.Body.Instructions.Count < 3) return 0;

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();
        int fixedCount = 0;

        var allDepths = new Dictionary<Instruction, HashSet<int>>();
        var predecessors = new Dictionary<Instruction, List<(Instruction pred, int arrivalDepth)>>();
        var workList = new Queue<(Instruction instr, int depth, Instruction? pred)>();
        workList.Enqueue((instructions[0], 0, null));

        foreach (var eh in method.Body.ExceptionHandlers)
        {
            if (eh.HandlerType == ExceptionHandlerType.Catch || eh.HandlerType == ExceptionHandlerType.Filter)
            {
                if (eh.HandlerStart != null)
                {
                    workList.Enqueue((eh.HandlerStart, 1, null));
                    if (!allDepths.ContainsKey(eh.HandlerStart)) allDepths[eh.HandlerStart] = new HashSet<int>();
                    allDepths[eh.HandlerStart].Add(1);
                }
            }
            else if (eh.HandlerType == ExceptionHandlerType.Finally || eh.HandlerType == ExceptionHandlerType.Fault)
            {
                if (eh.HandlerStart != null)
                {
                    workList.Enqueue((eh.HandlerStart, 0, null));
                    if (!allDepths.ContainsKey(eh.HandlerStart)) allDepths[eh.HandlerStart] = new HashSet<int>();
                    allDepths[eh.HandlerStart].Add(0);
                }
            }
            if (eh.FilterStart != null)
            {
                workList.Enqueue((eh.FilterStart, 1, null));
                if (!allDepths.ContainsKey(eh.FilterStart)) allDepths[eh.FilterStart] = new HashSet<int>();
                allDepths[eh.FilterStart].Add(1);
            }
        }

        int maxIter = instructions.Count * 20;
        while (workList.Count > 0 && maxIter-- > 0)
        {
            var (instr, depth, pred) = workList.Dequeue();

            if (!allDepths.ContainsKey(instr)) allDepths[instr] = new HashSet<int>();
            if (!allDepths[instr].Add(depth)) continue;

            if (pred != null)
            {
                if (!predecessors.ContainsKey(instr)) predecessors[instr] = new List<(Instruction, int)>();
                predecessors[instr].Add((pred, depth));
            }

            var (pops, pushes) = GetInstructionStackDelta(instr);
            if (depth < pops) continue;
            int newDepth = depth - pops + pushes;

            if (instr.OpCode == OpCodes.Br || instr.OpCode == OpCodes.Br_S ||
                instr.OpCode == OpCodes.Leave || instr.OpCode == OpCodes.Leave_S)
            {
                if (instr.Operand is Instruction target) workList.Enqueue((target, newDepth, instr));
                continue;
            }

            if (instr.OpCode == OpCodes.Switch && instr.Operand is Instruction[] targets)
            {
                int tLimit = targets.Length;
                for (int t = 0; t < tLimit; t++) workList.Enqueue((targets[t], newDepth, instr));
                continue;
            }

            if (instr.OpCode == OpCodes.Ret || instr.OpCode == OpCodes.Throw ||
                instr.OpCode == OpCodes.Rethrow || instr.OpCode == OpCodes.Endfinally)
                continue;

            if (instr.Operand is Instruction branchTarget)
            {
                bool isCond = IsConditionalBranch(instr.OpCode);
                if (isCond) workList.Enqueue((branchTarget, newDepth, instr));
            }

            if (instr.Next != null) workList.Enqueue((instr.Next, newDepth, instr));
        }

        foreach (var kvp in allDepths.ToList())
        {
            var instr = kvp.Key;
            var depths = kvp.Value;

            if (depths.Count <= 1) continue;

            int maxD = depths.Max();
            int minD = depths.Min();
            if (maxD == minD) continue;

            if (!predecessors.TryGetValue(instr, out var preds)) continue;

            foreach (var (pred, arrivalDepth) in preds.ToList())
            {
                if (arrivalDepth <= minD) continue;

                int diff = arrivalDepth - minD;
                if (diff > 10) continue;

                bool isUncond = pred.OpCode == OpCodes.Br || pred.OpCode == OpCodes.Br_S ||
                                pred.OpCode == OpCodes.Leave || pred.OpCode == OpCodes.Leave_S;

                try
                {
                    if (isUncond)
                    {
                        for (int i = 0; i < diff; i++) il.InsertAfter(pred, il.Create(OpCodes.Pop));
                        fixedCount++;
                    }
                }
                catch { }
            }
        }

        _stackMergeFixes += fixedCount;
        return fixedCount;
    }

    private int FixDeterministicBranches(MethodDefinition method)
    {
        if (method.Body == null || method.Body.Instructions.Count < 4) return 0;

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();
        int fixedCount = 0;

        var branchTargets = new HashSet<Instruction>();
        int bLimit = instructions.Count;
        for (int i = 0; i < bLimit; i++)
        {
            var instr = instructions[i];
            if (instr.Operand is Instruction bt) branchTargets.Add(bt);
            else if (instr.Operand is Instruction[] bts)
            {
                int btLimit = bts.Length;
                for (int b = 0; b < btLimit; b++) branchTargets.Add(bts[b]);
            }
        }

        int scanLimit = instructions.Count - 3;
        for (int i = 0; i < scanLimit; i++)
        {
            try
            {
                var i0 = instructions[i];
                var i1 = instructions[i + 1];
                var i2 = instructions[i + 2];
                var i3 = instructions[i + 3];

                bool isNullNullCompare = i0.OpCode == OpCodes.Ldnull && i1.OpCode == OpCodes.Ldnull && (i2.OpCode == OpCodes.Ceq || i2.OpCode == OpCodes.Cgt_Un);
                bool isZeroCompare = i0.OpCode == OpCodes.Ldc_I4_0 && i1.OpCode == OpCodes.Conv_U && (i2.OpCode == OpCodes.Ceq || i2.OpCode == OpCodes.Cgt_Un);
                bool isZeroZeroCompare = (i0.OpCode == OpCodes.Ldc_I4_0 || i0.OpCode == OpCodes.Ldc_I4_0) && (i1.OpCode == OpCodes.Ldc_I4_0 || i1.OpCode == OpCodes.Ldc_I4_M1) && (i2.OpCode == OpCodes.Ceq || i2.OpCode == OpCodes.Cgt_Un);
                bool isSameConstCompare = (i0.OpCode == OpCodes.Ldc_I4 || i0.OpCode == OpCodes.Ldc_I4_S || i0.OpCode == OpCodes.Ldc_I4_0 || i0.OpCode == OpCodes.Ldc_I4_1 || i0.OpCode == OpCodes.Ldc_I4_2 || i0.OpCode == OpCodes.Ldc_I4_3 || i0.OpCode == OpCodes.Ldc_I4_4 || i0.OpCode == OpCodes.Ldc_I4_5 || i0.OpCode == OpCodes.Ldc_I4_6 || i0.OpCode == OpCodes.Ldc_I4_7 || i0.OpCode == OpCodes.Ldc_I4_8 || i0.OpCode == OpCodes.Ldc_I4_M1) &&
                    i0.OpCode == i1.OpCode && i0.Operand != null && i0.Operand.Equals(i1.Operand) && (i2.OpCode == OpCodes.Ceq || i2.OpCode == OpCodes.Cgt_Un || i2.OpCode == OpCodes.Clt || i2.OpCode == OpCodes.Clt_Un || i2.OpCode == OpCodes.Beq || i2.OpCode == OpCodes.Beq_S || i2.OpCode == OpCodes.Bne_Un || i2.OpCode == OpCodes.Bne_Un_S);

                bool isDeterministic = isNullNullCompare || isZeroCompare || isZeroZeroCompare || isSameConstCompare;
                if (!isDeterministic) continue;

                bool comparisonResult = i2.OpCode == OpCodes.Ceq || i2.OpCode == OpCodes.Beq || i2.OpCode == OpCodes.Beq_S;
                bool isComparisonBranch = i2.OpCode == OpCodes.Beq || i2.OpCode == OpCodes.Beq_S || i2.OpCode == OpCodes.Bne_Un || i2.OpCode == OpCodes.Bne_Un_S || i2.OpCode == OpCodes.Bge || i2.OpCode == OpCodes.Bge_S || i2.OpCode == OpCodes.Bgt || i2.OpCode == OpCodes.Bgt_S || i2.OpCode == OpCodes.Ble || i2.OpCode == OpCodes.Ble_S || i2.OpCode == OpCodes.Blt || i2.OpCode == OpCodes.Blt_S;

                if (isComparisonBranch)
                {
                    Instruction? target = i2.Operand as Instruction;
                    il.Replace(i0, il.Create(OpCodes.Nop));
                    il.Replace(i1, il.Create(OpCodes.Nop));

                    if (comparisonResult && target != null) il.Replace(i2, il.Create(OpCodes.Br, target));
                    else il.Replace(i2, il.Create(OpCodes.Nop));

                    fixedCount++;
                    i += 2;
                    continue;
                }

                bool isCondBranch = i3.OpCode == OpCodes.Brfalse || i3.OpCode == OpCodes.Brfalse_S || i3.OpCode == OpCodes.Brtrue || i3.OpCode == OpCodes.Brtrue_S;
                if (!isCondBranch) continue;

                Instruction? branchTarget = i3.Operand as Instruction;
                il.Replace(i0, il.Create(OpCodes.Nop));
                il.Replace(i1, il.Create(OpCodes.Nop));
                il.Replace(i2, il.Create(OpCodes.Nop));

                if (i3.OpCode == OpCodes.Brfalse || i3.OpCode == OpCodes.Brfalse_S)
                {
                    if (!comparisonResult && branchTarget != null) il.Replace(i3, il.Create(OpCodes.Br, branchTarget));
                    else il.Replace(i3, il.Create(OpCodes.Nop));
                }
                else
                {
                    if (comparisonResult && branchTarget != null) il.Replace(i3, il.Create(OpCodes.Br, branchTarget));
                    else il.Replace(i3, il.Create(OpCodes.Nop));
                }

                fixedCount++;
                i += 3;
            }
            catch { }
        }

        _deadBranchesFixed += fixedCount;
        return fixedCount;
    }

    private int EnhancedDeadCodeCleanup(MethodDefinition method)
    {
        if (method.Body == null || method.Body.Instructions.Count < 2) return 0;

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();
        int cleaned = 0;
        bool changed = true;
        int maxRounds = 5;

        while (changed && maxRounds-- > 0)
        {
            changed = false;
            var branchTargets = new HashSet<Instruction>();
            int limit = instructions.Count;
            for (int i = 0; i < limit; i++)
            {
                var instr = instructions[i];
                if (instr.Operand is Instruction bt) branchTargets.Add(bt);
                else if (instr.Operand is Instruction[] bts)
                {
                    int bLimit = bts.Length;
                    for (int b = 0; b < bLimit; b++) branchTargets.Add(bts[b]);
                }
            }

            int scanLimit = instructions.Count - 1;
            for (int i = 0; i < scanLimit; i++)
            {
                var push = instructions[i];
                var pop = instructions[i + 1];

                if (pop.OpCode != OpCodes.Pop) continue;
                if (branchTargets.Contains(push) || branchTargets.Contains(pop)) continue;

                var (pops, pushes) = GetInstructionStackDelta(push);

                if (pushes == 1 && pops == 0 &&
                    push.OpCode != OpCodes.Call && push.OpCode != OpCodes.Callvirt &&
                    push.OpCode != OpCodes.Newobj && push.OpCode != OpCodes.Ldftn &&
                    push.OpCode != OpCodes.Ldvirtftn && push.OpCode != OpCodes.Dup &&
                    push.OpCode != OpCodes.Ldloca && push.OpCode != OpCodes.Ldloca_S &&
                    push.OpCode != OpCodes.Ldflda && push.OpCode != OpCodes.Ldsflda &&
                    push.OpCode != OpCodes.Ldarga && push.OpCode != OpCodes.Ldarga_S)
                {
                    try
                    {
                        il.Remove(pop);
                        il.Remove(push);
                        cleaned++;
                        changed = true;
                        break;
                    }
                    catch { }
                }

                if ((push.OpCode == OpCodes.Conv_I1 || push.OpCode == OpCodes.Conv_I2 ||
                     push.OpCode == OpCodes.Conv_I4 || push.OpCode == OpCodes.Conv_I8 ||
                     push.OpCode == OpCodes.Conv_U1 || push.OpCode == OpCodes.Conv_U2 ||
                     push.OpCode == OpCodes.Conv_U4 || push.OpCode == OpCodes.Conv_U8 ||
                     push.OpCode == OpCodes.Conv_U || push.OpCode == OpCodes.Conv_R4 ||
                     push.OpCode == OpCodes.Conv_R8 || push.OpCode == OpCodes.Conv_Ovf_I ||
                     push.OpCode == OpCodes.Conv_Ovf_I4 || push.OpCode == OpCodes.Conv_Ovf_I8 ||
                     push.OpCode == OpCodes.Conv_Ovf_U || push.OpCode == OpCodes.Conv_Ovf_U4 ||
                     push.OpCode == OpCodes.Castclass || push.OpCode == OpCodes.Isinst ||
                     push.OpCode == OpCodes.Box || push.OpCode == OpCodes.Unbox ||
                     push.OpCode == OpCodes.Unbox_Any ||
                     push.OpCode == OpCodes.Ldind_I || push.OpCode == OpCodes.Ldind_I1 ||
                     push.OpCode == OpCodes.Ldind_I2 || push.OpCode == OpCodes.Ldind_I4 ||
                     push.OpCode == OpCodes.Ldind_I8 || push.OpCode == OpCodes.Ldind_R4 ||
                     push.OpCode == OpCodes.Ldind_R8 || push.OpCode == OpCodes.Ldind_Ref ||
                     push.OpCode == OpCodes.Ldind_U1 || push.OpCode == OpCodes.Ldind_U2 ||
                     push.OpCode == OpCodes.Ldind_U4 || push.OpCode == OpCodes.Ldobj ||
                     push.OpCode == OpCodes.Ldlen || push.OpCode == OpCodes.Ldtoken) &&
                    !branchTargets.Contains(push))
                {
                    bool pathAFullySucceeded = false;
                    if (i > 0)
                    {
                        var prev = instructions[i - 1];
                        var (prevPops, prevPushes) = GetInstructionStackDelta(prev);
                        bool pathAAttempted = prevPushes == 1 && prevPops == 0 && !branchTargets.Contains(prev);
                        if (pathAAttempted)
                        {
                            try
                            {
                                il.Remove(pop);
                                il.Remove(push);
                                il.Remove(prev);
                                pathAFullySucceeded = true;
                                cleaned++;
                                changed = true;
                                break;
                            }
                            catch { }
                        }
                    }

                    if (!pathAFullySucceeded)
                    {
                        try
                        {
                            il.Remove(pop);
                            il.Replace(push, il.Create(OpCodes.Pop));
                            cleaned++;
                            changed = true;
                            break;
                        }
                        catch { }
                    }
                }

                if (push.OpCode == OpCodes.Newobj && push.Operand is MethodReference ctorRef && ctorRef.Parameters.Count == 0 && !branchTargets.Contains(push))
                {
                    try
                    {
                        il.Remove(pop);
                        il.Remove(push);
                        cleaned++;
                        changed = true;
                        break;
                    }
                    catch { }
                }
            }

            int altLimit = instructions.Count - 2;
            for (int i = 0; i < altLimit; i++)
            {
                var i0 = instructions[i];
                var i1 = instructions[i + 1];
                var i2 = instructions[i + 2];

                if (i2.OpCode != OpCodes.Pop) continue;
                if (branchTargets.Contains(i0) || branchTargets.Contains(i1) || branchTargets.Contains(i2)) continue;

                if ((i0.OpCode == OpCodes.Ldftn || i0.OpCode == OpCodes.Ldvirtftn) && i1.OpCode == OpCodes.Newobj)
                {
                    try
                    {
                        il.Remove(i2);
                        il.Remove(i1);
                        il.Remove(i0);
                        cleaned++;
                        changed = true;
                        break;
                    }
                    catch { }
                }

                if (i0.OpCode == OpCodes.Callvirt && i0.Operand is MethodReference mr0 &&
                    mr0.ReturnType.MetadataType != MetadataType.Void &&
                    (i1.OpCode == OpCodes.Stloc || i1.OpCode == OpCodes.Stloc_S ||
                     i1.OpCode == OpCodes.Stloc_0 || i1.OpCode == OpCodes.Stloc_1 ||
                     i1.OpCode == OpCodes.Stloc_2 || i1.OpCode == OpCodes.Stloc_3) &&
                    i2.OpCode == OpCodes.Pop)
                {
                    var localVar = ResolveLocVariable(i1, method.Body);
                    if (localVar != null)
                    {
                        bool isLocalUsedAfter = false;
                        for (int j = i + 2; j < instructions.Count; j++)
                        {
                            var scanVar = ResolveLocVariable(instructions[j], method.Body);
                            if (scanVar != null &&
                                (instructions[j].OpCode == OpCodes.Ldloc ||
                                 instructions[j].OpCode == OpCodes.Ldloc_S ||
                                 instructions[j].OpCode == OpCodes.Ldloc_0 ||
                                 instructions[j].OpCode == OpCodes.Ldloc_1 ||
                                 instructions[j].OpCode == OpCodes.Ldloc_2 ||
                                 instructions[j].OpCode == OpCodes.Ldloc_3 ||
                                 instructions[j].OpCode == OpCodes.Ldloca ||
                                 instructions[j].OpCode == OpCodes.Ldloca_S) &&
                                scanVar == localVar)
                            {
                                isLocalUsedAfter = true;
                                break;
                            }
                        }

                        if (!isLocalUsedAfter)
                        {
                            try
                            {
                                il.Remove(i2);
                                il.Remove(i1);
                                il.Remove(i0);
                                cleaned++;
                                changed = true;
                                break;
                            }
                            catch { }
                        }
                    }
                }
            }

            int nopRunStart = -1;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Nop)
                {
                    if (nopRunStart < 0) nopRunStart = i;
                }
                else
                {
                    if (nopRunStart >= 0 && i - nopRunStart > 3)
                    {
                        try
                        {
                            for (int j = i - 1; j > nopRunStart; j--)
                            {
                                if (instructions[j].OpCode == OpCodes.Nop && !branchTargets.Contains(instructions[j]))
                                {
                                    il.Remove(instructions[j]);
                                    cleaned++;
                                    changed = true;
                                }
                            }
                            break;
                        }
                        catch { }
                    }
                    nopRunStart = -1;
                }
            }
        }
        _deadSequencesCleaned += cleaned;
        return cleaned;
    }

    private static bool IsConditionalBranch(OpCode op)
    {
        return op == OpCodes.Brtrue || op == OpCodes.Brtrue_S ||
               op == OpCodes.Brfalse || op == OpCodes.Brfalse_S ||
               op == OpCodes.Beq || op == OpCodes.Beq_S ||
               op == OpCodes.Bne_Un || op == OpCodes.Bne_Un_S ||
               op == OpCodes.Bge || op == OpCodes.Bge_S ||
               op == OpCodes.Bge_Un || op == OpCodes.Bge_Un_S ||
               op == OpCodes.Bgt || op == OpCodes.Bgt_S ||
               op == OpCodes.Bgt_Un || op == OpCodes.Bgt_Un_S ||
               op == OpCodes.Ble || op == OpCodes.Ble_S ||
               op == OpCodes.Ble_Un || op == OpCodes.Ble_Un_S ||
               op == OpCodes.Blt || op == OpCodes.Blt_S ||
               op == OpCodes.Blt_Un || op == OpCodes.Blt_Un_S;
    }

    private void Step20_SanitizeUnsafePointerConversions()
    {
        int fixes = 0;
        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                var il = method.Body.GetILProcessor();
                var instructions = method.Body.Instructions;

                for (int i = 0; i < instructions.Count; i++)
                {
                    var instr = instructions[i];
                    if (instr.OpCode == OpCodes.Conv_U && i > 0)
                    {
                        var prev = instructions[i - 1];
                        bool isChar = prev.OpCode == OpCodes.Ldarg_1 || prev.OpCode == OpCodes.Ldarg_2 || prev.OpCode == OpCodes.Ldarg_3 || prev.OpCode == OpCodes.Ldarg_S;
                        if (isChar)
                        {
                            il.InsertBefore(instr, il.Create(OpCodes.Conv_I4));
                            fixes++;
                            i++;
                        }
                    }
                }
            }
        }
        _stackMergeFixes += fixes;
        Console.WriteLine($"  Unsafe pointer conversions sanitized: {fixes}");
    }

    private void PreserveOriginalTypeReferences()
    {
        if (_originalTypeRefNames == null || _originalTypeRefNames.Count == 0) return;

        var currentTypeRefs = new HashSet<string>(_module.GetTypeReferences().Select(tr => tr.FullName));
        var missing = _originalTypeRefNames.Except(currentTypeRefs).Where(name => !ShouldSkipTypeRefPreservation(name)).ToList();
        if (missing.Count == 0) return;

        var container = _module.Types.FirstOrDefault(t => t.Name == "<PrivateImplementationDetails>");
        if (container == null)
        {
            container = new TypeDefinition("", "<PrivateImplementationDetails>", TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Abstract, _module.TypeSystem.Object);
            _module.Types.Add(container);
        }

        var preserveType = new TypeDefinition("", "<TypeRefPreservation>k__BackingField", TypeAttributes.NestedPrivate | TypeAttributes.Sealed, _module.TypeSystem.Object);
        container.NestedTypes.Add(preserveType);

        foreach (var fullName in missing)
        {
            try
            {
                var lastDot = fullName.LastIndexOf('.');
                string ns = lastDot >= 0 ? fullName.Substring(0, lastDot) : "";
                string name = lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;

                AssemblyNameReference? scope = null;
                foreach (var tr in _module.GetTypeReferences())
                {
                    if (tr.Namespace == ns && tr.Scope is AssemblyNameReference anr)
                    {
                        scope = anr;
                        break;
                    }
                }

                if (scope == null)
                {
                    if (fullName.StartsWith("UnityEngine.")) scope = _module.AssemblyReferences.FirstOrDefault(a => a.Name.StartsWith("UnityEngine."));
                    else if (fullName.StartsWith("System.Runtime.CompilerServices.")) scope = _module.AssemblyReferences.FirstOrDefault(a => a.Name == "netstandard");
                    else if (fullName.StartsWith("Oculus.")) scope = _module.AssemblyReferences.FirstOrDefault(a => a.Name.StartsWith("Oculus."));
                    else if (fullName.StartsWith("Sirenix.")) scope = _module.AssemblyReferences.FirstOrDefault(a => a.Name.Contains("Sirenix"));
                    else if (fullName.StartsWith("Pathfinding.")) scope = _module.AssemblyReferences.FirstOrDefault(a => a.Name.StartsWith("Astar"));
                    else if (fullName.StartsWith("Photon.")) scope = _module.AssemblyReferences.FirstOrDefault(a => a.Name.StartsWith("Photon"));
                }

                if (scope != null)
                {
                    var typeRef = new TypeReference(ns, name, _module, scope);
                    var imported = _module.ImportReference(typeRef);
                    var field = new FieldDefinition($"__preserved_{name.Replace('`', '_').Replace('/', '_')}", FieldAttributes.Private | FieldAttributes.Static, imported);
                    preserveType.Fields.Add(field);
                }
            }
            catch { }
        }
    }

    private static bool ShouldSkipTypeRefPreservation(string fullName)
    {
        if (fullName.StartsWith("Fusion.CodeGen")) return true;
        return fullName switch
        {
            "Fusion.CompareOperator" => true,
            "Fusion.DefaultForPropertyAttribute" => true,
            "Fusion.DrawIfAttribute" => true,
            "Fusion.DrawIfMode" => true,
            "Fusion.FixedBufferPropertyAttribute" => true,
            "Fusion.Native" => true,
            "Fusion.NetworkAssemblyWeavedAttribute" => true,
            "Fusion.NetworkBehaviourWeavedAttribute" => true,
            "Fusion.NetworkedWeavedAttribute" => true,
            "Fusion.NetworkInputWeavedAttribute" => true,
            "Fusion.NetworkRpcStaticWeavedInvokerAttribute" => true,
            "Fusion.NetworkRpcWeavedInvokerAttribute" => true,
            "Fusion.NetworkStructWeavedAttribute" => true,
            "Fusion.PreserveInPluginAttribute" => true,
            "Fusion.ReadWriteUtilsForWeaver" => true,
            "Fusion.WeaverGeneratedAttribute" => true,
            _ when fullName.StartsWith("Fusion.IElementReaderWriter") => true,
            _ when fullName.StartsWith("Fusion.Internal.Unity") => true,
            _ when fullName.StartsWith("Fusion.SerializableDictionary") => true,
            _ when fullName.StartsWith("Fusion.Simulation") => true,
            _ => false,
        };
    }

    private List<TypeDefinition> GetAllTypes()
    {
        var result = new List<TypeDefinition>();
        foreach (var type in _module.Types)
        {
            try
            {
                result.Add(type);
                CollectNestedTypes(type, result);
            }
            catch { }
        }
        return result;
    }

    private void CollectNestedTypes(TypeDefinition type, List<TypeDefinition> result)
    {
        var queue = new Queue<TypeDefinition>();
        foreach (var nested in type.NestedTypes)
        {
            queue.Enqueue(nested);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var nestedNested in current.NestedTypes)
            {
                queue.Enqueue(nestedNested);
            }
        }
    }

    private bool IsCompilerGeneratedType(TypeDefinition type)
    {
        if (string.IsNullOrEmpty(type.Name)) return false;
        return type.Name.StartsWith("<") || type.Name.StartsWith("VB$");
    }

    private bool IsNetworkBehaviour(TypeDefinition type)
    {
        return IsSubclassOf(type, "NetworkBehaviour");
    }

    private bool IsSimulationBehaviour(TypeDefinition type)
    {
        return IsSubclassOf(type, "SimulationBehaviour");
    }

    private bool IsSubclassOf(TypeDefinition type, string baseName)
    {
        var current = type.BaseType;
        int safety = 0;
        while (current != null && safety++ < 30)
        {
            if (current.Name == baseName) return true;
            try
            {
                var r = current.Resolve();
                current = r?.BaseType;
            }
            catch { break; }
        }
        return false;
    }

    private bool ImplementsInterface(TypeDefinition type, string ifaceName)
    {
        return type.Interfaces.Any(i => i.InterfaceType.Name == ifaceName);
    }

    private bool IsTypeCompatible(TypeReference paramType, CustomAttributeArgument arg)
    {
        try
        {
            if (paramType.FullName == arg.Type.FullName) return true;

            var paramMeta = paramType.MetadataType;
            var argMeta = arg.Type.MetadataType;
            if (paramMeta == argMeta) return true;

            if (paramType.IsValueType && !paramType.IsPrimitive)
            {
                var resolved = paramType.Resolve();
                if (resolved?.IsEnum == true && (argMeta == MetadataType.Int32 || argMeta == MetadataType.Int64)) return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private void RemoveCustomAttribute(ICustomAttributeProvider provider, string attrName, ref int counter)
    {
        var attr = provider.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == attrName);
        if (attr != null) { provider.CustomAttributes.Remove(attr); counter++; }
    }

    private void RemoveFieldsByPrefix(TypeDefinition type, string prefix, ref int counter)
    {
        var fields = type.Fields.Where(f => f.Name.StartsWith(prefix)).ToList();
        foreach (var f in fields) { type.Fields.Remove(f); counter++; }
    }

    private void AddCompilerGeneratedAttribute(FieldDefinition field)
    {
        try
        {
            var compilerGenType = ImportType("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
            if (compilerGenType.FullName == "System.Object") return;
            var compilerGenTypeDef = compilerGenType.Resolve();
            if (compilerGenTypeDef == null) return;

            var ctor = compilerGenTypeDef.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
            if (ctor == null) return;

            var ctorRef = _module.ImportReference(ctor);
            field.CustomAttributes.Add(new CustomAttribute(ctorRef));
        }
        catch { }
    }

    private void EnsureCompilerGeneratedOnMethod(MethodDefinition method)
    {
        if (method == null) return;
        try
        {
            if (method.CustomAttributes.Any(a => a.AttributeType.Name == "CompilerGeneratedAttribute")) return;

            var compilerGenType = ImportType("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
            if (compilerGenType.FullName == "System.Object") return;
            var compilerGenTypeDef = compilerGenType.Resolve();
            if (compilerGenTypeDef == null) return;

            var ctor = compilerGenTypeDef.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
            if (ctor == null) return;

            var ctorRef = _module.ImportReference(ctor);
            method.CustomAttributes.Add(new CustomAttribute(ctorRef));
            _accessorCompilerGeneratedAttrs++;
        }
        catch { }
    }

    private void ClearReadOnlyGetterAttribute(MethodDefinition? method)
    {
        if (method == null) return;
        try
        {
            method.CustomAttributes.RemoveWhere(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        }
        catch { }
    }

    private void EnsureBackingFieldMetadata(FieldDefinition backingField)
    {
        if ((backingField.Attributes & FieldAttributes.SpecialName) == 0) backingField.Attributes |= FieldAttributes.SpecialName;
        if (!backingField.CustomAttributes.Any(a => a.AttributeType.Name == "CompilerGeneratedAttribute")) AddCompilerGeneratedAttribute(backingField);
        if (!backingField.CustomAttributes.Any(a => a.AttributeType.Name == "DebuggerBrowsableAttribute")) AddDebuggerBrowsableNeverAttribute(backingField);
    }

    private void RenameFieldAndFixReferences(TypeDefinition declaringType, FieldDefinition field, string newName)
    {
        string oldName = field.Name;
        field.Name = newName;

        var types = GetAllTypes();
        foreach (var type in types)
        {
            foreach (var method in type.Methods)
            {
                if (method.Body == null) continue;
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.Operand is FieldReference fr)
                    {
                        try
                        {
                            var resolved = fr.Resolve();
                            if (resolved != null && ReferenceEquals(resolved, field)) fr.Name = newName;
                        }
                        catch { }
                    }
                }
            }
        }
    }

    private void CreateSimpleSetter(PropertyDefinition prop, FieldDefinition backingField)
    {
        try
        {
            var setter = new MethodDefinition($"set_{prop.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, _module.TypeSystem.Void);
            setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, prop.PropertyType));

            var il = setter.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, _module.ImportReference(backingField));
            il.Emit(OpCodes.Ret);

            EnsureCompilerGeneratedOnMethod(setter);
            prop.SetMethod = setter;
            prop.DeclaringType.Methods.Add(setter);
            _createdSetters++;
        }
        catch { }
    }

    private void AddDebuggerBrowsableNeverAttribute(FieldDefinition field)
    {
        try
        {
            var debuggerBrowsableType = ImportType("System.Diagnostics.DebuggerBrowsableAttribute");
            if (debuggerBrowsableType.FullName == "System.Object") return;
            var debuggerBrowsableTypeDef = debuggerBrowsableType.Resolve();
            if (debuggerBrowsableTypeDef == null) return;

            var ctor = debuggerBrowsableTypeDef.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "DebuggerBrowsableState");
            if (ctor == null) return;

            var ctorRef = _module.ImportReference(ctor);
            var attr = new CustomAttribute(ctorRef);
            var enumType = ImportType("System.Diagnostics.DebuggerBrowsableState");
            attr.ConstructorArguments.Add(new CustomAttributeArgument(enumType, 0));
            field.CustomAttributes.Add(attr);
        }
        catch { }
    }

    private TypeReference ImportType(string fullName)
    {
        var corLib = _module.TypeSystem.CoreLibrary;
        if (corLib != null)
        {
            try
            {
                var asm = (corLib is AssemblyNameReference anr) ? _resolver.Resolve(anr) : null;
                if (asm != null)
                {
                    var type = asm.MainModule.GetType(fullName);
                    if (type != null) return _module.ImportReference(type);
                }
            }
            catch { }
        }

        foreach (var asmRef in _module.AssemblyReferences)
        {
            try
            {
                var asm = _resolver.Resolve(asmRef);
                if (asm == null) continue;
                var type = asm.MainModule.GetType(fullName);
                if (type != null) return _module.ImportReference(type);
            }
            catch { }
        }

        try
        {
            foreach (var asm in _resolver.GetResolvedAssemblies())
            {
                var type = asm.MainModule.GetType(fullName);
                if (type != null) return _module.ImportReference(type);
            }
        }
        catch { }

        return _module.TypeSystem.Object;
    }

    private void EmitDefaultReturn(MethodDefinition method)
    {
        var il = method.Body.GetILProcessor();
        var retType = method.ReturnType;
        switch (retType.MetadataType)
        {
            case MetadataType.Void:
                il.Emit(OpCodes.Ret); break;
            case MetadataType.Boolean:
            case MetadataType.Byte:
            case MetadataType.SByte:
            case MetadataType.Int16:
            case MetadataType.UInt16:
            case MetadataType.Int32:
            case MetadataType.UInt32:
            case MetadataType.Char:
                il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret); break;
            case MetadataType.Int64:
            case MetadataType.UInt64:
                il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Conv_I8); il.Emit(OpCodes.Ret); break;
            case MetadataType.Single:
                il.Emit(OpCodes.Ldc_R4, 0f); il.Emit(OpCodes.Ret); break;
            case MetadataType.Double:
                il.Emit(OpCodes.Ldc_R8, 0.0); il.Emit(OpCodes.Ret); break;
            default:
                if (retType.IsValueType)
                {
                    var tmp = new VariableDefinition(_module.ImportReference(retType));
                    method.Body.Variables.Add(tmp);
                    method.Body.InitLocals = true;
                    il.Emit(OpCodes.Ldloca_S, tmp);
                    il.Emit(OpCodes.Initobj, _module.ImportReference(retType));
                    il.Emit(OpCodes.Ldloc, tmp);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Ret);
                }
                break;
        }
    }

    private int SanitizeCustomAttribute(CustomAttribute attr)
    {
        int count = 0;
        try
        {
            if (attr.Constructor != null)
            {
                var imported = ImportMethodReference(attr.Constructor);
                if (imported != null && !ReferenceEquals(imported, attr.Constructor))
                {
                    attr.Constructor = imported;
                    count++;
                }
            }

            for (int i = 0; i < attr.ConstructorArguments.Count; i++)
            {
                try
                {
                    var arg = attr.ConstructorArguments[i];
                    if (arg.Type != null)
                    {
                        var imported = _module.ImportReference(arg.Type);
                        if (!ReferenceEquals(imported, arg.Type))
                        {
                            attr.ConstructorArguments[i] = new CustomAttributeArgument(imported, arg.Value);
                            count++;
                        }
                    }
                }
                catch { }
            }

            for (int i = 0; i < attr.Properties.Count; i++)
            {
                try
                {
                    var prop = attr.Properties[i];
                    if (prop.Argument.Type != null)
                    {
                        var imported = _module.ImportReference(prop.Argument.Type);
                        if (!ReferenceEquals(imported, prop.Argument.Type))
                        {
                            attr.Properties[i] = new CustomAttributeNamedArgument(prop.Name, new CustomAttributeArgument(imported, prop.Argument.Value));
                            count++;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return count;
    }

    private MethodReference? ImportMethodReference(MethodReference mr)
    {
        try
        {
            var imported = _module.ImportReference(mr);
            if (!ReferenceEquals(imported, mr)) return imported;

            bool needsForceImport = false;
            try
            {
                var resolvedDecl = mr.DeclaringType?.Resolve();
                if (resolvedDecl != null && resolvedDecl.Module != _module) needsForceImport = true;
                if (mr.DeclaringType is TypeDefinition td && td.Module != _module) needsForceImport = true;
            }
            catch { }

            if (!needsForceImport) return imported;
            return ForceImportMethodReference(mr);
        }
        catch
        {
            try { return ForceImportMethodReference(mr); } catch { return null; }
        }
    }

    private MethodReference ForceImportMethodReference(MethodReference mr)
    {
        if (mr is GenericInstanceMethod gim)
        {
            var importedElement = ForceImportMethodReference(gim.ElementMethod);
            var importedGim = new GenericInstanceMethod(importedElement);
            foreach (var arg in gim.GenericArguments) importedGim.GenericArguments.Add(_module.ImportReference(arg));
            return importedGim;
        }

        var importedDeclType = ForceImportTypeReference(mr.DeclaringType);
        var importedRetType = _module.ImportReference(mr.ReturnType);

        var newMr = new MethodReference(mr.Name, importedRetType, importedDeclType)
        {
            HasThis = mr.HasThis,
            ExplicitThis = mr.ExplicitThis,
            CallingConvention = mr.CallingConvention
        };

        foreach (var param in mr.Parameters) newMr.Parameters.Add(new ParameterDefinition(_module.ImportReference(param.ParameterType)));
        if (mr.GenericParameters.Count > 0)
        {
            foreach (var gp in mr.GenericParameters) newMr.GenericParameters.Add(gp);
        }
        return newMr;
    }

    private TypeReference ForceImportTypeReference(TypeReference? tr)
    {
        if (tr == null) return _module.TypeSystem.Object;

        if (tr is GenericInstanceType git)
        {
            var importedElement = ForceImportTypeReference(git.ElementType);
            var importedGit = new GenericInstanceType(importedElement);
            foreach (var arg in git.GenericArguments) importedGit.GenericArguments.Add(_module.ImportReference(arg));
            return _module.ImportReference(importedGit);
        }

        if (tr is ArrayType at)
        {
            var importedElement = ForceImportTypeReference(at.ElementType);
            return _module.ImportReference(new ArrayType(importedElement, at.Rank));
        }

        if (tr is ByReferenceType brt)
        {
            var importedElement = ForceImportTypeReference(brt.ElementType);
            return _module.ImportReference(new ByReferenceType(importedElement));
        }

        if (tr is PointerType pt)
        {
            var importedElement = ForceImportTypeReference(pt.ElementType);
            return _module.ImportReference(new PointerType(importedElement));
        }

        if (tr is RequiredModifierType rmt)
        {
            var importedElement = ForceImportTypeReference(rmt.ElementType);
            return _module.ImportReference(new RequiredModifierType(rmt.ModifierType, importedElement));
        }

        if (tr is OptionalModifierType omt)
        {
            var importedElement = ForceImportTypeReference(omt.ElementType);
            return _module.ImportReference(new OptionalModifierType(omt.ModifierType, importedElement));
        }

        if (tr is PinnedType pnt)
        {
            var importedElement = ForceImportTypeReference(pnt.ElementType);
            return _module.ImportReference(new PinnedType(importedElement));
        }

        if (tr is SentinelType st)
        {
            var importedElement = ForceImportTypeReference(st.ElementType);
            return _module.ImportReference(new SentinelType(importedElement));
        }

        return _module.ImportReference(tr);
    }

    private FieldReference? ImportFieldReference(FieldReference fr)
    {
        try
        {
            var imported = _module.ImportReference(fr);
            if (!ReferenceEquals(imported, fr)) return imported;

            bool needsForceImport = false;
            try
            {
                var resolvedDecl = fr.DeclaringType?.Resolve();
                if (resolvedDecl != null && resolvedDecl.Module != _module) needsForceImport = true;
                if (fr.DeclaringType is TypeDefinition td && td.Module != _module) needsForceImport = true;
            }
            catch { }

            if (!needsForceImport) return imported;

            var importedDeclType = ForceImportTypeReference(fr.DeclaringType);
            var importedFieldType = _module.ImportReference(fr.FieldType);
            return new FieldReference(fr.Name, importedFieldType, importedDeclType);
        }
        catch
        {
            try
            {
                var importedDeclType = ForceImportTypeReference(fr.DeclaringType);
                var importedFieldType = _module.ImportReference(fr.FieldType);
                return new FieldReference(fr.Name, importedFieldType, importedDeclType);
            }
            catch { return null; }
        }
    }

    private void DiagnoseUnimportedReferences()
    {
        var types = GetAllTypes();
        foreach (var type in types)
        {
            foreach (var method in type.Methods.ToList())
            {
                if (method.Body == null) continue;
                foreach (var instr in method.Body.Instructions)
                {
                    try
                    {
                        if (instr.Operand is MethodReference mr)
                        {
                            var imported = _module.ImportReference(mr);
                            if (!ReferenceEquals(imported, mr))
                            {
                                Console.WriteLine($"    DIFF MethodRef: {mr.FullName} in {type.FullName}::{method.Name}");
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    private bool IsStackPushInstruction(Instruction instr)
    {
        var op = instr.OpCode;
        return op == OpCodes.Ldarg || op == OpCodes.Ldarg_0 || op == OpCodes.Ldarg_1 ||
               op == OpCodes.Ldarg_2 || op == OpCodes.Ldarg_3 || op == OpCodes.Ldarg_S ||
               op == OpCodes.Ldloc || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 ||
               op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S ||
               op == OpCodes.Ldloca || op == OpCodes.Ldloca_S ||
               op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 ||
               op == OpCodes.Ldc_I4_2 || op == OpCodes.Ldc_I4_3 || op == OpCodes.Ldc_I4_4 ||
               op == OpCodes.Ldc_I4_5 || op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 ||
               op == OpCodes.Ldc_I4_8 || op == OpCodes.Ldc_I4_M1 || op == OpCodes.Ldc_I4_S ||
               op == OpCodes.Ldc_R4 || op == OpCodes.Ldc_R8 || op == OpCodes.Ldc_I8 ||
               op == OpCodes.Ldstr || op == OpCodes.Ldnull || op == OpCodes.Ldftn ||
               op == OpCodes.Ldtoken || op == OpCodes.Ldfld || op == OpCodes.Ldsfld ||
               op == OpCodes.Ldflda || op == OpCodes.Ldsflda;
    }

    private bool SafeHasAttribute(ICustomAttributeProvider provider, string attrName)
    {
        try { return provider.CustomAttributes.Any(a => a.AttributeType.Name == attrName); } catch { return false; }
    }

    private bool ReferencesBurstClassInfo(Instruction instr)
    {
        return instr.Operand switch
        {
            TypeReference tr => tr.Name == "BurstClassInfo" || tr.Namespace.StartsWith("Unity.Burst") || (tr.Name == "FunctionPointer`1" && tr.Namespace.StartsWith("Unity.Burst")),
            MethodReference mr => mr.DeclaringType.Name == "BurstClassInfo" || mr.DeclaringType.Namespace.StartsWith("Unity.Burst"),
            FieldReference fr => fr.DeclaringType.Name == "BurstClassInfo" || fr.DeclaringType.Namespace.StartsWith("Unity.Burst") || fr.FieldType.Namespace.StartsWith("Unity.Burst"),
            _ => false
        };
    }

    private bool IsBurstTypeReference(Instruction instr)
    {
        return instr.Operand switch
        {
            TypeReference tr => tr.Namespace.StartsWith("Unity.Burst") || (tr.Namespace.StartsWith("Unity.Collections") && tr.Name.Contains("NativeHashMap")),
            MethodReference mr => mr.DeclaringType.Namespace.StartsWith("Unity.Burst") || (mr.DeclaringType.Namespace.StartsWith("Unity.Collections") && mr.DeclaringType.Name.Contains("NativeHashMap")),
            FieldReference fr => fr.DeclaringType.Namespace.StartsWith("Unity.Burst") || fr.FieldType.Namespace.StartsWith("Unity.Burst") || fr.FieldType.Name == "SharedStatic`1",
            _ => false
        };
    }

    private bool IsBrfalse(OpCode op) => op == OpCodes.Brfalse || op == OpCodes.Brfalse_S;

    private static bool IsFusionCapacityTypeReference(TypeReference typeRef)
    {
        if (typeRef.Namespace != "Fusion") return false;
        var name = typeRef.Name;
        if (name.Length < 2 || name[0] != '_') return false;

        int limit = name.Length;
        for (int i = 1; i < limit; i++)
        {
            if (!char.IsDigit(name[i])) return false;
        }
        return true;
    }

    private void FixGlobalILArtifacts(TypeDefinition type)
    {
        foreach (var method in type.Methods.ToList())
        {
            if (method.Body == null) continue;
            try
            {
                var il = method.Body.GetILProcessor();
                var instructions = method.Body.Instructions;
                bool modified = false;

                int limit = instructions.Count;
                for (int i = 0; i < limit; i++)
                {
                    var instr = instructions[i];
                    if (instr.OpCode == OpCodes.Call && instr.Operand is MethodReference mr && mr.Name == "op_Implicit")
                    {
                        var declaringTypeName = mr.DeclaringType?.Name ?? "";
                        if (declaringTypeName.Contains("NetworkString") || declaringTypeName.Contains("ReadOnlySpan"))
                        {
                            il.Replace(instr, il.Create(OpCodes.Nop));
                            modified = true;
                        }
                    }
                }
                if (modified) MarkMethodModified(method);
            }
            catch { }
        }
    }

    private void FixStaleFieldReferencesInMethodBodies(TypeDefinition type, FieldDefinition changedField)
    {
        var freshFieldRef = _module.ImportReference(changedField);
        bool isRefType = !changedField.FieldType.IsValueType;

        foreach (var method in type.Methods.ToList())
        {
            if (!method.HasBody || method.Body.Instructions.Count == 0) continue;
            try
            {
                PrepareMethod(method.Body);
                var il = method.Body.GetILProcessor();
                var instructions = method.Body.Instructions;
                bool modified = false;

                for (int i = 0; i < instructions.Count; i++)
                {
                    var instr = instructions[i];
                    if (isRefType && instr.OpCode == OpCodes.Ldflda && IsFieldRefMatch(instr.Operand, changedField))
                    {
                        if (i + 1 < instructions.Count && instructions[i + 1].OpCode == OpCodes.Initobj)
                        {
                            var ldnullInstr = il.Create(OpCodes.Ldnull);
                            var stfldInstr = il.Create(OpCodes.Stfld, freshFieldRef);
                            il.Replace(instr, ldnullInstr);
                            il.Replace(instructions[i + 1], stfldInstr);
                            modified = true;
                            i++;
                            continue;
                        }

                        if (i + 1 < instructions.Count && instructions[i + 1].OpCode == OpCodes.Conv_U)
                        {
                            var ldnullInstr = il.Create(OpCodes.Ldnull);
                            il.Replace(instr, ldnullInstr);
                            il.Replace(instructions[i + 1], il.Create(OpCodes.Nop));
                            modified = true;
                            i++;
                            continue;
                        }
                    }

                    if ((instr.OpCode == OpCodes.Stfld || instr.OpCode == OpCodes.Ldfld ||
                         instr.OpCode == OpCodes.Ldsfld || instr.OpCode == OpCodes.Stsfld) &&
                        IsFieldRefMatch(instr.Operand, changedField) &&
                        instr.Operand is FieldReference fr && !ReferenceEquals(fr, freshFieldRef))
                    {
                        var newInstr = il.Create(instr.OpCode, freshFieldRef);
                        il.Replace(instr, newInstr);
                        modified = true;
                    }

                    if ((instr.OpCode == OpCodes.Ldflda || instr.OpCode == OpCodes.Ldsflda) && IsFieldRefMatch(instr.Operand, changedField))
                    {
                        instr.Operand = freshFieldRef;
                        modified = true;
                    }
                }
                FinalizeMethod(method.Body);
            }
            catch { }
        }
    }

    private void FixStaleFieldReferencesAfterRename(TypeDefinition type, string oldFieldName, string newFieldName)
    {
        var renamedField = type.Fields.FirstOrDefault(f => f.Name == newFieldName);
        if (renamedField == null) return;

        var freshRef = _module.ImportReference(renamedField);
        foreach (var method in type.Methods.ToList())
        {
            if (method.Body == null) continue;
            try
            {
                var instructions = method.Body.Instructions;
                int limit = instructions.Count;
                for (int i = 0; i < limit; i++)
                {
                    var instr = instructions[i];
                    if (instr.Operand is FieldReference fr && fr.Name == oldFieldName && fr.DeclaringType != null && fr.DeclaringType.FullName == type.FullName)
                    {
                        instr.Operand = freshRef;
                    }
                }
            }
            catch { }
        }
    }

    private static bool IsFieldRefMatch(object? operand, FieldDefinition fieldDef)
    {
        if (operand is not FieldReference fr) return false;
        if (fr.Name != fieldDef.Name) return false;
        return fr.DeclaringType?.FullName == fieldDef.DeclaringType?.FullName;
    }

    private void PrintStatistics()
    {
        Console.WriteLine("\n[Deweaver] Execution completed successfully.");
    }
}

class TolerantAssemblyResolver : IAssemblyResolver
{
    private readonly DefaultAssemblyResolver _inner;
    private readonly Dictionary<string, AssemblyDefinition> _stubs = new();
    private readonly HashSet<AssemblyDefinition> _resolvedAssemblies = new();

    public TolerantAssemblyResolver(DefaultAssemblyResolver inner)
    {
        _inner = inner;
    }

    public IEnumerable<AssemblyDefinition> GetResolvedAssemblies()
    {
        return _resolvedAssemblies.Concat(_stubs.Values).Distinct();
    }

    public void AddSearchDirectory(string directory)
    {
        _inner.AddSearchDirectory(directory);
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        try
        {
            var asm = _inner.Resolve(name);
            if (asm != null) _resolvedAssemblies.Add(asm);
            return asm;
        }
        catch
        {
            return GetOrCreateStub(name);
        }
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        try
        {
            var asm = _inner.Resolve(name, parameters);
            if (asm != null) _resolvedAssemblies.Add(asm);
            return asm;
        }
        catch
        {
            return GetOrCreateStub(name);
        }
    }

    private AssemblyDefinition GetOrCreateStub(AssemblyNameReference name)
    {
        if (_stubs.TryGetValue(name.Name, out var existing)) return existing;

        var asmName = new AssemblyNameDefinition(name.Name, name.Version);
        var stub = AssemblyDefinition.CreateAssembly(asmName, name.Name, ModuleKind.Dll);
        _stubs[name.Name] = stub;
        return stub;
    }

    public void Dispose()
    {
        foreach (var stub in _stubs.Values) stub.Dispose();
        _inner.Dispose();
    }
}

static class CecilExtensions
{
    public static void RemoveWhere<T>(this Collection<T> collection, Func<T, bool> predicate) where T : class
    {
        var toRemove = collection.Where(predicate).ToList();
        int limit = toRemove.Count;
        for (int i = 0; i < limit; i++) collection.Remove(toRemove[i]);
    }
}
