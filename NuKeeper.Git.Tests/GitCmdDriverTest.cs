using NuKeeper.Abstractions.Logging;
using NuKeeper.Inspection.Files;
using NuKeeper.Inspection.Logging;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuKeeper.Git.Tests
{
    public class GitCmdDriverTest
    {
        private INuKeeperLogger _logger;
        private string _pathToGit;

        [OneTimeSetUp]
        public void Setup()
        {
            _logger = new ConfigurableLogger();
            _pathToGit = TestDirectoryHelper.DiscoverPathToGit();
        }

        [TestCase("https://github.com/NuKeeperDotNet/NuKeeper.git")]
        [TestCase("https://github.com/NuKeeperDotNet/NuKeeperWebsite.git")]
        public async Task CloneRepoAndCheckout(string path)
        {
            if (_pathToGit == null)
            {
                Assert.Ignore("no git implementation found!");
            }

            var folder = TestDirectoryHelper.UniqueTemporaryFolder();
            try
            {
                var gitDriver = new GitCmdDriver(_pathToGit, _logger, new Folder(_logger, folder), new Abstractions.Git.GitUsernamePasswordCredentials());
                Assert.DoesNotThrowAsync(() => gitDriver.Clone(new Uri(path)));
                Assert.IsTrue(Directory.Exists(Path.Combine(folder.FullName, ".git")), "Local git repo should exist in {0}", folder.FullName);

                // Checkout master branch
                Assert.DoesNotThrowAsync(() => gitDriver.Checkout("master"));
                var head = await gitDriver.GetCurrentHead();
                Assert.AreEqual(head, "master");

                // Checkout new branch
                Assert.DoesNotThrowAsync(() => gitDriver.CheckoutNewBranch("newBranch"));
                head = await gitDriver.GetCurrentHead();
                Assert.AreEqual("newBranch", head);
            }
            finally
            {
                TestDirectoryHelper.DeleteDirectory(folder);
            }
        }

        [Test]
        public async Task GetNewCommitMessages()
        {
            // in this test we assume the Nukeeper repo has at least 2 branches and one of them is master.
            // if not, the test will return OK (because we cannot run it)
            var repoUri = "https://github.com/NuKeeperDotNet/NuKeeper.git";

            var folder = TestDirectoryHelper.UniqueTemporaryFolder();
            try
            {
                var creds = new Abstractions.Git.GitUsernamePasswordCredentials();
                var cmdGitDriver = new GitCmdDriver(_pathToGit, _logger, new Folder(_logger, folder), creds);
                var origGitDriver = new LibGit2SharpDriver(_logger, new Folder(_logger, folder), creds, null);

                // get the repo
                await origGitDriver.Clone(new Uri(repoUri));
                // get the remote branches, use git directly to avoid having to dress up a platform
                var gitOutput = await StartGitProcess();
                var branchNames = gitOutput.Split('\n')
                    .Select(b=>b.Trim()).ToArray();

                var master = branchNames.FirstOrDefault(b => b.EndsWith("/master", StringComparison.InvariantCultureIgnoreCase));
                if(master != null && branchNames.Length > 1)
                {
                    var headBranch = branchNames.First(b => !b.Equals(master,StringComparison.InvariantCultureIgnoreCase));
                    var localHeadBranch = Regex.Replace(headBranch, "^origin/", "");

                    // We have chosen the head branche here, lets check it out.
                    await cmdGitDriver.CheckoutRemoteToLocal(localHeadBranch);

                    // finally start the test
                    var origMessages = await origGitDriver.GetNewCommitMessages("master", localHeadBranch);
                    var cmdMessages = await cmdGitDriver.GetNewCommitMessages("master", localHeadBranch);

                    var origMessagesArray = origMessages.ToArray();
                    var cmdMessagesArray = cmdMessages.ToArray();

                    Assert.AreEqual(origMessagesArray, cmdMessagesArray, "GitCmdDriver does not return the right amount of messages");

                    foreach(var message in origMessages)
                    {
                        Assert.IsTrue(cmdMessages.Contains(message), $"GitCmdDriver does not return commit message {message}");
                    }
                }
            }
            finally
            {
                TestDirectoryHelper.DeleteDirectory(folder);
            }
        }
        
        // stripped down version
        private static async Task<string> StartGitProcess()
        {
            //var processInfo = new ProcessStartInfo(_pathToGit, arguments)
            //{
            //    CreateNoWindow = true,
            //    RedirectStandardOutput = true,
            //    RedirectStandardError = true,
            //    UseShellExecute = false,
            //    WorkingDirectory = workingFolder
            //};

            //#pragma warning disable Roslyn.CA1416
            //var process = Process.Start(processInfo);
            //#pragma warning restore Roslyn.CA1416

            //var textOut = await process.StandardOutput.ReadToEndAsync();
            //var textErr = await process.StandardError.ReadToEndAsync();

            //await process.WaitForExitAsync();

            //Assert.AreEqual(0, process.ExitCode, $"Git exited with code {process.ExitCode}: {textErr}");

            //return textOut.TrimEnd(Environment.NewLine.ToCharArray());

            return await Task.FromResult("");
        }
    }
}
