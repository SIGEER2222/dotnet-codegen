﻿using HandlebarsDotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Dotnet.CodeGen.CustomHandlebars
{
    public static class HandlebarsConfigurationHelper
    {
        static public IEnumerable<IHelper> DefaultHelpers { get { foreach (var h in _Helpers) yield return h; } }

        static readonly IHelper[] _Helpers;

#pragma warning disable CA1810 // Initialize reference type static fields inline
#pragma warning disable S3963 // "static" fields should be initialized inline
        static HandlebarsConfigurationHelper()
        {
            var thisAssembly = typeof(HandlebarsConfigurationHelper).Assembly;
            _Helpers = GetHelpersFromAssembly(thisAssembly).ToArray();
        }

        public static IHelper[] GetHelpersFromFolder(string projectFolderPath, string artifactDirectory)
        {
            var csproj = Directory.GetFiles(projectFolderPath, "*.csproj").FirstOrDefault() ?? throw new InvalidDataException($"No .csproj file found in the directory {projectFolderPath}");
            var projName = Path.GetFileNameWithoutExtension(csproj);
            string outputFolder = Path.Combine(artifactDirectory, projName);

            // Build the project
            var dotnetCommand = $"build {csproj} -o \"{outputFolder}\" -c Release";
            var start = new ProcessStartInfo("dotnet", dotnetCommand)
            {
                CreateNoWindow = true
            };
            var process = new Process
            {
                StartInfo = start
            };
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Something bad happened when building the project '{csproj}'");
            }

            // Dynamic load of the assemblies, starting with the one with some helper
            var helpers = new List<IHelper>();
            var assemblyName = $"{projName}.dll";
            var assemblyFilePath = Directory.GetFiles(outputFolder, assemblyName).FirstOrDefault() ?? throw new InvalidDataException($"No .csproj file found in the directory {projectFolderPath}"); ;
            var loadedAssembly = Assembly.LoadFrom(assemblyFilePath);
            var helps = GetHelpersFromAssembly(loadedAssembly);
            helpers.AddRange(helps);

            return helpers.ToArray();
        }

        public static List<IHelper> GetHelpersFromAssembly(Assembly thisAssembly)
        {
            var helpers = new List<IHelper>();
            foreach (var type in GetHelperTypesFromAssembly(thisAssembly))
            {
                var ctor = type.GetConstructor(new Type[0]) ?? throw new InvalidProgramException("Helpers implementations should have a public, parameterless ctor");
                var instance = ctor.Invoke(new object[0]);
                helpers.Add((IHelper)instance);
            }
            return helpers;
        }

        public static IEnumerable<Type> GetHelperTypesFromAssembly(Assembly thisAssembly)
        {
            var iHelperType = typeof(IHelper);
            foreach (var type in thisAssembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface))
            {
                if (iHelperType.IsAssignableFrom(type))
                {
                    yield return type;
                }
            }
        }

#pragma warning restore S3963 // "static" fields should be initialized inline
#pragma warning restore CA1810 // Initialize reference type static fields inline

        private static void RegisterDefaultHelpers(HandlebarsConfiguration handlebarsConfiguration)
        {
            foreach (var h in _Helpers)
            {
                h.Setup(handlebarsConfiguration);
            }
        }

        public static IHandlebars GetHandlebars(string rootDirectory) => Handlebars.Create(GetHandlebarsConfiguration(rootDirectory));
        public static IHandlebars GetHandlebars(IEnumerable<string> directories) => Handlebars.Create(GetHandlebarsConfiguration(directories));

        public static HandlebarsConfiguration GetHandlebarsConfiguration(string rootDirectory)
            => GetHandleBarsConfiguration(new SameDirectoryPartialTemplateResolver(rootDirectory));
        public static HandlebarsConfiguration GetHandlebarsConfiguration(IEnumerable<string> directories)
            => GetHandleBarsConfiguration(new MultipleDirectoriesPartialTemplateResolver(directories));

        private static HandlebarsConfiguration GetHandleBarsConfiguration(IPartialTemplateResolver templateResolver)
        {
            var configuration = new HandlebarsConfiguration();
            RegisterDefaultHelpers(configuration);
            configuration.PartialTemplateResolver = templateResolver;
            return configuration;
        }

        private static bool TryRegisterPartialFile(string directory, IHandlebars env, string partialName)
        {
            var partialPath = Path.Combine(directory, $"_{partialName}.hbs");
            if (!File.Exists(partialPath))
            {
                //return false;
                throw new IOException($"Unable to find the partial template file {partialPath}");
            }
            env.RegisterTemplate(partialName, File.ReadAllText(partialPath));
            return true;
        }

        class MultipleDirectoriesPartialTemplateResolver : IPartialTemplateResolver
        {
            private readonly string[] _directories;

            public MultipleDirectoriesPartialTemplateResolver(IEnumerable<string> directories)
            {
                _directories = directories.ToArray();
            }

            public bool TryRegisterPartial(IHandlebars env, string partialName, string templatePath)
            {
                var result = true;
                foreach (var directory in _directories)
                {
                    result = result && TryRegisterPartialFile(directory, env, partialName);
                }
                return result;
            }
        }

        class SameDirectoryPartialTemplateResolver : IPartialTemplateResolver
        {
            private readonly string _rootDirectory;

            public SameDirectoryPartialTemplateResolver(string rootDirectory)
            {
                _rootDirectory = rootDirectory;
            }

            public bool TryRegisterPartial(IHandlebars env, string partialName, string templatePath)
            {
                return TryRegisterPartialFile(_rootDirectory, env, partialName);
            }
        }
    }
}
