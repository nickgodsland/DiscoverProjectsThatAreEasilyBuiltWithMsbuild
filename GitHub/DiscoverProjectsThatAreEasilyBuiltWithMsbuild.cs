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
    internal static class DiscoverProjectsThatAreEasilyBuiltWithMsbuild
    {
        private static void Main()
        {
            var buildFolder = GetBuildFolder();

            var searchClient = new SearchClient(new ApiConnection(new Connection(new ProductHeaderValue("test"))));
            var searchRepositoriesRequest = new SearchRepositoriesRequest
            {
                Language = Language.CSharp,
                Forks = Range.GreaterThan(400) // 15 gets 75 projects for F#
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
            var outCsv = "out.csv";
            File.AppendAllText(outCsv, "ProjectName, SolutionCount, EasyToBuildCount" + Environment.NewLine);

            Console.WriteLine();
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

                File.AppendAllText(outCsv,
                    $"{gitHubSourceFolder},{solutionFiles.Length},{successfullSolutionBuildCount}" + Environment.NewLine);

                Console.WriteLine($"{gitHubSourceFolder},{solutionFiles.Length},{successfullSolutionBuildCount}");
                try
                {
                    Directory.Delete(gitHubSourceFolder, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);g
                }
            }
        }

        private static void DownloadProjectsFromGitHub(IEnumerable<Repository> searchRepositoryResult,
            string buildFolder)
        {
            var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = 1};
            Parallel.ForEach(searchRepositoryResult, parallelOptions, r =>
            {
                var archivePath = Path.Combine(buildFolder, r.Name + ".zip");
                var destinationDirectoryName = Path.Combine(buildFolder, r.Name);
                if (!File.Exists(archivePath))
                {
                    var downloadUrl = r.HtmlUrl + "/archive/master.zip";
                    Console.WriteLine("Downloading from: {0}", downloadUrl);
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(downloadUrl, archivePath);
                    }
                }

                try
                {
                    Console.WriteLine("Extracting to: {0}", destinationDirectoryName);
                    ZipFile.ExtractToDirectory(archivePath, destinationDirectoryName);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Directory.Delete(destinationDirectoryName, true);
                }
            });
        }

        private static string GetBuildFolder()
        {
            const string folder = "c:\\s";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        private static int Build(string fullName)
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo(@"C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe")
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
