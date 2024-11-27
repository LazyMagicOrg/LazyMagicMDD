using LazyMagic;
using NJsonSchema.Validation;

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

        [Fact]
        public async Task TestDirectiveValidationAsync()
        {

            var logger = new TestLogger();
            var solutionRootFolderPath = @"C:\Users\Conda\source\repos\_Dev\LazyMagic\LazyMagicMDD";
            var projectWithTestFiles = "TestDirectiveValidation";
            var folderWithTestFiles = "DirectiveFiles";
            var testDir = Path.Combine(solutionRootFolderPath, projectWithTestFiles, folderWithTestFiles);
            var files = Directory.GetFiles(testDir);

            foreach (string directiveFilePath in Directory.GetFiles(testDir))
            {

                var lzSolution = new LzSolution(logger, solutionRootFolderPath);

                await Assert.ThrowsAsync<Exception>(async () =>
                {
                    await lzSolution.TestDirectiveValidation(directiveFilePath);
                    throw new Exception($"Sample error message: {directiveFilePath}");
                });
            }
        }
    }
}