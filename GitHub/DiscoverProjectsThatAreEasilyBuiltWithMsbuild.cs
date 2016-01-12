using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Octokit;

namespace GitHub
{
    internal static class FindProjectsThatAreEasilyBuilt
    {
        private static void Main()
        {
            var buildFolder = GetBuildFolder();

            var searchClient = new SearchClient(new ApiConnection(new Connection(new ProductHeaderValue("test"))));
            var searchRepositoriesRequest = new SearchRepositoriesRequest
            {
                Language = Language.CSharp,
                Forks = Range.GreaterThan(400)
            };

            var searchRepositoryResult =
                searchClient.SearchRepo(searchRepositoriesRequest)
                    .Result.Items.ToList();

            DownloadProjectsFromGitHub(searchRepositoryResult, buildFolder);

            AttemptToBuildProjects(buildFolder);

            Console.WriteLine("Finished. Press any key.");
            Console.ReadKey();
        }

        private static void AttemptToBuildProjects(string buildFolder)
        {
            Console.WriteLine("ProjectName, SolutionCount, EasyToBuildCount");
            foreach (var gitHubSourceFolder in Directory.GetDirectories(buildFolder))
            {
                var solutionFiles = new DirectoryInfo(gitHubSourceFolder).GetFiles("*.sln", SearchOption.AllDirectories);

                var successfullSolutionBuildCount = 0;
                foreach (var sln in solutionFiles)
                {
                    var exitCode = Build(sln.FullName);
                    if (exitCode == 0)
                        successfullSolutionBuildCount++;
                }

                Console.WriteLine($"{gitHubSourceFolder},{solutionFiles.Length},{successfullSolutionBuildCount}");
            }
        }

        private static void DownloadProjectsFromGitHub(IEnumerable<Repository> searchRepositoryResult, string buildFolder)
        {
            var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = 4};
            Parallel.ForEach(searchRepositoryResult, parallelOptions, r =>
            {
                var archivePath = Path.Combine(buildFolder, r.Name + ".zip");
                var destinationDirectoryName = Path.Combine(buildFolder, r.Name);

                var downloadUrl = r.HtmlUrl + "/archive/master.zip";

                using (var client = new WebClient())
                {
                    client.DownloadFile(downloadUrl, archivePath);
                }
                ZipFile.ExtractToDirectory(archivePath, destinationDirectoryName);
            });
        }

        private static string GetBuildFolder()
        {
            const string folder = "c:\\s";
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static int Build(string fullName)
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe")
                {
                    Arguments = fullName
                }
            };

            p.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            p.EnableRaisingEvents = true;

            p.Start();
            p.WaitForExit();

            return p.ExitCode;
        }
    }
}
