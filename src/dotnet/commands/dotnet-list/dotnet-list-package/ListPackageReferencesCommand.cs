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

            // The following specific flags require one of the options that support these flags.
            var specificFlags = new[] { "include-prerelease", "highest-patch", "highest-minor", "config", "source" };
            var optionsSupportingSpecificFlag = new[] { "outdated", "deprecated", "vulnerable" };

            foreach (var specificFlag in specificFlags)
            {
                if (_appliedCommand.HasOption(specificFlag))
                {
                    CheckForRequiredOption(_appliedCommand, specificFlag, optionsSupportingSpecificFlag);
                }
            }

            // The following flags cannot be combined in a single command.
            CheckForInvalidCommandOptionCombinations(_appliedCommand, "outdated", "deprecated");
            CheckForInvalidCommandOptionCombinations(_appliedCommand, "outdated", "vulnerable");
            CheckForInvalidCommandOptionCombinations(_appliedCommand, "deprecated", "vulnerable");

            return args.ToArray();
        }

        /// <summary>
        /// A check for a required combination of a specific flag with one of the supporting options.
        /// </summary>
        /// <param name="appliedCommand"></param>
        /// <param name="specificFlag">The specific flag that requires the usage of one of the supporting options.</param>
        /// <param name="supportingOptions">The command options supporting the specific flag.</param>
        private void CheckForRequiredOption(AppliedOption appliedCommand, string specificFlag, string[] supportingOptions)
        {
            var supportingOptionDetected = false;
            foreach (var supportingOption in supportingOptions)
            {
                if (appliedCommand.HasOption(supportingOption))
                {
                    supportingOptionDetected = true;
                    break;
                }
            }

            if (!supportingOptionDetected)
            {
                throw new GracefulException(
                    LocalizableStrings.MandatoryOptionMissing,
                    specificFlag,
                    string.Join(",", supportingOptions));
            }
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
