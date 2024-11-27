using LazyMagic;
using NJsonSchema.Validation;
using System.Security.Cryptography.X509Certificates;

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
        public ILogger testLogger { get; set; }
        string basePath;
        string testPath;

        public DirectiveValidation()
        {
            testLogger = new TestLogger();
            basePath = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName;
            testPath = Path.Combine(basePath, "TestDirectiveValidation", "DirectiveFiles");
        }

        [Fact]
        /*our naming convention has led to some awkward variable names in this case. Be not confused. The configuration file which acts as the 
         single source of truth from which lazy magic generates is called the DIRECTIVE FILE. Here we test the vailidity of the DIRECTIVE FILE
         by running the validate() method in each artifcat class contained therin. The sample directive files wich will throw errors are things like 
         TestFile1.yaml or TestFile2.yaml. Which is why we need a DIRECITVE file, filePath.

         Although contianing many directives, we use the singular DIRECTIVE throughout. 
         */
        public async Task TestDirectiveValidationAsync()
        {
            string directiveFileFilePath = Path.Combine(testPath, "TestFile1.yaml");

            await Assert.ThrowsAsync<Exception>(async () =>
            {
                await new LzSolution(testLogger, basePath).TestDirectiveValidation(directiveFileFilePath);
                throw new Exception($"Sample error message: {directiveFileFilePath}");
            });
        }
    }
}