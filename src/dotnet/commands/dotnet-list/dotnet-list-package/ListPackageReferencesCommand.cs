// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.List.PackageReferences
{
    internal class ListPackageReferencesCommand : CommandBase
    {
        //The file or directory passed down by the command
        private readonly string _fileOrDirectory;
        private AppliedOption _appliedCommand;

        public ListPackageReferencesCommand(
            AppliedOption appliedCommand,
            ParseResult parseResult) : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }

            _fileOrDirectory = PathUtility.GetAbsolutePath(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()),
                                                           appliedCommand.Arguments.Single());

            _appliedCommand = appliedCommand["package"];
        }

        public override int Execute()
        {
            return NuGetCommand.Run(TransformArgs());
        }

        private string[] TransformArgs()
        {
            var args = new List<string>
            {
                "package",
                "list",
            };

            args.Add(GetProjectOrSolution());

            args.AddRange(_appliedCommand.OptionValuesToBeForwarded());

            if (_appliedCommand.HasOption("include-prerelease"))
            {
                CheckForOutdatedOrDeprecatedOrVulnerable("--include-prerelease");
            }

            if (_appliedCommand.HasOption("highest-patch"))
            {
                CheckForOutdatedOrDeprecatedOrVulnerable("--highest-patch");
            }

            if (_appliedCommand.HasOption("highest-minor"))
            {
                CheckForOutdatedOrDeprecatedOrVulnerable("--highest-minor");
            }

            if (_appliedCommand.HasOption("config"))
            {
                CheckForOutdatedOrDeprecatedOrVulnerable("--config");
            }

            if (_appliedCommand.HasOption("source"))
            {
                CheckForOutdatedOrDeprecatedOrVulnerable("--source");
            }

            CheckForInvalidCommandOptionCombinations("outdated", "deprecated");
            CheckForInvalidCommandOptionCombinations("outdated", "vulnerable");
            CheckForInvalidCommandOptionCombinations("deprecated", "vulnerable");

            return args.ToArray();
        }

        /// <summary>
        /// A check for invalid combinations of specific options.
        /// If the combination is invalid, an error is thrown.
        /// </summary>
        private void CheckForInvalidCommandOptionCombinations(AppliedOption appliedCommand, string option1, string option2)
        {
            if (appliedCommand.HasOption(option1) && appliedCommand.HasOption(option2))
            {
                throw new GracefulException(LocalizableStrings.OptionsCannotBeCombined, option1, option2);
            }
        }

        /// <summary>
        /// A check for the outdated, deprecated and vulnerable specific options. 
        /// If --outdated, --deprecated, or --vulnerable are not present,
        /// these options must not be used and an error is thrown.
        /// </summary>
        /// <param name="option"></param>
        private void CheckForOutdatedOrDeprecatedOrVulnerable(string option)
        {
            if (!_appliedCommand.HasOption("deprecated") && !_appliedCommand.HasOption("outdated") && !_appliedCommand.HasOption("vulnerable"))
            {
                throw new GracefulException(LocalizableStrings.OutdatedOrDeprecatedOrVulnerableOptionOnly, option);
            }
        }

        /// <summary>
        /// Gets a solution file or a project file from a given directory.
        /// If the given path is a file, it just returns it after checking
        /// it exists.
        /// </summary>
        /// <returns>Path to send to the command</returns>
        private string GetProjectOrSolution()
        {
            string resultPath = _fileOrDirectory;

            if (Directory.Exists(resultPath))
            {
                var possibleSolutionPath = Directory.GetFiles(resultPath, "*.sln", SearchOption.TopDirectoryOnly);

                //If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
                if (possibleSolutionPath.Count() > 1)
                {
                    throw new GracefulException(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, resultPath);
                }
                //If a single solution is found, use it.
                else if (possibleSolutionPath.Count() == 1)
                {
                    return possibleSolutionPath[0];
                }
                //If no solutions are found, look for a project file
                else
                {
                    var possibleProjectPath = Directory.GetFiles(resultPath, "*.*proj", SearchOption.TopDirectoryOnly)
                                              .Where(path => !path.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
                                              .ToList();

                    //No projects found throws an error that no sln nor projs were found
                    if (possibleProjectPath.Count() == 0)
                    {
                        throw new GracefulException(LocalizableStrings.NoProjectsOrSolutions, resultPath);
                    }
                    //A single project found, use it
                    else if (possibleProjectPath.Count() == 1)
                    {
                        return possibleProjectPath[0];
                    }
                    //More than one project found. Not sure which one to choose
                    else
                    {
                        throw new GracefulException(CommonLocalizableStrings.MoreThanOneProjectInDirectory, resultPath);
                    }
                }
            }

            if (!File.Exists(resultPath))
            {
                throw new GracefulException(LocalizableStrings.FileNotFound, resultPath);
            }

            return resultPath;
        }
    }
}
