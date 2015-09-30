// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Tests.TestUtilities;
using Microsoft.Data.Entity.TestUtilities.FakeProvider;
using Microsoft.Framework.Logging;
using Xunit;

namespace Microsoft.Data.Entity.Storage
{
    public class RelationalCommandTest
    {
        [Fact]
        public void Configures_DbCommand()
        {
            var fakeConnection = CreateConnection();

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "CommandText",
                new RelationalParameter[0]);

            relationalCommand.ExecuteNonQuery(fakeConnection);

            Assert.Equal(1, fakeConnection.DbConnections.Count);
            Assert.Equal(1, fakeConnection.DbConnections[0].DbCommands.Count);

            var command = fakeConnection.DbConnections[0].DbCommands[0];

            Assert.Equal("CommandText", command.CommandText);
            Assert.Null(command.Transaction);
            Assert.Equal(FakeDbCommand.DefaultCommandTimeout, command.CommandTimeout);
        }

        [Fact]
        public void Configures_DbCommand_with_transaction()
        {
            var fakeConnection = CreateConnection();

            var relationalTransaction = fakeConnection.BeginTransaction();

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "CommandText",
                new RelationalParameter[0]);

            relationalCommand.ExecuteNonQuery(fakeConnection);

            Assert.Equal(1, fakeConnection.DbConnections.Count);
            Assert.Equal(1, fakeConnection.DbConnections[0].DbCommands.Count);

            var command = fakeConnection.DbConnections[0].DbCommands[0];

            Assert.Same(relationalTransaction.GetService(), command.Transaction);
        }

        [Fact]
        public void Configures_DbCommand_with_timeout()
        {
            var fakeConnection = CreateConnection(e => e.CommandTimeout = 42);

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "CommandText",
                new RelationalParameter[0]);

            relationalCommand.ExecuteNonQuery(fakeConnection);

            Assert.Equal(1, fakeConnection.DbConnections.Count);
            Assert.Equal(1, fakeConnection.DbConnections[0].DbCommands.Count);

            var command = fakeConnection.DbConnections[0].DbCommands[0];

            Assert.Equal(42, command.CommandTimeout);
        }

        [Fact]
        public void Configures_DbCommand_with_parameters()
        {
            var fakeConnection = CreateConnection();

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "CommandText",
                new[]
                {
                    new RelationalParameter("FirstParameter", 17, new RelationalTypeMapping("int", DbType.Int32), false),
                    new RelationalParameter("SecondParameter", 18L,  new RelationalTypeMapping("long", DbType.Int64), true),
                    new RelationalParameter("ThirdParameter", null,  new RelationalTypeMapping("null", FakeDbParameter.DefaultDbType), null)
                });

            relationalCommand.ExecuteNonQuery(fakeConnection);

            Assert.Equal(1, fakeConnection.DbConnections.Count);
            Assert.Equal(1, fakeConnection.DbConnections[0].DbCommands.Count);
            Assert.Equal(3, fakeConnection.DbConnections[0].DbCommands[0].Parameters.Count);

            var parameter = fakeConnection.DbConnections[0].DbCommands[0].Parameters[0];

            Assert.Equal("FirstParameter", parameter.ParameterName);
            Assert.Equal(17, parameter.Value);
            Assert.Equal(ParameterDirection.Input, parameter.Direction);
            Assert.Equal(false, parameter.IsNullable);
            Assert.Equal(DbType.Int32, parameter.DbType);

            parameter = fakeConnection.DbConnections[0].DbCommands[0].Parameters[1];

            Assert.Equal("SecondParameter", parameter.ParameterName);
            Assert.Equal(18L, parameter.Value);
            Assert.Equal(ParameterDirection.Input, parameter.Direction);
            Assert.Equal(true, parameter.IsNullable);
            Assert.Equal(DbType.Int64, parameter.DbType);

            parameter = fakeConnection.DbConnections[0].DbCommands[0].Parameters[2];

            Assert.Equal("ThirdParameter", parameter.ParameterName);
            Assert.Equal(DBNull.Value, parameter.Value);
            Assert.Equal(ParameterDirection.Input, parameter.Direction);
            Assert.Equal(FakeDbParameter.DefaultDbType, parameter.DbType);
        }

        [Fact]
        public void Can_ExecuteNonQuery()
        {
            var executeNonQueryCount = 0;
            var connectionState = ConnectionState.Closed;
            var disposeCount = -1;

            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeNonQuery: c =>
                    {
                        executeNonQueryCount++;
                        connectionState = c.Connection.State;
                        disposeCount = c.DisposeCount;
                        return 1;
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var log = new List<Tuple<LogLevel, string>>();

            var relationalCommand = new RelationalCommand(
                CreateLogger(CreateLoggerFactory(log)),
                "ExecuteNonQuery Command",
                new RelationalParameter[0]);

            relationalCommand.ExecuteNonQuery(fakeConnection);

            // Durring command execution
            Assert.Equal(1, executeNonQueryCount);
            Assert.Equal(ConnectionState.Open, connectionState);
            Assert.Equal(0, disposeCount);

            // After command execution
            Assert.Equal(ConnectionState.Closed, fakeConnection.DbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);

            // Logging
            Assert.Equal(1, log.Count);
            Assert.Equal(LogLevel.Verbose, log[0].Item1);
            Assert.Equal("ExecuteNonQuery Command", log[0].Item2);
        }

        [Fact]
        public virtual async Task Can_ExecuteNonQueryAsync()
        {
            var executeNonQueryCount = 0;
            var connectionState = ConnectionState.Closed;
            var disposeCount = -1;

            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeNonQueryAsync: (c, ct) =>
                    {
                        executeNonQueryCount++;
                        connectionState = c.Connection.State;
                        disposeCount = c.DisposeCount;
                        return Task.FromResult(1);
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var log = new List<Tuple<LogLevel, string>>();

            var relationalCommand = new RelationalCommand(
                CreateLogger(CreateLoggerFactory(log)),
                "ExecuteNonQuery Command",
                new RelationalParameter[0]);

            await relationalCommand.ExecuteNonQueryAsync(fakeConnection);

            // Durring command execution
            Assert.Equal(1, executeNonQueryCount);
            Assert.Equal(ConnectionState.Open, connectionState);
            Assert.Equal(0, disposeCount);

            // After command execution
            Assert.Equal(ConnectionState.Closed, fakeConnection.DbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);

            // Logging
            Assert.Equal(1, log.Count);
            Assert.Equal(LogLevel.Verbose, log[0].Item1);
            Assert.Equal("ExecuteNonQuery Command", log[0].Item2);
        }

        [Fact]
        public void Can_ExecuteScalar()
        {
            var executeScalarCount = 0;
            var connectionState = ConnectionState.Closed;
            var disposeCount = -1;

            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeScalar: c =>
                    {
                        executeScalarCount++;
                        connectionState = c.Connection.State;
                        disposeCount = c.DisposeCount;
                        return "ExecuteScalar Result";
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var log = new List<Tuple<LogLevel, string>>();

            var relationalCommand = new RelationalCommand(
                CreateLogger(CreateLoggerFactory(log)),
                "ExecuteScalar Command",
                new RelationalParameter[0]);

            var result = (string)relationalCommand.ExecuteScalar(fakeConnection);

            Assert.Equal("ExecuteScalar Result", result);

            // Durring command execution
            Assert.Equal(1, executeScalarCount);
            Assert.Equal(ConnectionState.Open, connectionState);
            Assert.Equal(0, disposeCount);

            // After command execution
            Assert.Equal(ConnectionState.Closed, fakeConnection.DbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);

            // Logging
            Assert.Equal(1, log.Count);
            Assert.Equal(LogLevel.Verbose, log[0].Item1);
            Assert.Equal("ExecuteScalar Command", log[0].Item2);
        }

        [Fact]
        public async Task Can_ExecuteScalarAsync()
        {
            var executeScalarCount = 0;
            var connectionState = ConnectionState.Closed;
            var disposeCount = -1;

            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeScalarAsync: (c, ct) =>
                    {
                        executeScalarCount++;
                        connectionState = c.Connection.State;
                        disposeCount = c.DisposeCount;
                        return Task.FromResult<object>("ExecuteScalar Result");
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var log = new List<Tuple<LogLevel, string>>();

            var relationalCommand = new RelationalCommand(
                CreateLogger(CreateLoggerFactory(log)),
                "ExecuteScalar Command",
                new RelationalParameter[0]);

            var result = (string)await relationalCommand.ExecuteScalarAsync(fakeConnection);

            Assert.Equal("ExecuteScalar Result", result);

            // Durring command execution
            Assert.Equal(1, executeScalarCount);
            Assert.Equal(ConnectionState.Open, connectionState);
            Assert.Equal(0, disposeCount);

            // After command execution
            Assert.Equal(ConnectionState.Closed, fakeConnection.DbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);

            // Logging
            Assert.Equal(1, log.Count);
            Assert.Equal(LogLevel.Verbose, log[0].Item1);
            Assert.Equal("ExecuteScalar Command", log[0].Item2);
        }

        [Fact]
        public void Can_ExecuteReader()
        {
            var executeReaderCount = 0;
            var connectionState = ConnectionState.Closed;
            var disposeCount = -1;

            var dbDataReader = new FakeDbDataReader();

            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeReader: (c, b) =>
                    {
                        executeReaderCount++;
                        connectionState = c.Connection.State;
                        disposeCount = c.DisposeCount;
                        return dbDataReader;
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var log = new List<Tuple<LogLevel, string>>();

            var relationalCommand = new RelationalCommand(
                CreateLogger(CreateLoggerFactory(log)),
                "ExecuteReader Command",
                new RelationalParameter[0]);

            var result = relationalCommand.ExecuteReader(fakeConnection);

            Assert.Same(dbDataReader, result.DbDataReader);

            // Durring command execution
            Assert.Equal(1, executeReaderCount);
            Assert.Equal(ConnectionState.Open, connectionState);
            Assert.Equal(0, disposeCount);

            // After command execution
            Assert.Equal(ConnectionState.Open, fakeConnection.DbConnection.State);
            Assert.Equal(0, fakeDbConnection.DbCommands[0].DisposeCount);

            // After reader dispose
            result.Dispose();
            Assert.Equal(ConnectionState.Closed, fakeConnection.DbConnection.State);
            Assert.Equal(1, dbDataReader.DisposeCount);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);

            // Logging
            Assert.Equal(1, log.Count);
            Assert.Equal(LogLevel.Verbose, log[0].Item1);
            Assert.Equal("ExecuteReader Command", log[0].Item2);
        }

        [Fact]
        public async Task Can_ExecuteReaderAsync()
        {
            var executeReaderCount = 0;
            var connectionState = ConnectionState.Closed;
            var disposeCount = -1;

            var dbDataReader = new FakeDbDataReader();

            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeReaderAsync: (c, b, ct) =>
                    {
                        executeReaderCount++;
                        connectionState = c.Connection.State;
                        disposeCount = c.DisposeCount;
                        return Task.FromResult<DbDataReader>(dbDataReader);
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var log = new List<Tuple<LogLevel, string>>();

            var relationalCommand = new RelationalCommand(
                CreateLogger(CreateLoggerFactory(log)),
                "ExecuteReader Command",
                new RelationalParameter[0]);

            var result = await relationalCommand.ExecuteReaderAsync(fakeConnection);

            Assert.Same(dbDataReader, result.DbDataReader);

            // Durring command execution
            Assert.Equal(1, executeReaderCount);
            Assert.Equal(ConnectionState.Open, connectionState);
            Assert.Equal(0, disposeCount);

            // After command execution
            Assert.Equal(ConnectionState.Open, fakeConnection.DbConnection.State);
            Assert.Equal(0, fakeDbConnection.DbCommands[0].DisposeCount);

            // After reader dispose
            result.Dispose();
            Assert.Equal(ConnectionState.Closed, fakeConnection.DbConnection.State);
            Assert.Equal(1, dbDataReader.DisposeCount);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);

            // Logging
            Assert.Equal(1, log.Count);
            Assert.Equal(LogLevel.Verbose, log[0].Item1);
            Assert.Equal("ExecuteReader Command", log[0].Item2);
        }

        [Fact]
        public void ExecuteNonQuery_closes_connection_on_exception()
        {
            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeNonQuery: (c) =>
                    {
                        throw new DbUpdateException("ExecuteNonQuery Exception", new InvalidOperationException());
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "ExecuteNonQuery Command",
                new RelationalParameter[0]);

            Assert.Throws<DbUpdateException>(() => relationalCommand.ExecuteNonQuery(fakeConnection));
            Assert.Equal(ConnectionState.Closed, fakeDbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);
        }

        [Fact]
        public async Task ExecuteNonQueryAsync_closes_connection_on_exception()
        {
            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeNonQueryAsync: (c, ct) =>
                    {
                        throw new DbUpdateException("ExecuteNonQuery Exception", new InvalidOperationException());
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "ExecuteNonQuery Command",
                new RelationalParameter[0]);

            await Assert.ThrowsAsync<DbUpdateException>(() => relationalCommand.ExecuteNonQueryAsync(fakeConnection));
            Assert.Equal(ConnectionState.Closed, fakeDbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);
        }

        [Fact]
        public void ExecuteScalar_closes_connection_on_exception()
        {
            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeScalar: (c) =>
                    {
                        throw new DbUpdateException("ExecuteScalar Exception", new InvalidOperationException());
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "ExecuteScalar Command",
                new RelationalParameter[0]);

            Assert.Throws<DbUpdateException>(() => relationalCommand.ExecuteScalar(fakeConnection));
            Assert.Equal(ConnectionState.Closed, fakeDbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);
        }

        [Fact]
        public async Task ExecuteScalarAsync_closes_connection_on_exception()
        {
            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeScalarAsync: (c, ct) =>
                    {
                        throw new DbUpdateException("ExecuteScalar Exception", new InvalidOperationException());
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "ExecuteScalar Command",
                new RelationalParameter[0]);

            await Assert.ThrowsAsync<DbUpdateException>(() => relationalCommand.ExecuteScalarAsync(fakeConnection));
            Assert.Equal(ConnectionState.Closed, fakeDbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);
        }

        [Fact]
        public void ExecuteReader_closes_connection_on_exception()
        {
            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeReader: (c, b) =>
                    {
                        throw new DbUpdateException("ExecuteReader Exception", new InvalidOperationException());
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "ExecuteReader Command",
                new RelationalParameter[0]);

            Assert.Throws<DbUpdateException>(() => relationalCommand.ExecuteReader(fakeConnection));
            Assert.Equal(ConnectionState.Closed, fakeDbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);
        }

        [Fact]
        public async Task ExecuteReaderAsync_closes_connection_on_exception()
        {
            var fakeDbConnection = CreateDbConnection(
                new FakeCommandExecutor(
                    executeReaderAsync: (c, b, ct) =>
                    {
                        throw new DbUpdateException("ExecuteReader Exception", new InvalidOperationException());
                    }));

            var fakeConnection = CreateConnection(fakeDbConnection);

            var relationalCommand = new RelationalCommand(
                CreateLogger(),
                "ExecuteReader Command",
                new RelationalParameter[0]);

            await Assert.ThrowsAsync<DbUpdateException>(() => relationalCommand.ExecuteReaderAsync(fakeConnection));
            Assert.Equal(ConnectionState.Closed, fakeDbConnection.State);
            Assert.Equal(1, fakeDbConnection.DbCommands[0].DisposeCount);
        }

        private static FakeDbConnection CreateDbConnection(FakeCommandExecutor commandExecutor)
            => FakeProviderTestHelpers.CreateDbConnection(commandExecutor);

        private static FakeRelationalConnection CreateConnection()
            => FakeProviderTestHelpers.CreateConnection();

        private static FakeRelationalConnection CreateConnection(Action<FakeRelationalOptionsExtension> setup)
        {
            var optionsExtension = FakeProviderTestHelpers.CreateOptionsExtension();

            setup(optionsExtension);

            return FakeProviderTestHelpers.CreateConnection(
                FakeProviderTestHelpers.CreateOptions(
                    optionsExtension));
        }

        private static FakeRelationalConnection CreateConnection(FakeDbConnection dbConnection)
            => FakeProviderTestHelpers.CreateConnection(
                FakeProviderTestHelpers.CreateOptions(
                    FakeProviderTestHelpers.CreateOptionsExtension(dbConnection)));

        private static ILogger CreateLogger(ILoggerFactory loggerFactory = null)
            => loggerFactory != null
                ? loggerFactory.CreateLogger<RelationalCommand>()
                : CreateLoggerFactory().CreateLogger<RelationalCommand>();

        private static ILoggerFactory CreateLoggerFactory(List<Tuple<LogLevel, string>> log = null)
            => log != null
                ? new ListLoggerFactory(log, n => n == "Microsoft.Data.Entity.Storage.RelationalCommand")
                : new ListLoggerFactory(null, n => false);
    }
}
