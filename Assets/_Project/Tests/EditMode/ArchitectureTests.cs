using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace IronHorizon.Tests
{
    /// <summary>
    /// Enforces the layer boundaries from GDD section 12 and the determinism rules from section 0 (rule 6)
    /// by reading the asmdef files and the simulation source code.
    /// </summary>
    public class ArchitectureTests
    {
        private static readonly string[] SimulationFolders =
        {
            "Core/Sim", "Combat", "Movement", "Economy", "Vision", "AI"
        };

        private static readonly string[] PresentationAssemblies =
        {
            "IronHorizon.Presentation", "IronHorizon.UI", "IronHorizon.Tools"
        };

        private static readonly string[] ForbiddenSimulationAssemblies =
        {
            "Unity.AI.Navigation", "UnityEngine.PhysicsModule", "UnityEngine.AIModule"
        };

        // Tokens that break determinism inside the simulation (GDD section 0, rule 6).
        private static readonly string[] ForbiddenSimulationTokens =
        {
            "UnityEngine.Random", "Random.Range", "Random.value", "System.Random",
            "Time.deltaTime", "Time.time", "Time.fixedDeltaTime",
            "Physics.", "Rigidbody", "NavMesh", "CharacterController",
            "DateTime.Now", "Stopwatch"
        };

        [Test]
        public void CoreSim_ReferencesOnlyMathCollectionsAndBurst()
        {
            var allowed = new HashSet<string> { "Unity.Mathematics", "Unity.Collections", "Unity.Burst" };
            var refs = ReadReferences("Core/Sim");
            var extra = refs.Where(r => !allowed.Contains(r)).ToList();
            Assert.IsEmpty(extra, "Core/Sim must only reference Unity.Mathematics, Unity.Collections and Unity.Burst.");
        }

        [Test]
        public void SimulationAssemblies_DoNotReferencePresentationUiToolsOrNavigation()
        {
            foreach (string folder in SimulationFolders)
            {
                var refs = ReadReferences(folder);
                foreach (string forbidden in PresentationAssemblies.Concat(ForbiddenSimulationAssemblies))
                {
                    Assert.IsFalse(refs.Contains(forbidden), folder + " must not reference " + forbidden);
                }
            }
        }

        [Test]
        public void SimulationSource_ContainsNoNonDeterministicApis()
        {
            foreach (string folder in SimulationFolders)
            {
                foreach (string file in SourceFiles(folder))
                {
                    string code = StripComments(File.ReadAllText(file));
                    foreach (string token in ForbiddenSimulationTokens)
                    {
                        Assert.IsFalse(code.Contains(token), Relative(file) + " uses forbidden API '" + token + "'.");
                    }
                }
            }
        }

        [Test]
        public void CoreSim_UsesIntegerMathOnly()
        {
            // GDD 12.1: float math in the simulation must live in Burst jobs; Core/Sim is integer-only.
            var floatWord = new Regex(@"\b(float|double|decimal|Single|Double)\b");
            foreach (string file in SourceFiles("Core/Sim"))
            {
                string code = StripComments(File.ReadAllText(file));
                Assert.IsFalse(floatWord.IsMatch(code), Relative(file) + " uses floating point types.");
            }
        }

        private static List<string> ReadReferences(string folder)
        {
            string dir = Path.Combine(ProjectPaths.Scripts, folder);
            string[] asmdefs = Directory.GetFiles(dir, "*.asmdef");
            Assert.AreEqual(1, asmdefs.Length, "Expected exactly one asmdef in " + folder);

            string json = File.ReadAllText(asmdefs[0]);
            Match block = Regex.Match(json, "\"references\"\\s*:\\s*\\[(?<body>[^\\]]*)\\]");
            Assert.IsTrue(block.Success, "No references array in " + asmdefs[0]);

            return Regex.Matches(block.Groups["body"].Value, "\"(?<name>[^\"]+)\"")
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value)
                .ToList();
        }

        private static IEnumerable<string> SourceFiles(string folder)
        {
            return Directory.GetFiles(Path.Combine(ProjectPaths.Scripts, folder), "*.cs", SearchOption.AllDirectories);
        }

        private static string StripComments(string code)
        {
            code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(code, @"//.*", string.Empty);
        }

        private static string Relative(string file)
        {
            return file.Substring(ProjectPaths.ProjectRoot.Length + 1);
        }
    }
}
