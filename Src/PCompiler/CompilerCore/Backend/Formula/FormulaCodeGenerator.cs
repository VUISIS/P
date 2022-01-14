using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Antlr4.Runtime.Dfa;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.Formula
{
    public class FormulaCodeGenerator : ICodeGenerator
    {
        private CompilationContext context;

        public IEnumerable<CompiledFile> GenerateCode(ICompilationJob job, Scope globalScope)
        {
            this.context = new CompilationContext(job);

            CompiledFile file = GenerateSource(globalScope);
            return new List<CompiledFile> {file};
        }

        public CompiledFile GenerateSource(Scope globalScope)
        {
            CompiledFile source = new CompiledFile(context.FileName);
            source.Stream.WriteLine($"model {context.ProjectName} of P at \"P.4ml\" {{");
            GenerateEnums(source.Stream, globalScope.Enums);
            GenerateTypedefs(source.Stream, globalScope.Typedefs);
            GenerateEvents(source.Stream, globalScope.Events);
            GenerateEventSets(source.Stream, globalScope.EventSets);
            GenerateFunctions(source.Stream, globalScope.Functions);
            GenerateInterfaces(source.Stream, globalScope.Interfaces);
            GenerateMachines(source.Stream, globalScope.Machines);
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
                stream.Write(')');
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
            return context.GenerateId();
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

                GenerateList(stream, "StringList", nameList, x => x);
                stream.Write(", ");
                GenerateList(stream, "IntegerList", valueList, x => x.ToString());
                stream.WriteLine($",\"{GenerateId()}\").");
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

                stream.WriteLine($"\"{GenerateId()}\").");
            }
        }

        public void GenerateEvents(StringWriter stream, IEnumerable<PEvent> events)
        {
            foreach (var evt in events)
            {
                stream.Write($"EventDecl(\"{evt.Name}\", ");
                if (evt.Assume != -1)
                {
                    stream.Write($"AssumeMaxInstances({evt.Assume}), ");
                }
                else if (evt.Assert != -1)
                {
                    stream.Write($"AssertMaxInstances({evt.Assert}), ");
                }
                else
                {
                    stream.Write("NIL, ");
                }

                GenerateTypeExpr(stream, evt.PayloadType);
                stream.Write($", {GenerateId()}).");
            }
        }

        public void GenerateEventSets(StringWriter stream, IEnumerable<NamedEventSet> eventSets)
        {
            foreach (var eventSet in eventSets)
            {
                stream.Write($"EventDecl(\"{eventSet.Name}\", ");
                GenerateList(stream, "EventNameList", eventSet.Events,
                    evt => evt.Name);
                stream.Write(").");
            }
        }

        public void GenerateFunctions(StringWriter stream, IEnumerable<Function> functions)
        {
            foreach (var func in functions)
            {
                GenerateFunction(stream, func);
            }
        }

        public void GenerateFunction(StringWriter stream, Function func)
        {
            stream.Write($"FunDecl(\"{func.Name}\", ");
            if (func.Owner != null)
            {
                stream.Write($"\"{func.Owner.Name}\",");
            }
            else
            {
                stream.Write("NIL,");

            }

            GenerateList(stream, "NmdTupType", func.Signature.Parameters,
                (v, vstream) => GenerateNamedTupTypeField(vstream, v));
            stream.Write(",");
            GenerateTypeExpr(stream, func.Signature.ReturnType);
            stream.Write(",");
            GenerateList(stream, "NmdTupType", func.LocalVariables,
                (v, vstream) => GenerateNamedTupTypeField(vstream, v));
            stream.Write(",");
            if (func.Body == null)
            {
                stream.Write("NIL,");
            }
            else
            {
                GenerateStmt(stream, func.Body);
            }
        }

        public void GenerateNamedTupTypeField(StringWriter stream, Variable v)
        {
            stream.Write($"NmdTupTypeField({v.Name},");
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
                    stream.Write($"NewStmt(\"{s.Interface.Name},");
                    GenerateExprList(stream, s.Arguments);
                    stream.Write($",NIL,{GenerateId()})");
                    break;
                case RaiseStmt s:
                    stream.Write("Raise(");
                    GenerateExpr(stream, s.PEvent);
                    stream.Write(",");
                    GenerateExprList(stream, s.Payload);
                    stream.Write($",{GenerateId()})");
                    break;
                case SendStmt s:
                    stream.Write("Send(");
                    GenerateExpr(stream, s.MachineExpr);
                    stream.Write(",");
                    GenerateExpr(stream, s.Evt);
                    stream.Write(",");
                    GenerateExprList(stream, s.Arguments);
                    stream.Write($",{GenerateId()})");
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

                    stream.Write($",{GenerateId()})");
                    break;
                case FunCallStmt s:
                    stream.Write($"FunStmt({s.Function.Name},");
                    GenerateExprList(stream, s.ArgsList);
                    stream.Write($",NIL,{GenerateId()})");
                    break;

                case PopStmt:
                    stream.Write($"NulStmt(POP,{GenerateId()})");
                    break;

                case RemoveStmt s:
                    stream.Write("BinStmt(REMOVE,");
                    GenerateExpr(stream, s.Variable);
                    stream.Write(",NONE,");
                    GenerateExpr(stream, s.Value);
                    stream.Write($",{GenerateId()})");
                    break;

                case AssignStmt s:
                    stream.Write("BinStmt(ASSIGN,");
                    GenerateExpr(stream, s.Location);
                    stream.Write(",NONE,");
                    GenerateExpr(stream, s.Value);
                    stream.Write($",{GenerateId()})");
                    break;

                case MoveAssignStmt s:
                    stream.Write("BinStmt(ASSIGN,");
                    GenerateExpr(stream, s.ToLocation);

                    stream.Write(",Move,");
                    GenerateVariable(stream, s.FromVariable);
                    stream.Write($",{GenerateId()})");
                    break;

                case SwapAssignStmt s:
                    stream.Write("BinStmt(ASSIGN,");
                    GenerateExpr(stream, s.NewLocation);

                    stream.Write(",Swap,");
                    GenerateVariable(stream, s.OldLocation);
                    stream.Write($",{GenerateId()})");
                    break;

                case InsertStmt s:
                    stream.Write("InsertStmt(");
                    GenerateExpr(stream, s.Variable);
                    stream.Write(",");
                    GenerateExpr(stream, s.Index);
                    stream.Write(",");
                    GenerateExpr(stream, s.Value);
                    stream.Write($",{GenerateId()})");
                    break;

                case AddStmt s:
                    stream.Write("BinStmt(INSERT,");
                    GenerateExpr(stream, s.Variable);
                    stream.Write(",NONE,");
                    GenerateExpr(stream, s.Value);
                    stream.Write($",{GenerateId()})");
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

                    stream.Write(")");
                    break;

                case WhileStmt s:
                    stream.Write("While(");
                    GenerateExpr(stream, s.Condition);
                    stream.Write(",");
                    GenerateStmt(stream, s.Body);
                    stream.Write($"{GenerateId()})");
                    break;

                case IfStmt s:
                    stream.Write("Ite(");
                    GenerateExpr(stream, s.Condition);
                    stream.Write(",");
                    GenerateStmt(stream, s.ThenBranch);
                    stream.Write(",");
                    GenerateStmt(stream, s.ElseBranch);
                    stream.Write($",{GenerateId()})");
                    break;

                case CompoundStmt s:
                    if (s.Statements.Count == 1)
                    {
                        GenerateStmt(stream, s.Statements[0]);
                    }
                    else
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
                            stream.Write(")");
                        }
                    }

                    break;

                case ReceiveStmt s:
                    stream.Write("Receive(");
                    GenerateCases(stream, s.Cases);
                    stream.Write($",0,{GenerateId()})");
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

                    stream.Write($",{GenerateId()})");
                    break;

                case PrintStmt s:
                    stream.Write("Print(");
                    GenerateExpr(stream, s.Message);
                    stream.Write($",NIL, NIL,{GenerateId()})");
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

                    stream.Write($",{GenerateId()})");
                    break;

                case BreakStmt s:
                    stream.Write($"Break({GenerateId()})");
                    break;

                case ContinueStmt s:
                    stream.Write($"Continue({GenerateId()})");
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
                    stream.Write($"NulApp({e.Value}, {GenerateId()})");
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
                    stream.Write("BinApp(IDX");
                    GenerateExpr(stream, e.SeqExpr);
                    stream.Write(",");
                    GenerateExpr(stream, e.IndexExpr);
                    stream.Write($",{GenerateId()})");
                    break;

                case ContainsExpr e:
                    stream.Write("BinApp(IDX");
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
                        stream.Write($"NamedExprs(\"{names.Current},");
                        GenerateExpr(stream, exprs.Current);
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
            stream.Write($"Name(\"{v.Name}\")");
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
                stream.Write($"Cases(\"{GetEventName(kv.Key)}");
                stream.Write(",");
                GenerateFunction(stream, kv.Value);
                stream.Write(",");
                parenCount++;
            }

            stream.Write("NIL");
            for (int i = 0; i < parenCount; i++)
            {
                stream.Write(")");
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
                e => e.Name);
            stream.Write($",{GenerateId()})");

        }

        public void GenerateMachines(StringWriter stream, IEnumerable<Machine> machines)
        {
            foreach (Machine machine in machines)
            {
                GenerateMachine(stream, machine);
            }
        }

        protected String MachineKind(Machine machine)
        {
            return machine.IsSpec ? "SPEC" : "REAL";
        }

        public void GenerateMachine(StringWriter stream, Machine machine)
        {
            stream.Write($"MachineDecl(\"{machine.Name}\",{GenerateId()})");
            stream.Write($"MachineKind(\"{machine.Name}\", {MachineKind(machine)})");
            stream.Write($"MachineStart(\"{machine.Name}\",\"{machine.StartState.Name}\")");
            stream.Write($"MachineProtoDecl(\"{machine.Name}\",");
            GenerateTypeExpr(stream, machine.PayloadType);
            foreach (PEvent evt in machine.Receives.Events)
            {
                stream.Write($"MachineReceives(\"{machine.Name}\",\"{evt.Name}\")");
            }

            foreach (PEvent evt in machine.Sends.Events)
            {
                stream.Write($"MachineSends(\"{machine.Name}\",\"{evt.Name}\")");
            }

            foreach (PEvent evt in machine.Observes.Events)
            {
                stream.Write($"ObservesDecl(\"{machine.Name}\",\"{evt.Name}\")");
            }

            foreach (Variable v in machine.Fields)
            {
                stream.Write($"VarDecl(\"{v.Name}\", \"{machine.Name}\",");
                GenerateTypeExpr(stream, v.Type);
                stream.Write($",{GenerateId()})");
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
            stream.Write($"StateDecl(\"{state.QualifiedName}\", \"{state.OwningMachine.Name}\",");
            if (state.Entry.IsAnon)
            {
                GenerateAnonFuncDecl(stream, state.Entry);
            }
            else
            {
                stream.Write($"\"{state.Entry.Name}\",");
            }

            if (state.Exit.IsAnon)
            {
                GenerateAnonFuncDecl(stream, state.Exit);
            }
            else
            {
                stream.Write($"\"{state.Exit.Name}\",");
            }

            stream.Write($"\"{temperatureToString(state.Temperature)}\",");
            stream.Write($",{GenerateId()})");
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