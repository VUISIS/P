using System;
using System.Collections.Generic;
using System.IO;
using Plang.Compiler.TypeChecker;

using Plang.Compiler.TypeChecker.AST.Declarations;
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
            source.Stream.WriteLine("}");
            return source;
        }

        public void WriteList<T>(StringWriter stream, string ItemName, IEnumerable<T> objs,
            Func<T, String> valueExtractor)
        {
            var parenCount = 0;
            foreach (var o in objs)
            {
                stream.Write($"{ItemName}({valueExtractor(o)},");
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
                WriteList(stream, "StringList", nameList, x => x);
                stream.Write(", ");
                WriteList(stream, "IntegerList", valueList, x => x.ToString());
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
                    GenerateType(stream, typeDef.Type);
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
                GenerateType(stream, evt.PayloadType);
                stream.Write($", {GenerateId()}).");
            }
        }

        public void GenerateEventSets(StringWriter stream, IEnumerable<NamedEventSet> eventSets)
        {
            foreach (var eventSet in eventSets)
            {
                stream.Write($"EventDecl(\"{eventSet.Name}\", ");
                WriteList(stream, "EventNameList", eventSet.Events,
                    evt => evt.Name);
                stream.Write(").");
            }
        }

        public void GenerateFunctions(StringWriter stream, IEnumerable<Function> functions)
        {
            foreach (var func in functions)
            {
                
//                stream.Write();
            }
        }
        public void GenerateType(StringWriter stream, PLanguageType plt)
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
                    GenerateType(stream, t.KeyType);       
                    stream.Write(",");
                    GenerateType(stream, t.ValueType); 
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
                        GenerateType(stream, fieldType);
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
                    GenerateType(stream, t.ElementType);
                    stream.Write(")");
                    break;
                case SetType t:
                    stream.Write($"SeqType(");
                    GenerateType(stream, t.ElementType);
                    stream.Write(")");
                    break;
                    break;
                case TupleType t:
                    stream.Write("Tuple(");
                    foreach (var tt in t.Types)
                    {
                        GenerateType(stream, tt);
                        stream.Write(",");
                    }
                    stream.Write("NIL)");
                    break;
                case TypeDefType t:
                    stream.Write($"NameType(\"{t.TypeDefDecl.Name}\")");
                    break;
            }
        }
    }
}