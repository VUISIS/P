namespace Plang.Compiler.Backend.Formula
{
    public class CompilationContext : CompilationContextBase
    {
        public CompilationContext(ICompilationJob job)
            : base(job)
        {
            FileName = $"{ProjectName}.4ml";
        }

        public string FileName { get; }

        private int nextIdNumber = 0;

        public string GenerateId()
        {
            return FileName + ":" + nextIdNumber++;
        }
    }
}