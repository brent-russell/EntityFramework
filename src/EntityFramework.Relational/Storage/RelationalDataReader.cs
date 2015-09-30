// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Storage
{
    public class RelationalDataReader : IDisposable
    {
        private readonly IRelationalConnection _connection;
        private readonly DbCommand _command;
        private readonly DbDataReader _reader;

        private bool _diposed;

        public RelationalDataReader(
            [NotNull] IRelationalConnection connection,
            [NotNull] DbCommand command,
            [NotNull] DbDataReader reader)
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(command, nameof(command));
            Check.NotNull(reader, nameof(reader));

            _connection = connection;
            _command = command;
            _reader = reader;
        }

        public DbDataReader DbDataReader => _reader;

        public void Dispose()
        {
            if (!_diposed)
            {
                _reader.Dispose();
                _command.Dispose();
                _connection.Close();

                _diposed = true;
            }
        }
    }
}
