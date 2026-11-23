using System;
using System.IO;

namespace IronHorizon.Tests
{
    /// <summary>Locates the Unity project root from inside Unity or from the dotnet test runner.</summary>
    internal static class ProjectPaths
    {
        private const string Marker = "Assets/_Project/Scripts";

        public static string ProjectRoot
        {
            get
            {
                string found = SearchUp(Directory.GetCurrentDirectory()) ?? SearchUp(AppContext.BaseDirectory);
                if (found == null)
                {
                    throw new DirectoryNotFoundException("Could not find a folder containing " + Marker);
                }

                return found;
            }
        }

        public static string Scripts => Path.Combine(ProjectRoot, "Assets", "_Project", "Scripts");

        private static string SearchUp(string start)
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, Marker)))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return null;
        }
    }
}
