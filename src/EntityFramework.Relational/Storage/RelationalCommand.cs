// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Storage
{
    public class RelationalCommand
    {
        public RelationalCommand(
            [NotNull] ILogger logger,
            [NotNull] string commandText,
            [NotNull] IReadOnlyList<RelationalParameter> parameters)
        {
            Check.NotNull(commandText, nameof(commandText));
            Check.NotNull(parameters, nameof(parameters));

            Logger = logger;
            CommandText = commandText;
            Parameters = parameters;
        }

        protected virtual ILogger Logger { get; }

        public RelationalCommand(
            [NotNull] string commandText,
            [NotNull] IReadOnlyList<RelationalParameter> parameters)
        {
            Check.NotNull(commandText, nameof(commandText));
            Check.NotNull(parameters, nameof(parameters));

            CommandText = commandText;
            Parameters = parameters;
        }

        public virtual string CommandText { get; }

        public virtual IReadOnlyList<RelationalParameter> Parameters { get; }


        public virtual void ExecuteNonQuery([NotNull] IRelationalConnection connection)
        {
            Check.NotNull(connection, nameof(connection));

            connection.Open();

            try
            {
                using (var dbCommand = CreateCommand(connection))
                {
                    dbCommand.ExecuteNonQuery();
                }
            }
            finally
            {
                connection.Close();
            }
        }

        public virtual async Task ExecuteNonQueryAsync(
            [NotNull] IRelationalConnection connection,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));

            await connection.OpenAsync(cancellationToken);

            try
            {
                using (var dbCommand = CreateCommand(connection))
                {
                    await dbCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally
            {
                connection.Close();
            }
        }

        public virtual object ExecuteScalar([NotNull] IRelationalConnection connection)
        {
            Check.NotNull(connection, nameof(connection));

            connection.Open();

            try
            {
                using (var dbCommand = CreateCommand(connection))
                {
                    return dbCommand.ExecuteScalar();
                }
            }
            finally
            {
                connection.Close();
            }
        }

        public virtual async Task<object> ExecuteScalarAsync(
            [NotNull] IRelationalConnection connection,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));

            await connection.OpenAsync(cancellationToken);

            try
            {
                using (var dbCommand = CreateCommand(connection))
                {
                    return await dbCommand.ExecuteScalarAsync(cancellationToken);
                }
            }
            finally
            {
                connection.Close();
            }
        }

        public virtual RelationalDataReader ExecuteReader([NotNull] IRelationalConnection connection)
        {
            Check.NotNull(connection, nameof(connection));

            connection.Open();

            var dbCommand = CreateCommand(connection);

            try
            {
                return new RelationalDataReader(
                    connection,
                    dbCommand,
                    dbCommand.ExecuteReader());
            }
            catch
            {
                dbCommand.Dispose();
                connection.Close();
                throw;
            }
        }

        public virtual async Task<RelationalDataReader> ExecuteReaderAsync(
            [NotNull] IRelationalConnection connection,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));

            await connection.OpenAsync(cancellationToken);

            var dbCommand = CreateCommand(connection);

            try
            {
                return new RelationalDataReader(
                    connection,
                    dbCommand,
                    await dbCommand.ExecuteReaderAsync());
            }
            catch
            {
                dbCommand.Dispose();
                connection.Close();
                throw;
            }
        }

        public virtual DbCommand CreateCommand([NotNull] IRelationalConnection connection)
        {
            Check.NotNull(connection, nameof(connection));

            var command = connection.DbConnection.CreateCommand();
            command.CommandText = CommandText;

            if (connection.Transaction != null)
            {
                command.Transaction = connection.Transaction.GetService();
            }

            if (connection.CommandTimeout != null)
            {
                command.CommandTimeout = (int)connection.CommandTimeout;
            }

            foreach (var parameter in Parameters)
            {
                command.Parameters.Add(
                    parameter.RelationalTypeMapping.CreateParameter(
                        command,
                        parameter.Name,
                        parameter.Value,
                        parameter.Nullable));
            }

            if (Logger.IsEnabled(LogLevel.Verbose))
            {
                Logger.LogCommand(command);
            }

            return command;
        }
    }
}
