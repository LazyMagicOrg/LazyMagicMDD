using LazyMagic;
using NJsonSchema.Validation;
using System.Security.Cryptography.X509Certificates;
using Xunit.Abstractions;

namespace TestDirectiveValidation
{
    public class TestLogger : ILogger
    {
        public void Info(string message) { }
        public Task InfoAsync(string message) => Task.CompletedTask;
        public void Error(Exception ex, string message) { }
        public Task ErrorAsync(Exception ex, string message) => Task.CompletedTask;
    }

    public class DirectiveValidation
    {
        private readonly ITestOutputHelper xUnitLogger;
        public ILogger testLogger { get; set; }
        string basePath;
        string testPath;

        public DirectiveValidation(ITestOutputHelper xUnitLogger)
        {
            this.xUnitLogger = xUnitLogger;
            testLogger = new TestLogger();
            basePath = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName;
            testPath = Path.Combine(basePath, "TestDirectiveValidation", "DirectiveFiles");
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /* The DIRECTIVE FILE is a configuration file which acts as the single source of truth
         * Although contianing many directives, we use the singular DIRECTIVE throughout. 
         * 
         * each artifcat knows how to validate() its reference in a  DIRECTIVE FILE
        
         * we use sample directive files like TestFile1.yaml or TestFile2.yaml to throw instructive erros */
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        //Container_Missing_1_Ref_StoreLambda_StoreModule.yaml
        [Fact]
        public async Task MissingReference_Container_Module_Singular_Async()
        {
            //test only checks for first child
            string testFilePath = Path.Combine(
                testPath, 
                "testfile1.yaml"
                );
            
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                Console.WriteLine("run");
                await new LzSolution(testLogger, basePath).TestDirectiveValidation(testFilePath);
            });
            
            Assert.Contains("StoreLambda", exception.Message);
            Assert.Contains("StoreModule", exception.Message);
            xUnitLogger.WriteLine(exception.Message);
        }
        [Fact]
        public async Task MissingReference_Container_Module_Multiple_Async()
        {
            //test only checks for first child
            string testFilePath = Path.Combine(
                testPath,
                "testfile2.yaml"
                );

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                Console.WriteLine("run");
                await new LzSolution(testLogger, basePath).TestDirectiveValidation(testFilePath);
            });

            Assert.Contains("StoreLambda", exception.Message);
            Assert.Contains("StoreModule", exception.Message);
            Assert.Contains("LzMessagingModule", exception.Message);
            xUnitLogger.WriteLine(exception.Message);
        }
    }
}