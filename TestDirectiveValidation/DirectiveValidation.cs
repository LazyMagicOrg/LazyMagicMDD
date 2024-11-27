using LazyMagic;

namespace TestDirectiveValidation
{
    public class DirectiveValidation
    {
        [Fact]
        public async Task TestDirectiveValidationAsync()
        {

            var solutionRootFolderPath = @"C:\Users\Conda\source\repos\_Dev\LazyMagic\LazyMagicMDD";
            var projectWithTestFiles = "TestDirectiveValidation";
            var folderWithTestFiles = "DirectiveFiles";
            var testDir = File.ReadAllText(Path.Combine(solutionRootFolderPath, projectWithTestFiles, folderWithTestFiles));
           
            foreach (string directiveFilePath in Directory.GetFiles(testDir))
            {
                var lzSolution = new LzSolution(null, solutionRootFolderPath);
                
                await Assert.ThrowsAsync<Exception>(async () =>
                {
                    await lzSolution.TestDirectiveValidation(directiveFilePath);
                });
            }
        }
    }
}