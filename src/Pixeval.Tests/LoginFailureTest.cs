// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common;
using Pixeval.Storage;

namespace Pixeval.Tests;

[TestClass]
public sealed class LoginFailureTest
{
    [TestMethod]
    public void NetworkIoFailure_IsNotReportedAsAStorageFailure()
    {
        var exception = new HttpRequestException("Connection failed", new IOException("Connection closed"));

        Assert.IsFalse(LoginErrorHelper.IsStorageFailure(exception));
        Assert.IsTrue(LoginErrorHelper.IsStorageFailure(new IOException("Disk full")));
        Assert.IsTrue(LoginErrorHelper.IsStorageFailure(
            new InvalidOperationException("Cannot save login", new UnauthorizedAccessException("Access denied"))));
    }

    [TestMethod]
    public void NativeDatabaseFailure_IsReportedAsAStorageFailure()
    {
        Assert.IsTrue(LoginErrorHelper.IsStorageFailure(new StorageError.Database("Disk is read-only")));
    }
}
