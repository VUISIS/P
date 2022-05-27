using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Antlr4.Runtime.Dfa;
using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.IntermediateLanguage
{
    public class IntermediateLanguageCodeGenerator : ICodeGenerator
    {
        private CompilationContext context;

        public IEnumerable<CompiledFile> GenerateCode(ICompilationJob job, Scope globalScope)
        {
            this.context = new CompilationContext(job);

            CompiledFile file = GenerateSource(globalScope);
            CompiledFile include = GenerateInclude(globalScope);
            return new List<CompiledFile> {file, include};
        }

        public CompiledFile GenerateInclude(Scope globalScope)
        {
            CompiledFile include = new CompiledFile("Intermediate.4ml");
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = GetType().Namespace+".Intermediate.4ml";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string result = reader.ReadToEnd();
                        include.Stream.Write(result);
                    }
                }
            }

            return include;
        }

        public CompiledFile GenerateSource(Scope globalScope)
        {
            CompiledFile source = new CompiledFile(context.FileName);
            source.Stream.WriteLine($"model {context.ProjectName} of Intermediate at \"Intermediate.4ml\" {{");
            //GenerateEnums(source.Stream, globalScope.Enums);
            //GenerateTypedefs(source.Stream, globalScope.Typedefs);
            GenerateEvents(source.Stream, globalScope.Events);
            GenerateEventSets(source.Stream, globalScope.EventSets);
//            GenerateFunctions(source.Stream, globalScope.Functions);
//            GenerateInterfaces(source.Stream, globalScope.Interfaces);
            Dictionary<String, Machine> machineMap = new Dictionary<string, Machine>();
            GenerateMachines(source.Stream, globalScope.Machines, machineMap);
//            GenerateStateGroups(source.Stream, globalScope.StateGroups);
            GenerateStates(source.Stream, globalScope.States);
            // variables generated per machine
            // Not sure what to do with implementations
            // implementations
            // Not sure what to do with safety tests
            // safety tests
            // Not sure what to do with refinement tests
            // refinement tests
            // Not sure what to do with named modules
            // named modules
            source.Stream.WriteLine("}");
            return source;
        }

        public void GenerateList<T>(StringWriter stream, string itemName, IEnumerable<T> objs,
            Func<T, String> valueExtractor)
        {
            var parenCount = 0;
            foreach (var o in objs)
            {
                stream.Write($"{itemName}({valueExtractor(o)},");
                parenCount++;
            }

            stream.Write("NIL");
            while (parenCount > 0)
            {
                stream.Write(")");
                parenCount--;
            }
        }

        public void GenerateList<T>(StringWriter stream, string itemName, IEnumerable<T> objs,
            Action<T, StringWriter> valueExtractor)
        {
            var parenCount = 0;
            foreach (var o in objs)
            {
                stream.Write($"{itemName}(");
                valueExtractor(o, stream);
                stream.Write(",");
                parenCount++;
            }

            stream.Write("NIL");
            while (parenCount > 0)
            {
                stream.Write(")");
                parenCount--;
            }
        }

        public string GenerateId()
        {
            return "\""+context.GenerateId()+"\"";
        }

        public void GenerateEnums(StringWriter stream, IEnumerable<PEnum> enums)
        {
            foreach (PEnum penum in enums)
            {
                stream.Write($"  EnumTypeDef(\"{penum.Name}\", ");
                var nameList = new List<string>();
                var valueList = new List<int>();
                foreach (EnumElem elem in penum.Values)
                {
                    nameList.Add(elem.Name);
                    valueList.Add(elem.Value);
                }

                GenerateList(stream, "StringList", nameList, x => "\""+x+"\"");
                stream.Write(", ");
                GenerateList(stream, "IntegerList", valueList, x => x.ToString());
                stream.WriteLine($",{GenerateId()}).");
            }
        }

        public void GenerateTypedefs(StringWriter stream, IEnumerable<TypeDef> typedefs)
        {
            foreach (var typeDef in typedefs)
            {
                stream.Write($"TypeDef(\"{typeDef.Name}\",");
                if (typeDef.Name == null)
                {
                    stream.Write("NIL,");
                }
                else
                {
                    GenerateTypeExpr(stream, typeDef.Type);
                    stream.Write(",");
                }

                stream.WriteLine($"{GenerateId()}).");
            }
        }

        public void GenerateEventType(StringWriter stream, String eventName, PLanguageType evtType)
        {
            /*
            switch (evtType)
            {
                case DataType t:
                    stream.Write($"  EventParameter(\"{eventName}\", 1, \"{t.CanonicalRepresentation}\").");
                    break;
                case EnumType t:
                    stream.Write($"  EventParameter(\"{eventName}\", 1, \"{t.EnumDecl.Name }\").");
                    break;
                case ForeignType t:
                    stream.Write($"  EventParameter(\"{eventName}\", 1, \"{t.CanonicalRepresentation}\").");
                    break;
                case MapType t:
                    break;
                case NamedTupleType t:
                    PLanguageType fieldType;
                    var typesEnum = t.Types.GetEnumerator();
                    var paramNumber = 1;
                    foreach (var name in t.Names)
                    {
                        typesEnum.MoveNext();
                        fieldType = typesEnum.Current;
                        stream.Write($"  EventParameter(\"{eventName}\", {paramNumber}, \"{fieldType}\").");
                        numParens++;
                    }

                    stream.Write("NIL");
                    while (numParens > 0)
                    {
                        stream.Write(")");
                        numParens--;
                    }

                    break;
                case PermissionType t:
                    stream.Write($"NameType(\"{t.CanonicalRepresentation}\")");
                    break;
                case PrimitiveType t:
                    stream.Write($"BaseType({t.CanonicalRepresentation.ToUpper()})");
                    break;
                case SequenceType t:
                    stream.Write($"SeqType(");
                    GenerateTypeExpr(stream, t.ElementType);
                    stream.Write(")");
                    break;
                case SetType t:
                    stream.Write($"SeqType(");
                    GenerateTypeExpr(stream, t.ElementType);
                    stream.Write(")");
                    break;
                case TupleType t:
                    stream.Write("Tuple(");
                    foreach (var tt in t.Types)
                    {
                        GenerateTypeExpr(stream, tt);
                        stream.Write(",");
                    }

                    stream.Write("NIL)");
                    break;
                case TypeDefType t:
                    stream.Write($"NameType(\"{t.TypeDefDecl.Name}\")");
                    break;

                default:
                    throw new Exception("Unable to handle type: " + plt.GetType().Name);
            }
            */
        }
        
        public void GenerateEvents(StringWriter stream, IEnumerable<PEvent> events)
        {
            foreach (var evt in events)
            {
                stream.WriteLine($"  {evt.Name} is Event(\"{evt.Name}\").");
//                GenerateEventType(stream, evt.Name, evt.PayloadType);
            }
        }

        public void GenerateEventSets(StringWriter stream, IEnumerable<NamedEventSet> eventSets)
        {
            foreach (var eventSet in eventSets)
            {
                stream.WriteLine($"  {eventSet.Name} is EventGroup(\"{eventSet.Name}\").");
                foreach (PEvent evt in eventSet.Events)
                {
                    stream.WriteLine($"  EventInGroup(\"{eventSet.Name}\", \"{evt.Name}\").");
                }
            }
        }

        public void GenerateFunctions(StringWriter stream, IEnumerable<Function> functions)
        {
            foreach (var func in functions)
            {
                GenerateFunction(stream, func);
                stream.WriteLine(".");
            }
        }

        public void GenerateFunction(StringWriter stream, Function func)
        {
            if (func.IsAnon)
            {
                stream.Write($"AnonFunDecl(");
            }
            else
            {
                stream.Write($"FunDecl(\"{func.Name}\", ");
            }
            if (func.Owner != null)
            {
                stream.Write($"\"{func.Owner.Name}\",");
            }
            else
            {
                stream.Write("NIL,");

            }

            if (func.IsAnon)
            {
                stream.Write($"\"{func.ParentFunction.Name}\",");
            }

            if (!func.IsAnon)
            {
                GenerateList(stream, "NmdTupType", func.Signature.Parameters,
                    (v, vstream) => GenerateNamedTupTypeField(vstream, v));
                stream.Write(",");
                GenerateTypeExpr(stream, func.Signature.ReturnType);
                stream.Write(",");
            }

            GenerateList(stream, "NmdTupType", func.LocalVariables,
                (v, vstream) => GenerateNamedTupTypeField(vstream, v));
            stream.Write(",");
            if (func.Body == null)
            {
                stream.Write("NIL");
            }
            else
            {
                GenerateStmt(stream, func.Body);
            }
            stream.Write($", {GenerateId()})");
        }

        public void GenerateNamedTupTypeField(StringWriter stream, Variable v)
        {
            stream.Write($"NmdTupTypeField(\"{v.Name}\",");
            GenerateTypeExpr(stream, v.Type);
            stream.Write(")");
        }

        public void GenerateTypeExpr(StringWriter stream, PLanguageType plt)
        {
            switch (plt)
            {
                case DataType t:
                    stream.Write($"NameType(\"{t.CanonicalRepresentation}\")");
                    break;
                case EnumType t:
                    stream.Write($"NameType(\"{t.EnumDecl.Name}\")");
                    break;
                case ForeignType t:
                    stream.Write($"NameType(\"{t.CanonicalRepresentation}\")");
                    break;
                case MapType t:
                    stream.Write($"MapType(");
                    GenerateTypeExpr(stream, t.KeyType);
                    stream.Write(",");
                    GenerateTypeExpr(stream, t.ValueType);
                    stream.Write(")");
                    break;
                case NamedTupleType t:
                    PLanguageType fieldType;
                    var typesEnum = t.Types.GetEnumerator();
                    var numParens = 0;
                    foreach (var name in t.Names)
                    {
                        typesEnum.MoveNext();
                        fieldType = typesEnum.Current;
                        stream.Write($"NmdTupType(NmdTupTypeField(\"{name}\",");
                        GenerateTypeExpr(stream, fieldType);
                        stream.Write("),");
                        numParens++;
                    }

                    stream.Write("NIL");
                    while (numParens > 0)
                    {
                        stream.Write(")");
                        numParens--;
                    }

                    break;
                case PermissionType t:
                    stream.Write($"NameType(\"{t.CanonicalRepresentation}\")");
                    break;
                case PrimitiveType t:
                    stream.Write($"BaseType({t.CanonicalRepresentation.ToUpper()})");
                    break;
                case SequenceType t:
                    stream.Write($"SeqType(");
                    GenerateTypeExpr(stream, t.ElementType);
                    stream.Write(")");
                    break;
                case SetType t:
                    stream.Write($"SeqType(");
                    GenerateTypeExpr(stream, t.ElementType);
                    stream.Write(")");
                    break;
                case TupleType t:
                    stream.Write("Tuple(");
                    foreach (var tt in t.Types)
                    {
                        GenerateTypeExpr(stream, tt);
                        stream.Write(",");
                    }

                    stream.Write("NIL)");
                    break;
                case TypeDefType t:
                    stream.Write($"NameType(\"{t.TypeDefDecl.Name}\")");
                    break;

                default:
                    throw new Exception("Unable to handle type: " + plt.GetType().Name);
            }
        }

        public void GenerateStmt(StringWriter stream, IPStmt stmt)
        {
            switch (stmt)
            {
                case CtorStmt s:
                    stream.Write($"NewStmt(\"{s.Interface.Name}\",");
                    GenerateExprList(stream, s.Arguments);
                    stream.WriteLine($",NIL,{GenerateId()})");
                    break;
                case RaiseStmt s:
                    stream.Write("Raise(");
                    GenerateExpr(stream, s.PEvent);
                    stream.Write(",");
                    GenerateExprList(stream, s.Payload);
                    stream.WriteLine($",{GenerateId()})");
                    break;
                case SendStmt s:
                    stream.Write("Send(");
                    GenerateExpr(stream, s.MachineExpr);
                    stream.Write(",");
                    GenerateExpr(stream, s.Evt);
                    stream.Write(",");
                    GenerateExprList(stream, s.Arguments);
                    stream.WriteLine($",{GenerateId()})");
                    break;
                case AnnounceStmt s:
                    stream.Write("Announce(");
                    GenerateExpr(stream, s.PEvent);
                    stream.Write(",");
                    // Both the formula definition and the P grammar allow Announce to have a list
                    // of expressions, but the AnnounceStmt can only have 1 expr in the payload
                    // Here is where the parser generates one from the list of exprs:
                    //  return new AnnounceStmt(context, evtExpr, args.Count == 0 ? null : args[0]);
                    if (s.Payload == null)
                    {
                        stream.Write("NIL");
                    }
                    else
                    {
                        GenerateExpr(stream, s.Payload);
                    }

                    stream.WriteLine($",{GenerateId()})");
                    break;
                case FunCallStmt s:
                    stream.Write($"FunStmt(\"{s.Function.Name}\",");
                    GenerateExprList(stream, s.ArgsList);
                    stream.WriteLine($",NIL,0,{GenerateId()})"); // TODO: where should label come from?
                    break;

                case PopStmt:
                    stream.WriteLine($"NulStmt(POP,{GenerateId()})");
                    break;

                case RemoveStmt s:
                    stream.Write("BinStmt(REMOVE,");
                    GenerateExpr(stream, s.Variable);
                    stream.Write(",NONE,");
                    GenerateExpr(stream, s.Value);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case AssignStmt s:
                    stream.Write("BinStmt(ASSIGN,");
                    GenerateExpr(stream, s.Location);
                    stream.Write(",NONE,");
                    GenerateExpr(stream, s.Value);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case MoveAssignStmt s:
                    stream.Write("BinStmt(ASSIGN,");
                    GenerateExpr(stream, s.ToLocation);

                    stream.Write(",MOVE,");
                    GenerateVariable(stream, s.FromVariable);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case SwapAssignStmt s:
                    stream.Write("BinStmt(ASSIGN,");
                    GenerateExpr(stream, s.NewLocation);

                    stream.Write(",SWAP,");
                    GenerateVariable(stream, s.OldLocation);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case InsertStmt s:
                    stream.Write("InsertStmt(");
                    GenerateExpr(stream, s.Variable);
                    stream.Write(",");
                    GenerateExpr(stream, s.Index);
                    stream.Write(",");
                    GenerateExpr(stream, s.Value);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case AddStmt s:
                    stream.Write("BinStmt(INSERT,");
                    GenerateExpr(stream, s.Variable);
                    stream.Write(",NONE,");
                    GenerateExpr(stream, s.Value);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case ReturnStmt s:
                    stream.Write("Return(");
                    if (s.ReturnValue == null)
                    {
                        stream.Write("NIL");
                    }
                    else
                    {
                        GenerateExpr(stream, s.ReturnValue);
                    }

                    stream.WriteLine($",{GenerateId()})");
                    break;

                case WhileStmt s:
                    stream.Write("While(");
                    GenerateExpr(stream, s.Condition);
                    stream.Write(",");
                    GenerateStmt(stream, s.Body);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case IfStmt s:
                    stream.Write("Ite(");
                    GenerateExpr(stream, s.Condition);
                    stream.Write(",");
                    GenerateStmt(stream, s.ThenBranch);
                    stream.Write(",");
                    GenerateStmt(stream, s.ElseBranch);
                    stream.WriteLine($",{GenerateId()})");
                    break;

                case CompoundStmt s:
                    if (s.Statements.Count == 0)
                    {
                        stream.Write($"NulStmt(SKIP, {GenerateId()})");
                    }
                    else if (s.Statements.Count == 1)
                    {
                        GenerateStmt(stream, s.Statements[0]);
                    }
                    else if (s.Statements.Count > 1)
                    {
                        int parenCount = 0;
                        for (int i = 0; i < s.Statements.Count - 1; i++)
                        {
                            stream.Write("Seq(");
                            GenerateStmt(stream, s.Statements[i]);
                            stream.Write(",");
                            parenCount++;
                        }

                        GenerateStmt(stream, s.Statements[^1]);
                        for (int i = 0; i < parenCount; i++)
                        {
                            stream.Write($",{GenerateId()})");
                        }
                    }

                    break;

                case ReceiveStmt s:
                    stream.Write("Receive(");
                    GenerateCases(stream, s.Cases);
                    stream.WriteLine($",0,{GenerateId()})");
                    break;

                case AssertStmt s:
                    stream.Write("Assert(");
                    GenerateExpr(stream, s.Assertion);
                    if (s.Message == null)
                    {
                        stream.Write("NIL");
                    }
                    else
                    {
                        GenerateExpr(stream, s.Message);
                    }

                    stream.WriteLine($",{GenerateId()})");
                    break;

                case PrintStmt s:
                    stream.Write("Print(");
                    GenerateExpr(stream, s.Message);
                    stream.WriteLine($",NIL, NIL,{GenerateId()})");
                    break;

                case GotoStmt s:
                    stream.Write("Goto(");
                    stream.Write($"QualifiedName(\"{s.State.Name}\"),");
                    if (s.Payload == null)
                    {
                        stream.Write("NIL");
                    }
                    else
                    {
                        GenerateExpr(stream, s.Payload);
                    }

                    stream.WriteLine($",{GenerateId()})");
                    break;

                case BreakStmt s:
                    stream.WriteLine($"Break({GenerateId()})");
                    break;

                case ContinueStmt s:
                    stream.WriteLine($"Continue({GenerateId()})");
                    break;

                default:
                    throw new Exception("Unable to handle expression type " + stmt.GetType().Name);

            }
        }

        public void GenerateExprList(StringWriter stream, IEnumerable<IPExpr> exprs)
        {
            var parenCount = 0;
            foreach (var o in exprs)
            {
                stream.Write($"Exprs(NONE,");
                GenerateExpr(stream, o);
                stream.Write(",");
                parenCount++;
            }

            stream.Write("NIL");
            while (parenCount > 0)
            {
                stream.Write(")");
                parenCount--;
            }
        }

        public void GenerateExpr(StringWriter stream, IPExpr expr)
        {
            switch (expr)
            {
                case CtorExpr e:
                    stream.Write($"New(\"{e.Interface.Name}\",");
                    GenerateExprList(stream, e.Arguments);
                    stream.Write($",{GenerateId()})");
                    break;

                case FunCallExpr e:
                    stream.Write($"FunApp(\"{e.Function.Name}\",");
                    GenerateExprList(stream, e.Arguments);
                    // unclear what label should be
                    stream.Write(",0");
                    stream.Write($",{GenerateId()})");
                    break;

                case NullLiteralExpr e:
                    stream.Write($"NulApp(NULL, {GenerateId()})");
                    break;

                case IntLiteralExpr e:
                    stream.Write($"NulApp({e.Value}, {GenerateId()})");
                    break;

                case BoolLiteralExpr e:
                    stream.Write($"NulApp({e.Value.ToString().ToUpper()}, {GenerateId()})");
                    break;

                case FloatLiteralExpr e:
                    stream.Write($"NulApp({e.Value}, {GenerateId()})");
                    break;

                case ThisRefExpr e:
                    stream.Write($"NulApp(THIS, {GenerateId()})");
                    break;

                case NondetExpr e:
                    stream.Write($"NulApp(NONDET, {GenerateId()})");
                    break;

                // no more halt expr?

                case UnaryOpExpr e:
                    stream.Write($"UnApp({UnaryOpToString(e.Operation)}, ");
                    GenerateExpr(stream, e.SubExpr);
                    stream.Write($",{GenerateId()})");
                    break;

                case ValuesExpr e:
                    stream.Write("UnApp(VALUES,");
                    GenerateExpr(stream, e.Expr);
                    stream.Write($",{GenerateId()})");
                    break;

                case KeysExpr e:
                    stream.Write("UnApp(KEYS,");
                    GenerateExpr(stream, e.Expr);
                    stream.Write($",{GenerateId()})");
                    break;

                case SizeofExpr e:
                    stream.Write("UnApp(SIZEOF,");
                    GenerateExpr(stream, e.Expr);
                    stream.Write($",{GenerateId()})");
                    break;

                case BinOpExpr e:
                    stream.Write($"BinApp({BinaryOpToString(e.Operation)},");
                    GenerateExpr(stream, e.Lhs);
                    stream.Write(",");
                    GenerateExpr(stream, e.Rhs);
                    stream.Write($",{GenerateId()})");
                    break;

                case SeqAccessExpr e:
                    stream.Write("BinApp(IDX,");
                    GenerateExpr(stream, e.SeqExpr);
                    stream.Write(",");
                    GenerateExpr(stream, e.IndexExpr);
                    stream.Write($",{GenerateId()})");
                    break;

                case ContainsExpr e:
                    stream.Write("BinApp(IDX,");
                    GenerateExpr(stream, e.Item);
                    stream.Write(",");
                    GenerateExpr(stream, e.Collection);
                    stream.Write($",{GenerateId()})");
                    break;

                case DefaultExpr e:
                    stream.Write("Default(");
                    GenerateTypeExpr(stream, e.Type);
                    stream.Write($",{GenerateId()})");
                    break;

                case CastExpr e:
                    stream.Write("Cast(");
                    GenerateExpr(stream, e.SubExpr);
                    stream.Write(",");
                    GenerateTypeExpr(stream, e.Type);
                    stream.Write($",{GenerateId()})");
                    break;

                case CoerceExpr e:
                    stream.Write("Convert(");
                    GenerateExpr(stream, e.SubExpr);
                    stream.Write(",");
                    GenerateTypeExpr(stream, e.Type);
                    stream.Write($",{GenerateId()})");
                    break;

                case UnnamedTupleExpr e:
                    stream.Write("Tuple(");
                    GenerateExprList(stream, e.TupleFields);
                    stream.Write($",{GenerateId()})");
                    break;

                case NamedTupleExpr e:
                    stream.Write("NamedTuple(");
                    NamedTupleType namedTupleType = (NamedTupleType) e.Type;
                    int parenCount = 0;
                    IEnumerator<string> names = namedTupleType.Names.GetEnumerator();
                    IEnumerator<IPExpr> exprs = e.TupleFields.GetEnumerator();
                    while (names.MoveNext())
                    {
                        exprs.MoveNext();
                        stream.Write($"NamedExprs(\"{names.Current}\",");
                        GenerateExpr(stream, exprs.Current);
                        stream.Write(",");
                        parenCount++;
                    }

                    stream.Write("NIL");
                    while (parenCount > 0)
                    {
                        stream.Write(")");
                        parenCount--;
                    }

                    stream.Write($",{GenerateId()})");
                    names.Dispose();
                    exprs.Dispose();
                    break;

                case NamedTupleAccessExpr e:
                    stream.Write("Field(");
                    GenerateExpr(stream, e.SubExpr);
                    stream.Write($",\"{e.FieldName}\",{GenerateId()})");
                    break;
                
                case TupleAccessExpr e:
                    stream.Write("Field(");
                    GenerateExpr(stream, e.SubExpr);
                    stream.Write($"{e.FieldNo},{GenerateId()})");
                    break;
                
                case VariableAccessExpr e:
                    stream.Write($"Name(\"{e.Variable.Name}\"");
                    stream.Write($",{GenerateId()})");
                    break;
               
                case CloneExpr e:
                    stream.Write($"Clone(");
                    GenerateExpr(stream, e.Term);
                    stream.Write($",{GenerateId()})");
                    break;
                
                case LinearAccessRefExpr e:
                    if (e.LinearType == LinearType.Move)
                    {
                        stream.Write($"Name(\"{e.Variable.Name}\"");
                        stream.Write($",{GenerateId()})");
                    }
                    break;
                
                case EventRefExpr e:
                    stream.Write($"Name(\"{e.Value.Name}\"");
                    stream.Write($",{GenerateId()})");
                    break;
                
                case EnumElemRefExpr e:
                    stream.Write($"Name(\"{e.Value.Name}\"");
                    stream.Write($",{GenerateId()})");
                    break;
                
                case SetAccessExpr e:
                    stream.Write($"SetAcc(");
                    GenerateExpr(stream, e.SetExpr);
                    stream.Write(",");
                    GenerateExpr(stream, e.IndexExpr);
                    stream.Write($",{GenerateId()})");
                    break;
                
                case MapAccessExpr e:
                    stream.Write($"MapAcc(");
                    GenerateExpr(stream, e.MapExpr);
                    stream.Write(",");
                    GenerateExpr(stream, e.IndexExpr);
                    stream.Write($",{GenerateId()})");
                    break;
                
                case ChooseExpr e:
                    stream.Write($"Choose(");
                    GenerateExpr(stream, e.SubExpr);
                    stream.Write($",{GenerateId()})");
                    break;
                
                default:
                    throw new Exception("Unable to handle expression type " + expr.GetType().Name);
            }

        }

        public String UnaryOpToString(UnaryOpType opType)
        {
            switch (opType)
            {
                case UnaryOpType.Negate:
                    return "NEG";
                case UnaryOpType.Not:
                    return "NOT";
            }

            return "Unknown";
        }

        public String BinaryOpToString(BinOpType opType)
        {
            switch (opType)
            {
                case BinOpType.Add: return "ADD";
                case BinOpType.And: return "AND";
                case BinOpType.Div: return "DIV";
                case BinOpType.Eq: return "EQ";
                case BinOpType.Ge: return "GE";
                case BinOpType.Gt: return "GT";
                case BinOpType.Le: return "LE";
                case BinOpType.Lt: return "LT";
                case BinOpType.Mul: return "MUL";
                case BinOpType.Neq: return "NEQ";
                case BinOpType.Or: return "OR";
                case BinOpType.Sub: return "SUB";
            }

            return "Unknown";
        }


        public void GenerateVariable(StringWriter stream, Variable v)
        {
            stream.Write($"Name(\"{v.Name}\",{GenerateId()})");
        }

        public String GetEventName(PEvent evt)
        {
            if (evt.Name == "halt") return "HALT";
            if (evt.Name == "null") return "NULL";
            return evt.Name;
        }

        public void GenerateCases(StringWriter stream, IReadOnlyDictionary<PEvent, Function> cases)
        {
            int parenCount = 0;
            foreach (KeyValuePair<PEvent, Function> kv in cases)
            {
                stream.Write($"Cases(\"{GetEventName(kv.Key)}\"");
                stream.Write(",");
                GenerateFunction(stream, kv.Value);
                stream.Write(",");
                parenCount++;
            }

            stream.Write("NIL");
            for (int i = 0; i < parenCount; i++)
            {
                stream.Write($",{GenerateId()})");
            }
        }

        public void GenerateInterfaces(StringWriter stream, IEnumerable<Interface> interfaces)
        {
            foreach (Interface intface in interfaces)
            {
                GenerateInterface(stream, intface);
            }
        }

        public void GenerateInterface(StringWriter stream, Interface intface)
        {
            stream.Write($"InterfaceDecl(\"{intface.Name}\",");
            GenerateList(stream, "InterfaceType", intface.ReceivableEvents.Events,
                e => "\""+e.Name+"\"");
            stream.Write(",");
            GenerateTypeExpr(stream, intface.PayloadType);
            stream.WriteLine($",{GenerateId()}).");

        }

        public void GenerateMachines(StringWriter stream, IEnumerable<Machine> machines, Dictionary<String,Machine> machineMap)
        {
            foreach (Machine machine in machines)
            {
                GenerateMachine(stream, machine, machineMap);
            }

            foreach (Machine machine in machines)
            {
                GenerateMachineConnections(stream, machine, machineMap);
            }
        }

        protected String MachineKind(Machine machine)
        {
            return machine.IsSpec ? "SPEC" : "REAL";
        }

        public void GenerateMachine(StringWriter stream, Machine machine, Dictionary<String,Machine> machineMap)
        {
            stream.WriteLine($"  {machine.Name} is Component(\"{machine.Name}\").");
            machineMap[machine.Name] = machine;
            foreach (State state in machine.States)
            {
                GenerateState(stream, state);
            }

            foreach (PEvent evt in machine.Sends.Events)
            {
                stream.WriteLine($"  ComponentSendsEvent({machine.Name}, {evt.Name}).");
            }

            foreach (PEvent evt in machine.Receives.Events)
            {
                stream.WriteLine($"  ComponentReceivesEvent({machine.Name}, {evt.Name}).");
            }
        }

        public String GetTypeName(PLanguageType plt)
        {
            Console.WriteLine("Type name is "+plt);
                switch (plt)
                {
                case DataType t:
                    return t.CanonicalRepresentation;
                case ForeignType t:
                    return t.CanonicalRepresentation;
                case EnumType t:
                    return null;
                case MapType t:
                    return null;
                case NamedTupleType t:
                    return null;
                case PermissionType t:
                    return t.CanonicalRepresentation;
                case PrimitiveType t:
                    return null;
                case SequenceType t:
                    return null;
                case SetType t:
                    return null;
                case TupleType t:
                    return null;
                case TypeDefType t:
                    return t.TypeDefDecl.Name;
                default:
                    return null;
                }
        }

        public void GenerateMachineConnections(StringWriter stream, Machine machine,
            Dictionary<String, Machine> machineMap)
        {
            foreach (Variable var in machine.Fields)
            {
                var typeName = GetTypeName(var.Type);
                Console.WriteLine("Got typeName "+typeName+" for var "+var.Name);
                if (typeName == null) continue;
                if (machineMap.ContainsKey(typeName))
                {
                    stream.WriteLine($"  Connection(\"{var.Name}\", {machine.Name}, {typeName}).");
                }
            }
        }
        public void GenerateStates(StringWriter stream, IEnumerable<State> states)
        {
            foreach (State state in states)
            {
                GenerateState(stream, state);
            }
        }

        protected string temperatureToString(StateTemperature temp)
        {
            switch (temp)
            {
                case StateTemperature.Cold: return "COLD";
                case StateTemperature.Warm: return "WARM";
                case StateTemperature.Hot: return "HOT";
            }

            return "";
        }

        public void GenerateState(StringWriter stream, State state)
        {
            stream.WriteLine($"  Condition({state.OwningMachine.Name}, \"{state.Name}\").");
        }

        public void GenerateAnonFuncDecl(StringWriter stream, Function func)
        {
            stream.Write($"AnonFunDecl(");
            if (func.Owner != null)
            {
                stream.Write($"\"{func.Owner.Name}\",");
            }
            else
            {
                stream.Write("NIL,");
            }

            if (func.ParentFunction != null)
            {
                stream.Write($"\"{func.ParentFunction.Name}\",");
            }
            else
            {
                stream.Write("NIL,");
            }
            GenerateList(stream, "NmdTupType", func.LocalVariables,
                (v, vstream) => GenerateNamedTupTypeField(vstream, v));
            GenerateStmt(stream, func.Body);
            stream.Write($",{GenerateId()})");
        }
    }
}