// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

#if DNX451 || DNXCORE50
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.DependencyInjection;
#endif

namespace Microsoft.Data.Entity.Design
{
    public class DbContextOperations
    {
        private readonly ILoggerProvider _loggerProvider;
        private readonly string _assemblyName;
        private readonly string _startupAssemblyName;
        private readonly string _environment;
        private readonly IServiceProvider _dnxServices;
        private readonly LazyRef<ILogger> _logger;
        private Assembly _assembly;

        public DbContextOperations(
            [NotNull] ILoggerProvider loggerProvider,
            [NotNull] string assemblyName,
            [NotNull] string startupAssemblyName,
            [CanBeNull] string environment,
            [CanBeNull] IServiceProvider dnxServices = null)
        {
            Check.NotNull(loggerProvider, nameof(loggerProvider));
            Check.NotEmpty(assemblyName, nameof(assemblyName));
            Check.NotEmpty(startupAssemblyName, nameof(startupAssemblyName));

            _loggerProvider = loggerProvider;
            _assemblyName = assemblyName;
            _startupAssemblyName = startupAssemblyName;
            _environment = environment;
            _dnxServices = dnxServices;
            _logger = new LazyRef<ILogger>(() => _loggerProvider.CreateCommandsLogger());
        }

        public virtual DbContext CreateContext([CanBeNull] string contextType)
        {
            var type = GetContextType(contextType);

            // TODO: Allow other construction patterns (See #639)
            DbContext context = null;

#if DNX451 || DNXCORE50
            context = TryCreateContextFromStartup(type);
#endif

            if (context == null)
            {
                context = (DbContext)Activator.CreateInstance(type);
            }

            var loggerFactory = context.GetService<ILoggerFactory>();
            loggerFactory.AddProvider(_loggerProvider);

            return context;
        }

#if DNX451 || DNXCORE50
        private DbContext TryCreateContextFromStartup(Type type)
        {
            var hostBuilder = new WebHostBuilder(_dnxServices)
                .UseEnvironment(
                    !string.IsNullOrEmpty(_environment)
                        ? _environment
                        : EnvironmentName.Development);
            if (_startupAssemblyName != null)
            {
                hostBuilder.UseStartup(_startupAssemblyName);
            }

            var appServices = hostBuilder.Build().ApplicationServices;

            return (DbContext)appServices.GetService(type);
        }
#endif

        public virtual IEnumerable<Type> GetContextTypes()
            => GetAssembly().GetTypes().Where(
                t => !t.GetTypeInfo().IsAbstract
                    && !t.GetTypeInfo().IsGenericType
                    && typeof(DbContext).IsAssignableFrom(t))
            .Concat(
                GetAssembly().GetConstructibleTypes()
                   .Where(t => typeof(Migration).IsAssignableFrom(t.AsType()))
                   .Select(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType)
                   .Where(t => t != null))
            .Distinct();

        public virtual Type GetContextType([CanBeNull] string name)
        {
            var contextType = FindContextType(name);
            _logger.Value.LogVerbose(CommandsStrings.LogUseContext(contextType.Name));

            return contextType;
        }

        private Assembly GetAssembly()
        {
            if (_assembly == null)
            {
                try
                {
                    _assembly = Assembly.Load(new AssemblyName(_assemblyName));
                }
                catch (Exception ex)
                {
                    throw new OperationException(CommandsStrings.UnreferencedAssembly(_assemblyName, _startupAssemblyName), ex);
                }
            }

            return _assembly;
        }

        private Type FindContextType(string name)
        {
            var types = GetContextTypes();

            Type[] candidates;

            if (string.IsNullOrEmpty(name))
            {
                candidates = types.Take(2).ToArray();
                if (candidates.Length == 0)
                {
                    throw new OperationException(CommandsStrings.NoContext);
                }
                if (candidates.Length == 1)
                {
                    return candidates[0];
                }

                throw new OperationException(CommandsStrings.MultipleContexts);
            }

            candidates = FilterTypes(types, name, ignoreCase: true).ToArray();
            if (candidates.Length == 0)
            {
                throw new OperationException(CommandsStrings.NoContextWithName(name));
            }
            if (candidates.Length == 1)
            {
                return candidates[0];
            }

            // Disambiguate using case
            candidates = FilterTypes(candidates, name).ToArray();
            if (candidates.Length == 0)
            {
                throw new OperationException(CommandsStrings.MultipleContextsWithName(name));
            }
            if (candidates.Length == 1)
            {
                return candidates[0];
            }

            // Allow selecting types in the default namespace
            candidates = candidates.Where(t => t.Namespace == null).ToArray();
            if (candidates.Length == 0)
            {
                throw new OperationException(CommandsStrings.MultipleContextsWithQualifiedName(name));
            }

            Debug.Assert(candidates.Length == 1, "candidates.Length is not 1.");

            return candidates[0];
        }

        private static IEnumerable<Type> FilterTypes(
            [NotNull] IEnumerable<Type> types,
            [NotNull] string name,
            bool ignoreCase = false)
        {
            Debug.Assert(types != null, "types is null.");
            Debug.Assert(!string.IsNullOrEmpty(name), "name is null or empty.");

            var comparisonType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            return types.Where(
                t => string.Equals(t.Name, name, comparisonType)
                     || string.Equals(t.FullName, name, comparisonType)
                     || string.Equals(t.AssemblyQualifiedName, name, comparisonType));
        }
    }
}