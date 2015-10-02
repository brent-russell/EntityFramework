// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Extensions;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Update
{
    public abstract class ReaderModificationCommandBatch : ModificationCommandBatch
    {
        private readonly IRelationalCommandBuilderFactory _commandBuilderFactory;
        private readonly ISensitiveDataLogger _logger;
        private readonly TelemetrySource _telemetrySource;

        private readonly List<ModificationCommand> _modificationCommands = new List<ModificationCommand>();

        protected virtual StringBuilder CachedCommandText { get; [param: NotNull] set; }

        protected virtual int LastCachedCommandIndex { get; set; }

        protected ReaderModificationCommandBatch(
            [NotNull] IRelationalCommandBuilderFactory commandBuilderFactory,
            [NotNull] ISqlGenerator sqlGenerator,
            [NotNull] IUpdateSqlGenerator updateSqlGenerator,
            [NotNull] ISensitiveDataLogger logger,
            [NotNull] TelemetrySource telemetrySource)
        {
            Check.NotNull(commandBuilderFactory, nameof(commandBuilderFactory));
            Check.NotNull(updateSqlGenerator, nameof(updateSqlGenerator));
            Check.NotNull(logger, nameof(logger));
            Check.NotNull(telemetrySource, nameof(telemetrySource));

            _commandBuilderFactory = commandBuilderFactory;
            _logger = logger;
            _telemetrySource = telemetrySource;

            SqlGenerator = sqlGenerator;
            UpdateSqlGenerator = updateSqlGenerator;
        }

        protected virtual ISqlGenerator SqlGenerator { get; }

        protected virtual IUpdateSqlGenerator UpdateSqlGenerator { get; }

        public override IReadOnlyList<ModificationCommand> ModificationCommands => _modificationCommands;

        public override bool AddCommand(ModificationCommand modificationCommand)
        {
            Check.NotNull(modificationCommand, nameof(modificationCommand));

            if (ModificationCommands.Count == 0)
            {
                ResetCommandText();
            }

            if (!CanAddCommand(modificationCommand))
            {
                return false;
            }

            _modificationCommands.Add(modificationCommand);

            if (!IsCommandTextValid())
            {
                ResetCommandText();
                _modificationCommands.RemoveAt(_modificationCommands.Count - 1);
                return false;
            }

            return true;
        }

        protected virtual void ResetCommandText()
        {
            CachedCommandText = new StringBuilder();
            UpdateSqlGenerator.AppendBatchHeader(CachedCommandText);
            LastCachedCommandIndex = -1;
        }

        protected abstract bool CanAddCommand([NotNull] ModificationCommand modificationCommand);

        protected abstract bool IsCommandTextValid();

        protected virtual string GetCommandText()
        {
            for (var i = LastCachedCommandIndex + 1; i < ModificationCommands.Count; i++)
            {
                UpdateCachedCommandText(i);
            }

            return CachedCommandText.ToString();
        }

        protected virtual void UpdateCachedCommandText(int commandPosition)
        {
            var newModificationCommand = ModificationCommands[commandPosition];

            switch (newModificationCommand.EntityState)
            {
                case EntityState.Added:
                    UpdateSqlGenerator.AppendInsertOperation(CachedCommandText, newModificationCommand);
                    break;
                case EntityState.Modified:
                    UpdateSqlGenerator.AppendUpdateOperation(CachedCommandText, newModificationCommand);
                    break;
                case EntityState.Deleted:
                    UpdateSqlGenerator.AppendDeleteOperation(CachedCommandText, newModificationCommand);
                    break;
            }

            LastCachedCommandIndex = commandPosition;
        }

        protected virtual DbCommand CreateStoreCommand(
            [NotNull] string commandText,
            [NotNull] IRelationalConnection connection)
        {
            var commandBuilder = _commandBuilderFactory
                .Create()
                .Append(commandText);

            foreach (var columnModification in ModificationCommands.SelectMany(t => t.ColumnModifications))
            {
                PopulateParameters(commandBuilder, columnModification);
            }

            return commandBuilder.BuildRelationalCommand().CreateCommand(connection);
        }

        protected virtual void PopulateParameters(
            [NotNull] RelationalCommandBuilder commandBuilder,
            [NotNull] ColumnModification columnModification)
        {
            if (columnModification.ParameterName != null)
            {
                commandBuilder.AddParameter(
                    SqlGenerator.GenerateParameterName(columnModification.ParameterName),
                    columnModification.Value,
                    columnModification.Property);
            }

            if (columnModification.OriginalParameterName != null)
            {
                commandBuilder.AddParameter(
                    SqlGenerator.GenerateParameterName(columnModification.OriginalParameterName),
                    columnModification.OriginalValue,
                    columnModification.Property);
            }
        }

        public override void Execute(IRelationalConnection connection)
        {
            Check.NotNull(connection, nameof(connection));

            var commandText = GetCommandText();

            using (var storeCommand = CreateStoreCommand(commandText, connection))
            {
                _logger.LogCommand(storeCommand);
                _telemetrySource.WriteCommand("Microsoft.Data.Entity.BeforeExecuteReader", storeCommand);

                try
                {
                    using (var reader = storeCommand.ExecuteReader())
                    {
                        Consume(reader);
                    }
                }
                catch (DbUpdateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DbUpdateException(RelationalStrings.UpdateStoreException, ex);
                }
            }
        }

        public override async Task ExecuteAsync(
            IRelationalConnection connection,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));

            var commandText = GetCommandText();

            using (var storeCommand = CreateStoreCommand(commandText, connection))
            {
                _logger.LogCommand(storeCommand);
                _telemetrySource.WriteCommand("Microsoft.Data.Entity.BeforeExecuteReader", storeCommand);

                try
                {
                    using (var reader = await storeCommand.ExecuteReaderAsync(cancellationToken))
                    {
                        await ConsumeAsync(reader, cancellationToken);
                    }
                }
                catch (DbUpdateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DbUpdateException(RelationalStrings.UpdateStoreException, ex);
                }
            }
        }

        protected abstract void Consume([NotNull] DbDataReader reader);

        protected abstract Task ConsumeAsync(
            [NotNull] DbDataReader reader,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
