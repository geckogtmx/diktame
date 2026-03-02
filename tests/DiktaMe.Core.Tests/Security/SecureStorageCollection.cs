
namespace DiktaMe.Core.Tests.Security;

/// <summary>
/// xUnit collection definition that disables parallel execution for all tests
/// sharing the same DPAPI-encrypted keys.dat file via <see cref="Core.Security.SecureStorage"/>.
/// </summary>
[CollectionDefinition("SecureStorage", DisableParallelization = true)]
public sealed class SecureStorageCollection { }
