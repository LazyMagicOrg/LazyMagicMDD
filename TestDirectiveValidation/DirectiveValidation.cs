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

        [Fact]
        public async Task MissingReference_Service_Api_Async()
        {
            string testFilePath = Path.Combine(
                testPath,
                "testfileA.yaml"
                );

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                Console.WriteLine("run");
                await new LzSolution(testLogger, basePath).TestDirectiveValidation(testFilePath);
            });

            Assert.Contains("Service", exception.Message);     //what
            Assert.Contains("StoreApi", exception.Message);    //what it's missing
            Assert.Contains("ConsumerApi", exception.Message); //what it's missing
            xUnitLogger.WriteLine(exception.Message);
        }
        [Fact]
        public async Task MissingReference_Webapp_Api_Async()
        {
            string testFilePath = Path.Combine(
                testPath,
                "testfileB.yaml"
                );

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                Console.WriteLine("run");
                await new LzSolution(testLogger, basePath).TestDirectiveValidation(testFilePath);
            });

            Assert.Contains("AdminApp", exception.Message);    //what
            Assert.Contains("StoreApi", exception.Message);    //what's missing
            Assert.Contains("AdminApp", exception.Message);    //what's missing
            xUnitLogger.WriteLine(exception.Message);
        }
        [Fact]
        public async Task MissingReference_Api_Container_Async()
        {
            string testFilePath = Path.Combine(
                testPath,
                "testfileC.yaml"
                );

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                Console.WriteLine("run");
                await new LzSolution(testLogger, basePath).TestDirectiveValidation(testFilePath);
            });

            Assert.Contains("StoreApi",    exception.Message);  //what
            Assert.Contains("StoreLambda", exception.Message);  //what it's missing
            xUnitLogger.WriteLine(exception.Message);
        }
        [Fact]
        public async Task MissingReference_Container_Module_Async()
        {
            string testFilePath = Path.Combine(
                testPath,
                "testfileD.yaml"
                );

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                Console.WriteLine("run");
                await new LzSolution(testLogger, basePath).TestDirectiveValidation(testFilePath);
            });

            Assert.Contains("StoreLambda", exception.Message); //what
            Assert.Contains("StoreModule", exception.Message); //whats missing
            xUnitLogger.WriteLine(exception.Message);
        }
    }
}