using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuKeeper.Abstractions.Inspections.Files;
using NuKeeper.Abstractions.Logging;

namespace NuKeeper.Inspection.Files
{
    #pragma warning disable CA1031
    public class Folder : IFolder
    {
        private readonly INuKeeperLogger _logger;
        private readonly DirectoryInfo _root;

        public Folder(INuKeeperLogger logger, DirectoryInfo root)
        {
            _logger = logger;
            _root = root;
        }

        public string FullPath => _root.FullName;

        public IReadOnlyCollection<FileInfo> Find(string pattern)
        {
            _logger.Detailed($"Starting to search for files with pattern: {pattern}");
            var list = new List<FileInfo>();
            using var enumerator = _root.EnumerateFiles("*", SearchOption.AllDirectories).GetEnumerator();

            while (true)
            {
                try
                {
                    if (!enumerator.MoveNext())
                        break;

                    var file = enumerator.Current;
                    if (file is not null && Regex.IsMatch(file.Name, pattern))
                    {
                        _logger.Detailed($"Added file: {file.Name}");
                        list.Add(enumerator.Current);
                    }
                    else
                    {
                        _logger.Detailed($"Skip file: {file?.Name}");
                    }
                }
                catch (Exception e)
                {
                  _logger.Detailed($"Inner loop: {e.Message}{Environment.NewLine}" +
                                   $"{e.Source}{Environment.NewLine}" +
                                   $"{e.StackTrace}{Environment.NewLine}");
                }
            }

            _logger.Detailed($"Returning list of {list.Count} files");
            return list;
        }

        public void TryDelete()
        {
            _logger.Detailed($"Attempting delete of folder {_root.FullName}");

            try
            {
                DeleteDirectoryInternal(_root.FullName);
                _logger.Detailed($"Deleted folder {_root.FullName}");
            }
            catch (IOException ex)
            {
                _logger.Detailed($"Folder delete failed: {ex.GetType().Name} {ex.Message}");
            }
        }

        /// <summary>
        /// https://stackoverflow.com/questions/1157246/unauthorizedaccessexception-trying-to-delete-a-file-in-a-folder-where-i-can-dele
        /// </summary>
        /// <param name="targetDir"></param>
        private void DeleteDirectoryInternal(string targetDir)
        {
            // remove any "read-only" flag that would prevent the delete
            File.SetAttributes(targetDir, FileAttributes.Normal);

            var files = Directory.GetFiles(targetDir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            var subDirs = Directory.GetDirectories(targetDir);
            foreach (string dir in subDirs)
            {
                DeleteDirectoryInternal(dir);
            }

            Directory.Delete(targetDir, false);
        }
    }
}
